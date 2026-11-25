using System;
using System.Collections.Generic;

namespace ProjectPluto
{
    // The radioactive exhaust trail: points dropped along the flight path, each a weak decaying source.
    public sealed class PlutoTrailModel
    {
        public const int MaxPoints = 400;

        public struct Point
        {
            public PlutoVec Position;   // body frame
            public double PowerMW;      // reactor power when dropped
            public double Time;         // universal time when dropped
        }

        private readonly List<Point> points = new List<Point>();
        private double lastAdded = double.NegativeInfinity;

        public int Count => points.Count;
        public Point this[int i] => points[i];

        // Drops a point when the reactor is powered and the interval has passed.
        public void AddIfDue(PlutoVec position, double powerMW, double now)
        {
            if (powerMW <= 0 || now - lastAdded < PlutoConstants.TrailIntervalSeconds - 1e-9)
                return;
            lastAdded = now;
            points.Add(new Point { Position = position, PowerMW = powerMW, Time = now });
            if (points.Count > MaxPoints)
                points.RemoveAt(0);
        }

        public void Prune(double now)
        {
            int expired = 0;
            while (expired < points.Count && now - points[expired].Time > PlutoConstants.TrailLifeSeconds)
                expired++;
            if (expired > 0)
                points.RemoveRange(0, expired);
        }

        // Equivalent reactor power (MW) of point i at time now, for the dose formula.
        public double StrengthMW(int i, double now)
        {
            Point p = points[i];
            double age = Math.Max(0, now - p.Time);
            return p.PowerMW * PlutoConstants.TrailFraction * Math.Exp(-age / PlutoConstants.TrailDecaySeconds);
        }

        // 0..1 visibility of point i (fades with its strength).
        public double Fade(int i, double now) =>
            Math.Exp(-Math.Max(0, now - points[i].Time) / PlutoConstants.TrailDecaySeconds);
    }
}
