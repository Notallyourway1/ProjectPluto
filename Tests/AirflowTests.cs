namespace ProjectPluto.Tests
{
    public static class AirflowTests
    {
        // Full cooling = Tory II-C's 910 kg/s through a 0.8 m² intake: sea level at about 929 m/s.
        public static void Airflow_FullAtSeaLevelMach27()
        {
            Assert.Equal(1, PlutoReactorModel.AirflowFraction(1.225, 910 / (1.225 * 0.8)), 1e-9, "full");
        }

        public static void Airflow_ZeroWhenStoppedOrInVacuum()
        {
            Assert.Equal(0, PlutoReactorModel.AirflowFraction(1.225, 0), 1e-9, "stopped");
            Assert.Equal(0, PlutoReactorModel.AirflowFraction(0, 1000), 1e-9, "vacuum");
        }

        public static void Airflow_CappedAtOne()
        {
            Assert.Equal(1, PlutoReactorModel.AirflowFraction(1.225, 3000), 1e-9, "capped");
        }

        // The governor keeps demand within what the airflow can cool, so high cruise does not SCRAM.
        public static void Airflow_GovernedDemandNeverOverheats()
        {
            var r = new PlutoReactorModel();
            r.Start();
            for (int i = 0; i < 3000; i++)
                r.Update(0.1, PlutoReactorModel.GovernedDemand(1, 0.38), 0.38);
            Assert.True(r.State == PlutoReactorState.Running, "still running");
            Assert.True(r.CoreTempC <= PlutoConstants.CoreTempFullC + 1, "within design temperature");
        }
    }
}
