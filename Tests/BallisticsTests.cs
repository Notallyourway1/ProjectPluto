using System;

namespace ProjectPluto.Tests
{
    public static class BallisticsTests
    {
        private static readonly PlutoVec G = new PlutoVec(0, -9.81, 0);
        private static double Flat(PlutoVec p) => p.Y;

        public static void Ballistics_FlatGroundNoDragMatchesAnalytic()
        {
            var impact = PlutoBallistics.PredictImpact(new PlutoVec(0, 150, 0), new PlutoVec(1020, 30, 0), G, 0, Flat);
            double t = (30 + Math.Sqrt(30 * 30 + 2 * 9.81 * 150)) / 9.81;
            Assert.Equal(1020 * t, impact.X, 1020 * t * 0.01, "range");
        }

        public static void Ballistics_DragShortensRange()
        {
            var noDrag = PlutoBallistics.PredictImpact(new PlutoVec(0, 150, 0), new PlutoVec(1020, 30, 0), G, 0, Flat);
            var drag = PlutoBallistics.PredictImpact(new PlutoVec(0, 150, 0), new PlutoVec(1020, 30, 0), G, 1e-4, Flat);
            Assert.True(drag.X < noDrag.X, "drag shortens range");
        }

        public static void Ballistics_TossTriggerFiresOncePastTarget()
        {
            var trigger = new PlutoTossTrigger();
            var target = new PlutoVec(5000, 0, 0);
            var track = new PlutoVec(1, 0, 0);
            int fired = 0;
            double firedAt = double.NaN;
            for (double x = 4000; x <= 6000; x += 100)
            {
                if (trigger.ShouldRelease(new PlutoVec(x, 0, 0), target, track))
                {
                    fired++;
                    firedAt = x;
                }
            }
            Assert.Equal(1, fired, 0, "fires once");
            Assert.Equal(5000, firedAt, 1e-9, "fires at first impact past target");
        }

        public static void Ballistics_TossTriggerIgnoresWideMiss()
        {
            var trigger = new PlutoTossTrigger();
            bool fired = false;
            for (double x = 4000; x <= 6000; x += 100)
                fired |= trigger.ShouldRelease(new PlutoVec(x, 0, 1000), new PlutoVec(5000, 0, 0), new PlutoVec(1, 0, 0));
            Assert.True(!fired, "never fires on a 1 km miss");
        }

        public static void Ballistics_ArmingDelayPreventsImmediateDetonation()
        {
            var fuse = new PlutoWarheadFuse();
            Assert.True(!fuse.Update(0.5, -1), "not armed at 0.5 s");
            Assert.True(fuse.Update(1.6, -1), "armed at 2.1 s");
        }

        public static void Ballistics_SafetyTimerDetonates()
        {
            var fuse = new PlutoWarheadFuse();
            bool fired = false;
            for (int i = 0; i < 1200 && !fired; i++)
                fired = fuse.Update(0.1, 500);
            Assert.True(fired, "detonates by safety timer");
        }

        public static void Ballistics_BodyRelativeStateUnchangedByFrameShift()
        {
            var pos = new PlutoVec(600000, 150, 0);
            var vel = new PlutoVec(1020, 30, 0);
            PlutoBallistics.Step(ref pos, ref vel, G, 0, 0.02);
            var bodyA = new PlutoVec(0, 0, 0);
            var bodyB = new PlutoVec(-123456.7, 9876.5, 42);
            var relA = (bodyA + pos) - bodyA;
            var relB = (bodyB + pos) - bodyB;
            Assert.Equal(relA.X, relB.X, 1e-6, "x");
            Assert.Equal(relA.Y, relB.Y, 1e-6, "y");
        }
    }
}
