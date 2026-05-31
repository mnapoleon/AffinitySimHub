using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
            AssertReferenceVersion(references, "SimHub.Plugins", new Version(1, 0, 9631, 22016));
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
