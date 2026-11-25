using System;
using System.Collections.Generic;

namespace ProjectPluto
{
    public sealed class PlutoPid
    {
        private readonly double kp, ki, kd, min, max;
        private double integral, lastError;
        private bool hasLast;

        public PlutoPid(double kp, double ki, double kd, double min, double max)
        {
            this.kp = kp;
            this.ki = ki;
            this.kd = kd;
            this.min = min;
            this.max = max;
        }

        public double Update(double error, double dt)
        {
            if (dt <= 0)
                return Clamp(kp * error + ki * integral);
            double derivative = hasLast ? (error - lastError) / dt : 0;
            lastError = error;
            hasLast = true;

            double candidate = integral + error * dt;
            double output = kp * error + ki * candidate + kd * derivative;
            // Anti-windup: only keep integrating while the output is not pinned at a limit.
            if (output <= max && output >= min)
                integral = candidate;
            return Clamp(kp * error + ki * integral + kd * derivative);
        }

        public void Reset()
        {
            integral = 0;
            hasLast = false;
        }

        private double Clamp(double v) => v < min ? min : v > max ? max : v;
    }

    public static class PlutoTerrainLookahead
    {
        // Lowest altitude above sea level that keeps runHeight over every sample, assuming the missile
        // can climb no steeper than climbDeg toward samples further ahead. Sample i lies i × stepM ahead
        // (sample 0 is the ground directly below); pass 0 for water.
        public static double DesiredAltitudeAsl(IList<double> terrainAheadAsl, double runHeight, double stepM,
            double climbDeg)
        {
            double slope = Math.Tan(climbDeg * Math.PI / 180);
            double desired = double.NegativeInfinity;
            for (int i = 0; i < terrainAheadAsl.Count; i++)
                desired = Math.Max(desired, terrainAheadAsl[i] + runHeight - i * stepM * slope);
            return terrainAheadAsl.Count == 0 ? runHeight : desired;
        }
    }

    public enum PlutoRunPhase { Idle, Dive, Follow, Done, Aborted }

    public sealed class PlutoRunController
    {
        // Vertical acceleration the flight path may ask for when levelling off (1.5 g), and the time
        // constant for small height errors. Together they cap the sink rate so a Mach 3 dive can pull out.
        private const double LevelOffAccel = 1.5 * 9.81;
        private const double SmallErrorSeconds = 2;
        private const double MaxBankDeg = 60;

        private readonly double runHeight;

        public PlutoRunPhase Phase { get; private set; } = PlutoRunPhase.Idle;
        public string Message { get; private set; } = "";
        public double TargetFlightPathDeg { get; private set; }
        public bool Active => Phase == PlutoRunPhase.Dive || Phase == PlutoRunPhase.Follow;

        public PlutoRunController(double runHeight)
        {
            this.runHeight = Math.Max(runHeight, PlutoConstants.RunHeightMinM);
        }

        public bool TryEngage(double altitudeAsl, double atmosphereTop, int warheadsLeft)
        {
            if (Active)
                return true;
            if (warheadsLeft <= 0)
                return Refuse("No warheads left.");
            if (altitudeAsl >= atmosphereTop)
                return Refuse("Bombing run needs air: descend into the atmosphere first.");
            if (altitudeAsl > PlutoConstants.RunMaxEngageAltM)
                return Refuse($"Descend below {PlutoConstants.RunMaxEngageAltM / 1000:0} km to start the bombing run.");
            Phase = PlutoRunPhase.Dive;
            Message = "Bombing run: diving to run height.";
            return true;
        }

        public void Abort(string reason)
        {
            if (!Active)
                return;
            Phase = PlutoRunPhase.Aborted;
            Message = reason;
        }

        public void Update(double dt, double agl, double desiredAsl, double altitudeAsl, double speedMs,
            bool manualInput, bool reactorRunning, bool inAtmosphere, int warheadsLeft)
        {
            if (!Active)
                return;
            if (manualInput)
            {
                Abort("Bombing run aborted: manual control.");
                return;
            }
            if (!reactorRunning)
            {
                Abort("Bombing run aborted: reactor not running.");
                return;
            }
            if (!inAtmosphere)
            {
                Abort("Bombing run aborted: left the atmosphere.");
                return;
            }
            if (warheadsLeft <= 0)
            {
                Phase = PlutoRunPhase.Done;
                Message = "Bombing run complete: control returned.";
                return;
            }

            if (Phase == PlutoRunPhase.Dive && Math.Abs(agl - runHeight) < PlutoConstants.RunCaptureBandM)
            {
                Phase = PlutoRunPhase.Follow;
                Message = "Bombing run: following terrain.";
            }

            TargetFlightPathDeg = FlightPathFor(desiredAsl - altitudeAsl, speedMs);
        }

        // Flight-path angle for a height error: the vertical speed is limited so that it can be taken
        // out at LevelOffAccel by the time the error reaches zero, and eases off for small errors.
        public static double FlightPathFor(double heightErrorM, double speedMs)
        {
            double size = Math.Abs(heightErrorM);
            double vertical = Math.Sign(heightErrorM)
                * Math.Min(Math.Sqrt(2 * LevelOffAccel * size), size / SmallErrorSeconds);
            double ratio = Math.Max(-1, Math.Min(1, vertical / Math.Max(speedMs, 1)));
            double angle = Math.Asin(ratio) * 180 / Math.PI;
            return Math.Max(-PlutoConstants.MaxDiveDeg, Math.Min(PlutoConstants.MaxClimbDeg, angle));
        }

        // A warhead has been released on the target: the run is over.
        public void NotifyReleasedOnTarget()
        {
            if (!Active)
                return;
            Phase = PlutoRunPhase.Done;
            Message = "Warhead released on target: control returned.";
        }

        // Bank wanted for a heading error (degrees, as PlutoVec.SignedAngle(track, toTarget, up) gives it).
        // A bank measured about the forward axis has the opposite sign convention, hence the minus.
        public static double WantedBankDeg(double headingErrorDeg) =>
            -Math.Max(-MaxBankDeg, Math.Min(MaxBankDeg, headingErrorDeg * 2));

        // Pitch authority while banked: full when level, none at 90° or more (pulling would not lift the nose).
        public static double PitchScaleForBank(double bankDeg)
        {
            double wrapped = ((bankDeg + 180) % 360 + 360) % 360 - 180;
            return Math.Max(0, Math.Cos(wrapped * Math.PI / 180));
        }

        public static double? HeadingToTarget(bool hasTarget, double bearing) => hasTarget ? bearing : (double?)null;

        private bool Refuse(string reason)
        {
            Message = reason;
            return false;
        }
    }
}
