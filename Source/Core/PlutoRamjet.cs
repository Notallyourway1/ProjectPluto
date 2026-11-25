using System;

namespace ProjectPluto
{
    // Ideal nuclear ramjet. Air enters the scoop, is slowed (which heats it), takes up the reactor's heat
    // to no hotter than the core, and leaves faster than it came: thrust = airflow × speed gained.
    public static class PlutoRamjet
    {
        public const double Gamma = 1.4;
        public const double GasConstant = 287.05;   // J/(kg·K), air
        public const double SpecificHeat = 1005;     // J/(kg·K), air
        public const double CoreTempK = PlutoConstants.CoreTempFullC + 273.15;
        // Inlet and nozzle losses together (gameplay estimate).
        public const double Efficiency = 0.85;

        public const double MaxRatingKN = 5000;

        // Rated thrust to give KSP's engine, which multiplies it by the throttle it applies, so the result is
        // thrustN. Near-zero throttle gives no rating (and so no thrust), never a huge one.
        public static double EngineRatingKN(double thrustN, double throttle)
        {
            if (thrustN <= 0 || throttle < 0.005)
                return 0;
            return Math.Min(MaxRatingKN, thrustN / 1000 / Math.Max(throttle, 0.05));
        }

        public struct Result
        {
            public double MassFlowKgS;
            public double StagnationTempK;
            public double ExitTempK;
            public double HeatAddedW;
            public double ThrustN;
            // How much of the reactor's full power the air could carry away (0..1).
            public double CoolingFraction;
        }

        // gamma and gasConstant default to air; pass the body's own (Kopernicus atmospheres) when known.
        public static Result Compute(double reactorPowerW, double densityKgM3, double speedMs, double ambientTempK,
            double intakeAreaM2 = PlutoConstants.IntakeAreaM2, double gamma = Gamma, double gasConstant = GasConstant)
        {
            var r = new Result { StagnationTempK = ambientTempK, ExitTempK = ambientTempK };
            if (densityKgM3 <= 0 || speedMs <= 0 || ambientTempK <= 0)
                return r;

            double soundSpeed = Math.Sqrt(gamma * gasConstant * ambientTempK);
            double mach = speedMs / soundSpeed;
            r.StagnationTempK = ambientTempK * (1 + 0.5 * (gamma - 1) * mach * mach);
            r.MassFlowKgS = densityKgM3 * speedMs * intakeAreaM2;
            r.ExitTempK = r.StagnationTempK;

            double absorbable = r.MassFlowKgS * SpecificHeat * Math.Max(0, CoreTempK - r.StagnationTempK);
            r.CoolingFraction = Math.Min(1, absorbable / (PlutoConstants.ReactorMaxMW * 1e6));
            r.HeatAddedW = Math.Min(Math.Max(0, reactorPowerW), absorbable);
            if (r.HeatAddedW <= 0)
                return r;

            r.ExitTempK = r.StagnationTempK + r.HeatAddedW / (r.MassFlowKgS * SpecificHeat);
            double exitSpeed = speedMs * Math.Sqrt(r.ExitTempK / r.StagnationTempK);
            r.ThrustN = Efficiency * r.MassFlowKgS * (exitSpeed - speedMs);
            return r;
        }
    }
}
