using System.Collections.Generic;
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
            Assert.IsTrue(plugin.Settings.GameDebugLogging.ContainsKey("iracing"));
            Assert.IsTrue(plugin.Settings.GameDebugLogging.ContainsKey("lmu"));
            Assert.IsFalse(plugin.Settings.GameDebugLogging["assettocorsa"]);
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
    }
}
