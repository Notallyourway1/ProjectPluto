namespace ProjectPluto.Tests
{
    public static class AirflowTests
    {
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
