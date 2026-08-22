using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinitySettingsTests
    {
        [TestMethod]
        public void NewSettings_DisablesDebugLoggingByDefault()
        {
            AffinitySettings settings = new AffinitySettings();

            Assert.IsFalse(settings.EnableDebugLogging);
            Assert.IsNotNull(settings.GameDebugLogging);
            Assert.AreEqual(0, settings.GameDebugLogging.Count);
        }

        [TestMethod]
        public void EnsureDefaultGameDebugLoggingSettings_AddsSupportedGamesDisabled()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            MethodInfo method = typeof(AffinityPlugin).GetMethod(
                "EnsureDefaultGameDebugLoggingSettings",
                BindingFlags.Instance | BindingFlags.NonPublic);

            method.Invoke(plugin, null);

            Assert.IsFalse(plugin.Settings.EnableDebugLogging);
            Assert.IsTrue(plugin.Settings.GameDebugLogging.ContainsKey("assettocorsa"));
            Assert.IsTrue(plugin.Settings.GameDebugLogging.ContainsKey("assettocorsacompetizione"));
            Assert.IsTrue(plugin.Settings.GameDebugLogging.ContainsKey("iracing"));
            Assert.IsTrue(plugin.Settings.GameDebugLogging.ContainsKey("lmu"));
            Assert.IsFalse(plugin.Settings.GameDebugLogging["assettocorsa"]);
            Assert.IsFalse(plugin.Settings.GameDebugLogging["assettocorsacompetizione"]);
            Assert.IsFalse(plugin.Settings.GameDebugLogging["iracing"]);
            Assert.IsFalse(plugin.Settings.GameDebugLogging["lmu"]);
        }

        [TestMethod]
        public void EnsureGameDebugLoggingConfigured_AddsSupportedGameDisabled()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            MethodInfo method = typeof(AffinityPlugin).GetMethod(
                "EnsureGameDebugLoggingConfigured",
                BindingFlags.Instance | BindingFlags.NonPublic);

            bool added = (bool)method.Invoke(plugin, new object[] { "LMU" });

            Assert.IsTrue(added);
            Assert.IsTrue(plugin.Settings.GameDebugLogging.ContainsKey("lmu"));
            Assert.IsFalse(plugin.Settings.GameDebugLogging["lmu"]);
        }

        [TestMethod]
        public void RefreshGameDebugLoggingOptions_RendersMissingEntriesUnchecked()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            MethodInfo method = typeof(AffinityPlugin).GetMethod(
                "RefreshGameDebugLoggingOptions",
                BindingFlags.Instance | BindingFlags.NonPublic);

            method.Invoke(plugin, null);

            GameDebugLoggingOption option = plugin.GameDebugLoggingOptions
                .First(entry => entry.SettingsKey == "assettocorsa");

            Assert.IsFalse(option.IsEnabled);
        }

        [TestMethod]
        public void RefreshGameDebugLoggingOptions_RendersAccOptionWithFriendlyLabel()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            MethodInfo method = typeof(AffinityPlugin).GetMethod(
                "RefreshGameDebugLoggingOptions",
                BindingFlags.Instance | BindingFlags.NonPublic);

            method.Invoke(plugin, null);

            GameDebugLoggingOption option = plugin.GameDebugLoggingOptions
                .First(entry => entry.SettingsKey == "assettocorsacompetizione");

            Assert.AreEqual("Assetto Corsa Competizione", option.DisplayName);
            Assert.IsFalse(option.IsEnabled);
        }

        [TestMethod]
        public void RefreshGameDebugLoggingOptions_MatchesSupportedProfilesInDisplayNameOrder()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            plugin.Settings.GameDebugLogging["iracing"] = true;
            MethodInfo method = typeof(AffinityPlugin).GetMethod(
                "RefreshGameDebugLoggingOptions",
                BindingFlags.Instance | BindingFlags.NonPublic);

            method.Invoke(plugin, null);

            IAffinityGameProfile[] expectedProfiles = AffinityGameProfileRegistry.CreateDefault()
                .SupportedProfiles
                .OrderBy(profile => profile.DisplayName)
                .ToArray();
            CollectionAssert.AreEqual(
                expectedProfiles.Select(profile => profile.SettingsKey).ToArray(),
                plugin.GameDebugLoggingOptions.Select(option => option.SettingsKey).ToArray());
            CollectionAssert.AreEqual(
                expectedProfiles.Select(profile => profile.DisplayName).ToArray(),
                plugin.GameDebugLoggingOptions.Select(option => option.DisplayName).ToArray());
            Assert.IsTrue(plugin.GameDebugLoggingOptions.Single(option => option.SettingsKey == "iracing").IsEnabled);
            Assert.IsTrue(plugin.GameDebugLoggingOptions
                .Where(option => option.SettingsKey != "iracing")
                .All(option => !option.IsEnabled));
        }

        [TestMethod]
        public void Reset_RestoresDefaultsAndClearsGameLoggingSelections()
        {
            AffinitySettings settings = new AffinitySettings
            {
                DisplayInMiles = true,
                EnableDebugLogging = true,
                GameDebugLogging = new Dictionary<string, bool>
                {
                    ["iracing"] = false
                }
            };

            settings.Reset();

            Assert.IsFalse(settings.DisplayInMiles);
            Assert.IsFalse(settings.EnableDebugLogging);
            Assert.AreEqual(0, settings.GameDebugLogging.Count);
        }

        [TestMethod]
        public void AffinitySettings_DoesNotExposeEnablePluginProperty()
        {
            Assert.IsNull(typeof(AffinitySettings).GetProperty("EnablePlugin", BindingFlags.Public | BindingFlags.Instance));
        }

        [TestMethod]
        public void IsDebugLoggingEnabled_UpdatesSettingAndNotifiesBinding()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            List<string> changedProperties = new List<string>();
            plugin.PropertyChanged += (sender, args) => changedProperties.Add(args.PropertyName);

            plugin.IsDebugLoggingEnabled = true;
            plugin.IsDebugLoggingEnabled = false;

            CollectionAssert.AreEqual(
                new[] { nameof(AffinityPlugin.IsDebugLoggingEnabled), nameof(AffinityPlugin.IsDebugLoggingEnabled) },
                changedProperties.Where(name => name == nameof(AffinityPlugin.IsDebugLoggingEnabled)).ToList());
            Assert.IsFalse(plugin.Settings.EnableDebugLogging);
        }

        [TestMethod]
        public void NewPlugin_ExposesUnsavedSettingsStatus()
        {
            AffinityPlugin plugin = new AffinityPlugin();

            Assert.AreEqual("Settings not saved in this session", plugin.SettingsStatus);
        }

        [TestMethod]
        public void SaveSettings_SetsSavedStatusAfterSuccessfulWrite()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            string settingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Affinity.settings.json");
            SetSettingsPath(plugin, settingsPath);

            plugin.SaveSettings();

            Assert.IsTrue(plugin.SettingsStatus.StartsWith("Settings saved at "));
        }

        [TestMethod]
        public void SaveSettings_RaisesPropertyChangedWhenSettingsStatusChanges()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            string settingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Affinity.settings.json");
            SetSettingsPath(plugin, settingsPath);
            List<string> changedProperties = new List<string>();
            plugin.PropertyChanged += (sender, args) => changedProperties.Add(args.PropertyName);

            plugin.SaveSettings();

            Assert.IsTrue(changedProperties.Contains(nameof(AffinityPlugin.SettingsStatus)));
        }

        [TestMethod]
        public void SaveSettings_SetsFailedStatusWhenWriteFails()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            string directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            SetSettingsPath(plugin, directoryPath);

            plugin.SaveSettings();

            Assert.AreEqual("Settings save failed; see SimHub log", plugin.SettingsStatus);
        }

        private static void SetSettingsPath(AffinityPlugin plugin, string settingsPath)
        {
            FieldInfo settingsPathField = typeof(AffinityPlugin).GetField("_settingsPath", BindingFlags.Instance | BindingFlags.NonPublic);

            settingsPathField.SetValue(plugin, settingsPath);
        }
    }
}
