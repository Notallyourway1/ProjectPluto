namespace ProjectPluto
{
    // The belly scoop's shock system. Too fast and it "unstarts": the shock is expelled, airflow collapses,
    // and it only restarts after the missile has been slowed well below the limit for a moment.
    public sealed class PlutoInlet
    {
        public enum Change { None, Unstarted, Restarted }

        private double slowFor;

        public bool Started { get; private set; } = true;
        public double CaptureFactor => Started ? 1 : PlutoConstants.UnstartCapture;

        public Change Update(double dt, double mach)
        {
            if (Started)
            {
                if (mach <= PlutoConstants.UnstartMach)
                    return Change.None;
                Started = false;
                slowFor = 0;
                return Change.Unstarted;
            }

            slowFor = mach < PlutoConstants.RestartMach ? slowFor + dt : 0;
            if (slowFor < PlutoConstants.RestartSeconds - 1e-9)
                return Change.None;
            Started = true;
            return Change.Restarted;
        }
    }
}
