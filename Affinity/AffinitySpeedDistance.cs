using System;

namespace Affinity
{
    internal sealed class AffinitySpeedDistance
    {
        private double _lastSeconds = -1.0;
        private double _lastSpeedMetersPerSecond;
        private long? _lastSampleSequence;

        public double Meters { get; private set; }

        public void Update(double speedKmh, double seconds, bool paused, long? sampleSequence)
        {
            if (paused ||
                !sampleSequence.HasValue ||
                double.IsNaN(speedKmh) ||
                double.IsInfinity(speedKmh))
            {
                BreakSamplingChain();
                return;
            }

            if (_lastSampleSequence.HasValue && _lastSampleSequence.Value == sampleSequence.Value)
            {
                BreakSamplingChain();
                _lastSampleSequence = sampleSequence;
                return;
            }

            if (_lastSampleSequence.HasValue && _lastSampleSequence.Value > sampleSequence.Value)
            {
                // Treat tick rollover/reset as a fresh anchor so it cannot bridge the old timeline.
                BreakSamplingChain();
            }

            double speed = Math.Abs(speedKmh) / 3.6;
            double elapsed = seconds - _lastSeconds;
            if (_lastSeconds >= 0.0 && elapsed > 0.0 && elapsed <= 5.0)
            {
                Meters += (_lastSpeedMetersPerSecond + speed) * 0.5 * elapsed;
            }

            _lastSeconds = seconds;
            _lastSpeedMetersPerSecond = speed;
            _lastSampleSequence = sampleSequence;
        }

        public void Reset()
        {
            Meters = 0.0;
            _lastSampleSequence = null;
            BreakSamplingChain();
        }

        private void BreakSamplingChain()
        {
            _lastSeconds = -1.0;
            _lastSpeedMetersPerSecond = 0.0;
        }
    }
}
