using System;

namespace GameReaderCommon
{
    public class GameData
    {
        public bool GameRunning { get; set; }

        public Guid SessionId { get; set; }

        public string GameName { get; set; } = string.Empty;

        public StatusDataBase NewData;

        public StatusDataBase OldData;
    }

    public abstract class StatusDataBase
    {
        public string CarModel { get; set; } = string.Empty;

        public int CompletedLaps { get; set; }

        public bool IsSessionRestart { get; set; }

        public double ReportedTrackLength { get; set; }

        public double SessionOdo { get; set; }

        public double SpeedKmh { get; set; }

        public double TrackLength { get; set; }

        public string TrackName { get; set; } = string.Empty;

        public string TrackNameWithConfig { get; set; } = string.Empty;

        public double TrackPositionMeters { get; set; }

        public double TrackPositionPercent { get; set; }

        public abstract object GetRawDataObject();
    }
}
