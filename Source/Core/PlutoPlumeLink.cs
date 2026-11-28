using System;

namespace ProjectPluto
{
    // Keeps one value of another mod's effect (a Waterfall plume controller) in step with the plugin.
    // The value already there is checked on every call rather than remembered, because the other mod
    // can rebuild its controllers (back to 0) at any time, and it is not ready until that mod has set
    // itself up. An error from the other mod switches the link off instead of reaching the caller.
    public sealed class PlutoPlumeLink
    {
        public enum Outcome { Sent, Unchanged, NotReady, GaveUp, Failed, Off }

        // Calls (physics frames, about 10 s) to wait for the controller before deciding it is missing.
        public const int GiveUpCalls = 500;

        private readonly string controller;
        private readonly Func<string, float[]> getValues;
        private readonly Action<string, float> setValue;
        private int waited;

        public PlutoPlumeLink(string controller, Func<string, float[]> getValues, Action<string, float> setValue)
        {
            this.controller = controller;
            this.getValues = getValues;
            this.setValue = setValue;
        }

        public bool IsOff { get; private set; }
        public Exception Error { get; private set; }

        public Outcome Push(float value)
        {
            if (IsOff)
                return Outcome.Off;
            if (float.IsNaN(value))
                value = 0f;
            try
            {
                float[] current = getValues(controller);
                if (current == null || current.Length == 0)
                {
                    if (++waited < GiveUpCalls)
                        return Outcome.NotReady;
                    IsOff = true;
                    return Outcome.GaveUp;
                }
                waited = 0;
                if (current[0] == value)
                    return Outcome.Unchanged;
                setValue(controller, value);
                return Outcome.Sent;
            }
            catch (Exception e)
            {
                IsOff = true;
                Error = e;
                return Outcome.Failed;
            }
        }
    }
}
