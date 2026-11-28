using System;

namespace ProjectPluto.Tests
{
    public static class PlumeLinkTests
    {
        // A stand-in for Waterfall's controller table: null until set up, then a one-value array.
        private sealed class FakePlume
        {
            public float[] Values;
            public int Sets;
            public bool Missing;
            public float[] Get(string name) => Missing ? new float[0] : name == "throttle" ? Values : new float[0];
            public void Set(string name, float value)
            {
                Sets++;
                for (int i = 0; i < Values.Length; i++)
                    Values[i] = value;
            }
        }

        private static PlutoPlumeLink LinkTo(FakePlume plume) => new PlutoPlumeLink("throttle", plume.Get, plume.Set);

        public static void Waits_UntilPlumeIsSetUp()
        {
            var plume = new FakePlume();
            var link = LinkTo(plume);
            Assert.True(link.Push(0.5f) == PlutoPlumeLink.Outcome.NotReady, "not set up yet");
            Assert.Equal(0, plume.Sets, 0, "nothing sent before set-up");
            plume.Values = new float[1];
            Assert.True(link.Push(0.5f) == PlutoPlumeLink.Outcome.Sent, "sent once set up");
            Assert.Equal(0.5, plume.Values[0], 1e-6, "plume holds the level");
        }

        public static void SendsOnlyChanges()
        {
            var plume = new FakePlume { Values = new float[1] };
            var link = LinkTo(plume);
            link.Push(0.7f);
            Assert.True(link.Push(0.7f) == PlutoPlumeLink.Outcome.Unchanged, "same level not resent");
            Assert.True(link.Push(0f) == PlutoPlumeLink.Outcome.Sent, "drop to zero sent");
            Assert.Equal(2, plume.Sets, 0, "two sends");
        }

        // Waterfall can rebuild its controllers (back to 0) at any time; the level must be sent again.
        public static void ResendsAfterPlumeIsRebuilt()
        {
            var plume = new FakePlume { Values = new float[1] };
            var link = LinkTo(plume);
            link.Push(0.9f);
            plume.Values = new float[1];
            Assert.True(link.Push(0.9f) == PlutoPlumeLink.Outcome.Sent, "resent after rebuild");
            Assert.Equal(0.9, plume.Values[0], 1e-6, "level restored");
        }

        public static void GivesUp_WhenControllerNeverAppears()
        {
            var plume = new FakePlume { Missing = true };
            var link = LinkTo(plume);
            for (int i = 1; i < PlutoPlumeLink.GiveUpCalls; i++)
                Assert.True(link.Push(1f) == PlutoPlumeLink.Outcome.NotReady, "still waiting");
            Assert.True(link.Push(1f) == PlutoPlumeLink.Outcome.GaveUp, "gives up once");
            Assert.True(link.Push(1f) == PlutoPlumeLink.Outcome.Off, "then stays off");
            Assert.True(link.IsOff, "off");
        }

        // An error inside the other mod must not reach the reactor's physics update.
        public static void TurnsOff_WhenPlumeThrows()
        {
            var link = new PlutoPlumeLink("throttle", _ => throw new InvalidOperationException("boom"), (n, v) => { });
            Assert.True(link.Push(1f) == PlutoPlumeLink.Outcome.Failed, "failure reported");
            Assert.True(link.Error is InvalidOperationException, "error kept for the log");
            Assert.True(link.Push(1f) == PlutoPlumeLink.Outcome.Off, "then off");
        }

        public static void NaN_IsSentAsZero()
        {
            var plume = new FakePlume { Values = new[] { 0.4f } };
            var link = LinkTo(plume);
            Assert.True(link.Push(float.NaN) == PlutoPlumeLink.Outcome.Sent, "sent");
            Assert.Equal(0, plume.Values[0], 0, "as zero");
            Assert.True(link.Push(float.NaN) == PlutoPlumeLink.Outcome.Unchanged, "not resent every frame");
        }
    }
}
