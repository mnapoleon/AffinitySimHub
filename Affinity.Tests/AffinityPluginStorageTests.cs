using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityPluginStorageTests
    {
        private string _tempDirectory;

        [TestInitialize]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "AffinityPluginStorageTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [TestCleanup]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [TestMethod]
        public void GetAffinityStorageRoot_ReturnsPluginsDataAffinitySiblingOfCommon()
        {
            string commonRoot = Path.Combine(_tempDirectory, "PluginsData", "Common");

            string result = AffinityPlugin.GetAffinityStorageRoot(commonRoot);

            Assert.AreEqual(Path.Combine(_tempDirectory, "PluginsData", "Affinity"), result);
        }

        [TestMethod]
        public void MigrateFileIfNeeded_MovesLegacyCommonRootFileWhenTargetMissing()
        {
            string targetPath = Path.Combine(_tempDirectory, "PluginsData", "Affinity", "Affinity.distance.db");
            string legacyPath = Path.Combine(_tempDirectory, "PluginsData", "Common", "Affinity.distance.db");
            Directory.CreateDirectory(Path.GetDirectoryName(legacyPath));
            File.WriteAllText(legacyPath, "legacy-db");

            AffinityPlugin.MigrateFileIfNeeded(targetPath, legacyPath);

            Assert.IsFalse(File.Exists(legacyPath));
            Assert.AreEqual("legacy-db", File.ReadAllText(targetPath));
        }

        [TestMethod]
        public void MigrateFileIfNeeded_LeavesLegacyFileWhenTargetAlreadyExists()
        {
            string targetPath = Path.Combine(_tempDirectory, "PluginsData", "Affinity", "Affinity.distance.db");
            string legacyPath = Path.Combine(_tempDirectory, "PluginsData", "Common", "Affinity.distance.db");
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(legacyPath));
            File.WriteAllText(targetPath, "new-db");
            File.WriteAllText(legacyPath, "legacy-db");

            AffinityPlugin.MigrateFileIfNeeded(targetPath, legacyPath);

            Assert.AreEqual("new-db", File.ReadAllText(targetPath));
            Assert.AreEqual("legacy-db", File.ReadAllText(legacyPath));
        }

        [TestMethod]
        public void ResolveLegacyDataPath_PrefersAffinityFolderThenCommonSubfolderThenCommonRoot()
        {
            string affinityRoot = Path.Combine(_tempDirectory, "PluginsData", "Affinity");
            string commonRoot = Path.Combine(_tempDirectory, "PluginsData", "Common");
            string affinityPath = Path.Combine(affinityRoot, "Affinity.distance.json");
            string commonSubfolderPath = Path.Combine(commonRoot, "Affinity", "Affinity.distance.json");
            string commonRootPath = Path.Combine(commonRoot, "Affinity.distance.json");
            Directory.CreateDirectory(Path.GetDirectoryName(commonSubfolderPath));
            File.WriteAllText(commonRootPath, "root");

            Assert.AreEqual(
                commonRootPath,
                AffinityPlugin.ResolveLegacyDataPath(affinityRoot, commonRoot));

            File.WriteAllText(commonSubfolderPath, "subfolder");
            Assert.AreEqual(
                commonSubfolderPath,
                AffinityPlugin.ResolveLegacyDataPath(affinityRoot, commonRoot));

            Directory.CreateDirectory(affinityRoot);
            File.WriteAllText(affinityPath, "affinity");
            Assert.AreEqual(
                affinityPath,
                AffinityPlugin.ResolveLegacyDataPath(affinityRoot, commonRoot));
        }

        [TestMethod]
        public void BackupFileIfPresent_CreatesAndOverwritesRollingBackup()
        {
            string databasePath = Path.Combine(_tempDirectory, "PluginsData", "Affinity", "Affinity.distance.db");
            string backupPath = databasePath + ".bak";
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath));
            File.WriteAllText(databasePath, "fresh-db");
            File.WriteAllText(backupPath, "stale-db");

            AffinityPlugin.BackupFileIfPresent(databasePath, backupPath);

            Assert.AreEqual("fresh-db", File.ReadAllText(backupPath));
        }
    }
}
