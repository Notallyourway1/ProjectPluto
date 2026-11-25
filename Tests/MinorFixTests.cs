using System;

namespace ProjectPluto.Tests
{
    public static class MinorFixTests
    {
        // Spec: warheads are thrown up and backward from the hatch.
        public static void Toss_IsUpAndBackward()
        {
            var v = new PlutoVec(1000, 0, 0);
            var up = new PlutoVec(0, 1, 0);
            PlutoVec toss = PlutoBallistics.TossVelocity(v, up);
            Assert.Equal(PlutoConstants.TossUpMs, toss.Y, 1e-9, "up");
            Assert.Equal(1000 - PlutoConstants.TossBackMs, toss.X, 1e-9, "back");
        }

        public static void Toss_AtRestIsStraightUp()
        {
            PlutoVec toss = PlutoBallistics.TossVelocity(new PlutoVec(0, 0, 0), new PlutoVec(0, 1, 0));
            Assert.Equal(PlutoConstants.TossUpMs, toss.Y, 1e-9, "up");
            Assert.Equal(0, toss.X, 1e-9, "no back without motion");
        }

        public static void Predict_ReportsFallTime()
        {
            PlutoBallistics.PredictImpact(new PlutoVec(0, 150, 0), new PlutoVec(1000, 30, 0),
                new PlutoVec(0, -9.81, 0), 0, p => p.Y, out double fallTime, 0.5, 60);
            double t = (30 + Math.Sqrt(30 * 30 + 2 * 9.81 * 150)) / 9.81;
            Assert.Equal(t, fallTime, 0.05, "fall time");
        }

        // Slow and low: the missile is still inside the blast when the warhead lands.
        public static void SelfDamage_SlowLowReleaseIsRisky()
        {
            var release = new PlutoVec(0, 150, 0);
            var v = new PlutoVec(300, 0, 0);
            var g = new PlutoVec(0, -9.81, 0);
            PlutoVec impact = PlutoBallistics.PredictImpact(release, PlutoBallistics.TossVelocity(v, new PlutoVec(0, 1, 0)),
                g, 0, p => p.Y, out double t, 0.5, 60);
            Assert.True(PlutoBallistics.MissileInsideBlast(release, v, impact, t, PlutoConstants.DamageRadiusM), "risky");
        }

        public static void SelfDamage_Mach3ReleaseIsSafe()
        {
            var release = new PlutoVec(0, 150, 0);
            var v = new PlutoVec(1020, 0, 0);
            var g = new PlutoVec(0, -9.81, 0);
            PlutoVec impact = PlutoBallistics.PredictImpact(release, PlutoBallistics.TossVelocity(v, new PlutoVec(0, 1, 0)),
                g, 7e-5, p => p.Y, out double t, 0.5, 60);
            Assert.True(!PlutoBallistics.MissileInsideBlast(release, v, impact, t, PlutoConstants.DamageRadiusM), "safe");
        }
    }
}
