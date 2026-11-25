using System;

namespace ProjectPluto.Tests
{
    public static class ReviewFixTests
    {
        private static PlutoVec Normalize(PlutoVec v) => v * (1 / v.Length);

        // Critical 1: for a target on the right, the commanded bank must be the same sign as the bank
        // angle measured when the missile is actually rolled to the right.
        public static void Bank_TurnsTowardTarget()
        {
            var up = new PlutoVec(0, 1, 0);
            var forward = new PlutoVec(0, 0, 1);
            foreach (var right in new[] { new PlutoVec(1, 0, 0), new PlutoVec(-1, 0, 0) })
            {
                double headingError = PlutoVec.SignedAngle(forward, right, up);
                double wanted = PlutoRunController.WantedBankDeg(headingError);
                PlutoVec rolledTop = Normalize(up + right * 0.3);
                double measured = PlutoVec.SignedAngle(up, rolledTop, forward);
                Assert.True(Math.Sign(wanted) == Math.Sign(measured), "bank toward target");
            }
        }

        public static void Bank_LimitedTo60()
        {
            Assert.Equal(60, Math.Abs(PlutoRunController.WantedBankDeg(170)), 1e-9, "limit");
            Assert.Equal(0, PlutoRunController.WantedBankDeg(0), 1e-9, "level when on track");
        }

        // Critical 2: a point-mass dive from 5 km at 1,000 m/s, with the flight path turning no faster
        // than 6 g allows, must level off near 150 m without going below 100 m.
        public static void Dive_FromCruiseNeverGoesBelow100m()
        {
            var run = new PlutoRunController(150);
            run.TryEngage(5000, 70000, 16);
            double alt = 5000, v = 1000, fpa = 0, dt = 0.02, minAlt = alt;
            double maxTurn = (6 * 9.81 - 9.81) / v; // rad/s
            for (double t = 0; t < 120; t += dt)
            {
                run.Update(dt, alt, 150, alt, v, false, true, true, 16);
                double target = run.TargetFlightPathDeg * Math.PI / 180;
                double step = Math.Max(-maxTurn * dt, Math.Min(maxTurn * dt, target - fpa));
                fpa += step;
                alt += v * Math.Sin(fpa) * dt;
                minAlt = Math.Min(minAlt, alt);
            }
            Assert.True(minAlt > 100, $"min altitude {minAlt:0} m stays above 100 m");
            Assert.Equal(150, alt, 30, "settles at run height");
        }

        public static void Dive_SteepestIs15Degrees()
        {
            var run = new PlutoRunController(150);
            run.TryEngage(9000, 70000, 16);
            run.Update(0.02, 8850, 150, 9000, 1000, false, true, true, 16);
            Assert.Equal(-PlutoConstants.MaxDiveDeg, run.TargetFlightPathDeg, 1e-9, "dive limited");
        }

        // Important 3: an on-target release ends the run.
        public static void Run_EndsAfterOnTargetRelease()
        {
            var run = new PlutoRunController(150);
            run.TryEngage(9000, 70000, 16);
            run.NotifyReleasedOnTarget();
            Assert.True(run.Phase == PlutoRunPhase.Done, "done");
        }

        // Important 4: no pitch command while inverted; full command when level.
        public static void Pitch_ScaledByBank()
        {
            Assert.Equal(1, PlutoRunController.PitchScaleForBank(0), 1e-9, "level");
            Assert.Equal(0, PlutoRunController.PitchScaleForBank(180), 1e-9, "inverted");
            Assert.Equal(0, PlutoRunController.PitchScaleForBank(-120), 1e-9, "past 90");
            Assert.Equal(0.5, PlutoRunController.PitchScaleForBank(60), 1e-9, "60 degrees");
        }

        // KSP multiplies the engine's rated thrust by throttle; the rating is set so the result is the
        // physics thrust, not throttle × physics thrust.
        public static void Thrust_EngineRatingGivesPhysicsThrust()
        {
            double rating = PlutoRamjet.EngineRatingKN(150e3, 0.5);
            Assert.Equal(150, rating * 0.5, 1e-9, "50% throttle → physics thrust");
            Assert.Equal(0, PlutoRamjet.EngineRatingKN(0, 0.5), 1e-9, "no thrust");
            Assert.True(PlutoRamjet.EngineRatingKN(300e3, 0) <= PlutoRamjet.MaxRatingKN, "capped at zero throttle");
        }

        // Optimisation: prediction with 0.5 s steps stays within 1% of the analytic range.
        public static void Ballistics_CoarseStepStillAccurate()
        {
            var impact = PlutoBallistics.PredictImpact(new PlutoVec(0, 150, 0), new PlutoVec(1020, 30, 0),
                new PlutoVec(0, -9.81, 0), 0, p => p.Y, 0.5, 60);
            double t = (30 + Math.Sqrt(30 * 30 + 2 * 9.81 * 150)) / 9.81;
            Assert.Equal(1020 * t, impact.X, 1020 * t * 0.01, "range");
        }

        // Optimisation: each prediction step looks the terrain up once, not twice.
        public static void Ballistics_OneTerrainLookupPerStep()
        {
            int calls = 0;
            PlutoBallistics.PredictImpact(new PlutoVec(0, 150, 0), new PlutoVec(1020, 30, 0),
                new PlutoVec(0, -9.81, 0), 0, p => { calls++; return p.Y; }, 0.5, 60);
            Assert.True(calls <= 22, $"{calls} lookups for a ~9.4 s fall at 0.5 s steps");
        }
    }
}
