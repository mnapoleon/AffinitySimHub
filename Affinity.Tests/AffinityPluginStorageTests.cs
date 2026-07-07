using System;
using System.IO;
using System.Linq;
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
        public void BackupFileIfPresent_CreatesLatestNumberedBackup()
        {
            string databasePath = Path.Combine(_tempDirectory, "PluginsData", "Affinity", "Affinity.distance.db");
            string backupPath = databasePath + ".bak";
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath));
            File.WriteAllText(databasePath, "fresh-db");

            AffinityPlugin.BackupFileIfPresent(databasePath, backupPath);

            Assert.AreEqual("fresh-db", File.ReadAllText(backupPath + ".1"));
        }

        [TestMethod]
        public void BackupFileIfPresent_RotatesNumberedBackupsAndKeepsFive()
        {
            string databasePath = Path.Combine(_tempDirectory, "PluginsData", "Affinity", "Affinity.distance.db");
            string backupPath = databasePath + ".bak";
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath));
            File.WriteAllText(databasePath, "fresh-db");
            File.WriteAllText(backupPath + ".1", "backup-1");
            File.WriteAllText(backupPath + ".2", "backup-2");
            File.WriteAllText(backupPath + ".3", "backup-3");
            File.WriteAllText(backupPath + ".4", "backup-4");
            File.WriteAllText(backupPath + ".5", "backup-5");

            AffinityPlugin.BackupFileIfPresent(databasePath, backupPath);

            Assert.AreEqual("fresh-db", File.ReadAllText(backupPath + ".1"));
            Assert.AreEqual("backup-1", File.ReadAllText(backupPath + ".2"));
            Assert.AreEqual("backup-2", File.ReadAllText(backupPath + ".3"));
            Assert.AreEqual("backup-3", File.ReadAllText(backupPath + ".4"));
            Assert.AreEqual("backup-4", File.ReadAllText(backupPath + ".5"));
            Assert.IsFalse(File.Exists(backupPath + ".6"));
        }

        [TestMethod]
        public void BackupFileIfPresent_MigratesExistingUnnumberedBackupIntoRotation()
        {
            string databasePath = Path.Combine(_tempDirectory, "PluginsData", "Affinity", "Affinity.distance.db");
            string backupPath = databasePath + ".bak";
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath));
            File.WriteAllText(databasePath, "fresh-db");
            File.WriteAllText(backupPath, "old-single-backup");

            AffinityPlugin.BackupFileIfPresent(databasePath, backupPath);

            Assert.AreEqual("fresh-db", File.ReadAllText(backupPath + ".1"));
            Assert.AreEqual("old-single-backup", File.ReadAllText(backupPath + ".2"));
            Assert.IsFalse(File.Exists(backupPath));
        }

        [TestMethod]
        public void GetSqliteInteropProbePaths_ReturnsPluginAndRecoveryCandidatesForEachArchitecture()
        {
            string pluginRoot = Path.Combine(_tempDirectory, "SimHub");
            string affinityStorageRoot = Path.Combine(_tempDirectory, "PluginsData", "Affinity");

            string[] x64Paths = AffinityPlugin.GetSqliteInteropProbePaths(pluginRoot, affinityStorageRoot, is64BitProcess: true).ToArray();
            string[] x86Paths = AffinityPlugin.GetSqliteInteropProbePaths(pluginRoot, affinityStorageRoot, is64BitProcess: false).ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    Path.Combine(pluginRoot, "x64", "SQLite.Interop.dll"),
                    Path.Combine(affinityStorageRoot, "sqlite-native", "x64", "SQLite.Interop.dll")
                },
                x64Paths);
            CollectionAssert.AreEqual(
                new[]
                {
                    Path.Combine(pluginRoot, "x86", "SQLite.Interop.dll"),
                    Path.Combine(affinityStorageRoot, "sqlite-native", "x86", "SQLite.Interop.dll")
                },
                x86Paths);
        }

        [TestMethod]
        public void TryCopySqliteInteropToRecoveryPath_CopiesNativeLibraryIntoAffinityStorage()
        {
            string sourcePath = Path.Combine(_tempDirectory, "SimHub", "x64", "SQLite.Interop.dll");
            string recoveryPath = Path.Combine(_tempDirectory, "PluginsData", "Affinity", "sqlite-native", "x64", "SQLite.Interop.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            File.WriteAllText(sourcePath, "sqlite-native");

            bool copied = AffinityPlugin.TryCopySqliteInteropToRecoveryPath(sourcePath, recoveryPath);

            Assert.IsTrue(copied);
            Assert.AreEqual("sqlite-native", File.ReadAllText(recoveryPath));
        }

        [TestMethod]
        public void BuildSqliteInitializationFailureMessage_IncludesReinstallGuidance()
        {
            string message = AffinityPlugin.BuildSqliteInitializationFailureMessage(new DllNotFoundException("SQLite.Interop.dll"));

            Assert.IsTrue(message.Contains("SQLite native files are missing or unloadable"));
            Assert.IsTrue(message.Contains("Reinstall Affinity"));
            Assert.IsTrue(message.Contains("SimHub update"));
        }
    }
}
