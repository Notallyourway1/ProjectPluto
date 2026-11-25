namespace ProjectPluto
{
    // Every number from the design in one place. Historical values are marked; the rest are gameplay choices.
    public static class PlutoConstants
    {
        // Reactor (historical: 600 MW design power, ~1,277 °C fuel element temperature).
        public const double ReactorMaxMW = 600;
        public const double CoreTempFullC = 1277;
        public const double AmbientC = 20;
        public const double ScramTempC = 1500;
        public const double RestartTempC = 800;
        public const double WarmupSeconds = 20;
        public const double TempTimeConstant = 10;

        // Radiation: dose rate = DoseK × (power / 600 MW) / r², in Sv/s with r in metres.
        // Calibrated so a Mach 3 pass 150 m overhead at full power gives about 0.28 Sv.
        public const double DoseK = 13600;
        public const double DoseWarn1 = 0.1;
        public const double DoseWarn2 = 1;
        public const double DoseLethal = 6;
        public const double BreachPowerThreshold = 0.1;
        public const double BreachStrength = 0.1;
        public const double BreachSeconds = 600;

        // Warheads (historical: 14–26 carried, usually given as 16).
        public const int WarheadCount = 16;
        public const double WarheadMassT = 0.5;
        public const double AutoReleaseSeconds = 60;
        public const double TossUpMs = 30;
        public const double WarheadArmSeconds = 2;
        public const double WarheadSafetySeconds = 120;
        public const double TossMaxCrossTrackM = 400;

        // 1 Mt effect sizes on Kerbin.
        public const double FireballRadiusM = 280;
        public const double CloudTopM = 3000;
        public const double DamageRadiusM = 500;

        // Bombing run.
        public const double RunHeightM = 150;
        public const double RunHeightMinM = 100;
        public const double RunMach = 3;
        public const double LookaheadStepM = 250;
        public const double LookaheadRangeM = 5000;
        public const double MaxG = 6;
        public const double MaxClimbDeg = 30;
        public const double MaxDiveDeg = 15;
        public const double RunMaxEngageAltM = 20000;
        public const double RunCaptureBandM = 30;
    }
}
