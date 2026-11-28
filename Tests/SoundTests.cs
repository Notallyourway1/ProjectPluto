namespace ProjectPluto.Tests
{
    public static class SoundTests
    {
        public static void Boom_LouderCloser()
        {
            Assert.True(PlutoSoundModel.BoomLoudness(500) > PlutoSoundModel.BoomLoudness(5000), "closer is louder");
            Assert.Equal(0.5, PlutoSoundModel.BoomLoudness(PlutoSoundModel.HalfLoudnessM), 1e-9, "half loudness");
            Assert.Equal(0, PlutoSoundModel.BoomLoudness(PlutoSoundModel.MaxHearRangeM), 1e-12, "silent at range");
            Assert.True(PlutoSoundModel.BoomLoudness(PlutoSoundModel.MaxHearRangeM - 1) < 0.001, "fades to the edge");
        }

        public static void Boom_FarIsMuffled()
        {
            Assert.True(PlutoSoundModel.BoomCutoffHz(0, false) > 20000, "open close by");
            Assert.True(PlutoSoundModel.BoomCutoffHz(30000, false) < 1000, "rumble far away");
            Assert.True(PlutoSoundModel.BoomCutoffHz(0, true) <= PlutoSoundModel.CabinCutoffHz, "muffled in the cabin");
        }

        public static void Boom_ArrivesAtSpeedOfSound()
        {
            Assert.True(!PlutoSoundModel.BoomArrived(3400, 9.9, 340), "not yet at 9.9 s");
            Assert.True(PlutoSoundModel.BoomArrived(3400, 10.1, 340), "arrived at 10.1 s");
            Assert.True(PlutoSoundModel.BoomArrived(3400, 10.1, 0), "fallback speed when unknown");
            Assert.True(PlutoSoundModel.BoomExpired(200, 340), "front past hearing range");
        }

        // A camera on a Mach 3 missile flying away from the burst never hears it.
        public static void Boom_OutrunBySupersonicListener()
        {
            bool heard = false;
            for (double t = 0; t < 170; t += 0.5)
                heard |= PlutoSoundModel.BoomArrived(500 + 1020 * t, t, 340);
            Assert.True(!heard, "outran the sound");
        }

        public static void Geiger_ClicksRiseWithDose()
        {
            Assert.Equal(0, PlutoSoundModel.GeigerClicksPerSecond(1e-7), 1e-12, "quiet below threshold");
            Assert.Equal(0, PlutoSoundModel.GeigerClicksPerSecond(double.NaN), 1e-12, "NaN is quiet");
            Assert.Equal(20, PlutoSoundModel.GeigerClicksPerSecond(1e-4), 1e-9, "two decades up");
            Assert.Equal(PlutoSoundModel.GeigerMaxClicks, PlutoSoundModel.GeigerClicksPerSecond(1e9), 1e-9, "capped");
        }

        public static void Ramjet_SoundFollowsThrust()
        {
            Assert.Equal(0, PlutoSoundModel.RamjetLevel(0, 1.5), 1e-12, "no thrust, no sound");
            Assert.Equal(0, PlutoSoundModel.RamjetLevel(-5, 1.5), 1e-12, "never negative");
            Assert.Equal(0.5, PlutoSoundModel.RamjetLevel(150e3, 1), 1e-9, "half");
            Assert.Equal(1, PlutoSoundModel.RamjetLevel(1e9, 1), 1e-12, "capped");
        }
    }
}
