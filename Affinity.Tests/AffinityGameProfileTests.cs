using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityGameProfileTests
    {
        [TestMethod]
        public void Resolve_RecognizesAllSupportedAliasesAndCanonicalMetadata()
        {
            AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

            AssertProfile(registry, "Assetto Corsa", "assettocorsa", "Assetto Corsa", "244210.jpg");
            AssertProfile(registry, "AssettoCorsaCompetizione", "assettocorsacompetizione", "Assetto Corsa Competizione", "805550.jpg");
            AssertProfile(registry, "Assetto Corsa EVO", "assettocorsaevo", "Assetto Corsa EVO", "3058630.jpg");
            AssertProfile(registry, "Automobilista2", "automobilista2", "Automobilista 2", "1066890.jpg");
            AssertProfile(registry, "iRacing", "iracing", "iRacing", "iRacing.jpg");
            AssertProfile(registry, "LMU", "lmu", "Le Mans Ultimate", "2399420.jpg");
            AssertProfile(registry, "Project Motor Racing", "projectmotorracing", "Project Motor Racing", "299970.jpg");
            AssertProfile(registry, "RFactor2", "rfactor2", "rFactor 2", "365960.jpg");
            AssertProfile(registry, "R3E", "raceroomracingexperience", "RaceRoom Racing Experience", "211500.jpg");
            AssertProfile(registry, "RRRE", "raceroomracingexperience", "RaceRoom Racing Experience", "211500.jpg");
        }

        [TestMethod]
        public void Resolve_ReturnsUnsupportedFallbackForMissingOrUnknownNames()
        {
            AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

            Assert.IsFalse(registry.Resolve(null).IsSupported);
            Assert.IsFalse(registry.Resolve(string.Empty).IsSupported);
            Assert.IsFalse(registry.Resolve("Unknown Game").IsSupported);
            Assert.AreEqual(string.Empty, registry.Resolve("Unknown Game").SettingsKey);
            Assert.IsFalse(registry.Resolve("Le Mans Ultimate").IsSupported);
            Assert.AreEqual("lmu", registry.ResolveLogo("Le Mans Ultimate").SettingsKey);
        }

        [TestMethod]
        public void SupportedProfiles_HaveUniqueKeysNamesAndLogos()
        {
            IAffinityGameProfile[] profiles = AffinityGameProfileRegistry.CreateDefault()
                .SupportedProfiles
                .ToArray();

            Assert.AreEqual(9, profiles.Length);
            Assert.AreEqual(9, profiles.Select(item => item.SettingsKey).Distinct().Count());
            Assert.AreEqual(9, profiles.Select(item => item.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.AreEqual(9, profiles.Select(item => item.LogoFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.IsTrue(profiles.All(item => item.IsSupported));
        }

        [TestMethod]
        public void TrackDisplay_MapsOnlyAssettoCorsaClassic()
        {
            Dictionary<string, string> map = new Dictionary<string, string>
            {
                ["ks_brands_hatch-indy"] = "Brands Hatch - Indy"
            };
            AffinityTrackDisplayContext context = new AffinityTrackDisplayContext(map);
            AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

            Assert.AreEqual("Brands Hatch - Indy", registry.Resolve("AssettoCorsa").GetTrackDisplayName("ks_brands_hatch-indy", context));
            Assert.AreEqual("ks_brands_hatch-indy", registry.Resolve("Assetto Corsa EVO").GetTrackDisplayName("ks_brands_hatch-indy", context));
            Assert.AreEqual("ks_brands_hatch-indy", registry.Resolve("AssettoCorsaCompetizione").GetTrackDisplayName("ks_brands_hatch-indy", context));
        }

        [TestMethod]
        public void CircuitDisplay_PreservesExistingPerGameRules()
        {
            AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

            AssertParts(registry.Resolve("AssettoCorsa"), "monza_short", "monza_short", "monza_short");
            AssertParts(registry.Resolve("LMU"), "Le Mans - 24h", "Le Mans - 24h", "Le Mans - 24h");
            AssertParts(registry.Resolve("Automobilista2"), "Buenos_Aires-Buenos_Aires_Circuito_15", "Buenos Aires", "Buenos Aires Circuito 15");
            AssertParts(registry.Resolve("RFactor2"), "Lime Rock Park -- No Chicanes", "Lime Rock Park", "No Chicanes");
            AssertParts(registry.Resolve("iRacing"), "spielberg_gp-Grand Prix", "Spielberg GP", "Grand Prix");
        }

        [TestMethod]
        public void AccProfile_PromotesOnlyCompactCodeToLongerDescriptiveTrack()
        {
            IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("AssettoCorsaCompetizione");

            Assert.IsTrue(profile.CanPromoteTrackContext("barcelona", "Barcelona Grand Prix"));
            Assert.IsFalse(profile.CanPromoteTrackContext("Barcelona Grand Prix", "barcelona"));
            Assert.IsFalse(profile.CanPromoteTrackContext("barcelona", "barcelona"));
            Assert.IsFalse(AffinityGameProfileRegistry.CreateDefault().Resolve("AssettoCorsa")
                .CanPromoteTrackContext("barcelona", "Barcelona Grand Prix"));
        }

        private static void AssertProfile(
            AffinityGameProfileRegistry registry,
            string alias,
            string settingsKey,
            string displayName,
            string logoFileName)
        {
            IAffinityGameProfile profile = registry.Resolve(alias);
            Assert.IsTrue(profile.IsSupported, alias);
            Assert.AreEqual(settingsKey, profile.SettingsKey, alias);
            Assert.AreEqual(displayName, profile.DisplayName, alias);
            Assert.AreEqual(logoFileName, profile.LogoFileName, alias);
        }

        private static void AssertParts(
            IAffinityGameProfile profile,
            string trackDisplayName,
            string expectedCircuitName,
            string expectedCircuitLayout)
        {
            CircuitDisplayParts parts = profile.GetCircuitDisplayParts(trackDisplayName);

            Assert.AreEqual(expectedCircuitName, parts.CircuitNameDisplay);
            Assert.AreEqual(expectedCircuitLayout, parts.CircuitLayoutDisplay);
        }
    }
}
