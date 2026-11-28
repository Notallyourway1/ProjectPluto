using System;
using System.Globalization;

namespace ProjectPluto
{
    public enum PlutoReactorState { Cold, Warming, Running, Scrammed, Breached }

    // Game-independent reactor: power ramps toward the throttle demand, the core temperature relaxes
    // toward a level set by power and airflow, and lost airflow while running overheats it into a SCRAM.
    public sealed class PlutoReactorModel
    {
        public PlutoReactorState State { get; private set; } = PlutoReactorState.Cold;
        public double PowerFraction { get; private set; }
        public double CoreTempC { get; private set; } = PlutoConstants.AmbientC;
        public double PowerMW => PowerFraction * PlutoConstants.ReactorMaxMW;
        public bool IsPowered => State == PlutoReactorState.Warming || State == PlutoReactorState.Running;

        public void Start()
        {
            if (State == PlutoReactorState.Cold)
                State = PlutoReactorState.Warming;
        }

        public void Scram()
        {
            if (!IsPowered)
                return;
            State = PlutoReactorState.Scrammed;
            PowerFraction = 0;
        }

        public void Restart()
        {
            if (State == PlutoReactorState.Scrammed && CoreTempC < PlutoConstants.RestartTempC)
                State = PlutoReactorState.Warming;
        }

        public void Breach()
        {
            State = PlutoReactorState.Breached;
            PowerFraction = 0;
        }

        public void Update(double dt, double demand, double airflowFraction)
        {
            if (dt <= 0)
                return;

            if (IsPowered)
            {
                double target = Clamp01(demand);
                PowerFraction = PowerFraction < target
                    ? Math.Min(target, PowerFraction + dt / PlutoConstants.WarmupSeconds)
                    : Math.Max(target, PowerFraction - dt * PlutoConstants.PowerDropPerSecond);
                if (State == PlutoReactorState.Warming && PowerFraction >= target)
                    State = PlutoReactorState.Running;
            }

            // While warming the boost phase has little airflow; treat cooling as adequate (design ruling).
            double cooling = State == PlutoReactorState.Running ? Math.Max(Clamp01(airflowFraction), 0.05) : 1;
            double equilibrium = PlutoConstants.AmbientC
                + (PlutoConstants.CoreTempFullC - PlutoConstants.AmbientC) * PowerFraction / cooling;
            CoreTempC += (equilibrium - CoreTempC) * (1 - Math.Exp(-dt / PlutoConstants.TempTimeConstant));

            if (State == PlutoReactorState.Running && CoreTempC > PlutoConstants.ScramTempC)
                Scram();
        }

        // Power wanted this frame. During the boost the reactor warms to full power whatever the throttle
        // (design ruling); a restart in flight follows the throttle and the airflow like normal running.
        public static double Demand(PlutoReactorState state, bool boostersAttached, double throttle, double airflowFraction) =>
            state == PlutoReactorState.Warming && boostersAttached ? 1 : GovernedDemand(throttle, airflowFraction);

        // Throttle demand limited to what the current airflow can cool at design temperature.
        public static double GovernedDemand(double throttle, double airflowFraction) =>
            Math.Min(Clamp01(throttle), Clamp01(airflowFraction));

        // Heat flowing into the missile's structure, in kW.
        public static double StructureHeatKW(double powerMW, double coreTempC) =>
            Math.Max(0, powerMW) * 1000 * PlutoConstants.StructureLeakFraction
            + Math.Max(0, coreTempC - PlutoConstants.CoreTempFullC) * PlutoConstants.HotCoreKWPerK;

        public string Save() => string.Join(";",
            State.ToString(),
            PowerFraction.ToString("R", CultureInfo.InvariantCulture),
            CoreTempC.ToString("R", CultureInfo.InvariantCulture));

        public static PlutoReactorModel Load(string saved)
        {
            var model = new PlutoReactorModel();
            string[] parts = (saved ?? "").Split(';');
            if (parts.Length == 3
                && Enum.TryParse(parts[0], out PlutoReactorState state)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double power)
                && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double temp))
            {
                model.State = state;
                model.PowerFraction = Clamp01(power);
                model.CoreTempC = temp;
            }
            return model;
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
