namespace ProjectPluto.Tests
{
    public static class TrailLookTests
    {
        public static void TrailLook_SpreadsWithAge()
        {
            Assert.Equal(PlutoTrailLook.NozzleWidthM, PlutoTrailLook.WidthM(0), 1e-9, "thin at the nozzle");
            Assert.True(PlutoTrailLook.WidthM(60) > PlutoTrailLook.WidthM(10), "wider when older");
            Assert.Equal(PlutoTrailLook.SpreadWidthM, PlutoTrailLook.WidthM(1e6), 1e-9, "capped");
        }

        public static void TrailLook_FadesInThenOut()
        {
            Assert.Equal(0, PlutoTrailLook.Alpha(0), 1e-9, "no hard edge at the nozzle");
            Assert.True(PlutoTrailLook.Alpha(1) > PlutoTrailLook.Alpha(0.1), "thickens into view");
            Assert.True(PlutoTrailLook.Alpha(120) < PlutoTrailLook.Alpha(5), "fades with age");
            Assert.Equal(0, PlutoTrailLook.Alpha(PlutoConstants.TrailLifeSeconds), 1e-9, "gone at end of life");
            Assert.True(PlutoTrailLook.Alpha(1) <= PlutoTrailLook.PeakAlpha, "never above peak");
        }

        public static void TrailLook_CoolsToBlueGrey()
        {
            PlutoTrailLook.Colour(0, out double r0, out _, out double b0);
            PlutoTrailLook.Colour(120, out double r1, out _, out double b1);
            Assert.True(r0 > b0, "warm when fresh");
            Assert.True(b1 > r1, "blue-grey when old");
        }
    }
}
