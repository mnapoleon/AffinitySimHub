using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityGameProfileBoundaryTests
    {
        [TestMethod]
        public void PluginAndSummaryBuilder_DoNotClassifyGamesDirectly()
        {
            string root = FindRepositoryRoot();
            string plugin = File.ReadAllText(Path.Combine(root, "Affinity", "AffinityPlugin.cs"));
            string summaries = File.ReadAllText(Path.Combine(root, "Affinity", "AffinitySummaryBuilder.cs"));

            string[] forbidden =
            {
                "IsAssettoCorsaGame",
                "IsRaceRoomGame",
                "IsAutomobilista2Game",
                "IsProjectMotorRacingGame",
                "IsIRacingGame",
                "IsRFactor2Game",
                "IsLmuGame",
                "AffinityGameLogic"
            };

            foreach (string value in forbidden)
            {
                StringAssert.DoesNotContain(plugin, value);
                StringAssert.DoesNotContain(summaries, value);
            }
        }

        private static string FindRepositoryRoot()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(path) &&
                !Directory.Exists(Path.Combine(path, ".git")) &&
                !File.Exists(Path.Combine(path, ".git")))
            {
                path = Path.GetDirectoryName(path);
            }

            Assert.IsFalse(string.IsNullOrEmpty(path), "Could not locate the repository root for source inspection tests.");
            return path;
        }
    }
}
