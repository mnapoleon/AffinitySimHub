using System;

namespace Affinity
{
    internal sealed class AssettoCorsaProfile : AffinityGameProfileBase
    {
        public AssettoCorsaProfile()
            : base("assettocorsa", "Assetto Corsa", "244210.jpg", "Assetto Corsa")
        {
        }

        public override string GetTrackDisplayName(
            string rawTrackNameWithConfig,
            AffinityTrackDisplayContext context)
        {
            if (string.IsNullOrWhiteSpace(rawTrackNameWithConfig))
            {
                return rawTrackNameWithConfig;
            }

            if (context?.AssettoCorsaTrackMap != null &&
                context.AssettoCorsaTrackMap.TryGetValue(rawTrackNameWithConfig, out string mappedName))
            {
                return mappedName;
            }

            return rawTrackNameWithConfig;
        }

        public override CircuitDisplayParts GetCircuitDisplayParts(string trackDisplayName)
        {
            return DuplicateCircuitDisplay(trackDisplayName);
        }
    }

    internal sealed class AssettoCorsaCompetizioneProfile : AffinityGameProfileBase
    {
        public AssettoCorsaCompetizioneProfile()
            : base(
                "assettocorsacompetizione",
                "Assetto Corsa Competizione",
                "805550.jpg",
                "AssettoCorsaCompetizione")
        {
        }

        public override CircuitDisplayParts GetCircuitDisplayParts(string trackDisplayName)
        {
            return DuplicateCircuitDisplay(trackDisplayName);
        }

        public override bool CanPromoteTrackContext(
            string previousTrackNameWithConfig,
            string updatedTrackNameWithConfig)
        {
            if (string.IsNullOrWhiteSpace(previousTrackNameWithConfig) ||
                string.IsNullOrWhiteSpace(updatedTrackNameWithConfig) ||
                string.Equals(
                    previousTrackNameWithConfig.Trim(),
                    updatedTrackNameWithConfig.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!LooksLikeCompactTrackCode(previousTrackNameWithConfig) ||
                LooksLikeCompactTrackCode(updatedTrackNameWithConfig))
            {
                return false;
            }

            string previousNormalized = AffinityGameName.Normalize(previousTrackNameWithConfig);
            string updatedNormalized = AffinityGameName.Normalize(updatedTrackNameWithConfig);
            if (string.IsNullOrWhiteSpace(previousNormalized) ||
                string.IsNullOrWhiteSpace(updatedNormalized) ||
                updatedNormalized.Length <= previousNormalized.Length)
            {
                return false;
            }

            return updatedNormalized.Contains(previousNormalized);
        }

        private static bool LooksLikeCompactTrackCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            if (trimmed.IndexOf(' ') >= 0)
            {
                return false;
            }

            foreach (char character in trimmed)
            {
                if (character == '_' || character == '-')
                {
                    continue;
                }

                if (!char.IsLetterOrDigit(character))
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal sealed class AssettoCorsaEvoProfile : AffinityGameProfileBase
    {
        public AssettoCorsaEvoProfile()
            : base("assettocorsaevo", "Assetto Corsa EVO", "3058630.jpg", "Assetto Corsa EVO")
        {
        }
    }

    internal sealed class Automobilista2Profile : AffinityGameProfileBase
    {
        private const int ObservedReplayGameState = 6;

        public Automobilista2Profile()
            : base("automobilista2", "Automobilista 2", "1066890.jpg", "Automobilista2")
        {
        }

        public override bool CapturesSessionStartTrackPosition => true;

        public override bool AcceptsInitialPositionSnap => true;

        public override TelemetryDisposition EvaluateTelemetry(AffinityTelemetryContext context)
        {
            TelemetryDisposition disposition = base.EvaluateTelemetry(context);
            if (disposition != TelemetryDisposition.Active)
            {
                return disposition;
            }

            if (AffinityReplayDetector.TryGetBooleanMemberValue(
                    context.Status,
                    "IsInGarage",
                    out bool isInGarage) &&
                isInGarage)
            {
                return TelemetryDisposition.Inactive;
            }

            if (AffinityReplayDetector.TryGetBooleanMemberValue(
                    context.Status,
                    "IsSpectator",
                    out bool isSpectator) &&
                isSpectator)
            {
                return TelemetryDisposition.Inactive;
            }

            object rawData = AffinityReplayDetector.GetRawStatusDataObject(context.Status);
            if (AffinityReplayDetector.TryGetIntegerMemberValue(rawData, "mGameState", out int gameState) &&
                gameState == ObservedReplayGameState)
            {
                return TelemetryDisposition.Inactive;
            }

            if (!AffinityReplayDetector.TryGetIntegerMemberValue(
                    rawData,
                    "mViewedParticipantIndex",
                    out int viewedParticipantIndex) ||
                viewedParticipantIndex < 0 ||
                context.RuntimeState == null)
            {
                return TelemetryDisposition.Active;
            }

            if (context.RuntimeState.Automobilista2PlayerViewedParticipantIndex < 0)
            {
                context.RuntimeState.Automobilista2PlayerViewedParticipantIndex = viewedParticipantIndex;
                return TelemetryDisposition.Active;
            }

            return viewedParticipantIndex != context.RuntimeState.Automobilista2PlayerViewedParticipantIndex
                ? TelemetryDisposition.Inactive
                : TelemetryDisposition.Active;
        }
    }

    internal sealed class IRacingProfile : AffinityGameProfileBase
    {
        public IRacingProfile()
            : base("iracing", "iRacing", "iRacing.jpg", "iRacing")
        {
        }

        public override bool ShouldIgnoreTransientReset(AffinityDistanceSampleContext context)
        {
            if (context.DistanceMode != AffinityDistanceMode.StatefulDerived ||
                context.Status == null ||
                context.LastObservedCompletedLaps <= 0 ||
                context.LastObservedSessionMeters <= Math.Max(100.0, context.TrackLengthMeters * 0.25))
            {
                return false;
            }

            return context.CompletedLaps == 0 &&
                context.Status.SpeedKmh < 1.0 &&
                context.Status.TrackPositionMeters <= 1.0 &&
                context.Status.TrackPositionPercent <= 0.001;
        }

        public override CircuitDisplayParts GetCircuitDisplayParts(string trackDisplayName)
        {
            CircuitDisplayParts parts = base.GetCircuitDisplayParts(trackDisplayName);
            parts.CircuitNameDisplay = ToCircuitTitleCase(parts.CircuitNameDisplay);
            return parts;
        }

        private static string ToCircuitTitleCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value ?? string.Empty;
            }

            string[] words = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < words.Length; index++)
            {
                string word = words[index];
                if (string.Equals(word, "gp", StringComparison.OrdinalIgnoreCase))
                {
                    words[index] = "GP";
                    continue;
                }

                words[index] = char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
            }

            return string.Join(" ", words);
        }
    }

    internal sealed class LeMansUltimateProfile : AffinityGameProfileBase
    {
        public LeMansUltimateProfile()
            : base("lmu", "Le Mans Ultimate", "2399420.jpg", "LMU")
        {
        }

        internal override bool MatchesNormalizedLogoName(string normalizedGameName)
        {
            return base.MatchesNormalizedLogoName(normalizedGameName) ||
                string.Equals(normalizedGameName, "lemansultimate", StringComparison.Ordinal);
        }

        public override TelemetryDisposition EvaluateTelemetry(AffinityTelemetryContext context)
        {
            TelemetryDisposition disposition = base.EvaluateTelemetry(context);
            if (disposition != TelemetryDisposition.Active)
            {
                return disposition;
            }

            return IsUnknownContextValue(context.CarModel, "Unknown Car") ||
                IsUnknownContextValue(context.TrackNameWithConfig, "Unknown Track")
                ? TelemetryDisposition.WaitingForContext
                : TelemetryDisposition.Active;
        }

        public override CircuitDisplayParts GetCircuitDisplayParts(string trackDisplayName)
        {
            return DuplicateCircuitDisplay(trackDisplayName);
        }

        public override bool ShouldIgnoreLapIncrement(AffinityDistanceSampleContext context)
        {
            if (context.Status == null ||
                context.LapDelta <= 0 ||
                context.TrackLengthMeters <= 0.0)
            {
                return false;
            }

            double trackPositionMeters = AffinityGameProfileBase.GetTrackPositionWithinLapMeters(
                context.Status,
                context.TrackLengthMeters);
            bool nearLineAtExit = trackPositionMeters <= Math.Max(100.0, context.TrackLengthMeters * 0.025) ||
                trackPositionMeters >= context.TrackLengthMeters - 5.0;

            return context.CompletedLaps > 0 &&
                context.Status.SpeedKmh < 1.0 &&
                nearLineAtExit &&
                context.LastObservedSessionMeters >= context.TrackLengthMeters;
        }

        public override bool ShouldIgnorePlaceholderSessionStart(AffinityDistanceSampleContext context)
        {
            if (context.Status == null ||
                context.CompletedLaps <= 0 ||
                context.TrackLengthMeters <= 0.0)
            {
                return false;
            }

            double trackPositionMeters = AffinityGameProfileBase.GetTrackPositionWithinLapMeters(
                context.Status,
                context.TrackLengthMeters);
            bool nearLineAtExit = trackPositionMeters <= Math.Max(100.0, context.TrackLengthMeters * 0.025) ||
                trackPositionMeters >= context.TrackLengthMeters - 5.0;
            bool looksLikeNegativeLapBoundarySentinel = trackPositionMeters <= (-context.TrackLengthMeters + 5.0) ||
                context.Status.TrackPositionPercent <= -0.99;
            bool hasIgnoredSessionMarker = context.LastIgnoredSessionMeters >= 0.0;
            bool looksLikeResetSessionOdo = context.Status.SessionOdo >= 0.0 &&
                context.Status.SessionOdo <= 0.01;

            return context.Status.SpeedKmh < 1.0 &&
                (nearLineAtExit || looksLikeNegativeLapBoundarySentinel) &&
                (hasIgnoredSessionMarker || looksLikeResetSessionOdo);
        }

        private static bool IsUnknownContextValue(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ||
                string.Equals(value.Trim(), fallback, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class ProjectMotorRacingProfile : AffinityGameProfileBase
    {
        public ProjectMotorRacingProfile()
            : base("projectmotorracing", "Project Motor Racing", "299970.jpg", "Project Motor Racing")
        {
        }

        public override bool CapturesSessionStartTrackPosition => true;

        public override bool UsesStationaryStartupAnchor => true;

        public override bool AcceptsInitialPositionSnap => true;
    }

    internal sealed class RFactor2Profile : AffinityGameProfileBase
    {
        public RFactor2Profile()
            : base("rfactor2", "rFactor 2", "365960.jpg", "RFactor2")
        {
        }

        public override bool ShouldIgnoreLowSpeedLineWrap(AffinityDistanceSampleContext context)
        {
            if (context.Status == null ||
                context.TrackLengthMeters <= 0.0 ||
                Math.Abs(context.DeltaTrackPositionMeters) <= context.TrackLengthMeters * 0.5)
            {
                return false;
            }

            double trackPositionMeters = AffinityGameProfileBase.GetTrackPositionWithinLapMeters(
                context.Status,
                context.TrackLengthMeters);
            bool nearLine = trackPositionMeters <= 5.0 ||
                trackPositionMeters >= context.TrackLengthMeters - 5.0;

            return Math.Max(0, context.CompletedLaps) == 0 &&
                context.Status.SpeedKmh <= 80.0 &&
                nearLine;
        }

        public override bool ShouldIgnoreLapIncrement(AffinityDistanceSampleContext context)
        {
            if (context.Status == null ||
                context.LapDelta <= 0 ||
                context.TrackLengthMeters <= 0.0)
            {
                return false;
            }

            double trackPositionMeters = AffinityGameProfileBase.GetTrackPositionWithinLapMeters(
                context.Status,
                context.TrackLengthMeters);
            bool nearLine = trackPositionMeters <= 5.0 ||
                trackPositionMeters >= context.TrackLengthMeters - 5.0;

            return context.CompletedLaps > 0 &&
                context.Status.SpeedKmh < 5.0 &&
                nearLine &&
                context.LastObservedSessionMeters >= context.TrackLengthMeters;
        }

        public override CircuitDisplayParts GetCircuitDisplayParts(string trackDisplayName)
        {
            return SplitCircuitDisplay(trackDisplayName, "--");
        }
    }

    internal sealed class RaceRoomProfile : AffinityGameProfileBase
    {
        public RaceRoomProfile()
            : base(
                "raceroomracingexperience",
                "RaceRoom Racing Experience",
                "211500.jpg",
                "RaceRoom Racing Experience",
                "R3E",
                "RRRE")
        {
        }

        public override bool UsesLapCounterDistanceFloor => true;

        public override TelemetryDisposition EvaluateTelemetry(AffinityTelemetryContext context)
        {
            TelemetryDisposition disposition = base.EvaluateTelemetry(context);
            if (disposition != TelemetryDisposition.Active)
            {
                return disposition;
            }

            object rawData = AffinityReplayDetector.GetRawStatusDataObject(context.Status);
            if (AffinityReplayDetector.TryGetMemberValue(rawData, "FinishStatus", out object finishStatusValue) &&
                AffinityReplayDetector.TryGetBooleanValue(finishStatusValue, out bool isFinishedStatusActive) &&
                isFinishedStatusActive)
            {
                return TelemetryDisposition.Inactive;
            }

            if (AffinityReplayDetector.TryGetMemberValue(
                    rawData,
                    "GamePlayerInGarage",
                    out object playerInGarageValue) &&
                AffinityReplayDetector.TryGetBooleanValue(playerInGarageValue, out bool isPlayerInGarage) &&
                isPlayerInGarage)
            {
                return TelemetryDisposition.Inactive;
            }

            return TelemetryDisposition.Active;
        }
    }
}
