using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
