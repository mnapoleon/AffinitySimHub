using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimHub.Plugins;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityAssemblyReferenceTests
    {
        [TestMethod]
        public void AffinityAssemblyReferencesSimHubRuntimeAssemblyVersions()
        {
            string affinityAssemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Affinity.dll");
            AssemblyName[] references = Assembly.ReflectionOnlyLoadFrom(affinityAssemblyPath).GetReferencedAssemblies();

            AssertReferenceVersion(references, "GameReaderCommon", new Version(1, 0, 0, 0));
            AssertReferenceVersion(references, "SimHub.Logging", new Version(1, 0, 0, 0));
            AssertReferenceVersion(references, "SimHub.Plugins", new Version(1, 0, 9631, 22016));
        }

        [TestMethod]
        public void AffinityPluginExposesSemanticVersionDisplayString()
        {
            AffinityPlugin plugin = new AffinityPlugin();

            Assert.IsTrue(
                Regex.IsMatch(plugin.PluginVersionDisplay, @"^\d+\.\d+\.\d+$"),
                "The Settings tab should show a major.minor.patch semantic version string without extra build metadata.");
        }

        [TestMethod]
        public void AffinityPluginImplementsCurrentSimHubPluginInterfaces()
        {
            Type pluginType = typeof(AffinityPlugin);

            Assert.IsTrue(typeof(IPlugin).IsAssignableFrom(pluginType));
            Assert.IsTrue(typeof(IDataPlugin).IsAssignableFrom(pluginType));
            Assert.IsTrue(typeof(IWPFSettings).IsAssignableFrom(pluginType));
            Assert.IsTrue(typeof(IWPFSettingsV2).IsAssignableFrom(pluginType));

            InterfaceMapping pluginInterfaceMap = pluginType.GetInterfaceMap(typeof(IPlugin));
            Assert.IsTrue(
                pluginInterfaceMap.InterfaceMethods.Any(method => method.Name == "set_PluginManager"),
                "The SDK stub must require IPlugin.PluginManager so release builds implement SimHub's runtime interface.");
        }

        [TestMethod]
        public void GameDataStubModelsNewDataAsField()
        {
            Type gameDataType = typeof(GameReaderCommon.GameData);

            Assert.IsNotNull(
                gameDataType.GetField("NewData"),
                "GameData.NewData must be a field to match SimHub's runtime GameReaderCommon assembly.");
            Assert.IsNull(
                gameDataType.GetProperty("NewData"),
                "GameData.NewData must not be a property because SimHub does not expose get_NewData().");
        }

        [TestMethod]
        public void PluginManagerStubModelsCommonStoragePathAsStringArray()
        {
            Type pluginManagerType = typeof(PluginManager);

            Assert.IsNotNull(
                pluginManagerType.GetMethod("GetCommonStoragePath", new[] { typeof(string[]) }),
                "PluginManager.GetCommonStoragePath must compile to the string[] overload exposed by SimHub.");
            Assert.IsNull(
                pluginManagerType.GetMethod("GetCommonStoragePath", new[] { typeof(string) }),
                "PluginManager.GetCommonStoragePath(string) does not exist in SimHub and would fail at runtime.");
        }

        [TestMethod]
        public void PluginManagerStubModelsAddPropertyAsGenericFourArgumentMethod()
        {
            Type pluginManagerType = typeof(PluginManager);

            Assert.IsTrue(
                pluginManagerType
                    .GetMethods()
                    .Any(method =>
                        method.Name == "AddProperty" &&
                        method.IsGenericMethodDefinition &&
                        method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(
                            new[] { typeof(string), typeof(Type), method.GetGenericArguments()[0], typeof(string) })),
                "PluginManager.AddProperty must compile to SimHub's generic four-argument overload.");
            Assert.IsFalse(
                pluginManagerType
                    .GetMethods()
                    .Any(method =>
                        method.Name == "AddProperty" &&
                        !method.IsGenericMethodDefinition &&
                        method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(
                            new[] { typeof(string), typeof(Type), typeof(object) })),
                "PluginManager.AddProperty(string, Type, object) does not exist in SimHub and would fail at runtime.");
        }

        [TestMethod]
        public void LoggingStubUsesSimHubLoggingAssembly()
        {
            Assert.AreEqual(
                "SimHub.Logging",
                typeof(SimHub.Logging).Assembly.GetName().Name,
                "SimHub.Logging must come from the SimHub.Logging assembly, not the SimHub.Plugins stub assembly.");
            Assert.AreEqual(
                "log4net",
                typeof(SimHub.Logging).GetProperty("Current").PropertyType.Assembly.GetName().Name,
                "SimHub.Logging.Current must use log4net.ILog like the real SimHub API.");
        }

        [TestMethod]
        public void PluginSource_DoesNotReferenceRemovedAffinityEnabledProperty()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(path) && !Directory.Exists(Path.Combine(path, ".git")))
            {
                path = Path.GetDirectoryName(path);
            }

            Assert.IsFalse(string.IsNullOrEmpty(path), "Could not locate the repository root for source inspection tests.");
            string pluginSource = File.ReadAllText(Path.Combine(path, "Affinity", "AffinityPlugin.cs"));

            Assert.IsFalse(
                pluginSource.Contains("\"Affinity.Enabled\""),
                "Expected AffinityPlugin.cs to stop referencing the removed Affinity.Enabled property.");
        }

        private static void AssertReferenceVersion(AssemblyName[] references, string name, Version expectedVersion)
        {
            AssemblyName reference = references.SingleOrDefault(candidate => candidate.Name == name);

            Assert.IsNotNull(reference, $"Expected Affinity.dll to reference {name}.");
            Assert.AreEqual(
                expectedVersion,
                reference.Version,
                $"Affinity.dll must reference SimHub's runtime {name} identity so SimHub can discover the plugin.");
        }

    }
}
