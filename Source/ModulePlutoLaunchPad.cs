using UnityEngine;

namespace ProjectPluto
{
    // A ground launcher for the Pluto missile. Attached under the missile's belly in the editor, it tilts
    // the craft to the launch elevation, stands on a tower that reaches the ground, holds the missile
    // still until staged, then lets go and stays behind on the ground.
    public sealed class ModulePlutoLaunchPad : PartModule, IStageSeparator
    {
        // Physics layer of terrain and the KSC buildings (the stock clamp anchors to the same layer).
        private const int GroundMask = 1 << 15;
        private const float GroundRayM = 200f;
        private const float EditorRefreshSeconds = 0.25f;

        [KSPField] public string railTransform = "Rail";
        [KSPField] public string standTransform = "Stand";
        [KSPField] public string towerTransform = "Tower";
        [KSPField] public string footTransform = "Foot";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Launch elevation", guiUnits = "°"),
            UI_FloatRange(minValue = 15f, maxValue = 90f, stepIncrement = 5f, scene = UI_Scene.Editor)]
        public float launchElevation = (float)PlutoLaunchGeometry.DefaultElevationDeg;

        [KSPField(isPersistant = true)] public bool released;
        [KSPField(isPersistant = true)] public float towerLength = 4f;

        private Transform rail, stand, tower, foot;
        private FixedJoint anchor;
        private float editorTimer;

        public override string GetInfo() =>
            "Ground launcher for Project Pluto. Attach it under the missile's belly; the craft tilts to the " +
            "launch elevation set here. It holds the missile still until staged, then stays on the ground.";

        // Also sizes the tower on load: a launch puts the craft's lowest collider on the ground before
        // OnStart runs, and that should be the foot at its editor length.
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            FindTransforms();
            SetTower(towerLength);
        }

        private void FindTransforms()
        {
            rail = part.FindModelTransform(railTransform);
            stand = part.FindModelTransform(standTransform);
            tower = part.FindModelTransform(towerTransform);
            foot = part.FindModelTransform(footTransform);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            FindTransforms();

            if (HighLogic.LoadedSceneIsEditor)
            {
                Fields[nameof(launchElevation)].uiControlEditor.onFieldChanged = (field, old) => TiltCraft();
                GameEvents.onEditorPartEvent.Add(OnEditorPartEvent);
            }
            else if (HighLogic.LoadedSceneIsFlight)
            {
                Fields[nameof(launchElevation)].guiActiveEditor = false;
                if (!released)
                    part.PermanentGroundContact = true;
            }
            SetTower(towerLength);
        }

        private void OnDestroy()
        {
            GameEvents.onEditorPartEvent.Remove(OnEditorPartEvent);
            if (anchor != null)
                Destroy(anchor);
        }

        private void OnEditorPartEvent(ConstructionEventType type, Part p)
        {
            if (p == part && type == ConstructionEventType.PartAttached)
                TiltCraft();
        }

        [KSPEvent(guiActiveEditor = true, guiName = "Tilt craft to launch elevation")]
        public void TiltCraftEvent() => TiltCraft();

        // Turns the whole craft about the missile so its nose is at launchElevation with this pad underneath.
        private void TiltCraft()
        {
            Part missile = part.parent;
            Part root = EditorLogic.RootPart;
            if (missile == null || root == null)
                return;

            Vector3 up = Vector3.up;
            Vector3 nose = missile.transform.up;
            Vector3 padSide = Vector3.ProjectOnPlane(part.transform.position - missile.transform.position, nose);
            if (padSide.sqrMagnitude < 1e-6f)
                padSide = missile.transform.forward;
            Vector3 belly = padSide.normalized;

            PlutoVec heading = PlutoLaunchGeometry.Heading(ToVec(nose), ToVec(belly), ToVec(up));
            PlutoLaunchGeometry.TargetAxes(launchElevation, heading, ToVec(up), out PlutoVec wantNose, out PlutoVec wantBelly);
            Quaternion turn = Quaternion.LookRotation(ToVector3(wantBelly), ToVector3(wantNose))
                * Quaternion.Inverse(Quaternion.LookRotation(belly, nose));

            Vector3 pivot = missile.transform.position;
            root.transform.position = pivot + turn * (root.transform.position - pivot);
            root.transform.rotation = turn * root.transform.rotation;
            editorTimer = 0;
            if (EditorLogic.fetch != null && EditorLogic.fetch.ship != null)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        // IStageSeparator: the pad stages like a launch clamp.
        public int GetStageIndex(int fallback) => part.inverseStage;

        public override bool IsStageable() => !released;

        public override void OnActive()
        {
            if (HighLogic.LoadedSceneIsFlight)
                Release();
        }

        [KSPAction("Release missile")]
        public void ReleaseAction(KSPActionParam param) => Release();

        // Lets the missile go. The pad keeps its ground anchor and stays where it is.
        public void Release()
        {
            if (released)
                return;
            released = true;
            if (part.parent != null)
                part.decouple(0f);
            else
            {
                // The pad is the craft's root: detach every child instead.
                for (int i = part.children.Count - 1; i >= 0; i--)
                    part.children[i].decouple(0f);
            }
            part.stackIcon.SetIconColor(XKCDColors.SlateGrey);
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null)
                return;
            // A joint to the world must not survive packing: floating-origin shifts would drag it.
            if (vessel.packed)
            {
                if (anchor != null)
                    Destroy(anchor);
                return;
            }
            if (anchor == null && part.Rigidbody != null)
            {
                MeasureTower();
                anchor = part.gameObject.AddComponent<FixedJoint>();
                anchor.breakForce = float.PositiveInfinity;
                anchor.breakTorque = float.PositiveInfinity;
                part.PermanentGroundContact = true;
            }
        }

        // Stretches the tower down to the ground found under the stand.
        private void MeasureTower()
        {
            if (stand == null)
                return;
            OrientStand(Up());
            if (Physics.Raycast(stand.position, -Up(), out RaycastHit hit, GroundRayM, GroundMask))
                SetTower((float)PlutoLaunchGeometry.FlightTowerLength(hit.distance));
        }

        public void LateUpdate()
        {
            if (rail != null && part.parent != null)
            {
                // Keep the cradle along the missile, however the pad was turned when attached.
                Vector3 axis = Vector3.ProjectOnPlane(part.parent.transform.up, part.transform.up);
                if (axis.sqrMagnitude > 1e-6f)
                    rail.rotation = Quaternion.LookRotation(axis, part.transform.up);
            }
            if (stand == null)
                return;
            if (HighLogic.LoadedSceneIsFlight)
            {
                // The tower stays upright under the missile; after release it does not move.
                if (!released && vessel != null && !vessel.packed)
                    OrientStand(Up());
                return;
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                OrientStand(Vector3.up);
                editorTimer -= Time.deltaTime;
                if (editorTimer <= 0)
                {
                    editorTimer = EditorRefreshSeconds;
                    FitTowerInEditor();
                }
            }
        }

        // Editor: the foot hangs below the craft's lowest point, so the launch puts the foot on the ground.
        private void FitTowerInEditor()
        {
            ShipConstruct ship = EditorLogic.fetch != null ? EditorLogic.fetch.ship : null;
            if (ship == null || !ship.Contains(part))
                return;
            float lowest = float.PositiveInfinity;
            foreach (Part p in ship.parts)
            {
                if (p == null || p.FindModuleImplementing<ModulePlutoLaunchPad>() != null)
                    continue;
                foreach (Collider c in p.GetComponentsInChildren<Collider>())
                {
                    if (c.enabled && !c.isTrigger)
                        lowest = Mathf.Min(lowest, c.bounds.min.y);
                }
            }
            if (!float.IsInfinity(lowest))
                SetTower((float)PlutoLaunchGeometry.EditorTowerLength(stand.position.y, lowest));
        }

        private void OrientStand(Vector3 worldUp)
        {
            Vector3 forward = Vector3.ProjectOnPlane(part.parent != null ? part.parent.transform.up : part.transform.forward, worldUp);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.ProjectOnPlane(part.transform.forward, worldUp);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.ProjectOnPlane(part.transform.right, worldUp);
            stand.rotation = Quaternion.LookRotation(forward, worldUp);
        }

        private void SetTower(float length)
        {
            towerLength = length;
            if (tower != null)
                tower.localScale = new Vector3(1f, length, 1f);
            if (foot != null)
                foot.localPosition = new Vector3(0f, -length, 0f);
        }

        private Vector3 Up() => (part.transform.position - vessel.mainBody.position).normalized;

        private static PlutoVec ToVec(Vector3 v) => new PlutoVec(v.x, v.y, v.z);
        private static Vector3 ToVector3(PlutoVec v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
