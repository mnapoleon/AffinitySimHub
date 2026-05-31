using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityProjectReferenceConfigurationTests
    {
        [TestMethod]
        public void AffinityProjectUsesRepoManagedSimHubReferences()
        {
            XDocument project = XDocument.Load(GetRepoPath("Affinity", "Affinity.csproj"));

            string simHubReferencePath = project
                .Descendants("SimHubReferencePath")
                .Single()
                .Value;
            string simHubPluginsHintPath = project
                .Descendants("Reference")
                .Single(element => (string)element.Attribute("Include") == "SimHub.Plugins")
                .Element("HintPath")
                ?.Value;

            Assert.AreEqual(@"$(MSBuildThisFileDirectory)..\lib\SimHub\", simHubReferencePath);
            Assert.AreEqual(@"$(SimHubReferencePath)SimHub.Plugins.dll", simHubPluginsHintPath);
            Assert.IsFalse(
                project
                    .Descendants("ItemGroup")
                    .Attributes("Condition")
                    .Any(attribute => attribute.Value.Contains("UseSimHubSdkStubs")),
                "Affinity.csproj should compile against committed SimHub reference DLLs without a stub toggle.");
        }

        [TestMethod]
        public void AffinityTestsProjectUsesRepoManagedSimHubReferences()
        {
            XDocument project = XDocument.Load(GetRepoPath("Affinity.Tests", "Affinity.Tests.csproj"));

            string simHubReferencePath = project
                .Descendants("SimHubReferencePath")
                .Single()
                .Value;
            string simHubPluginsHintPath = project
                .Descendants("Reference")
                .Single(element => (string)element.Attribute("Include") == "SimHub.Plugins")
                .Element("HintPath")
                ?.Value;

            Assert.AreEqual(@"$(MSBuildThisFileDirectory)..\lib\SimHub\", simHubReferencePath);
            Assert.AreEqual(@"$(SimHubReferencePath)SimHub.Plugins.dll", simHubPluginsHintPath);
            Assert.IsFalse(
                project
                    .Descendants("ItemGroup")
                    .Attributes("Condition")
                    .Any(attribute => attribute.Value.Contains("UseSimHubSdkStubs")),
                "Affinity.Tests.csproj should compile against committed SimHub reference DLLs without a stub toggle.");
        }

        private static string GetRepoPath(params string[] parts)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(path) && !Directory.Exists(Path.Combine(path, ".git")))
            {
                path = Path.GetDirectoryName(path);
            }

            Assert.IsFalse(string.IsNullOrEmpty(path), "Could not locate the repository root for project configuration tests.");
            return parts.Aggregate(path, Path.Combine);
        }
    }
}
