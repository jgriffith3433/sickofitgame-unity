using System;
using System.IO;
using NUnit.Framework;
using SickOfIt.Data;
using SickOfIt.Tools.Editor;

namespace SickOfIt.Tests.Editor
{
    /// <summary>The player build's GameDefinition bundle (PlayerDataSplit, GameDefinitionBoot) and the deploy's size check.</summary>
    public class PlayerDataSplitTests
    {
        [TearDown]
        public void Reset() => GameDefinition.Loading = false;

        [Test]
        public void WhenReadyRunsAtOnceOutsideABundleBoot()
        {
            int ran = 0;
            GameDefinition.WhenReady(() => ran++);
            Assert.AreEqual(1, ran);
        }

        [Test]
        public void WhenReadyWaitsForTheBootToProvideTheDefinition()
        {
            var definition = GameDefinition.Instance;
            Assert.IsNotNull(definition, "the editor loads it from Resources");
            GameDefinition.Loading = true;
            int ran = 0;
            GameDefinition.WhenReady(() => ran++);
            Assert.AreEqual(0, ran);
            GameDefinition.Provide(definition);
            Assert.AreEqual(1, ran);
            Assert.IsFalse(GameDefinition.Loading);
            Assert.AreSame(definition, GameDefinition.Instance);
        }

        [Test]
        public void TheEditorAndTestsUseTheDefinitionInResources()
        {
            Assert.IsFalse(PlayerDataSplit.Active, "a split must never outlive the deploy that made it");
            Assert.AreEqual(PlayerDataSplit.ResourcesPath, SickOfIt.Tools.Editor.GameConfig.GameConfigSync.DefinitionPath);
            Assert.AreEqual(new[] { "a.unity" }, PlayerDataSplit.PlayerScenes(new[] { "a.unity" }));
        }

        [Test]
        public void TheSizeCheckFailsOnAFileGitHubWouldRefuse()
        {
            string folder = Path.Combine(Path.GetTempPath(), "soi-guard-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(folder, "Build"));
            try
            {
                File.WriteAllBytes(Path.Combine(folder, "Build", "small.data"), new byte[1024]);
                BuildOutputGuard.Check(folder, _ => { });

                using (var big = File.Create(Path.Combine(folder, "Build", "SickOfIt.data.unityweb")))
                    big.SetLength(BuildOutputGuard.MaxBytes + 1);
                var error = Assert.Throws<Exception>(() => BuildOutputGuard.Check(folder, _ => { }));
                StringAssert.Contains("Build/SickOfIt.data.unityweb", error.Message);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
