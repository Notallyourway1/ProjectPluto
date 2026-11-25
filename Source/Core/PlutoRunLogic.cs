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
        // Horizontal distance over which an altitude error is corrected when choosing a flight-path angle.
        private const double CorrectionDistanceM = 2000;

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

        public void Update(double dt, double agl, double desiredAsl, double altitudeAsl, bool manualInput,
            bool reactorRunning, bool inAtmosphere, int warheadsLeft)
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

            double angle = Math.Atan2(desiredAsl - altitudeAsl, CorrectionDistanceM) * 180 / Math.PI;
            TargetFlightPathDeg = Math.Max(-PlutoConstants.MaxDiveDeg, Math.Min(PlutoConstants.MaxClimbDeg, angle));
        }

        public static double? HeadingToTarget(bool hasTarget, double bearing) => hasTarget ? bearing : (double?)null;

        private bool Refuse(string reason)
        {
            Message = reason;
            return false;
        }
    }
}
