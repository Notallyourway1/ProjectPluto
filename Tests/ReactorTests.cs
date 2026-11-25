namespace ProjectPluto.Tests
{
    public static class ReactorTests
    {
        private static void Run(PlutoReactorModel r, double seconds, double demand, double airflow)
        {
            for (double t = 0; t < seconds - 1e-9; t += 0.1)
                r.Update(0.1, demand, airflow);
        }

        private static PlutoReactorModel RunningReactor()
        {
            var r = new PlutoReactorModel();
            r.Start();
            Run(r, 20, 1, 1);
            return r;
        }

        public static void Reactor_StartsCold()
        {
            var r = new PlutoReactorModel();
            Assert.True(r.State == PlutoReactorState.Cold, "cold");
            Assert.Equal(0, r.PowerMW, 1e-9, "power");
        }

        public static void Reactor_WarmsToFullIn20s()
        {
            var r = RunningReactor();
            Assert.Equal(600, r.PowerMW, 1, "power");
            Assert.True(r.State == PlutoReactorState.Running, "running");
        }

        public static void Reactor_FullPowerTempIs1277()
        {
            var r = RunningReactor();
            Run(r, 120, 1, 1);
            Assert.Equal(1277, r.CoreTempC, 5, "temp");
        }

        // The boost phase has little airflow; the reactor must not SCRAM on the pad while warming.
        public static void Reactor_WarmingIgnoresAirflow()
        {
            var r = new PlutoReactorModel();
            r.Start();
            Run(r, 20, 1, 0);
            Assert.True(r.State == PlutoReactorState.Running, "running after warm-up");
        }

        public static void Reactor_NoAirflowScrams()
        {
            var r = RunningReactor();
            Run(r, 60, 1, 0);
            Assert.True(r.State == PlutoReactorState.Scrammed, "scrammed");
            Assert.Equal(0, r.PowerMW, 1e-9, "power");
        }

        public static void Reactor_ManualScram()
        {
            var r = RunningReactor();
            r.Scram();
            Assert.True(r.State == PlutoReactorState.Scrammed, "scrammed");
            Assert.Equal(0, r.PowerMW, 1e-9, "power");
        }

        public static void Reactor_RestartNeedsCooldown()
        {
            var r = RunningReactor();
            Run(r, 60, 1, 1);
            r.Scram();
            r.Restart();
            Assert.True(r.State == PlutoReactorState.Scrammed, "still scrammed while hot");
            Run(r, 300, 1, 1);
            r.Restart();
            Assert.True(r.State == PlutoReactorState.Warming, "warming after cooldown");
        }

        public static void Reactor_BreachStopsPower()
        {
            var r = RunningReactor();
            r.Breach();
            Run(r, 1, 1, 1);
            Assert.True(r.State == PlutoReactorState.Breached, "breached");
            Assert.Equal(0, r.PowerMW, 1e-9, "power");
        }

        public static void Reactor_SaveLoadRoundTrip()
        {
            var r = RunningReactor();
            Run(r, 7.3, 0.6, 1);
            var copy = PlutoReactorModel.Load(r.Save());
            Assert.True(copy.State == r.State, "state");
            Assert.Equal(r.PowerFraction, copy.PowerFraction, 1e-9, "power");
            Assert.Equal(r.CoreTempC, copy.CoreTempC, 1e-9, "temp");
        }

        public static void Reactor_LoadOfGarbageIsCold()
        {
            var r = PlutoReactorModel.Load("not a save");
            Assert.True(r.State == PlutoReactorState.Cold, "cold");
        }
    }
}
