using System;

namespace ProjectPluto
{
    // Double-precision vector so positions can be kept relative to a planet without float error.
    public struct PlutoVec
    {
        public double X, Y, Z;

        public PlutoVec(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public static PlutoVec operator +(PlutoVec a, PlutoVec b) => new PlutoVec(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static PlutoVec operator -(PlutoVec a, PlutoVec b) => new PlutoVec(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static PlutoVec operator *(PlutoVec a, double s) => new PlutoVec(a.X * s, a.Y * s, a.Z * s);
        public static double Dot(PlutoVec a, PlutoVec b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static PlutoVec Cross(PlutoVec a, PlutoVec b) =>
            new PlutoVec(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        // Angle from a to b in degrees, signed by the direction of a × b along axis.
        public static double SignedAngle(PlutoVec a, PlutoVec b, PlutoVec axis)
        {
            double cos = Dot(a, b) / Math.Max(a.Length * b.Length, 1e-12);
            double angle = Math.Acos(Math.Max(-1, Math.Min(1, cos))) * 180 / Math.PI;
            return Dot(axis, Cross(a, b)) < 0 ? -angle : angle;
        }
    }

    public static class PlutoBallistics
    {
        // Constant-acceleration step with quadratic drag: a = g − dragPerM·|v|·v. Exact for gravity alone,
        // so prediction can use coarse steps (and few terrain lookups) without losing accuracy.
        public static void Step(ref PlutoVec pos, ref PlutoVec vel, PlutoVec gravity, double dragPerM, double dt)
        {
            PlutoVec accel = gravity - vel * (dragPerM * vel.Length);
            pos = pos + vel * dt + accel * (0.5 * dt * dt);
            vel = vel + accel * dt;
        }

        // Warhead velocity at release: the missile's velocity plus a toss up and backward from the hatch.
        public static PlutoVec TossVelocity(PlutoVec missileVelocity, PlutoVec up)
        {
            PlutoVec toss = missileVelocity + up * PlutoConstants.TossUpMs;
            double speed = missileVelocity.Length;
            if (speed > 1)
                toss = toss - missileVelocity * (PlutoConstants.TossBackMs / speed);
            return toss;
        }

        // True when the missile, flying on at missileVelocity, is still within radius of the impact point
        // when a warhead released from releasePos lands after fallTime.
        public static bool MissileInsideBlast(PlutoVec releasePos, PlutoVec missileVelocity, PlutoVec impact,
            double fallTime, double radius) =>
            (releasePos + missileVelocity * fallTime - impact).Length < radius;

        // Where a warhead released with this state would hit the ground (or where it is after maxT).
        public static PlutoVec PredictImpact(PlutoVec pos, PlutoVec vel, PlutoVec gravity, double dragPerM,
            Func<PlutoVec, double> heightAboveGround, double dt = 0.05, double maxT = 120) =>
            PredictImpact(pos, vel, gravity, dragPerM, heightAboveGround, out _, dt, maxT);

        public static PlutoVec PredictImpact(PlutoVec pos, PlutoVec vel, PlutoVec gravity, double dragPerM,
            Func<PlutoVec, double> heightAboveGround, out double fallTime, double dt = 0.05, double maxT = 120)
        {
            fallTime = maxT;
            double hBefore = heightAboveGround(pos);
            for (double t = 0; t < maxT; t += dt)
            {
                PlutoVec before = pos;
                Step(ref pos, ref vel, gravity, dragPerM, dt);
                double hAfter = heightAboveGround(pos);
                if (hAfter <= 0)
                {
                    // Interpolate to the ground crossing within the step.
                    double f = Math.Max(0, Math.Min(1, hBefore > hAfter ? hBefore / (hBefore - hAfter) : 1));
                    fallTime = t + f * dt;
                    return before + (pos - before) * f;
                }
                hBefore = hAfter;
            }
            return pos;
        }
    }

    // Fires once, when the predicted impact first reaches the target along the track and the
    // cross-track miss is small enough.
    public sealed class PlutoTossTrigger
    {
        private bool hasPrevious;
        private double previousAlong;
        private bool fired;

        public bool ShouldRelease(PlutoVec predictedImpact, PlutoVec target, PlutoVec trackDir)
        {
            if (fired)
                return false;

            PlutoVec offset = target - predictedImpact;
            double along = PlutoVec.Dot(offset, trackDir);
            double cross = (offset - trackDir * along).Length;
            bool crossed = hasPrevious && previousAlong > 0 && along <= 0;
            hasPrevious = true;
            previousAlong = along;

            if (crossed && cross < PlutoConstants.TossMaxCrossTrackM)
            {
                fired = true;
                return true;
            }
            return false;
        }

        public void Reset()
        {
            hasPrevious = false;
            fired = false;
        }
    }

    public sealed class PlutoWarheadFuse
    {
        public double Age { get; private set; }

        // True when the warhead should detonate this step.
        public bool Update(double dt, double heightAboveGround)
        {
            Age += dt;
            if (Age >= PlutoConstants.WarheadSafetySeconds - 1e-6)
                return true;
            return Age >= PlutoConstants.WarheadArmSeconds && heightAboveGround <= 0;
        }
    }
}
