using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SickOfIt.Data;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;

namespace SickOfIt.Core.Addressables
{
    /// <summary>
    /// The game's art and sound, loaded when a moment needs them and released after (a phone's browser, iOS Safari above
    /// all, kills a page that holds the whole game). A player build's GameDefinition carries no art or sound: the deploy
    /// (Tools/Editor PlayerDataSplit) empties every sheet, sprite and clip reference, makes each asset its own Addressables
    /// bundle and writes the <see cref="AssetManifest"/>: where each reference was and which scope needs it. Here an owner
    /// ("level", "hero", "video", "death", "screen:codex", ...) <see cref="Acquire"/>s scopes: their bundles load and the
    /// references are put back into GameDefinition, so the code that reads them doesn't change; the owner's previous scopes
    /// are released after the new ones are in (what both need never unloads). Sounds no scope holds load on first play
    /// (<see cref="Request"/>), the least recently used dropped past <see cref="DemandCap"/>. Parsed content learns of each
    /// reference through <see cref="EntryApplied"/> / <see cref="EntryCleared"/>. In the editor and tests
    /// <see cref="Managed"/> is false and everything is a no-op: GameDefinition keeps its references.
    /// </summary>
    public static class GameAssets
    {
        public const string Core = "core";
        public const string Hero = "hero";
        /// <summary>The art only screens after the main menu read (select cards, Shaft, Yooba, shop): loaded behind the menu.</summary>
        public const string LateCore = "core.late";
        public static string Level(string levelName) => "level/" + levelName;
        public static string Scene(string sceneId) => "scene/" + sceneId;
        public static string Video(string videoName) => "video/" + videoName;
        public static string Death(string deathName) => "death/" + deathName;
        public static string Sound(string id) => "sound/" + id;
        public static string RegistryAsset(string id) => "asset/" + id;

        /// <summary>On-demand scopes kept at once (each a sound or one piece of art)...</summary>
        public const int DemandCap = 48;
        /// <summary>...and the memory they may take (a decoded clip is its samples x channels x 4 bytes in the browser).</summary>
        public const long DemandBudgetBytes = 40L * 1024 * 1024;
        const string DemandOwner = "demand";

        /// <summary>A player build whose art and sound load here.</summary>
        public static bool Managed { get; private set; }

        /// <summary>The scene a level is about to play instead of its own script (LevelRuntime.PendingScene), whose art loads with it.</summary>
        public static string PendingLevelScene { get; set; }

        /// <summary>A reference was put back (its GameDefinition property path).</summary>
        public static event Action<string> EntryApplied;
        /// <summary>A reference was emptied again (its asset released).</summary>
        public static event Action<string> EntryCleared;

        class Load
        {
            public string address;
            public AsyncOperationHandle<Texture2D> texture;
            public AsyncOperationHandle<IList<Sprite>> sprites;
            public AsyncOperationHandle<AudioClip> clip;
            public int refs;
            public bool done;
            public bool wantTexture, wantSprites, wantClip;
            public Sprite[] sorted;
        }

        static GameDefinition definition;
        static AssetManifest manifest;
        static readonly Dictionary<string, AssetManifestScope> scopes = new Dictionary<string, AssetManifestScope>(StringComparer.Ordinal);
        static readonly Dictionary<string, List<int>> entriesOf = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        static readonly Dictionary<string, Load> loads = new Dictionary<string, Load>(StringComparer.Ordinal);
        static readonly Dictionary<string, HashSet<string>> owners = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        static readonly Dictionary<Texture2D, Sprite[]> spritesOf = new Dictionary<Texture2D, Sprite[]>();
        static readonly Dictionary<string, float> demandUsed = new Dictionary<string, float>(StringComparer.Ordinal);
        static readonly Dictionary<string, Action> demandWaiting = new Dictionary<string, Action>(StringComparer.Ordinal);
        static readonly Dictionary<string, FieldPath> paths = new Dictionary<string, FieldPath>(StringComparer.Ordinal);

        /// <summary>Takes the boot's GameDefinition; a build with a manifest is managed from here on.</summary>
        public static void Init(GameDefinition loaded)
        {
            definition = loaded;
            Managed = loaded != null && loaded.ManagedAssets;
            if (!Managed)
                return;
            manifest = JsonUtility.FromJson<AssetManifest>(loaded.assetManifest) ?? new AssetManifest();
            scopes.Clear();
            foreach (var scope in manifest.scopes)
            {
                if (scope != null && !string.IsNullOrEmpty(scope.id))
                    scopes[scope.id] = scope;
            }
            entriesOf.Clear();
            for (int i = 0; i < manifest.entries.Length; i++)
            {
                var entry = manifest.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.address))
                    continue;
                if (!entriesOf.TryGetValue(entry.address, out var list))
                    entriesOf[entry.address] = list = new List<int>();
                list.Add(i);
            }
            GameDefinition.LoadedSprites = sheet => sheet != null && spritesOf.TryGetValue(sheet, out var sprites) ? sprites : null;
            Debug.Log($"[GameAssets] {manifest.entries.Length} references in {entriesOf.Count} bundles, {scopes.Count} scopes load on demand.");
        }

        static bool lateStarted;

        /// <summary>The late core (everything but the menu's own) is in, or there is none to wait for.</summary>
        public static bool LateReady => !Knows(LateCore) || IsLoaded(LateCore);

        /// <summary>
        /// Starts the late core once the menu has had <paramref name="delaySeconds"/> to show itself, so its downloads and
        /// texture uploads don't compete with the boot or the first frames. Held for the rest of the session. Safe to call twice.
        /// </summary>
        public static void LoadLateInBackground(float delaySeconds)
        {
            if (!Managed || lateStarted || !Knows(LateCore))
                return;
            lateStarted = true;
            Runner.Get().StartCoroutine(LateInBackground(delaySeconds));
        }

        static IEnumerator LateInBackground(float delaySeconds)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            yield return Acquire(LateCore, new[] { LateCore });
        }

        /// <summary>Waits for the late core (starting it now if the menu hasn't yet); <paramref name="progress"/> gets 0..1.</summary>
        public static IEnumerator EnsureLate(Action<float> progress = null)
        {
            if (LateReady)
                yield break;
            lateStarted = true;
            yield return Acquire(LateCore, new[] { LateCore }, progress);
        }

        /// <summary>The manifest has this scope.</summary>
        public static bool Knows(string scope) => Managed && scope != null && scopes.ContainsKey(scope);

        /// <summary>Every bundle of this scope is in.</summary>
        public static bool IsLoaded(string scope)
        {
            if (!Managed)
                return true;
            foreach (string address in AddressesOf(new[] { scope }))
            {
                if (!loads.TryGetValue(address, out var load) || !load.done)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gives <paramref name="owner"/> exactly these scopes: first lets go of what the owner held and no longer needs
        /// (a phone's browser must never hold two floors at once; what both need stays), then loads what is missing and
        /// waits for it. <paramref name="progress"/> gets 0..1.
        /// </summary>
        public static IEnumerator Acquire(string owner, IEnumerable<string> wanted, Action<float> progress = null)
        {
            if (!Managed)
                yield break;
            var next = AddressesOf(wanted);
            owners.TryGetValue(owner, out var before);
            if (before != null)
            {
                foreach (string address in before)
                {
                    if (!next.Contains(address))
                        Unretain(address);
                }
            }
            var added = new List<Load>();
            foreach (string address in next)
            {
                if (before != null && before.Contains(address))
                    continue;
                added.Add(Retain(address));
            }
            owners[owner] = next;

            float started = Time.realtimeSinceStartup;
            while (true)
            {
                int done = 0;
                foreach (var load in added)
                {
                    if (load.done)
                        done++;
                }
                progress?.Invoke(added.Count == 0 ? 1f : done / (float)added.Count);
                if (done == added.Count)
                    break;
                yield return null;
            }
            if (added.Count > 0)
            {
                Debug.Log($"[GameAssets] {owner}: {added.Count} bundles in {Time.realtimeSinceStartup - started:0.0} s ({Describe()}).");
                MemoryReport.Log("after loading " + owner);
            }
        }

        /// <summary>Drops everything <paramref name="owner"/> holds.</summary>
        public static void Release(string owner)
        {
            if (!Managed || !owners.TryGetValue(owner, out var held))
                return;
            owners.Remove(owner);
            foreach (string address in held)
                Unretain(address);
        }

        /// <summary>
        /// One scope wanted now (a sound no level holds, a piece of art): <paramref name="ready"/> runs once it is in
        /// (at once if it is). The least recently used of these are dropped past <see cref="DemandCap"/>.
        /// </summary>
        public static void Request(string scope, Action ready)
        {
            if (!Managed || !scopes.ContainsKey(scope))
            {
                ready?.Invoke();
                return;
            }
            demandUsed[scope] = Time.unscaledTime;
            if (IsLoaded(scope))
            {
                ready?.Invoke();
                return;
            }
            bool first = !demandWaiting.ContainsKey(scope);
            demandWaiting.TryGetValue(scope, out var waiting);
            demandWaiting[scope] = waiting + ready;
            if (!first)
                return;
            string owner = DemandOwner + ":" + scope;
            if (!owners.ContainsKey(owner))
            {
                var held = AddressesOf(new[] { scope });
                foreach (string address in held)
                    Retain(address);
                owners[owner] = held;
            }
            TrimDemand();
            Runner.Get().StartCoroutine(WhenLoaded(scope));
        }

        static IEnumerator WhenLoaded(string scope)
        {
            while (!IsLoaded(scope))
                yield return null;
            if (demandWaiting.TryGetValue(scope, out var ready))
            {
                demandWaiting.Remove(scope);
                ready?.Invoke();
            }
            TrimDemand(); // (its decoded size is known now)
        }

        static void TrimDemand()
        {
            while (demandUsed.Count > DemandCap || DemandBytes() > DemandBudgetBytes)
            {
                string oldest = null;
                float at = float.MaxValue;
                foreach (var pair in demandUsed)
                {
                    if (pair.Value < at && !demandWaiting.ContainsKey(pair.Key))
                    {
                        oldest = pair.Key;
                        at = pair.Value;
                    }
                }
                if (oldest == null)
                    return;
                demandUsed.Remove(oldest);
                Release(DemandOwner + ":" + oldest);
            }
        }

        // What the on-demand scopes hold now (decoded audio; art counts nothing here, it is small next to it).
        static long DemandBytes()
        {
            long total = 0;
            foreach (string scope in demandUsed.Keys)
            {
                if (!owners.TryGetValue(DemandOwner + ":" + scope, out var held))
                    continue;
                foreach (string address in held)
                {
                    if (loads.TryGetValue(address, out var load))
                        total += Bytes(load);
                }
            }
            return total;
        }

        /// <summary>A loaded clip's decoded size in the browser (a clip played compressed costs next to nothing).</summary>
        static long Bytes(Load load)
        {
            if (!load.done || !load.clip.IsValid() || load.clip.Status != AsyncOperationStatus.Succeeded || load.clip.Result == null)
                return 0;
            var clip = load.clip.Result;
            return clip.loadType == AudioClipLoadType.DecompressOnLoad ? (long)clip.samples * clip.channels * 4 : 0;
        }

        /// <summary>Decoded audio held now, all owners (for the memory readout).</summary>
        public static long AudioBytes
        {
            get
            {
                long total = 0;
                foreach (var load in loads.Values)
                    total += Bytes(load);
                return total;
            }
        }

        /// <summary>Loaded bundles and owners, for logs and the playtest.</summary>
        public static string Describe()
        {
            int done = 0;
            foreach (var load in loads.Values)
            {
                if (load.done)
                    done++;
            }
            return $"{done} of {loads.Count} bundles in, owners: {string.Join(", ", owners.Keys)}";
        }

        /// <summary>Bundles loaded now.</summary>
        public static int LoadedCount => loads.Count;

        static HashSet<string> AddressesOf(IEnumerable<string> wanted)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>();
            foreach (string id in wanted)
            {
                if (!string.IsNullOrEmpty(id))
                    stack.Push(id);
            }
            while (stack.Count > 0)
            {
                string id = stack.Pop();
                if (!seen.Add(id) || !scopes.TryGetValue(id, out var scope))
                    continue;
                foreach (int index in scope.entries)
                {
                    if (index >= 0 && index < manifest.entries.Length && !string.IsNullOrEmpty(manifest.entries[index].address))
                        result.Add(manifest.entries[index].address);
                }
                foreach (string include in scope.include)
                    stack.Push(include);
            }
            return result;
        }

        static Load Retain(string address)
        {
            if (loads.TryGetValue(address, out var load))
            {
                load.refs++;
                return load;
            }
            load = new Load { address = address, refs = 1 };
            loads[address] = load;
            bool texture = false, sprites = false, clip = false;
            foreach (int index in entriesOf[address])
            {
                var entry = manifest.entries[index];
                if (entry.kind == AssetManifestEntry.Texture)
                {
                    texture = true;
                    sprites |= entry.sliced;
                }
                else if (entry.kind == AssetManifestEntry.Clip)
                {
                    clip = true;
                }
                else
                {
                    sprites = true;
                }
            }
            load.wantTexture = texture;
            load.wantSprites = sprites;
            load.wantClip = clip;
            Begin(load);
            Runner.Get().StartCoroutine(Finish(load));
            return load;
        }

        static void Begin(Load load)
        {
            if (load.wantTexture)
                load.texture = AddressablesAPI.LoadAssetAsync<Texture2D>(load.address);
            if (load.wantSprites)
                load.sprites = AddressablesAPI.LoadAssetAsync<IList<Sprite>>(load.address);
            if (load.wantClip)
                load.clip = AddressablesAPI.LoadAssetAsync<AudioClip>(load.address);
        }

        static bool Failed(Load load) =>
            (load.texture.IsValid() && (load.texture.Status != AsyncOperationStatus.Succeeded || load.texture.Result == null))
            || (load.sprites.IsValid() && (load.sprites.Status != AsyncOperationStatus.Succeeded || load.sprites.Result == null))
            || (load.clip.IsValid() && (load.clip.Status != AsyncOperationStatus.Succeeded || load.clip.Result == null));

        /// <summary>Extra tries for a bundle that failed to download (a CDN's 503), spaced out; Addressables retries first (BundledAssetGroupSchema.RetryCount).</summary>
        const int LoadRetries = 2;

        static IEnumerator Finish(Load load)
        {
            for (int attempt = 0; ; attempt++)
            {
                if (load.texture.IsValid())
                    yield return load.texture;
                if (load.sprites.IsValid())
                    yield return load.sprites;
                if (load.clip.IsValid())
                    yield return load.clip;
                if (!loads.TryGetValue(load.address, out var current) || current != load)
                    yield break; // released while loading (Unretain let the handles go)
                if (attempt >= LoadRetries || !Failed(load))
                    break;
                Debug.LogWarning($"[GameAssets] {load.address} failed ({Failure(load)}); trying again ({attempt + 1} of {LoadRetries})");
                if (load.texture.IsValid())
                    AddressablesAPI.Release(load.texture);
                if (load.sprites.IsValid())
                    AddressablesAPI.Release(load.sprites);
                if (load.clip.IsValid())
                    AddressablesAPI.Release(load.clip);
                yield return new WaitForSecondsRealtime(1.5f * (attempt + 1));
                if (!loads.TryGetValue(load.address, out current) || current != load)
                    yield break; // released during the wait: its handles are already let go
                Begin(load);
            }
            load.done = true;

            if (load.sprites.IsValid() && load.sprites.Status == AsyncOperationStatus.Succeeded && load.sprites.Result != null)
            {
                var list = new List<Sprite>(load.sprites.Result.Count);
                foreach (var sprite in load.sprites.Result)
                {
                    if (sprite != null)
                        list.Add(sprite);
                }
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                load.sorted = list.ToArray();
            }
            var texture = load.texture.IsValid() && load.texture.Status == AsyncOperationStatus.Succeeded ? load.texture.Result : null;
            if (texture == null && load.sorted != null && load.sorted.Length > 0)
                texture = load.sorted[0].texture;
            if (texture != null && load.sorted != null)
                spritesOf[texture] = load.sorted;
            var audio = load.clip.IsValid() && load.clip.Status == AsyncOperationStatus.Succeeded ? load.clip.Result : null;

            bool failed = (load.texture.IsValid() && texture == null) || (load.clip.IsValid() && audio == null) || (load.sprites.IsValid() && load.sorted == null);
            if (failed)
                Debug.LogWarning($"[GameAssets] Couldn't load {load.address}: {Failure(load)}");

            foreach (int index in entriesOf[load.address])
            {
                var entry = manifest.entries[index];
                object value = null;
                switch (entry.kind)
                {
                    case AssetManifestEntry.Texture: value = texture; break;
                    case AssetManifestEntry.Clip: value = audio; break;
                    case AssetManifestEntry.Sprite: value = Named(load.sorted, entry.sprite); break;
                    case AssetManifestEntry.SpriteArray:
                        var array = new Sprite[entry.sprites.Length];
                        for (int i = 0; i < array.Length; i++)
                            array[i] = Named(load.sorted, entry.sprites[i]);
                        value = array;
                        break;
                }
                if (value == null)
                    continue;
                if (Path(entry.path).Set(definition, value))
                    EntryApplied?.Invoke(entry.path);
            }
        }

        static string Failure(Load load)
        {
            var e = (load.texture.IsValid() ? load.texture.OperationException : null)
                ?? (load.sprites.IsValid() ? load.sprites.OperationException : null)
                ?? (load.clip.IsValid() ? load.clip.OperationException : null);
            return e != null ? e.Message : "no asset";
        }

        static Sprite Named(Sprite[] sprites, string name)
        {
            if (sprites == null)
                return null;
            foreach (var sprite in sprites)
            {
                if (sprite != null && sprite.name == name)
                    return sprite;
            }
            return null;
        }

        static void Unretain(string address)
        {
            if (!loads.TryGetValue(address, out var load) || --load.refs > 0)
                return;
            loads.Remove(address);
            foreach (int index in entriesOf[address])
            {
                var entry = manifest.entries[index];
                // A sprite list keeps its length (code walks it), its sprites gone.
                object empty = entry.kind == AssetManifestEntry.SpriteArray ? new Sprite[entry.sprites.Length] : null;
                if (Path(entry.path).Set(definition, empty))
                    EntryCleared?.Invoke(entry.path);
            }
            if (load.texture.IsValid() && load.texture.Status == AsyncOperationStatus.Succeeded && load.texture.Result != null)
                spritesOf.Remove(load.texture.Result);
            if (load.sorted != null && load.sorted.Length > 0 && load.sorted[0] != null)
                spritesOf.Remove(load.sorted[0].texture);
            if (load.texture.IsValid())
                AddressablesAPI.Release(load.texture);
            if (load.sprites.IsValid())
                AddressablesAPI.Release(load.sprites);
            if (load.clip.IsValid())
                AddressablesAPI.Release(load.clip);
        }

        static FieldPath Path(string path)
        {
            if (!paths.TryGetValue(path, out var walker))
                paths[path] = walker = new FieldPath(path);
            return walker;
        }

        /// <summary>
        /// A Unity property path ("levels.Array.data[3].backgroundLayers.Array.data[0].sprite") walked by reflection: fields
        /// (public or [SerializeField]) and list or array elements; <see cref="Set"/> writes the last step.
        /// </summary>
        sealed class FieldPath
        {
            readonly List<(string field, int index)> steps = new List<(string, int)>();
            readonly string text;

            public FieldPath(string path)
            {
                text = path;
                foreach (string part in path.Split('.'))
                {
                    if (part == "Array")
                        continue;
                    if (part.StartsWith("data[", StringComparison.Ordinal) && part.EndsWith("]", StringComparison.Ordinal))
                        steps.Add((null, int.Parse(part.Substring(5, part.Length - 6))));
                    else
                        steps.Add((part, -1));
                }
            }

            public bool Set(object root, object value)
            {
                object current = root;
                for (int i = 0; i < steps.Count; i++)
                {
                    if (current == null)
                        return false;
                    bool last = i == steps.Count - 1;
                    var (name, index) = steps[i];
                    if (name == null)
                    {
                        if (!(current is IList list) || index >= list.Count)
                            return Broken();
                        if (last)
                        {
                            list[index] = value;
                            return true;
                        }
                        current = list[index];
                        continue;
                    }
                    var field = FieldOf(current.GetType(), name);
                    if (field == null)
                        return Broken();
                    if (last)
                    {
                        // A List<Sprite> field takes the loaded sprites as a list.
                        if (value is Sprite[] array && field.FieldType == typeof(List<Sprite>))
                            value = new List<Sprite>(array);
                        field.SetValue(current, value);
                        return true;
                    }
                    current = field.GetValue(current);
                }
                return false;
            }

            bool Broken()
            {
                Debug.LogWarning($"[GameAssets] The manifest's path {text} doesn't match GameDefinition.");
                return false;
            }

            static FieldInfo FieldOf(Type type, string name)
            {
                for (var t = type; t != null; t = t.BaseType)
                {
                    var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null)
                        return field;
                }
                return null;
            }
        }

        /// <summary>Runs the loads' coroutines (a hidden object that outlives scenes).</summary>
        sealed class Runner : MonoBehaviour
        {
            static Runner instance;

            public static Runner Get()
            {
                if (instance == null)
                {
                    var go = new GameObject("GameAssets") { hideFlags = HideFlags.HideInHierarchy };
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Runner>();
                }
                return instance;
            }
        }
    }
}
