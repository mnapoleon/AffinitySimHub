using System.Collections.Generic;
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
                EnablePlugin = false,
                DisplayInMiles = true,
                EnableDebugLogging = true,
                GameDebugLogging = new Dictionary<string, bool>
                {
                    ["iracing"] = false
                }
            };

            settings.Reset();

            Assert.IsTrue(settings.EnablePlugin);
            Assert.IsFalse(settings.DisplayInMiles);
            Assert.IsFalse(settings.EnableDebugLogging);
            Assert.AreEqual(0, settings.GameDebugLogging.Count);
        }
    }
}
