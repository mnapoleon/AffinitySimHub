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
        public Automobilista2Profile()
            : base("automobilista2", "Automobilista 2", "1066890.jpg", "Automobilista2")
        {
        }
    }

    internal sealed class IRacingProfile : AffinityGameProfileBase
    {
        public IRacingProfile()
            : base("iracing", "iRacing", "iRacing.jpg", "iRacing")
        {
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

        public override bool MatchesLogoName(string gameName)
        {
            return base.MatchesLogoName(gameName) ||
                string.Equals(AffinityGameName.Normalize(gameName), "lemansultimate", StringComparison.Ordinal);
        }

        public override CircuitDisplayParts GetCircuitDisplayParts(string trackDisplayName)
        {
            return DuplicateCircuitDisplay(trackDisplayName);
        }
    }

    internal sealed class ProjectMotorRacingProfile : AffinityGameProfileBase
    {
        public ProjectMotorRacingProfile()
            : base("projectmotorracing", "Project Motor Racing", "299970.jpg", "Project Motor Racing")
        {
        }
    }

    internal sealed class RFactor2Profile : AffinityGameProfileBase
    {
        public RFactor2Profile()
            : base("rfactor2", "rFactor 2", "365960.jpg", "RFactor2")
        {
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
    }
}
