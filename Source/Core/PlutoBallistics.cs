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
    }

    public static class PlutoBallistics
    {
        // Semi-implicit Euler with quadratic drag: a = g − dragPerM·|v|·v.
        public static void Step(ref PlutoVec pos, ref PlutoVec vel, PlutoVec gravity, double dragPerM, double dt)
        {
            PlutoVec accel = gravity - vel * (dragPerM * vel.Length);
            vel = vel + accel * dt;
            pos = pos + vel * dt;
        }

        // Where a warhead released with this state would hit the ground (or where it is after maxT).
        public static PlutoVec PredictImpact(PlutoVec pos, PlutoVec vel, PlutoVec gravity, double dragPerM,
            Func<PlutoVec, double> heightAboveGround, double dt = 0.05, double maxT = 120)
        {
            for (double t = 0; t < maxT; t += dt)
            {
                PlutoVec before = pos;
                double hBefore = heightAboveGround(pos);
                Step(ref pos, ref vel, gravity, dragPerM, dt);
                double hAfter = heightAboveGround(pos);
                if (hAfter <= 0)
                {
                    // Interpolate to the ground crossing within the step.
                    double f = hBefore > hAfter ? hBefore / (hBefore - hAfter) : 1;
                    return before + (pos - before) * Math.Max(0, Math.Min(1, f));
                }
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
