using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;

namespace SickOfIt.VideoFrameExtractor.Runtime
{
    /// <summary>
    /// Controls PlayableDirector playback for sprite frame sequences.
    /// Place this on the same GameObject as the PlayableDirector.
    ///
    /// Solves the initialization problem: the Timeline mixer needs ProcessFrame
    /// to discover clips and start chunk loading, but we don't want the Timeline
    /// to advance until chunks are ready.
    ///
    /// Flow:
    /// 1. Start() — kicks the PlayableDirector graph (Play + Pause at t=0)
    ///    so the mixer initializes and begins preloading chunks.
    /// 2. Update() — polls all bound SpriteFrameRenderers for IsLoaded.
    /// 3. When all ready — fires OnReady event and (if playOnReady) resumes playback.
    ///
    /// Auto-discovers all SpriteFrameRenderer bindings from SpriteFrameTrack
    /// tracks on the Timeline — no manual assignment needed.
    /// </summary>
    [RequireComponent(typeof(PlayableDirector))]
    public class SpriteFramePlaybackController : MonoBehaviour
    {
        [Header("Playback")]
        [Tooltip("Automatically start playback when all renderers report IsLoaded.\n" +
                 "Disable if another script (e.g., VideoManager) handles Play().")]
        [SerializeField] private bool playOnReady = true;

        [Header("Events")]
        [Tooltip("Fired once when all SpriteFrameRenderers on this director are loaded.")]
        [SerializeField] private UnityEvent onReady = new UnityEvent();

        [Header("Debug (Read-Only)")]
        [SerializeField] private bool isReady;
        [SerializeField] private int loadedCount;
        [SerializeField] private int totalRenderers;

        private PlayableDirector director;
        private readonly List<SpriteFrameRenderer> renderers = new List<SpriteFrameRenderer>();
        private bool graphKicked;

        #region Public Properties

        /// <summary>True when all bound SpriteFrameRenderers have loaded their initial chunks.</summary>
        public bool IsReady => isReady;

        /// <summary>Event fired once when all renderers are ready.</summary>
        public UnityEvent OnReady => onReady;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            director = GetComponent<PlayableDirector>();

            // Ensure the director doesn't auto-play — we control playback
            director.playOnAwake = false;
        }

        private void Start()
        {
            DiscoverRenderers();
            totalRenderers = renderers.Count;

            if (totalRenderers == 0)
            {
                // No sprite frame tracks — mark ready immediately
                isReady = true;
                onReady.Invoke();

                if (playOnReady)
                {
                    director.time = 0;
                    director.Play();
                }
                return;
            }

            KickGraph();
        }

        private void Update()
        {
            if (isReady) return;
            if (!graphKicked) return;

            // Poll all renderers
            int loaded = 0;
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null && renderers[i].IsLoaded)
                    loaded++;
            }
            loadedCount = loaded;

            if (loaded < totalRenderers) return;

            // All ready!
            isReady = true;
            onReady.Invoke();

            if (playOnReady)
            {
                director.time = 0;
                director.Resume();
            }
        }

        #endregion

        #region Graph Initialization

        /// <summary>
        /// Kicks the PlayableDirector graph: Play, Pause, then force Evaluate.
        /// Play() creates the graph, Pause() prevents it from advancing,
        /// Evaluate() forces one ProcessFrame at t=0 so the mixer discovers
        /// clips and starts preloading chunks.
        /// </summary>
        private void KickGraph()
        {
            director.time = 0;
            director.Play();
            director.Pause();
            director.Evaluate();
            graphKicked = true;
        }

        #endregion

        #region Auto-Discovery

        /// <summary>
        /// Finds all SpriteFrameRenderer bindings from SpriteFrameTrack tracks
        /// on the PlayableDirector's TimelineAsset.
        /// </summary>
        private void DiscoverRenderers()
        {
            renderers.Clear();

            if (director.playableAsset == null) return;

            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null) return;

            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is SpriteFrameTrack)
                {
                    var binding = director.GetGenericBinding(track) as SpriteFrameRenderer;
                    if (binding != null && !renderers.Contains(binding))
                        renderers.Add(binding);
                }
            }
        }

        #endregion
    }
}
