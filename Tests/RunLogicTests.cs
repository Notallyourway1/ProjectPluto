using System;
using System.Linq;

namespace ProjectPluto.Tests
{
    public static class RunLogicTests
    {
        private const double Step = 250;

        // Sample 0 is the ground directly below; sample i is i × 250 m ahead.
        private static double[] Terrain(Func<double, double> heightAt) =>
            Enumerable.Range(0, 21).Select(i => heightAt(i * Step)).ToArray();

        public static void Lookahead_FlatGroundHoldsRunHeight()
        {
            double h = PlutoTerrainLookahead.DesiredAltitudeAsl(Terrain(d => 0), 150, Step, 8);
            Assert.Equal(150, h, 1e-9, "flat");
        }

        public static void Lookahead_ClimbsEarlyForRidge()
        {
            double h = PlutoTerrainLookahead.DesiredAltitudeAsl(Terrain(d => d >= 3000 ? 600 : 0), 150, Step, 8);
            Assert.True(h >= 750 - 3000 * Math.Tan(8 * Math.PI / 180) - 1e-9, "clears ridge on an 8° climb");
            Assert.True(h > 150, "already climbing");
        }

        public static void Lookahead_FollowsGroundBelow()
        {
            double h = PlutoTerrainLookahead.DesiredAltitudeAsl(Terrain(d => 400), 150, Step, 8);
            Assert.Equal(550, h, 1e-9, "plateau");
        }

        public static void Pid_ConvergesOnStep()
        {
            var pid = new PlutoPid(1, 0.5, 0, -2, 2);
            double y = 0, reached = double.NaN;
            for (double t = 0; t < 30; t += 0.02)
            {
                double u = pid.Update(1 - y, 0.02);
                Assert.True(u >= -2 && u <= 2, "output within limits");
                y += (u - y) * 0.02 / 2;
                if (double.IsNaN(reached) && y >= 0.95)
                    reached = t;
            }
            Assert.True(reached < 20, "95% within 20 s");
        }

        private static PlutoRunController Engaged()
        {
            var run = new PlutoRunController(150);
            Assert.True(run.TryEngage(9000, 70000, 16), "engaged");
            return run;
        }

        public static void Run_EngageThenDiveThenFollow()
        {
            var run = Engaged();
            Assert.True(run.Phase == PlutoRunPhase.Dive, "dive");
            run.Update(0.02, 8850, 150, 9000, false, true, true, 16);
            Assert.True(run.Phase == PlutoRunPhase.Dive, "still diving high up");
            Assert.Equal(-PlutoConstants.MaxDiveDeg, run.TargetFlightPathDeg, 1e-9, "dive limited");
            run.Update(0.02, 160, 150, 160, false, true, true, 16);
            Assert.True(run.Phase == PlutoRunPhase.Follow, "follow at run height");
        }

        public static void Run_ManualInputAborts()
        {
            var run = Engaged();
            run.Update(0.02, 5000, 150, 5000, true, true, true, 16);
            Assert.True(run.Phase == PlutoRunPhase.Aborted, "aborted");
            Assert.True(!string.IsNullOrEmpty(run.Message), "message");
        }

        public static void Run_ScramAborts()
        {
            var run = Engaged();
            run.Update(0.02, 5000, 150, 5000, false, false, true, 16);
            Assert.True(run.Phase == PlutoRunPhase.Aborted, "aborted");
        }

        public static void Run_LeavingAtmosphereAborts()
        {
            var run = Engaged();
            run.Update(0.02, 5000, 150, 5000, false, true, false, 16);
            Assert.True(run.Phase == PlutoRunPhase.Aborted, "aborted");
        }

        public static void Run_DoneWhenWarheadsGone()
        {
            var run = Engaged();
            run.Update(0.02, 150, 150, 150, false, true, true, 0);
            Assert.True(run.Phase == PlutoRunPhase.Done, "done");
        }

        public static void Run_RefusesAboveAtmosphere()
        {
            var run = new PlutoRunController(150);
            Assert.True(!run.TryEngage(75000, 70000, 16), "refused");
            Assert.True(!string.IsNullOrEmpty(run.Message), "message");
            Assert.True(run.Phase == PlutoRunPhase.Idle, "idle");
        }

        public static void Run_RefusesAboveEngageCeiling()
        {
            var run = new PlutoRunController(150);
            Assert.True(!run.TryEngage(25000, 70000, 16), "refused above 20 km");
        }

        public static void Run_RefusesWithNoWarheads()
        {
            var run = new PlutoRunController(150);
            Assert.True(!run.TryEngage(9000, 70000, 0), "refused");
        }

        public static void Run_ClimbLimited()
        {
            var run = Engaged();
            run.Update(0.02, 100, 5000, 100, false, true, true, 16);
            Assert.Equal(PlutoConstants.MaxClimbDeg, run.TargetFlightPathDeg, 1e-9, "climb limited");
        }

        public static void Run_NoTargetHoldsHeading()
        {
            Assert.True(PlutoRunController.HeadingToTarget(false, 123) == null, "no target, no heading demand");
            Assert.Equal(123, PlutoRunController.HeadingToTarget(true, 123).Value, 1e-9, "target bearing");
        }
    }
}
