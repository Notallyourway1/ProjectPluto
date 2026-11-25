using System;

namespace ProjectPluto.Tests
{
    public static class TrailTests
    {
        private static readonly PlutoVec Origin = new PlutoVec(0, 0, 0);

        public static void Trail_AddsOnePointPerInterval()
        {
            var trail = new PlutoTrailModel();
            for (int i = 0; i < 500; i++)
                trail.AddIfDue(Origin, 600, i * 0.02);
            Assert.Equal(10, trail.Count, 0, "one point per second");
        }

        public static void Trail_NoPointsWithoutPower()
        {
            var trail = new PlutoTrailModel();
            trail.AddIfDue(Origin, 0, 0);
            Assert.Equal(0, trail.Count, 0, "cold reactor leaves no trail");
        }

        public static void Trail_StrengthDecays()
        {
            var trail = new PlutoTrailModel();
            trail.AddIfDue(Origin, 600, 0);
            double fresh = trail.StrengthMW(0, 0);
            Assert.Equal(600 * PlutoConstants.TrailFraction, fresh, 1e-9, "fresh strength");
            Assert.Equal(fresh / 2, trail.StrengthMW(0, PlutoConstants.TrailDecaySeconds * Math.Log(2)), 1e-6, "half-life");
        }

        public static void Trail_PointsExpire()
        {
            var trail = new PlutoTrailModel();
            trail.AddIfDue(Origin, 600, 0);
            trail.Prune(PlutoConstants.TrailLifeSeconds + 1);
            Assert.Equal(0, trail.Count, 0, "expired");
        }

        public static void Trail_CappedLength()
        {
            var trail = new PlutoTrailModel();
            for (double t = 0; t < 10000; t += 1)
                trail.AddIfDue(Origin, 600, t);
            Assert.True(trail.Count <= PlutoTrailModel.MaxPoints, "capped");
        }
    }
}
