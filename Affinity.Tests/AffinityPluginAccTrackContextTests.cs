using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affinity.Tests
{
    [TestClass]
    public class AffinityPluginAccTrackContextTests
    {
        [TestMethod]
        public void TryPromoteAccTrackContext_RekeysShortTrackIdIntoFriendlyTrackName()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            Guid sessionId = Guid.NewGuid();
            TrackBucket shortTrackBucket = new TrackBucket
            {
                GameName = "Assetto Corsa Competizione",
                CarModel = "Ferrari 296 GT3",
                TrackName = "brands_hatch",
                TrackNameWithConfig = "brands_hatch",
                TotalDistanceMeters = 0.0,
                UsedTime = 4.85
            };
            AffinityDatabase database = new AffinityDatabase();
            database.Games["Assetto Corsa Competizione"] = new GameBucket
            {
                Cars =
                {
                    ["Ferrari 296 GT3"] = new CarBucket
                    {
                        Tracks =
                        {
                            ["brands_hatch"] = shortTrackBucket
                        }
                    }
                }
            };

            SetField(plugin, "_database", database);
            SetField(plugin, "_activeSessionId", sessionId);
            SetField(plugin, "_activeContextKey", "Assetto Corsa Competizione|Ferrari 296 GT3|brands_hatch");
            SetProperty(plugin, "CurrentGameName", "Assetto Corsa Competizione");
            SetProperty(plugin, "CurrentCarModel", "Ferrari 296 GT3");
            SetProperty(plugin, "CurrentTrackName", "brands_hatch");
            SetProperty(plugin, "CurrentTrackNameWithConfig", "brands_hatch");
            SetProperty(plugin, "CurrentContextUsedTime", 4.85);

            MethodInfo method = typeof(AffinityPlugin).GetMethod(
                "TryPromoteAccTrackContext",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "Expected ACC track promotion helper to exist.");

            bool promoted = (bool)method.Invoke(
                plugin,
                new object[]
                {
                    sessionId,
                    "Assetto Corsa Competizione",
                    "Ferrari 296 GT3",
                    "Brands Hatch Circuit",
                    "Brands Hatch Circuit"
                });

            Assert.IsTrue(promoted);
            Assert.AreEqual("Brands Hatch Circuit", plugin.CurrentTrackName);
            Assert.AreEqual("Brands Hatch Circuit", plugin.CurrentTrackNameWithConfig);

            CarBucket carBucket = database.Games["Assetto Corsa Competizione"].Cars["Ferrari 296 GT3"];
            Assert.IsFalse(carBucket.Tracks.ContainsKey("brands_hatch"));
            Assert.IsTrue(carBucket.Tracks.ContainsKey("Brands Hatch Circuit"));
            Assert.AreSame(shortTrackBucket, carBucket.Tracks["Brands Hatch Circuit"]);
            Assert.AreEqual(4.85, carBucket.Tracks["Brands Hatch Circuit"].UsedTime, 0.001);

            string activeContextKey = (string)GetField(plugin, "_activeContextKey");
            Assert.IsTrue(activeContextKey.Contains("Brands Hatch Circuit"));
        }

        [TestMethod]
        public void TryPromoteAccTrackContext_RekeysTitleCaseShortTrackNameIntoFriendlyTrackName()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            Guid sessionId = Guid.NewGuid();
            TrackBucket shortTrackBucket = new TrackBucket
            {
                GameName = "Assetto Corsa Competizione",
                CarModel = "Lexus RCF GT3 2016",
                TrackName = "Zandvoort",
                TrackNameWithConfig = "Zandvoort",
                TotalDistanceMeters = 0.0,
                UsedTime = 5.03
            };
            AffinityDatabase database = new AffinityDatabase();
            database.Games["Assetto Corsa Competizione"] = new GameBucket
            {
                Cars =
                {
                    ["Lexus RCF GT3 2016"] = new CarBucket
                    {
                        Tracks =
                        {
                            ["Zandvoort"] = shortTrackBucket
                        }
                    }
                }
            };

            SetField(plugin, "_database", database);
            SetField(plugin, "_activeSessionId", sessionId);
            SetField(plugin, "_activeContextKey", "Assetto Corsa Competizione|Lexus RCF GT3 2016|Zandvoort");
            SetProperty(plugin, "CurrentGameName", "Assetto Corsa Competizione");
            SetProperty(plugin, "CurrentCarModel", "Lexus RCF GT3 2016");
            SetProperty(plugin, "CurrentTrackName", "Zandvoort");
            SetProperty(plugin, "CurrentTrackNameWithConfig", "Zandvoort");
            SetProperty(plugin, "CurrentContextUsedTime", 5.03);

            MethodInfo method = typeof(AffinityPlugin).GetMethod(
                "TryPromoteAccTrackContext",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "Expected ACC track promotion helper to exist.");

            bool promoted = (bool)method.Invoke(
                plugin,
                new object[]
                {
                    sessionId,
                    "Assetto Corsa Competizione",
                    "Lexus RCF GT3 2016",
                    "Circuit Zandvoort",
                    "Circuit Zandvoort"
                });

            Assert.IsTrue(promoted);
            Assert.AreEqual("Circuit Zandvoort", plugin.CurrentTrackName);
            Assert.AreEqual("Circuit Zandvoort", plugin.CurrentTrackNameWithConfig);

            CarBucket carBucket = database.Games["Assetto Corsa Competizione"].Cars["Lexus RCF GT3 2016"];
            Assert.IsFalse(carBucket.Tracks.ContainsKey("Zandvoort"));
            Assert.IsTrue(carBucket.Tracks.ContainsKey("Circuit Zandvoort"));
            Assert.AreSame(shortTrackBucket, carBucket.Tracks["Circuit Zandvoort"]);
            Assert.AreEqual(5.03, carBucket.Tracks["Circuit Zandvoort"].UsedTime, 0.001);

            string activeContextKey = (string)GetField(plugin, "_activeContextKey");
            Assert.IsTrue(activeContextKey.Contains("Circuit Zandvoort"));
        }

        [TestMethod]
        public void TryPromoteAccTrackContext_DoesNotRekeyDifferentTrack()
        {
            AffinityPlugin plugin = new AffinityPlugin();
            Guid sessionId = Guid.NewGuid();
            AffinityDatabase database = new AffinityDatabase();
            database.Games["Assetto Corsa Competizione"] = new GameBucket
            {
                Cars =
                {
                    ["Ferrari 296 GT3"] = new CarBucket
                    {
                        Tracks =
                        {
                            ["brands_hatch"] = new TrackBucket
                            {
                                GameName = "Assetto Corsa Competizione",
                                CarModel = "Ferrari 296 GT3",
                                TrackName = "brands_hatch",
                                TrackNameWithConfig = "brands_hatch",
                                UsedTime = 4.85
                            }
                        }
                    }
                }
            };

            SetField(plugin, "_database", database);
            SetField(plugin, "_activeSessionId", sessionId);
            SetProperty(plugin, "CurrentGameName", "Assetto Corsa Competizione");
            SetProperty(plugin, "CurrentCarModel", "Ferrari 296 GT3");
            SetProperty(plugin, "CurrentTrackName", "brands_hatch");
            SetProperty(plugin, "CurrentTrackNameWithConfig", "brands_hatch");

            MethodInfo method = typeof(AffinityPlugin).GetMethod(
                "TryPromoteAccTrackContext",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "Expected ACC track promotion helper to exist.");

            bool promoted = (bool)method.Invoke(
                plugin,
                new object[]
                {
                    sessionId,
                    "Assetto Corsa Competizione",
                    "Ferrari 296 GT3",
                    "Misano World Circuit",
                    "Misano World Circuit"
                });

            Assert.IsFalse(promoted);
            Assert.AreEqual("brands_hatch", plugin.CurrentTrackNameWithConfig);
        }

        private static object GetField(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected field {fieldName}.");
            return field.GetValue(instance);
        }

        private static void SetField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected field {fieldName}.");
            field.SetValue(instance, value);
        }

        private static void SetProperty(object instance, string propertyName, object value)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, $"Expected property {propertyName}.");
            property.SetValue(instance, value);
        }
    }
}
