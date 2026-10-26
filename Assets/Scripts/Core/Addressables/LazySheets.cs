using System;
using System.Collections.Generic;
using SickOfIt.Data;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;

namespace SickOfIt.Core.Addressables
{
    /// <summary>
    /// The hero's weapon-grip sheets loaded on demand. A player.json state with a <c>lazySheet</c> (and no
    /// <c>spriteSheet</c>) is not referenced by GameDefinition, so the boot doesn't load it with everything else (the
    /// art pass's 4096-px grip sheets ran the WebGL heap out of memory at boot, beta v246). The deploy's PlayerDataSplit
    /// makes each one addressable at its path; here it is loaded when the weapon is in his hands
    /// (<see cref="KeepStates"/>, from PlayerWeapons) or first asked for, and released a while after it no longer is.
    /// The editor and tests read the file straight from the project. Until a sheet is in, <see cref="Sprites"/> is
    /// null and the hero keeps the pose he has (the Animator's). Presentation only.
    /// </summary>
    public static class LazySheets
    {
        /// <summary>A sheet no longer wanted is released this long after (real time), so a frame on screen never loses its texture.</summary>
        const float ReleaseAfterSeconds = 5f;

        sealed class Sheet
        {
            public AsyncOperationHandle<IList<Sprite>> handle;
            public Sprite[] sprites;
            public bool failed;
            public bool wanted;
            public float unwantedSince = -1f;
        }

        static readonly Dictionary<string, Sheet> sheets = new Dictionary<string, Sheet>(StringComparer.Ordinal);
        static readonly HashSet<string> keep = new HashSet<string>(StringComparer.Ordinal);
        static readonly List<string> scratch = new List<string>();
        static float lastReleaseCheck;

        /// <summary>The on-demand sheet of a player.json state, or null when the state has its own spriteSheet (or isn't there).</summary>
        public static string SheetOf(string state)
        {
            var gd = GameDefinition.Instance;
            var states = gd != null && gd.player != null && gd.player.animationConfig != null ? gd.player.animationConfig.states : null;
            if (states == null || string.IsNullOrEmpty(state))
                return null;
            foreach (var s in states)
            {
                if (s != null && s.stateName == state)
                    return s.spriteSheet == null && !string.IsNullOrEmpty(s.lazySheet) ? s.lazySheet : null;
            }
            return null;
        }

        /// <summary>The sheet's sprites in name order, or null while it loads (the load starts here) or if it can't be loaded.</summary>
        public static Sprite[] Sprites(string address)
        {
            if (string.IsNullOrEmpty(address))
                return null;
            var sheet = Request(address);
            float now = Time.realtimeSinceStartup;
            if (now - lastReleaseCheck >= 1f)
            {
                lastReleaseCheck = now;
                ReleaseUnwanted(now);
            }
            return sheet.sprites;
        }

        /// <summary>True while the sheet is on its way (asked for, not loaded, not failed).</summary>
        public static bool Loading(string address)
        {
            if (string.IsNullOrEmpty(address))
                return false;
            var sheet = Request(address);
            return sheet.sprites == null && !sheet.failed;
        }

        /// <summary>
        /// The states whose sheets must be loaded now (every grip of every weapon he holds): those load, the rest are
        /// released <see cref="ReleaseAfterSeconds"/> later. States with their own spriteSheet are ignored.
        /// </summary>
        public static void KeepStates(IEnumerable<string> states)
        {
            keep.Clear();
            if (states != null)
            {
                foreach (string state in states)
                {
                    string address = SheetOf(state);
                    if (address != null)
                        keep.Add(address);
                }
            }
            float now = Time.realtimeSinceStartup;
            foreach (var pair in sheets)
            {
                bool wanted = keep.Contains(pair.Key);
                if (pair.Value.wanted && !wanted)
                    pair.Value.unwantedSince = now;
                pair.Value.wanted = wanted;
                if (wanted)
                    pair.Value.unwantedSince = -1f;
            }
            foreach (string address in keep)
                Request(address);
            ReleaseUnwanted(now);
        }

        static Sheet Request(string address)
        {
            if (!sheets.TryGetValue(address, out var sheet))
            {
                sheet = new Sheet { wanted = true };
                sheets[address] = sheet;
                Load(address, sheet);
            }
            // Asked for again (a clip plays it): wanted until the next KeepStates says otherwise.
            if (!sheet.wanted)
            {
                sheet.wanted = true;
                sheet.unwantedSince = -1f;
            }
            return sheet;
        }

        static void Load(string address, Sheet sheet)
        {
#if UNITY_EDITOR
            // The editor and tests: straight from the project, at once (GameDefinition is in Resources there).
            var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/" + address);
            var found = new List<Sprite>();
            foreach (var asset in all)
            {
                if (asset is Sprite sprite)
                    found.Add(sprite);
            }
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (found.Count > 0)
                sheet.sprites = found.ToArray();
            else
            {
                sheet.failed = true;
                Debug.LogWarning($"[LazySheets] {address} has no sprites in the project.");
            }
#else
            try
            {
                sheet.handle = AddressablesAPI.LoadAssetAsync<IList<Sprite>>(address);
                sheet.handle.Completed += h => Loaded(address, sheet, h);
            }
            catch (Exception e)
            {
                sheet.failed = true;
                Debug.LogWarning($"[LazySheets] Couldn't start loading {address}: {e.Message}");
            }
#endif
        }

#if !UNITY_EDITOR
        static void Loaded(string address, Sheet sheet, AsyncOperationHandle<IList<Sprite>> h)
        {
            if (!sheets.TryGetValue(address, out var current) || current != sheet)
                return; // released while it loaded
            if (h.Status == AsyncOperationStatus.Succeeded && h.Result != null && h.Result.Count > 0)
            {
                var found = new List<Sprite>(h.Result.Count);
                foreach (var sprite in h.Result)
                {
                    if (sprite != null)
                        found.Add(sprite);
                }
                // The same order as the importer's asset registry (by name), which every state's frames are counted in.
                found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                sheet.sprites = found.ToArray();
                return;
            }
            sheet.failed = true;
            Debug.LogWarning($"[LazySheets] Couldn't load {address}: {h.OperationException?.Message}");
        }
#endif

        static void ReleaseUnwanted(float now)
        {
            scratch.Clear();
            foreach (var pair in sheets)
            {
                var sheet = pair.Value;
                if (!sheet.wanted && sheet.unwantedSince >= 0f && now - sheet.unwantedSince >= ReleaseAfterSeconds)
                    scratch.Add(pair.Key);
            }
            foreach (string address in scratch)
            {
                var sheet = sheets[address];
                sheets.Remove(address);
                if (sheet.handle.IsValid())
                    AddressablesAPI.Release(sheet.handle);
            }
        }
    }
}
