using System;
using UnityEngine;

namespace ProjectPluto
{
    // A plugin-owned object (not a vessel) that falls under gravity. Its state is kept in the
    // celestial body's own frame, so KSP's floating origin and Krakensbane shifts never move it;
    // its transform is placed from that state every frame.
    public sealed class PlutoFallingObject : MonoBehaviour
    {
        public CelestialBody Body { get; private set; }
        public PlutoVec LocalPosition;
        public PlutoVec LocalVelocity;
        public double DragPerM;
        public double MaxLifetime = 60;
        public double Age { get; private set; }
        public Action<PlutoFallingObject> OnGround;
        public Func<PlutoFallingObject, double, bool> CustomUpdate;

        private Quaternion localRotation;

        public static PlutoFallingObject Attach(GameObject go, CelestialBody body, Vector3 worldPosition,
            Vector3 surfaceVelocity)
        {
            var falling = go.AddComponent<PlutoFallingObject>();
            falling.Body = body;
            falling.LocalPosition = ToLocalPoint(body, worldPosition);
            falling.LocalVelocity = ToLocalVector(body, surfaceVelocity);
            falling.localRotation = Quaternion.Inverse(body.transform.rotation) * go.transform.rotation;
            return falling;
        }

        public static PlutoVec ToLocalPoint(CelestialBody body, Vector3 world)
        {
            Vector3d local = body.transform.InverseTransformPoint(world);
            return new PlutoVec(local.x, local.y, local.z);
        }

        public static PlutoVec ToLocalVector(CelestialBody body, Vector3 world)
        {
            Vector3 local = body.transform.InverseTransformDirection(world);
            return new PlutoVec(local.x, local.y, local.z);
        }

        public static Vector3 ToWorldPoint(CelestialBody body, PlutoVec local) =>
            body.transform.TransformPoint(new Vector3((float)local.X, (float)local.Y, (float)local.Z));

        public Vector3 WorldPosition => ToWorldPoint(Body, LocalPosition);

        // Gravity toward the body centre, in the body's frame.
        public static PlutoVec Gravity(CelestialBody body, PlutoVec local)
        {
            double r = Math.Max(local.Length, 1);
            return local * (-body.gravParameter / (r * r * r));
        }

        // Height above terrain (or sea level over water) of a body-frame point.
        public static double HeightAboveGround(CelestialBody body, PlutoVec local)
        {
            Vector3 world = ToWorldPoint(body, local);
            double altitude = body.GetAltitude(world);
            double terrain = body.TerrainAltitude(body.GetLatitude(world), body.GetLongitude(world), true);
            if (body.ocean)
                terrain = Math.Max(terrain, 0);
            return altitude - terrain;
        }

        private void FixedUpdate()
        {
            if (Body == null || !HighLogic.LoadedSceneIsFlight)
            {
                Destroy(gameObject);
                return;
            }

            double dt = TimeWarp.fixedDeltaTime;
            Age += dt;
            PlutoBallistics.Step(ref LocalPosition, ref LocalVelocity, Gravity(Body, LocalPosition), DragPerM, dt);
            PlaceTransform();

            double height = HeightAboveGround(Body, LocalPosition);
            if (CustomUpdate != null)
            {
                if (CustomUpdate(this, height))
                    Destroy(gameObject);
                return;
            }

            if (height <= 0 || Age >= MaxLifetime)
            {
                OnGround?.Invoke(this);
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            if (Body != null)
                PlaceTransform();
        }

        private void PlaceTransform()
        {
            transform.position = WorldPosition;
            transform.rotation = Body.transform.rotation * localRotation;
        }
    }
}
