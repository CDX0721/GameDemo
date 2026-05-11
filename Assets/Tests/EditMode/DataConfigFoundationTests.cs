#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using GameDemo.Config;
using GameDemo.DataConfig;
using NUnit.Framework;
using UnityEngine;

namespace GameDemo.Tests.EditMode
{
    public class DataConfigFoundationTests
    {
        sealed class FakeTextProvider : IConfigTextProvider
        {
            readonly Dictionary<string, string> _store;

            public FakeTextProvider(Dictionary<string, string> store)
            {
                _store = store;
            }

            public bool TryGetText(string resourcePath, out string text, out string errorMessage)
            {
                text = null;
                errorMessage = null;

                if (!_store.TryGetValue(resourcePath, out text))
                {
                    errorMessage = $"Missing path: {resourcePath}";
                    return false;
                }

                return true;
            }
        }

        sealed class FakeAssetLoader : IAssetLoader
        {
            public string LastPath { get; private set; }
            public TextAsset ReturnValue { get; set; }
            public Exception ThrowError { get; set; }

            public T Load<T>(string path) where T : UnityEngine.Object
            {
                LastPath = path;
                if (ThrowError != null)
                {
                    throw ThrowError;
                }

                return ReturnValue as T;
            }
        }

        ConfigService CreateService(Dictionary<string, string> jsonMap)
        {
            return new ConfigService(
                new FakeTextProvider(jsonMap),
                new UnityJsonConfigSerializer(),
                new ConfigRepository());
        }

        [Test]
        public void Serializer_Deserializes_ConfigList_Successfully()
        {
            const string json = "{\"items\":[{\"id\":\"x\",\"clipPath\":\"A\",\"channel\":\"SFX\",\"volume\":1.0,\"loop\":false}]}";
            var serializer = new UnityJsonConfigSerializer();

            bool ok = serializer.TryDeserializeList<AudioConfig>(json, out List<AudioConfig> list, out string error);

            Assert.IsTrue(ok, error);
            Assert.NotNull(list);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("x", list[0].id);
        }

        [Test]
        public void Serializer_Rejects_MissingItemsArray()
        {
            const string json = "{\"data\":[]}";
            var serializer = new UnityJsonConfigSerializer();

            bool ok = serializer.TryDeserializeList<AudioConfig>(json, out List<AudioConfig> _, out string error);

            Assert.IsFalse(ok);
            Assert.IsTrue(error.Contains("items"));
        }

        [Test]
        public void DefaultValidator_Rejects_DuplicateId()
        {
            var validator = new DefaultConfigValidator<AudioConfig>();
            var records = new List<AudioConfig>
            {
                new AudioConfig { id = "dup" },
                new AudioConfig { id = "dup" }
            };

            ConfigValidationReport report = validator.Validate(records);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Issues.Count > 0);
        }

        [Test]
        public void Repository_Stores_And_Queries_ById()
        {
            var repo = new ConfigRepository();
            repo.SetTable(new List<GameDemo.Config.AudioSettings>
            {
                new GameDemo.Config.AudioSettings { id = "default", masterVolume = 0.8f }
            });

            bool found = repo.TryGet("default", out GameDemo.Config.AudioSettings settings);

            Assert.IsTrue(found);
            Assert.NotNull(settings);
            Assert.AreEqual(0.8f, settings.masterVolume);
        }

        [Test]
        public void Service_Loads_Valid_Config_Into_Repository()
        {
            var service = CreateService(new Dictionary<string, string>
            {
                ["cfg/audio"] = "{\"items\":[{\"id\":\"bgm_main\",\"clipPath\":\"A\",\"channel\":\"BGM\",\"volume\":1.0,\"loop\":true}]}"
            });

            ConfigLoadReport report = service.LoadTable<AudioConfig>("cfg/audio");
            bool found = service.TryGet("bgm_main", out AudioConfig loaded);

            Assert.IsTrue(report.Success);
            Assert.AreEqual(1, report.LoadedCount);
            Assert.IsTrue(found);
            Assert.NotNull(loaded);
            Assert.AreEqual("BGM", loaded.channel);
        }

        [Test]
        public void Service_Rejects_Invalid_Config_And_DoesNotStore()
        {
            var service = CreateService(new Dictionary<string, string>
            {
                ["cfg/bad"] = "{\"items\":[{\"id\":\"dup\"},{\"id\":\"dup\"}]}"
            });

            ConfigLoadReport report = service.LoadTable<AudioConfig>("cfg/bad");
            bool found = service.TryGet("dup", out AudioConfig _);

            Assert.IsFalse(report.Success);
            Assert.IsFalse(found);
            Assert.IsTrue(report.Issues.Count > 0);
        }

        [Test]
        public void AssetManagerTextProvider_Delegates_To_AssetLoader()
        {
            var fakeLoader = new FakeAssetLoader
            {
                ReturnValue = new TextAsset("{\"items\":[]}")
            };
            var provider = new AssetManagerTextProvider(fakeLoader);

            bool ok = provider.TryGetText("TestConfigs/audio_config_list", out string text, out string error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual("TestConfigs/audio_config_list", fakeLoader.LastPath);
            Assert.AreEqual("{\"items\":[]}", text);
        }

        [Test]
        public void AssetManagerTextProvider_Reports_Loader_Exception()
        {
            var fakeLoader = new FakeAssetLoader
            {
                ThrowError = new InvalidOperationException("loader exploded")
            };
            var provider = new AssetManagerTextProvider(fakeLoader);

            bool ok = provider.TryGetText("x", out string _, out string error);

            Assert.IsFalse(ok);
            Assert.IsTrue(error.Contains("loader exploded"));
        }

        [Test]
        public void AssetModule_Integration_Loads_Real_ResourceText()
        {
            AssetManager.Instance.ClearCache();
            var provider = new AssetManagerTextProvider(new AssetManagerLoader());

            bool ok = provider.TryGetText("TestConfigs/audio_settings_list", out string text, out string error);

            Assert.IsTrue(ok, error);
            Assert.IsTrue(text.Contains("\"id\": \"default\""));
            AssetManager.Instance.ClearCache();
        }

        [Test]
        public void ConfigModule_EndToEnd_Load_And_Query_Works()
        {
            AssetManager.Instance.ClearCache();
            ConfigModule.Instance.Initialize();
            ConfigModule.Instance.Clear();

            ConfigLoadReport report = ConfigModule.Instance.LoadTable<AudioConfig>("TestConfigs/audio_config_list");
            bool found = ConfigModule.Instance.TryGet("bgm_main", out AudioConfig audio);

            Assert.IsTrue(report.Success);
            Assert.AreEqual(2, report.LoadedCount);
            Assert.IsTrue(found);
            Assert.NotNull(audio);
            Assert.AreEqual("Audio/BGM/main_theme", audio.clipPath);
            AssetManager.Instance.ClearCache();
        }
    }
}
#endif
