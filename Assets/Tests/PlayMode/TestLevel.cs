using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using SickOfIt.Combat.Health;
using SickOfIt.Core;
using SickOfIt.Core.Rewind;
using SickOfIt.Gameplay;
using SickOfIt.Gameplay.Content;
using SickOfIt.Json;
using Object = UnityEngine.Object;

namespace SickOfIt.Tests.PlayMode
{
    /// <summary>
    /// A minimal level built in code: clock, rewind timeline, camera, a stationary player, and a GameplayWorld
    /// running content written inline. JSON in tests uses single quotes for readability.
    /// </summary>
    public sealed class TestLevel : ILevelHost
    {
        public GameClock Clock;
        public GameplayWorld World;
        public GameObject PlayerObject;
        public PlayerHealth PlayerHealth;
        public GameContent Content;
        public bool Completed;
        public bool Failed;
        public int CancelledCompletions;
        readonly List<Object> created = new List<Object>();

        public static string Json(string singleQuoted) => singleQuoted.Replace('\'', '"');

        /// <summary>Builds content from inline files ("entities/x.json" => json) and an optional level script. Tests use no assets.</summary>
        public static GameContent BuildContent(Dictionary<string, string> files, string script = null)
        {
            var sources = files.Select(f => new GameContent.FileSource { Path = f.Key, DisplayPath = f.Key, Json = JToken.Parse(Json(f.Value)) });
            var scripts = new List<GameContent.ScriptSource>();
            if (script != null)
                scripts.Add(new GameContent.ScriptSource { LevelName = "Test", DisplayPath = "test", JsonPath = "script", Json = JToken.Parse(Json(script)) });
            var noAssets = new RegistryAssetResolver(null);
            var content = GameContent.Build(sources, scripts, noAssets, noAssets);
            ContentValidator.Validate(content, content.Issues);
            Assert.That(content.Issues.HasErrors, Is.False, string.Join("\n", content.Issues));
            return content;
        }

        public static TestLevel Create(GameContent content, string levelName = "Test", Vector2 playerAt = default, UnityEngine.SceneManagement.Scene scene = default,
            SickOfIt.Data.Config.RewindBudgetConfig budget = null)
        {
            GameplayTelemetry.WriteFiles = false;
            var level = new TestLevel { Content = content, scene = scene };

            var clockGo = level.Make(new GameObject("Clock"));
            level.Clock = clockGo.AddComponent<GameClock>();
            clockGo.AddComponent<RewindTimeline>();

            var camGo = level.Make(new GameObject("Camera"));
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            level.PlayerObject = level.Make(new GameObject("Player"));
            level.PlayerObject.transform.position = playerAt;
            var body = level.PlayerObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            level.PlayerObject.AddComponent<CircleCollider2D>().radius = 0.3f;
            level.PlayerObject.AddComponent<SpriteRenderer>();
            level.PlayerObject.AddComponent<RewindableTransform>();
            level.PlayerHealth = level.PlayerObject.AddComponent<PlayerHealth>();
            // Prefabs get these from serialization; a component added in code starts with them null.
            level.PlayerHealth.onHealthChanged = new UnityEngine.Events.UnityEvent<int>();
            level.PlayerHealth.onDamaged = new UnityEngine.Events.UnityEvent();
            level.PlayerHealth.onDeath = new UnityEngine.Events.UnityEvent();
            level.PlayerObject.AddComponent<RewindableHealth>();
            level.PlayerHealth.InitializeForLevel(1);

            content.LevelScripts.TryGetValue(levelName, out var script);
            level.World = GameplayWorld.Create(new WorldSetup
            {
                Content = content,
                Script = script,
                LevelNumber = 1,
                LevelName = levelName,
                Player = level.PlayerObject,
                Camera = cam,
                Host = level,
                Scene = scene,
                RewindBudget = budget,
            });
            level.Make(level.World.gameObject);
            level.World.Begin();
            return level;
        }

        UnityEngine.SceneManagement.Scene scene;

        GameObject Make(GameObject go)
        {
            created.Add(go);
            if (scene.IsValid() && go.transform.parent == null && go.scene != scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        public void Destroy()
        {
            GameplayTelemetry.WriteFiles = false;
            foreach (var o in created)
            {
                if (o != null)
                    Object.Destroy(o);
            }
            foreach (var e in Object.FindObjectsByType<Entity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(e.gameObject);
        }

        /// <summary>Waits until game time reaches <paramref name="time"/> (or a real-time timeout).</summary>
        public IEnumerator Until(float time, float timeout = 30f)
        {
            float started = Time.realtimeSinceStartup;
            while (Clock.Time < time && Time.realtimeSinceStartup - started < timeout)
                yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        public IEnumerator UntilCondition(Func<bool> condition, float timeout = 30f)
        {
            float started = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - started < timeout)
                yield return new WaitForFixedUpdate();
        }

        /// <summary>Rewinds until game time is at or before <paramref name="time"/>, then resumes.</summary>
        public IEnumerator RewindTo(float time, float speed = 4f)
        {
            Clock.StartRewind(speed);
            float started = Time.realtimeSinceStartup;
            while (Clock.Time > time && Time.realtimeSinceStartup - started < 30f)
                yield return null;
            Clock.StopRewind();
            yield return new WaitForFixedUpdate();
        }

        public List<Entity> Alive(string defId = null) =>
            World.Live.Where(e => e.IsAlive && (defId == null || e.DefId == defId)).OrderBy(e => e.Serial).ToList();

        // ILevelHost
        public void CompleteLevel() => Completed = true;
        public void CancelCompletion() { Completed = false; CancelledCompletions++; }
        public void FailLevel() => Failed = true;
        public void GoToLevel(int levelNumber) { }
        public void PlayVideo(int videoNumber) { }
        public void ReturnToMenu() { }
    }
}
