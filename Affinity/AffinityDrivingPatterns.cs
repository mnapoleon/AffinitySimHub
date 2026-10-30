using System;

namespace Affinity
{
    public sealed class AffinitySessionRecord
    {
        public string SessionUid { get; set; }

        public string GameName { get; set; }

        public string CarModel { get; set; }

        public string TrackName { get; set; }

        public string TrackDisplayName { get; set; }

        public DateTime StartedUtc { get; set; }

        public DateTime EndedUtc { get; set; }

        public double DistanceMeters { get; set; }

        public double TimeDrivenSeconds { get; set; }
    }
}
