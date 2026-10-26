using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Input
{
    /// <summary>
    /// Plays one of player.json's animation states straight from its sprite sheet when the player's generated
    /// Animator doesn't have it yet (a scene clip such as Curious, Shout or Grabbed added to the config but not
    /// generated into Character.prefab's controller). The Animator is paused while it plays and picks up again,
    /// at its default state, as soon as the next real state is asked for (<see cref="Stop"/>).
    /// Pure presentation: it only changes the sprite on screen. Frames come from the asset registry
    /// (<see cref="GameDefinition.SpritesOf"/>), which holds every sheet the config references, or, in a player build, a found
    /// weapon's grip sheet loaded on demand (<see cref="SickOfIt.Core.Addressables.PlayerSheets"/>; false until it has loaded).
    /// </summary>
    public class PlayerSceneClip : MonoBehaviour
    {
        Animator animator;
        SpriteRenderer target;
        List<Sprite> frames;
        float fps;
        bool loop;
        float time;

        /// <summary>The state playing, or null.</summary>
        public string State { get; private set; }

        /// <summary>The sheet frame on screen (0-based), or -1.</summary>
        public int Frame { get; private set; } = -1;

        /// <summary>
        /// Starts <paramref name="state"/> from player.json's animationConfig on the Animator's sprite.
        /// False if the config has no such state or its sheet has no sprites in the asset registry.
        /// </summary>
        public bool Play(Animator source, string state)
        {
            var config = FindState(state);
            if (config == null || source == null)
                return false;
            var sprites = Filter(SickOfIt.Core.Addressables.PlayerSheets.Of(config), config.spriteNameFilter);
            var selected = config.SelectFrames(sprites);
            if (selected.Count == 0)
                return false;
            if (!source.TryGetComponent(out SpriteRenderer renderer))
                renderer = source.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null)
                return false;

            Stop();
            animator = source;
            target = renderer;
            frames = selected;
            fps = Mathf.Max(1, config.fps) * (config.speed > 0f ? config.speed : 1f);
            loop = config.loop;
            time = 0f;
            State = config.stateName;
            animator.enabled = false;
            Show();
            return true;
        }

        /// <summary>Stretches the playing one-shot clip over 'seconds' (a punch over its swing, so the hit falls mid-clip).</summary>
        public void FitTo(float seconds)
        {
            if (State == null || frames == null || seconds <= 0.01f)
                return;
            loop = false;
            fps = frames.Count / seconds;
        }

        /// <summary>Hands the sprite back to the Animator.</summary>
        public void Stop()
        {
            if (State == null)
                return;
            State = null;
            Frame = -1;
            frames = null;
            if (animator != null)
                animator.enabled = true;
        }

        void LateUpdate()
        {
            if (State == null)
                return;
            if (target == null || animator == null)
            {
                Stop();
                return;
            }
            time += Time.deltaTime;
            Show();
        }

        void Show()
        {
            int index = Mathf.FloorToInt(time * fps);
            index = loop ? index % frames.Count : Mathf.Min(index, frames.Count - 1);
            Frame = index;
            target.sprite = frames[index];
        }

        void OnDisable() => Stop();

        static AnimationStateConfig FindState(string state)
        {
            var gd = GameDefinition.Instance;
            var states = gd != null && gd.player != null && gd.player.animationConfig != null ? gd.player.animationConfig.states : null;
            if (states == null)
                return null;
            foreach (var s in states)
            {
                if (s != null && s.stateName == state)
                    return s;
            }
            return null;
        }

        static List<Sprite> Filter(Sprite[] sprites, string filter)
        {
            var kept = new List<Sprite>(sprites.Length);
            foreach (var sprite in sprites)
            {
                if (sprite != null && (string.IsNullOrEmpty(filter) || sprite.name.Contains(filter)))
                    kept.Add(sprite);
            }
            return kept;
        }
    }
}
