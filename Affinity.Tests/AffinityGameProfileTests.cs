using Affinity;
using GameReaderCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
            Assert.IsFalse(registry.Resolve(" Le-Mans Ultimate! ").IsSupported);
            Assert.AreEqual("lmu", registry.ResolveLogo(" Le-Mans Ultimate! ").SettingsKey);
        }

        [TestMethod]
        public void Resolve_PreservesFirstProfilePrecedenceForEquivalentNormalizedAliases()
        {
            IAffinityGameProfile first = new TestProfile("first", "Shared-Game");
            IAffinityGameProfile second = new TestProfile("second", "Shared Game");
            AffinityGameProfileRegistry registry = new AffinityGameProfileRegistry(new[] { first, second });

            Assert.AreSame(first, registry.Resolve(" shared.game "));
            Assert.AreSame(first, registry.ResolveLogo("SHARED_GAME"));
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
        public void DistanceCapabilities_MatchExistingGameBranches()
        {
            AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

            Assert.IsTrue(registry.SupportedProfiles.All(item => item.DistanceMode == AffinityDistanceMode.StatefulDerived));
            Assert.IsTrue(registry.Resolve("Automobilista2").CapturesSessionStartTrackPosition);
            Assert.IsTrue(registry.Resolve("ProjectMotorRacing").CapturesSessionStartTrackPosition);
            Assert.IsTrue(registry.Resolve("ProjectMotorRacing").UsesStationaryStartupAnchor);
            Assert.IsTrue(registry.Resolve("Automobilista2").AcceptsInitialPositionSnap);
            Assert.IsTrue(registry.Resolve("ProjectMotorRacing").AcceptsInitialPositionSnap);
            Assert.IsTrue(registry.Resolve("RaceRoom Racing Experience").UsesLapCounterDistanceFloor);
            Assert.IsFalse(registry.Resolve("iRacing").UsesLapCounterDistanceFloor);
            Assert.AreEqual(AffinityDistanceMode.Automatic, registry.Resolve("Unknown").DistanceMode);
        }

        [TestMethod]
        public void GetTrackPositionWithinLapMeters_UsesReportedMetersWhenValid()
        {
            ProfileStatusData status = new ProfileStatusData();
            SetMemberValue(status, "TrackPositionMeters", 1250.0);
            SetMemberValue(status, "TrackPositionPercent", 75.0);

            Assert.AreEqual(
                1250.0,
                AffinityGameProfileBase.GetTrackPositionWithinLapMeters(status, 4000.0),
                0.001);
        }

        [TestMethod]
        public void GetTrackPositionWithinLapMeters_AcceptsFractionAndWholeNumberPercentFallbacks()
        {
            ProfileStatusData fractionStatus = new ProfileStatusData();
            SetMemberValue(fractionStatus, "TrackPositionMeters", -1.0);
            SetMemberValue(fractionStatus, "TrackPositionPercent", 0.25);

            ProfileStatusData wholeNumberStatus = new ProfileStatusData();
            SetMemberValue(wholeNumberStatus, "TrackPositionMeters", -1.0);
            SetMemberValue(wholeNumberStatus, "TrackPositionPercent", 25.0);

            Assert.AreEqual(
                1000.0,
                AffinityGameProfileBase.GetTrackPositionWithinLapMeters(fractionStatus, 4000.0),
                0.001);
            Assert.AreEqual(
                1000.0,
                AffinityGameProfileBase.GetTrackPositionWithinLapMeters(wholeNumberStatus, 4000.0),
                0.001);
        }

        [TestMethod]
        public void GetTrackPositionWithinLapMeters_ClampsNearOverrunAndPreservesRawPositionBeyondTolerance()
        {
            ProfileStatusData clampStatus = new ProfileStatusData();
            SetMemberValue(clampStatus, "TrackPositionMeters", 4000.5);
            SetMemberValue(clampStatus, "TrackPositionPercent", 0.0);

            ProfileStatusData passthroughStatus = new ProfileStatusData();
            SetMemberValue(passthroughStatus, "TrackPositionMeters", 4505.0);
            SetMemberValue(passthroughStatus, "TrackPositionPercent", 0.0);

            Assert.AreEqual(
                4000.0,
                AffinityGameProfileBase.GetTrackPositionWithinLapMeters(clampStatus, 4000.0),
                0.001);
            Assert.AreEqual(
                4505.0,
                AffinityGameProfileBase.GetTrackPositionWithinLapMeters(passthroughStatus, 4000.0),
                0.001);
        }

        [TestMethod]
        public void GetTrackPositionWithinLapMeters_ReturnsInvalidSentinelWithoutStatusOrTrackLength()
        {
            Assert.AreEqual(-1.0, AffinityGameProfileBase.GetTrackPositionWithinLapMeters(null, 4000.0), 0.001);
            Assert.AreEqual(-1.0, AffinityGameProfileBase.GetTrackPositionWithinLapMeters(new ProfileStatusData(), 0.0), 0.001);
        }

        [TestMethod]
        public void ShouldIgnoreTransientReset_IRacingOwnsStoppedZeroDropRule()
        {
            ProfileStatusData status = new ProfileStatusData();
            SetMemberValue(status, "TrackPositionMeters", 0.0);
            SetMemberValue(status, "TrackPositionPercent", 0.0);
            SetMemberValue(status, "SpeedKmh", 0.0);
            AffinityDistanceSampleContext context = new AffinityDistanceSampleContext
            {
                Status = status,
                DistanceMode = AffinityDistanceMode.StatefulDerived,
                CompletedLaps = 0,
                TrackLengthMeters = 2336.0,
                LastObservedSessionMeters = 4860.0,
                LastObservedCompletedLaps = 2
            };
            AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

            Assert.IsTrue(registry.Resolve("iRacing").ShouldIgnoreTransientReset(context));
            Assert.IsTrue(registry.SupportedProfiles
                .Where(profile => profile.SettingsKey != "iracing")
                .All(profile => !profile.ShouldIgnoreTransientReset(context)));

            context.DistanceMode = AffinityDistanceMode.Automatic;
            Assert.IsFalse(registry.Resolve("iRacing").ShouldIgnoreTransientReset(context));
        }

        [TestMethod]
        public void ShouldIgnoreLowSpeedLineWrap_RFactor2OwnsPitLineOscillationRule()
        {
            ProfileStatusData status = new ProfileStatusData();
            SetMemberValue(status, "CompletedLaps", 0);
            SetMemberValue(status, "TrackPositionMeters", 2.0);
            SetMemberValue(status, "TrackPositionPercent", 0.00082);
            SetMemberValue(status, "SpeedKmh", 77.76);
            AffinityDistanceSampleContext context = new AffinityDistanceSampleContext
            {
                Status = status,
                DistanceMode = AffinityDistanceMode.StatefulDerived,
                CompletedLaps = 0,
                TrackLengthMeters = 2414.02,
                DeltaTrackPositionMeters = -2408.0
            };
            AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

            Assert.IsTrue(registry.Resolve("RFactor2").ShouldIgnoreLowSpeedLineWrap(context));
            Assert.IsTrue(registry.SupportedProfiles
                .Where(profile => profile.SettingsKey != "rfactor2")
                .All(profile => !profile.ShouldIgnoreLowSpeedLineWrap(context)));
        }

        [TestMethod]
        public void ShouldIgnoreLapIncrement_RFactor2OwnsNearStationaryLineRule()
        {
            ProfileStatusData status = new ProfileStatusData();
            SetMemberValue(status, "CompletedLaps", 1);
            SetMemberValue(status, "TrackPositionMeters", 2.0);
            SetMemberValue(status, "TrackPositionPercent", 0.00082);
            SetMemberValue(status, "SpeedKmh", 4.99);
            AffinityDistanceSampleContext context = new AffinityDistanceSampleContext
            {
                Status = status,
                DistanceMode = AffinityDistanceMode.StatefulDerived,
                CompletedLaps = 1,
                LapDelta = 1,
                TrackLengthMeters = 2414.02,
                LastObservedSessionMeters = 2414.02
            };
            AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

            Assert.IsTrue(registry.Resolve("RFactor2").ShouldIgnoreLapIncrement(context));
            Assert.IsTrue(registry.SupportedProfiles
                .Where(profile => profile.SettingsKey != "rfactor2")
                .All(profile => !profile.ShouldIgnoreLapIncrement(context)));
        }

        [TestMethod]
        public void ShouldIgnoreLapIncrement_LeMansUltimateOwnsExitLineRule()
        {
            ProfileStatusData status = new ProfileStatusData();
            SetMemberValue(status, "CompletedLaps", 4);
            SetMemberValue(status, "TrackPositionMeters", 85.41);
            SetMemberValue(status, "TrackPositionPercent", 0.01883);
            SetMemberValue(status, "SpeedKmh", 0.12);
            AffinityDistanceSampleContext context = new AffinityDistanceSampleContext
            {
                Status = status,
                DistanceMode = AffinityDistanceMode.StatefulDerived,
                CompletedLaps = 4,
                LapDelta = 1,
                TrackLengthMeters = 4535.80,
                LastObservedSessionMeters = 13529.19,
                LastIgnoredSessionMeters = -1.0
            };
            AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

            Assert.IsTrue(registry.Resolve("LMU").ShouldIgnoreLapIncrement(context));
            Assert.IsTrue(registry.SupportedProfiles
                .Where(profile => profile.SettingsKey != "lmu")
                .All(profile => !profile.ShouldIgnoreLapIncrement(context)));
        }

        [TestMethod]
        public void ShouldIgnorePlaceholderSessionStart_LeMansUltimateOwnsAllPlaceholderRules()
        {
            AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();
            IAffinityGameProfile lmuProfile = registry.Resolve("LMU");

            ProfileStatusData priorIgnoredStatus = new ProfileStatusData();
            SetMemberValue(priorIgnoredStatus, "TrackPositionMeters", 102.69);
            SetMemberValue(priorIgnoredStatus, "TrackPositionPercent", 0.02403);
            SetMemberValue(priorIgnoredStatus, "SpeedKmh", 0.01);
            SetMemberValue(priorIgnoredStatus, "SessionOdo", 1.0);
            AffinityDistanceSampleContext priorIgnoredContext = new AffinityDistanceSampleContext
            {
                Status = priorIgnoredStatus,
                CompletedLaps = 4,
                TrackLengthMeters = 4273.22,
                LastIgnoredSessionMeters = 17092.89
            };

            ProfileStatusData negativeSentinelStatus = new ProfileStatusData();
            SetMemberValue(negativeSentinelStatus, "TrackPositionMeters", -4900.67);
            SetMemberValue(negativeSentinelStatus, "TrackPositionPercent", -1.0);
            SetMemberValue(negativeSentinelStatus, "SpeedKmh", 0.0);
            SetMemberValue(negativeSentinelStatus, "SessionOdo", 1.0);
            AffinityDistanceSampleContext negativeSentinelContext = new AffinityDistanceSampleContext
            {
                Status = negativeSentinelStatus,
                CompletedLaps = 4,
                TrackLengthMeters = 4900.67,
                LastIgnoredSessionMeters = 19602.70
            };

            ProfileStatusData negativePercentOnlyStatus = new ProfileStatusData();
            SetMemberValue(negativePercentOnlyStatus, "TrackPositionMeters", 2450.0);
            SetMemberValue(negativePercentOnlyStatus, "TrackPositionPercent", -1.0);
            SetMemberValue(negativePercentOnlyStatus, "SpeedKmh", 0.0);
            SetMemberValue(negativePercentOnlyStatus, "SessionOdo", 1.0);
            AffinityDistanceSampleContext negativePercentOnlyContext = new AffinityDistanceSampleContext
            {
                Status = negativePercentOnlyStatus,
                CompletedLaps = 4,
                TrackLengthMeters = 4900.67,
                LastIgnoredSessionMeters = 19602.70
            };

            ProfileStatusData resetSessionOdoStatus = new ProfileStatusData();
            SetMemberValue(resetSessionOdoStatus, "TrackPositionMeters", 91.63);
            SetMemberValue(resetSessionOdoStatus, "TrackPositionPercent", 0.01701);
            SetMemberValue(resetSessionOdoStatus, "SpeedKmh", 0.01);
            SetMemberValue(resetSessionOdoStatus, "SessionOdo", 0.00006);
            AffinityDistanceSampleContext resetSessionOdoContext = new AffinityDistanceSampleContext
            {
                Status = resetSessionOdoStatus,
                CompletedLaps = 4,
                TrackLengthMeters = 5386.80,
                LastIgnoredSessionMeters = -1.0
            };

            Assert.IsTrue(lmuProfile.ShouldIgnorePlaceholderSessionStart(priorIgnoredContext));
            Assert.IsTrue(lmuProfile.ShouldIgnorePlaceholderSessionStart(negativeSentinelContext));
            Assert.IsTrue(lmuProfile.ShouldIgnorePlaceholderSessionStart(negativePercentOnlyContext));
            Assert.IsTrue(lmuProfile.ShouldIgnorePlaceholderSessionStart(resetSessionOdoContext));
            Assert.IsTrue(registry.SupportedProfiles
                .Where(profile => profile.SettingsKey != "lmu")
                .All(profile => !profile.ShouldIgnorePlaceholderSessionStart(priorIgnoredContext)));
            Assert.IsTrue(registry.SupportedProfiles
                .Where(profile => profile.SettingsKey != "lmu")
                .All(profile => !profile.ShouldIgnorePlaceholderSessionStart(negativeSentinelContext)));
            Assert.IsTrue(registry.SupportedProfiles
                .Where(profile => profile.SettingsKey != "lmu")
                .All(profile => !profile.ShouldIgnorePlaceholderSessionStart(negativePercentOnlyContext)));
            Assert.IsTrue(registry.SupportedProfiles
                .Where(profile => profile.SettingsKey != "lmu")
                .All(profile => !profile.ShouldIgnorePlaceholderSessionStart(resetSessionOdoContext)));
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

        [TestMethod]
        public void EvaluateTelemetry_UsesGenericReplayDetectionForSupportedAndFallbackProfiles()
        {
            GameData replayData = CreateGameDataWithStatus(new ReplayStatusData { IsGameReplay = true });
            AffinityTelemetryContext context = new AffinityTelemetryContext
            {
                GameData = replayData,
                Status = replayData.NewData,
                RuntimeState = new AffinityGameRuntimeState()
            };
            AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

            Assert.AreEqual(TelemetryDisposition.Replay, registry.Resolve("iRacing").EvaluateTelemetry(context));
            Assert.AreEqual(TelemetryDisposition.Replay, registry.Resolve("Unknown Game").EvaluateTelemetry(context));
        }

        [TestMethod]
        public void EvaluateTelemetry_GameSpecificOverridesPreserveGenericReplayPrecedence()
        {
            GameData replayData = CreateGameDataWithStatus(new ReplayStatusData { IsGameReplay = true });
            AffinityTelemetryContext context = new AffinityTelemetryContext
            {
                GameData = replayData,
                Status = replayData.NewData,
                CarModel = "Unknown Car",
                TrackNameWithConfig = "Unknown Track",
                RuntimeState = new AffinityGameRuntimeState()
            };
            AffinityGameProfileRegistry registry = AffinityGameProfileRegistry.CreateDefault();

            Assert.AreEqual(TelemetryDisposition.Replay, registry.Resolve("Automobilista2").EvaluateTelemetry(context));
            Assert.AreEqual(TelemetryDisposition.Replay, registry.Resolve("RRRE").EvaluateTelemetry(context));
            Assert.AreEqual(TelemetryDisposition.Replay, registry.Resolve("LMU").EvaluateTelemetry(context));
        }

        [TestMethod]
        public void EvaluateTelemetry_Automobilista2ReturnsInactiveInGarage()
        {
            IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("Automobilista2");
            AffinityTelemetryContext context = CreateTelemetryContext(
                new ProfileStatusData { IsInGarage = true },
                new AffinityGameRuntimeState());

            Assert.AreEqual(TelemetryDisposition.Inactive, profile.EvaluateTelemetry(context));
        }

        [TestMethod]
        public void EvaluateTelemetry_Automobilista2ReturnsInactiveForSpectatorTelemetry()
        {
            IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("Automobilista2");
            AffinityTelemetryContext context = CreateTelemetryContext(
                new ProfileStatusData { IsSpectator = true },
                new AffinityGameRuntimeState());

            Assert.AreEqual(TelemetryDisposition.Inactive, profile.EvaluateTelemetry(context));
        }

        [TestMethod]
        public void EvaluateTelemetry_Automobilista2ReturnsInactiveForObservedReplayGameState()
        {
            IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("Automobilista2");
            AffinityTelemetryContext context = CreateTelemetryContext(
                new ProfileStatusData
                {
                    RawData = new Automobilista2RawData
                    {
                        mViewedParticipantIndex = 3,
                        mGameState = 6
                    }
                },
                new AffinityGameRuntimeState());

            Assert.AreEqual(TelemetryDisposition.Inactive, profile.EvaluateTelemetry(context));
        }

        [TestMethod]
        public void EvaluateTelemetry_Automobilista2RejectsAChangedViewedParticipant()
        {
            IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("Automobilista2");
            AffinityGameRuntimeState runtimeState = new AffinityGameRuntimeState();

            TelemetryDisposition playerDisposition = profile.EvaluateTelemetry(CreateTelemetryContext(
                new ProfileStatusData
                {
                    RawData = new Automobilista2RawData { mViewedParticipantIndex = 3 }
                },
                runtimeState));
            TelemetryDisposition viewedParticipantDisposition = profile.EvaluateTelemetry(CreateTelemetryContext(
                new ProfileStatusData
                {
                    RawData = new Automobilista2RawData { mViewedParticipantIndex = 7 }
                },
                runtimeState));

            Assert.AreEqual(TelemetryDisposition.Active, playerDisposition);
            Assert.AreEqual(3, runtimeState.Automobilista2PlayerViewedParticipantIndex);
            Assert.AreEqual(TelemetryDisposition.Inactive, viewedParticipantDisposition);
        }

        [TestMethod]
        public void EvaluateTelemetry_Automobilista2LearnsANewPlayerAfterRuntimeStateReset()
        {
            IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("Automobilista2");
            AffinityGameRuntimeState runtimeState = new AffinityGameRuntimeState();

            Assert.AreEqual(
                TelemetryDisposition.Active,
                profile.EvaluateTelemetry(CreateTelemetryContext(
                    new ProfileStatusData
                    {
                        RawData = new Automobilista2RawData { mViewedParticipantIndex = 3 }
                    },
                    runtimeState)));

            runtimeState.Reset();

            Assert.AreEqual(
                TelemetryDisposition.Active,
                profile.EvaluateTelemetry(CreateTelemetryContext(
                    new ProfileStatusData
                    {
                        RawData = new Automobilista2RawData { mViewedParticipantIndex = 7 }
                    },
                    runtimeState)));
            Assert.AreEqual(7, runtimeState.Automobilista2PlayerViewedParticipantIndex);
        }

        [TestMethod]
        public void EvaluateTelemetry_RaceRoomReturnsInactiveAfterFinish()
        {
            IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("RRRE");
            AffinityTelemetryContext context = CreateTelemetryContext(
                new ProfileStatusData
                {
                    RawData = new RaceRoomRawData { FinishStatus = 1 }
                },
                new AffinityGameRuntimeState());

            Assert.AreEqual(TelemetryDisposition.Inactive, profile.EvaluateTelemetry(context));
        }

        [TestMethod]
        public void EvaluateTelemetry_RaceRoomReturnsInactiveInGarage()
        {
            IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("RRRE");
            AffinityTelemetryContext context = CreateTelemetryContext(
                new ProfileStatusData
                {
                    RawData = new RaceRoomRawData
                    {
                        FinishStatus = 0,
                        GamePlayerInGarage = 1
                    }
                },
                new AffinityGameRuntimeState());

            Assert.AreEqual(TelemetryDisposition.Inactive, profile.EvaluateTelemetry(context));
        }

        [TestMethod]
        public void EvaluateTelemetry_RaceRoomRemainsActiveWhenFinishAndGarageFlagsAreZero()
        {
            IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("RRRE");
            AffinityTelemetryContext context = CreateTelemetryContext(
                new ProfileStatusData
                {
                    RawData = new RaceRoomRawData
                    {
                        FinishStatus = 0,
                        GamePlayerInGarage = 0
                    }
                },
                new AffinityGameRuntimeState());

            Assert.AreEqual(TelemetryDisposition.Active, profile.EvaluateTelemetry(context));
        }

        [TestMethod]
        public void EvaluateTelemetry_LeMansUltimateWaitsForMissingCarContext()
        {
            IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("LMU");
            AffinityTelemetryContext context = CreateTelemetryContext(
                new ProfileStatusData(),
                new AffinityGameRuntimeState(),
                carModel: "Unknown Car",
                trackNameWithConfig: "Fuji Speedway");

            Assert.AreEqual(TelemetryDisposition.WaitingForContext, profile.EvaluateTelemetry(context));
        }

        [TestMethod]
        public void EvaluateTelemetry_LeMansUltimateWaitsForMissingTrackContext()
        {
            IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("LMU");
            AffinityTelemetryContext context = CreateTelemetryContext(
                new ProfileStatusData(),
                new AffinityGameRuntimeState(),
                carModel: "Porsche 963",
                trackNameWithConfig: "Unknown Track");

            Assert.AreEqual(TelemetryDisposition.WaitingForContext, profile.EvaluateTelemetry(context));
        }

        [TestMethod]
        public void EvaluateTelemetry_LeMansUltimateIsActiveWithCompleteContext()
        {
            IAffinityGameProfile profile = AffinityGameProfileRegistry.CreateDefault().Resolve("LMU");
            AffinityTelemetryContext context = CreateTelemetryContext(
                new ProfileStatusData(),
                new AffinityGameRuntimeState(),
                carModel: "Porsche 963",
                trackNameWithConfig: "Fuji Speedway");

            Assert.AreEqual(TelemetryDisposition.Active, profile.EvaluateTelemetry(context));
        }

        private static AffinityTelemetryContext CreateTelemetryContext(
            StatusDataBase status,
            AffinityGameRuntimeState runtimeState,
            string carModel = "Test Car",
            string trackNameWithConfig = "Test Track")
        {
            GameData data = CreateGameDataWithStatus(status);
            return new AffinityTelemetryContext
            {
                GameData = data,
                Status = status,
                CarModel = carModel,
                TrackNameWithConfig = trackNameWithConfig,
                RuntimeState = runtimeState
            };
        }

        private static GameData CreateGameDataWithStatus(StatusDataBase status)
        {
            GameData data = new GameData();
            SetMemberValue(data, "NewData", status);
            return data;
        }

        private static void SetMemberValue(object instance, string memberName, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo property = instance.GetType().GetProperty(memberName, flags);
            if (property != null && property.SetMethod != null)
            {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo field = instance.GetType().GetField(memberName, flags);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            Assert.Fail($"Expected {instance.GetType().Name} to expose writable member {memberName}.");
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

        private sealed class TestProfile : AffinityGameProfileBase
        {
            public TestProfile(string settingsKey, string runtimeAlias)
                : base(settingsKey, settingsKey, settingsKey + ".jpg", runtimeAlias)
            {
            }
        }

        private sealed class ReplayStatusData : StatusDataBase
        {
            public new bool IsGameReplay { get; set; }

            public override object GetRawDataObject()
            {
                return null;
            }
        }

        private sealed class ProfileStatusData : StatusDataBase
        {
            public bool IsInGarage { get; set; }

            public bool IsSpectator { get; set; }

            public object RawData { get; set; }

            public override object GetRawDataObject()
            {
                return RawData;
            }
        }

        private sealed class Automobilista2RawData
        {
            public int mViewedParticipantIndex;

            public int mGameState;
        }

        private sealed class RaceRoomRawData
        {
            public int FinishStatus { get; set; }

            public int GamePlayerInGarage { get; set; }
        }
    }
}
