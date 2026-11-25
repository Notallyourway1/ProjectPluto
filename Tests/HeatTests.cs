namespace ProjectPluto.Tests
{
    public static class HeatTests
    {
        public static void Heat_NoneWhenCold()
        {
            Assert.Equal(0, PlutoReactorModel.StructureHeatKW(0, PlutoConstants.AmbientC), 1e-9, "cold");
        }

        public static void Heat_SmallLeakAtDesignTemperature()
        {
            Assert.Equal(1200, PlutoReactorModel.StructureHeatKW(600, PlutoConstants.CoreTempFullC), 1e-6, "0.2% of 600 MW");
        }

        public static void Heat_OverheatedCoreSoaksAirframe()
        {
            double normal = PlutoReactorModel.StructureHeatKW(600, PlutoConstants.CoreTempFullC);
            double hot = PlutoReactorModel.StructureHeatKW(600, PlutoConstants.CoreTempFullC + 100);
            Assert.Equal(normal + 100 * PlutoConstants.HotCoreKWPerK, hot, 1e-6, "extra per degree");
        }

        public static void Heat_ScrammedHotCoreStillHeats()
        {
            Assert.True(PlutoReactorModel.StructureHeatKW(0, 1400) > 0, "decay of a hot core");
        }
    }
}
