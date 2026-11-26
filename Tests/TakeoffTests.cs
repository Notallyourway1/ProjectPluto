namespace ProjectPluto.Tests
{
    public static class TakeoffTests
    {
        private static PlutoTakeoffController Started(double height = 1000)
        {
            var t = new PlutoTakeoffController(height);
            t.Start();
            return t;
        }

        public static void Takeoff_ClearsRailBeforeSteering()
        {
            var t = Started();
            t.Update(1.0, 100, 1100, 200, false, false, false, true);
            Assert.True(t.Phase == PlutoTakeoffPhase.ClearRail, "still on the rail after 1 s");
            t.Update(1.0, 150, 1100, 50, false, false, false, true);
            Assert.True(t.Phase == PlutoTakeoffPhase.ClearRail, "too slow to steer");
            t.Update(0.1, 200, 1100, 150, false, false, false, true);
            Assert.True(t.Phase == PlutoTakeoffPhase.Climb, "climbing");
            Assert.True(t.TargetFlightPathDeg > 0, "climbs toward cruise height");
        }

        public static void Takeoff_HoldsLevelWhenSlowAfterBurnout()
        {
            var t = Started();
            t.Update(2, 100, 1100, 150, false, false, false, true);
            t.Update(0.02, 200, 1100, 300, false, true, true, true);
            Assert.True(t.TargetFlightPathDeg <= PlutoTakeoffController.SlowClimbDeg + 1e-9, "no climb while slow");
            t.Update(0.02, 200, 1100, 700, false, true, true, true);
            Assert.True(t.TargetFlightPathDeg > PlutoTakeoffController.SlowClimbDeg, "climbs once fast");
        }

        public static void Takeoff_CruisesOnlyAfterBurnout()
        {
            var t = Started();
            t.Update(2, 1090, 1100, 600, false, false, false, true);
            Assert.True(t.Phase == PlutoTakeoffPhase.Climb, "boosters still burning");
            t.Update(0.02, 1090, 1100, 600, false, true, true, true);
            Assert.True(t.Phase == PlutoTakeoffPhase.Cruise, "cruising");
            Assert.True(t.Flying, "still flying the missile in cruise");
        }

        public static void Takeoff_ManualInputEndsIt()
        {
            var t = Started();
            t.Update(0.02, 100, 1100, 50, true, false, false, true);
            Assert.True(t.Phase == PlutoTakeoffPhase.Ended && !t.Flying, "manual ends");
        }

        public static void Takeoff_ReactorLossEndsItOnlyAfterBurnout()
        {
            var t = Started();
            t.Update(2, 100, 1100, 200, false, false, false, true);
            Assert.True(t.Flying, "reactor still warming during the boost");
            t.Update(0.02, 100, 1100, 600, false, true, false, true);
            Assert.True(!t.Flying, "reactor out after burnout ends it");
        }

        public static void Takeoff_CruiseHeightClamped()
        {
            Assert.Equal(PlutoTakeoffController.MinCruiseHeightM, new PlutoTakeoffController(10).CruiseHeight, 1e-9, "min");
            Assert.Equal(PlutoTakeoffController.MaxCruiseHeightM, new PlutoTakeoffController(1e6).CruiseHeight, 1e-9, "max");
        }
    }
}
