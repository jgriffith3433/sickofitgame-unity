using System;
using System.Collections.Generic;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;

namespace SickOfIt.Core.Addressables
{
    /// <summary>
    /// The hero's sheets for player.json states, wherever they are. Normally every sheet rides with GameDefinition (the
    /// asset registry, <see cref="GameDefinition.SpritesOf"/>). A deploy's player build (Tools/Editor PlayerDataSplit) takes
    /// the found weapons' grip sheets (ShotgunIdle, WhipLash, EquipHalo, ...) out of GameDefinition, so they are not all
    /// loaded at boot (v246 ran the page out of memory there), and makes each one addressable as
    /// <see cref="Address"/>(state). Those load here when he gets the weapon (<see cref="Want"/>) or the first time a clip
    /// asks for one; until then <see cref="Of"/> returns nothing and the Animator keeps drawing him. Sheets a new level's
    /// hero doesn't hold any more are released. Presentation only.
    /// </summary>
    public static class PlayerSheets
    {
        public const string AddressPrefix = "PlayerSheet/";

        static readonly Dictionary<string, AsyncOperationHandle<IList<Sprite>>> loads = new Dictionary<string, AsyncOperationHandle<IList<Sprite>>>(StringComparer.Ordinal);
        static readonly Dictionary<string, Sprite[]> ready = new Dictionary<string, Sprite[]>(StringComparer.Ordinal);
        static readonly List<string> scratch = new List<string>();

        public static string Address(string state) => AddressPrefix + state;

        /// <summary>The state's sprites in sheet order (name order); empty while an on-demand sheet is still loading.</summary>
        public static Sprite[] Of(AnimationStateConfig state)
        {
            if (state == null)
                return Array.Empty<Sprite>();
            if (state.spriteSheet != null)
            {
                var gd = GameDefinition.Instance;
                return gd != null ? gd.SpritesOf(state.spriteSheet) : Array.Empty<Sprite>();
            }
            if (ready.TryGetValue(state.stateName ?? "", out var sprites))
                return sprites;
            Load(state.stateName);
            return Array.Empty<Sprite>();
        }

        /// <summary>
        /// The states the hero may play this level (his weapons' grips): starts loading those that are on demand.
        /// With 'releaseOthers' (a new level), the on-demand sheets not in the list are released.
        /// </summary>
        public static void Want(IEnumerable<string> states, bool releaseOthers)
        {
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (string state in states)
            {
                if (string.IsNullOrEmpty(state) || !wanted.Add(state) || !OnDemand(state))
                    continue;
                Load(state);
            }
            if (!releaseOthers)
                return;
            scratch.Clear();
            foreach (var key in loads.Keys)
            {
                if (!wanted.Contains(key))
                    scratch.Add(key);
            }
            foreach (string key in scratch)
            {
                var handle = loads[key];
                loads.Remove(key);
                ready.Remove(key);
                if (handle.IsValid())
                    AddressablesAPI.Release(handle);
            }
        }

        // A player.json state with no sheet of its own in GameDefinition: the split build left it to load here.
        static bool OnDemand(string stateName)
        {
            var gd = GameDefinition.Instance;
            var states = gd != null && gd.player != null && gd.player.animationConfig != null ? gd.player.animationConfig.states : null;
            if (states == null)
                return false;
            foreach (var s in states)
            {
                if (s != null && s.stateName == stateName)
                    return s.spriteSheet == null;
            }
            return false;
        }

        static void Load(string stateName)
        {
            if (string.IsNullOrEmpty(stateName) || loads.ContainsKey(stateName) || !Exists(Address(stateName)))
                return;
            var handle = AddressablesAPI.LoadAssetAsync<IList<Sprite>>(Address(stateName));
            loads[stateName] = handle;
            handle.Completed += done =>
            {
                if (!loads.TryGetValue(stateName, out var current) || !current.Equals(done))
                    return; // released meanwhile
                if (done.Status != AsyncOperationStatus.Succeeded || done.Result == null)
                {
                    Debug.LogWarning($"[PlayerSheets] Couldn't load the hero's {stateName} sheet: {done.OperationException?.Message}");
                    return;
                }
                var sprites = new List<Sprite>(done.Result.Count);
                foreach (var sprite in done.Result)
                {
                    if (sprite != null)
                        sprites.Add(sprite);
                }
                sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                ready[stateName] = sprites.ToArray();
            };
        }

        // Only the split build's catalog has these addresses; the editor and tests never ask Addressables for them.
        static bool Exists(string address)
        {
            foreach (var locator in AddressablesAPI.ResourceLocators)
            {
                if (locator.Locate(address, null, out var locations) && locations != null && locations.Count > 0)
                    return true;
            }
            return false;
        }
    }
}
