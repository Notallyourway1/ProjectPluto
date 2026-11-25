namespace ProjectPluto.Tests
{
    public static class LaunchPadTests
    {
        private static readonly PlutoVec Up = new PlutoVec(0, 1, 0);
        private static readonly PlutoVec East = new PlutoVec(1, 0, 0);

        public static void Launch_VerticalPointsNoseUp()
        {
            PlutoLaunchGeometry.TargetAxes(90, East, Up, out PlutoVec nose, out PlutoVec belly);
            Assert.Equal(1, nose.Y, 1e-9, "nose straight up");
            Assert.Equal(1, belly.X, 1e-9, "belly faces the heading");
        }

        public static void Launch_ElevationSetsNoseAngle()
        {
            PlutoLaunchGeometry.TargetAxes(45, East, Up, out PlutoVec nose, out PlutoVec belly);
            Assert.Equal(System.Math.Sqrt(0.5), nose.X, 1e-9, "nose forward");
            Assert.Equal(System.Math.Sqrt(0.5), nose.Y, 1e-9, "nose up 45°");
            Assert.True(belly.Y < 0, "belly faces down onto the pad");
            Assert.Equal(0, PlutoVec.Dot(nose, belly), 1e-9, "nose and belly at right angles");
            Assert.Equal(1, belly.Length, 1e-9, "unit belly");
        }

        public static void Launch_ElevationIsClamped()
        {
            PlutoLaunchGeometry.TargetAxes(0, East, Up, out PlutoVec nose, out _);
            Assert.Equal(System.Math.Sin(System.Math.PI / 12), nose.Y, 1e-9, "no lower than 15°");
        }

        public static void Launch_UprightMissileTipsTowardPad()
        {
            PlutoVec heading = PlutoLaunchGeometry.Heading(Up, new PlutoVec(0, 0, -1), Up);
            Assert.Equal(-1, heading.Z, 1e-9, "tips toward the pad side");
        }

        public static void Launch_TiltedMissileKeepsItsHeading()
        {
            PlutoVec heading = PlutoLaunchGeometry.Heading(new PlutoVec(0.5, 0.5, 0), new PlutoVec(0, 0, 1), Up);
            Assert.Equal(1, heading.X, 1e-9, "keeps the nose's heading");
        }

        public static void Launch_TowerReachesBelowCraft()
        {
            double length = PlutoLaunchGeometry.EditorTowerLength(10, 2);
            Assert.Equal(10 - (2 - PlutoLaunchGeometry.GroundClearanceM), length + PlutoLaunchGeometry.FootThicknessM,
                1e-9, "foot bottom sits the clearance below the lowest part");
            Assert.Equal(PlutoLaunchGeometry.MinTowerM, PlutoLaunchGeometry.EditorTowerLength(1, 5), 1e-9, "minimum");
            Assert.Equal(PlutoLaunchGeometry.MinTowerM, PlutoLaunchGeometry.FlightTowerLength(0.1), 1e-9, "flight minimum");
            Assert.Equal(7.6, PlutoLaunchGeometry.FlightTowerLength(8), 1e-9, "flight reaches the ground");
        }
    }
}
