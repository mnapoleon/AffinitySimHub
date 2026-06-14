using System;
using System.Collections.Generic;
using System.Text;
using GameReaderCommon;

namespace Affinity
{
    internal static class AffinityGameLogic
    {
        public static string NormalizeGameName(string gameName)
        {
            if (string.IsNullOrWhiteSpace(gameName))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(gameName.Length);
            foreach (char character in gameName)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        public static string GetDebugLoggingSettingsKey(string gameName)
        {
            string normalized = NormalizeGameName(gameName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (string.Equals(normalized, "r3e", StringComparison.Ordinal) ||
                string.Equals(normalized, "rrre", StringComparison.Ordinal))
            {
                return "raceroomracingexperience";
            }

            return normalized;
        }

        public static bool IsAssettoCorsaGame(string gameName)
        {
            string normalized = NormalizeGameName(gameName);
            return string.Equals(normalized, "assettocorsa", StringComparison.Ordinal) ||
                string.Equals(normalized, "assettocorsaevo", StringComparison.Ordinal);
        }

        public static bool IsRaceRoomGame(string gameName)
        {
            string normalized = NormalizeGameName(gameName);
            return string.Equals(normalized, "raceroomracingexperience", StringComparison.Ordinal) ||
                string.Equals(normalized, "r3e", StringComparison.Ordinal) ||
                string.Equals(normalized, "rrre", StringComparison.Ordinal);
        }

        public static bool IsAutomobilista2Game(string gameName)
        {
            string normalized = NormalizeGameName(gameName);
            return string.Equals(normalized, "automobilista2", StringComparison.Ordinal);
        }

        public static bool IsIRacingGame(string gameName)
        {
            string normalized = NormalizeGameName(gameName);
            return string.Equals(normalized, "iracing", StringComparison.Ordinal);
        }

        public static bool IsRFactor2Game(string gameName)
        {
            string normalized = NormalizeGameName(gameName);
            return string.Equals(normalized, "rfactor2", StringComparison.Ordinal);
        }

        public static bool IsLmuGame(string gameName)
        {
            string normalized = NormalizeGameName(gameName);
            return string.Equals(normalized, "lmu", StringComparison.Ordinal);
        }

        public static bool IsSupportedGame(string gameName)
        {
            return IsAssettoCorsaGame(gameName) ||
                IsRaceRoomGame(gameName) ||
                IsAutomobilista2Game(gameName) ||
                IsIRacingGame(gameName) ||
                IsRFactor2Game(gameName) ||
                IsLmuGame(gameName);
        }

        public static string GetDisplayTrackNameWithConfig(string gameName, string rawTrackNameWithConfig, IReadOnlyDictionary<string, string> assettoCorsaTrackMap)
        {
            if (!IsAssettoCorsaGame(gameName))
            {
                return rawTrackNameWithConfig;
            }

            if (string.IsNullOrWhiteSpace(rawTrackNameWithConfig))
            {
                return rawTrackNameWithConfig;
            }

            if (assettoCorsaTrackMap != null &&
                assettoCorsaTrackMap.TryGetValue(rawTrackNameWithConfig, out string mappedName))
            {
                return mappedName;
            }

            return rawTrackNameWithConfig;
        }

        public static double GetTrackPositionWithinLapMeters(StatusDataBase status, double trackLengthMeters)
        {
            if (status == null || trackLengthMeters <= 0.0)
            {
                return -1.0;
            }

            double trackPositionMeters = status.TrackPositionMeters;
            if (trackPositionMeters > trackLengthMeters + 1.0)
            {
                return trackPositionMeters;
            }

            if (trackPositionMeters < 0.0 && status.TrackPositionPercent > 0.0)
            {
                double trackPositionPercent = status.TrackPositionPercent > 1.0 && status.TrackPositionPercent <= 100.0
                    ? status.TrackPositionPercent / 100.0
                    : status.TrackPositionPercent;
                trackPositionMeters = trackPositionPercent * trackLengthMeters;
            }

            return Math.Max(0.0, Math.Min(trackPositionMeters, trackLengthMeters));
        }

        public static bool HasReliableTelemetryContext(string gameName, string carModel, string trackNameWithConfig)
        {
            if (!IsLmuGame(gameName))
            {
                return true;
            }

            return !IsUnknownContextValue(carModel, "Unknown Car") &&
                !IsUnknownContextValue(trackNameWithConfig, "Unknown Track");
        }

        private static bool IsUnknownContextValue(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ||
                string.Equals(value.Trim(), fallback, StringComparison.OrdinalIgnoreCase);
        }
    }
}
