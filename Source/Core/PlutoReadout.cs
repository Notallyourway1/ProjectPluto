using System.Globalization;

namespace ProjectPluto
{
    // One-line readouts for the right-click menu: a few rows refreshed a few times a second instead of
    // many rows formatted every frame.
    public static class PlutoReadout
    {
        public const string Group = "plutoReactor";
        public const string GroupTitle = "Reactor and engine";
        public const string WarheadGroup = "plutoWarheads";
        public const string WarheadGroupTitle = "Warheads";
        public const string AutopilotGroup = "plutoAutopilot";
        public const string AutopilotGroupTitle = "Autopilot";
        public const string StockGroup = "plutoStock";
        public const string StockGroupTitle = "Stock part settings";
        public const double RefreshSeconds = 0.25;

        public static string Reactor(string state, bool powered, double powerMW, double coreTempC) =>
            powered || coreTempC > PlutoConstants.AmbientC + 5
                ? string.Format(CultureInfo.InvariantCulture, "{0}, {1:0} MW, {2:0} °C", state, powerMW, coreTempC)
                : state;

        public static string Ramjet(bool powered, double thrustKN, double exhaustTempC, double airflow, bool inletStarted)
        {
            if (!inletStarted)
                return "INLET UNSTARTED: slow down";
            if (!powered)
                return "Off";
            return string.Format(CultureInfo.InvariantCulture, "{0:0} kN, exhaust {1:0} °C, cooling air {2:0}%",
                thrustKN, exhaustTempC, airflow * 100);
        }
    }
}
