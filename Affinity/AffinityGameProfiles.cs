using GameReaderCommon;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Affinity
{
    internal enum TelemetryDisposition
    {
        Active,
        Replay,
        Inactive,
        WaitingForContext
    }

    internal enum AffinityDistanceMode
    {
        Automatic,
        Derived,
        StatefulDerived
    }

    internal sealed class CircuitDisplayParts
    {
        public string CircuitNameDisplay { get; set; } = string.Empty;

        public string CircuitLayoutDisplay { get; set; } = string.Empty;
    }

    internal sealed class AffinityTrackDisplayContext
    {
        public AffinityTrackDisplayContext(IReadOnlyDictionary<string, string> assettoCorsaTrackMap)
        {
            AssettoCorsaTrackMap = assettoCorsaTrackMap;
        }

        public IReadOnlyDictionary<string, string> AssettoCorsaTrackMap { get; }
    }

    internal sealed class AffinityGameRuntimeState
    {
        public int Automobilista2PlayerViewedParticipantIndex { get; set; } = -1;

        public void Reset()
        {
            Automobilista2PlayerViewedParticipantIndex = -1;
        }
    }

    internal struct AffinityTelemetryContext
    {
        public AffinityTelemetryContext()
        {
            GameData = null;
            Status = null;
            CarModel = string.Empty;
            TrackNameWithConfig = string.Empty;
            RuntimeState = null;
        }

        public GameData GameData { get; set; }

        public StatusDataBase Status { get; set; }

        public string CarModel { get; set; } = string.Empty;

        public string TrackNameWithConfig { get; set; } = string.Empty;

        public AffinityGameRuntimeState RuntimeState { get; set; }
    }

    internal struct AffinityDistanceSampleContext
    {
        public StatusDataBase Status { get; set; }

        public AffinityDistanceMode DistanceMode { get; set; }

        public int CompletedLaps { get; set; }

        public int LapDelta { get; set; }

        public double TrackLengthMeters { get; set; }

        public double DeltaTrackPositionMeters { get; set; }

        public double SessionMeters { get; set; }

        public double DeltaMeters { get; set; }

        public double SessionStatefulAbsoluteMeters { get; set; }

        public double SessionStartTrackPositionMeters { get; set; }

        public double LastTrackPositionWithinLapMeters { get; set; }

        public double LastObservedSessionMeters { get; set; }

        public double LastIgnoredSessionMeters { get; set; }

        public int LastObservedCompletedLaps { get; set; }
    }

    internal interface IAffinityGameProfile
    {
        string SettingsKey { get; }

        string DisplayName { get; }

        string LogoFileName { get; }

        bool IsSupported { get; }

        bool Matches(string gameName);

        bool MatchesLogoName(string gameName);

        TelemetryDisposition EvaluateTelemetry(AffinityTelemetryContext context);

        string GetTrackDisplayName(string rawTrackNameWithConfig, AffinityTrackDisplayContext context);

        CircuitDisplayParts GetCircuitDisplayParts(string trackDisplayName);

        bool CanPromoteTrackContext(string previousTrackNameWithConfig, string updatedTrackNameWithConfig);

        AffinityDistanceMode DistanceMode { get; }

        bool CapturesSessionStartTrackPosition { get; }

        bool UsesStationaryStartupAnchor { get; }

        bool AcceptsInitialPositionSnap { get; }

        bool UsesLapCounterDistanceFloor { get; }

        bool ShouldIgnoreTransientReset(AffinityDistanceSampleContext context);

        bool ShouldIgnoreLowSpeedLineWrap(AffinityDistanceSampleContext context);

        bool ShouldIgnoreLapIncrement(AffinityDistanceSampleContext context);

        bool ShouldIgnorePlaceholderSessionStart(AffinityDistanceSampleContext context);
    }

    internal static class AffinityGameName
    {
        public static string Normalize(string gameName)
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
    }

    internal abstract class AffinityGameProfileBase : IAffinityGameProfile
    {
        private readonly HashSet<string> _normalizedRuntimeAliases;

        protected AffinityGameProfileBase(
            string settingsKey,
            string displayName,
            string logoFileName,
            params string[] runtimeAliases)
        {
            SettingsKey = settingsKey ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            LogoFileName = logoFileName ?? string.Empty;
            _normalizedRuntimeAliases = new HashSet<string>(
                (runtimeAliases ?? Array.Empty<string>())
                    .Select(AffinityGameName.Normalize)
                    .Where(alias => !string.IsNullOrWhiteSpace(alias)),
                StringComparer.Ordinal);
        }

        public string SettingsKey { get; }

        public string DisplayName { get; }

        public string LogoFileName { get; }

        public virtual bool IsSupported => true;

        public virtual AffinityDistanceMode DistanceMode => AffinityDistanceMode.StatefulDerived;

        public virtual bool CapturesSessionStartTrackPosition => false;

        public virtual bool UsesStationaryStartupAnchor => false;

        public virtual bool AcceptsInitialPositionSnap => false;

        public virtual bool UsesLapCounterDistanceFloor => false;

        public bool Matches(string gameName)
        {
            return MatchesNormalized(AffinityGameName.Normalize(gameName));
        }

        public bool MatchesLogoName(string gameName)
        {
            return MatchesNormalizedLogoName(AffinityGameName.Normalize(gameName));
        }

        internal bool MatchesNormalized(string normalizedGameName)
        {
            return !string.IsNullOrWhiteSpace(normalizedGameName) &&
                _normalizedRuntimeAliases.Contains(normalizedGameName);
        }

        internal virtual bool MatchesNormalizedLogoName(string normalizedGameName)
        {
            return MatchesNormalized(normalizedGameName);
        }

        public virtual TelemetryDisposition EvaluateTelemetry(AffinityTelemetryContext context)
        {
            return AffinityReplayDetector.IsReplay(context.GameData)
                ? TelemetryDisposition.Replay
                : TelemetryDisposition.Active;
        }

        public virtual string GetTrackDisplayName(
            string rawTrackNameWithConfig,
            AffinityTrackDisplayContext context)
        {
            return rawTrackNameWithConfig;
        }

        public virtual CircuitDisplayParts GetCircuitDisplayParts(string trackDisplayName)
        {
            return SplitCircuitDisplay(trackDisplayName, "-");
        }

        public virtual bool CanPromoteTrackContext(
            string previousTrackNameWithConfig,
            string updatedTrackNameWithConfig)
        {
            return false;
        }

        public virtual bool ShouldIgnoreTransientReset(AffinityDistanceSampleContext context)
        {
            return false;
        }

        public virtual bool ShouldIgnoreLowSpeedLineWrap(AffinityDistanceSampleContext context)
        {
            return false;
        }

        public virtual bool ShouldIgnoreLapIncrement(AffinityDistanceSampleContext context)
        {
            return false;
        }

        public virtual bool ShouldIgnorePlaceholderSessionStart(AffinityDistanceSampleContext context)
        {
            return false;
        }

        protected static CircuitDisplayParts DuplicateCircuitDisplay(string trackDisplayName)
        {
            string displayName = trackDisplayName ?? string.Empty;
            return new CircuitDisplayParts
            {
                CircuitNameDisplay = displayName,
                CircuitLayoutDisplay = displayName
            };
        }

        protected static CircuitDisplayParts SplitCircuitDisplay(string trackDisplayName, string separator)
        {
            string normalizedTrackDisplayName = NormalizeCircuitDisplayPart(trackDisplayName);
            if (string.IsNullOrWhiteSpace(trackDisplayName))
            {
                return new CircuitDisplayParts
                {
                    CircuitNameDisplay = normalizedTrackDisplayName,
                    CircuitLayoutDisplay = string.Empty
                };
            }

            int separatorIndex = trackDisplayName.IndexOf(separator, StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                return new CircuitDisplayParts
                {
                    CircuitNameDisplay = normalizedTrackDisplayName,
                    CircuitLayoutDisplay = string.Empty
                };
            }

            return new CircuitDisplayParts
            {
                CircuitNameDisplay = NormalizeCircuitDisplayPart(trackDisplayName.Substring(0, separatorIndex)),
                CircuitLayoutDisplay = NormalizeCircuitDisplayPart(trackDisplayName.Substring(separatorIndex + separator.Length))
            };
        }

        protected static string NormalizeCircuitDisplayPart(string value)
        {
            return (value ?? string.Empty).Trim().Replace('_', ' ');
        }
    }

    internal sealed class GenericAffinityGameProfile : AffinityGameProfileBase
    {
        public GenericAffinityGameProfile()
            : base(string.Empty, string.Empty, string.Empty)
        {
        }

        public override bool IsSupported => false;

        public override AffinityDistanceMode DistanceMode => AffinityDistanceMode.Automatic;
    }

    internal sealed class AffinityGameProfileRegistry
    {
        private readonly IAffinityGameProfile _fallbackProfile = new GenericAffinityGameProfile();

        public AffinityGameProfileRegistry(IEnumerable<IAffinityGameProfile> supportedProfiles)
        {
            IAffinityGameProfile[] profiles = (supportedProfiles ?? Enumerable.Empty<IAffinityGameProfile>())
                .ToArray();
            SupportedProfiles = new ReadOnlyCollection<IAffinityGameProfile>(profiles);
        }

        public IReadOnlyList<IAffinityGameProfile> SupportedProfiles { get; }

        public static AffinityGameProfileRegistry CreateDefault()
        {
            return new AffinityGameProfileRegistry(new IAffinityGameProfile[]
            {
                new AssettoCorsaProfile(),
                new AssettoCorsaCompetizioneProfile(),
                new AssettoCorsaEvoProfile(),
                new Automobilista2Profile(),
                new IRacingProfile(),
                new LeMansUltimateProfile(),
                new ProjectMotorRacingProfile(),
                new RFactor2Profile(),
                new RaceRoomProfile()
            });
        }

        public IAffinityGameProfile Resolve(string gameName)
        {
            string normalizedGameName = AffinityGameName.Normalize(gameName);
            for (int index = 0; index < SupportedProfiles.Count; index++)
            {
                IAffinityGameProfile profile = SupportedProfiles[index];
                bool matches = profile is AffinityGameProfileBase profileBase
                    ? profileBase.MatchesNormalized(normalizedGameName)
                    : profile.Matches(gameName);
                if (matches)
                {
                    return profile;
                }
            }

            return _fallbackProfile;
        }

        public IAffinityGameProfile ResolveLogo(string gameName)
        {
            string normalizedGameName = AffinityGameName.Normalize(gameName);
            for (int index = 0; index < SupportedProfiles.Count; index++)
            {
                IAffinityGameProfile profile = SupportedProfiles[index];
                bool matches = profile is AffinityGameProfileBase profileBase
                    ? profileBase.MatchesNormalizedLogoName(normalizedGameName)
                    : profile.MatchesLogoName(gameName);
                if (matches)
                {
                    return profile;
                }
            }

            return _fallbackProfile;
        }
    }
}
