using System;

namespace ProjectPluto
{
    public enum PlutoTakeoffPhase { Idle, ClearRail, Climb, Cruise, Ended }

    // Take-off autopilot: flies straight off the launcher, climbs to the cruise height (above the terrain
    // ahead) while the boosters burn, keeps speed up once the ramjet takes over, then cruises level until
    // the player moves the controls or starts the bombing run.
    public sealed class PlutoTakeoffController
    {
        // Straight off the rail for this long, and until this fast, before any steering.
        public const double ClearRailSeconds = 1.5;
        public const double ClearRailSpeedMs = 80;
        // Below this speed with the boosters gone the ramjet is weak, so the climb is held to about level.
        public const double SlowSpeedMs = 400;
        public const double SlowClimbDeg = 2;
        public const double CruiseCaptureM = 50;
        public const double MinCruiseHeightM = 300;
        public const double MaxCruiseHeightM = 5000;

        private readonly double cruiseHeight;
        private double railTime;

        public PlutoTakeoffPhase Phase { get; private set; } = PlutoTakeoffPhase.Idle;
        public string Message { get; private set; } = "";
        public double TargetFlightPathDeg { get; private set; }
        public bool Flying => Phase == PlutoTakeoffPhase.ClearRail || Phase == PlutoTakeoffPhase.Climb
            || Phase == PlutoTakeoffPhase.Cruise;
        public bool Steering => Phase == PlutoTakeoffPhase.Climb || Phase == PlutoTakeoffPhase.Cruise;
        public double CruiseHeight => cruiseHeight;

        public PlutoTakeoffController(double cruiseHeight)
        {
            this.cruiseHeight = Math.Max(MinCruiseHeightM, Math.Min(MaxCruiseHeightM, cruiseHeight));
        }

        public void Start()
        {
            if (Phase != PlutoTakeoffPhase.Idle)
                return;
            Phase = PlutoTakeoffPhase.ClearRail;
            railTime = 0;
            Message = $"Take-off autopilot: climbing to {cruiseHeight:0} m. Move the controls to take over.";
        }

        public void End(string reason)
        {
            if (!Flying)
                return;
            Phase = PlutoTakeoffPhase.Ended;
            Message = reason;
        }

        public void Update(double dt, double altitudeAsl, double desiredAsl, double speedMs, bool manualInput,
            bool boostersGone, bool reactorRunning, bool inAtmosphere)
        {
            if (!Flying)
                return;
            if (manualInput)
            {
                End("Take-off autopilot off: manual control.");
                return;
            }
            if (!inAtmosphere)
            {
                End("Take-off autopilot off: left the atmosphere.");
                return;
            }
            if (boostersGone && !reactorRunning)
            {
                End("Take-off autopilot off: reactor not running.");
                return;
            }

            if (Phase == PlutoTakeoffPhase.ClearRail)
            {
                railTime += dt;
                if (railTime < ClearRailSeconds || speedMs < ClearRailSpeedMs)
                    return;
                Phase = PlutoTakeoffPhase.Climb;
            }

            double error = desiredAsl - altitudeAsl;
            double target = PlutoRunController.FlightPathFor(error, speedMs);
            if (boostersGone && speedMs < SlowSpeedMs)
                target = Math.Min(target, SlowClimbDeg);
            TargetFlightPathDeg = target;

            if (Phase == PlutoTakeoffPhase.Climb && boostersGone && Math.Abs(error) < CruiseCaptureM)
            {
                Phase = PlutoTakeoffPhase.Cruise;
                Message = $"Take-off complete: cruising {cruiseHeight:0} m above the ground. Move the controls to take over.";
            }
        }
    }
}
