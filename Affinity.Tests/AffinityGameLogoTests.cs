using System.IO;
using Affinity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityGameLogoTests
    {
        [TestMethod]
        public void TryGetGameLogoFileName_ReturnsExpectedJpgNames()
        {
            Assert.AreEqual("244210.jpg", AffinityPlugin.TryGetGameLogoFileName("Assetto Corsa"));
            Assert.AreEqual("365950.jpg", AffinityPlugin.TryGetGameLogoFileName("rFactor 2"));
            Assert.AreEqual("iRacing.jpg", AffinityPlugin.TryGetGameLogoFileName("iRacing"));
        }

        [TestMethod]
        public void TryGetGameLogoFileName_NormalizesKnownDisplayVariants()
        {
            Assert.AreEqual("365950.jpg", AffinityPlugin.TryGetGameLogoFileName("Rfactor2"));
            Assert.AreEqual("365950.jpg", AffinityPlugin.TryGetGameLogoFileName("rfactor2"));
            Assert.AreEqual("805550.jpg", AffinityPlugin.TryGetGameLogoFileName("Assetto Corsa  Competizione"));
        }

        [TestMethod]
        public void TryGetGameLogoFileName_ReturnsNullForUnknownGame()
        {
            Assert.IsNull(AffinityPlugin.TryGetGameLogoFileName("Unknown Sim"));
        }

        [TestMethod]
        public void TryGetGameLogoPath_ReturnsMatchingJpgWhenFileExists()
        {
            string logosDirectory = Path.Combine(
                Path.GetTempPath(),
                "affinity-game-logos-tests",
                nameof(TryGetGameLogoPath_ReturnsMatchingJpgWhenFileExists));

            Directory.CreateDirectory(logosDirectory);
            string expectedPath = Path.Combine(logosDirectory, "244210.jpg");
            File.WriteAllBytes(expectedPath, new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });

            string resolvedPath = AffinityPlugin.TryGetGameLogoPath(logosDirectory, "Assetto Corsa");

            Assert.AreEqual(expectedPath, resolvedPath);
        }
    }
}
