using System.Collections.Generic;
using System.Reflection;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinitySettingsTests
    {
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
