using System;

namespace ProjectPluto
{
    // Geometry for the Pluto launch pad: where the missile's nose and belly point at a launch elevation,
    // and how long the pad's support tower must be to reach the ground.
    public static class PlutoLaunchGeometry
    {
        public const double MinElevationDeg = 15;
        public const double MaxElevationDeg = 90;
        public const double DefaultElevationDeg = 45;
        // The pad's foot hangs this far below the missile's lowest point, so the tail clears the ground.
        public const double GroundClearanceM = 1.0;
        // Thickness of the foot plate under the tower.
        public const double FootThicknessM = 0.4;
        public const double MinTowerM = 0.5;

        // Nose and belly directions for a launch at elevationDeg toward the horizontal heading.
        // The belly faces down the rail, so the pad sits underneath.
        public static void TargetAxes(double elevationDeg, PlutoVec heading, PlutoVec up,
            out PlutoVec nose, out PlutoVec belly)
        {
            double e = Math.PI / 180 * Math.Max(MinElevationDeg, Math.Min(MaxElevationDeg, elevationDeg));
            nose = heading * Math.Cos(e) + up * Math.Sin(e);
            belly = heading * Math.Sin(e) - up * Math.Cos(e);
        }

        // Horizontal heading the launch keeps: where the nose already points, or (missile upright) the
        // side the pad is on, so the missile tips over onto the pad.
        public static PlutoVec Heading(PlutoVec nose, PlutoVec padSide, PlutoVec up)
        {
            PlutoVec flat = nose - up * PlutoVec.Dot(nose, up);
            if (flat.Length < 0.05)
                flat = padSide - up * PlutoVec.Dot(padSide, up);
            double length = flat.Length;
            return length < 1e-9 ? new PlutoVec(1, 0, 0) : flat * (1 / length);
        }

        // Editor: tower length so the foot sits GroundClearanceM below the craft's lowest point.
        public static double EditorTowerLength(double pivotHeight, double craftLowest) =>
            Math.Max(MinTowerM, pivotHeight - craftLowest + GroundClearanceM - FootThicknessM);

        // Flight: tower length for the measured distance from the pivot to the ground.
        public static double FlightTowerLength(double groundDistance) =>
            Math.Max(MinTowerM, groundDistance - FootThicknessM);
    }
}
