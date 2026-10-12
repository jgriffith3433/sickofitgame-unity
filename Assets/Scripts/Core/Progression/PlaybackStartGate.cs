using System;
using System.Collections;
using System.Collections.Generic;
using SickOfIt.VideoFrameExtractor.Runtime;
using UnityEngine;

namespace SickOfIt.Core.Flow
{
    /// <summary>
    /// Holds a level, video, or death scene at time zero until its videos and
    /// audio are loaded, then waits a short beat so they can start together.
    /// </summary>
    public static class PlaybackStartGate
    {
        public const float SettleSeconds = 0.4f;
        const float ReadyTimeoutSeconds = 8f;
        const float ActiveTimeoutSeconds = 10f;

        public static IEnumerator Hold(
            GameClock clock,
            IList<SpriteFramePlayer> players,
            AudioSource source,
            Action beginPlayback = null)
        {
            if (clock != null)
                clock.Pause();

            float waited = 0f;
            while (waited < ReadyTimeoutSeconds && !MediaReady(players, source))
            {
                RequestAudioLoad(source);
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            waited = 0f;
            while (waited < ActiveTimeoutSeconds && !SourceActive(source))
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSecondsRealtime(SettleSeconds);

            SnapPlayers(players);

            if (beginPlayback != null)
                beginPlayback();
            else
                PlaySource(source);

            if (clock != null)
                clock.Play();
        }

        static bool MediaReady(IList<SpriteFramePlayer> players, AudioSource source)
        {
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player != null && !player.IsReady)
                        return false;
                }
            }

            var clip = source != null ? source.clip : null;
            if (clip == null)
                return true;

            return clip.loadState == AudioDataLoadState.Loaded
                || clip.loadState == AudioDataLoadState.Failed;
        }

        static void RequestAudioLoad(AudioSource source)
        {
            var clip = source != null ? source.clip : null;
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
        }

        static bool SourceActive(AudioSource source)
        {
            if (source == null || source.clip == null)
                return true;
            return source.gameObject.activeInHierarchy;
        }

        static void SnapPlayers(IList<SpriteFramePlayer> players)
        {
            if (players == null)
                return;

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                    continue;
                player.UseGameClock(true);
                player.SetTime(0f);
                player.Play();
            }
        }

        static void PlaySource(AudioSource source)
        {
            if (source == null || source.clip == null)
                return;
            if (!source.gameObject.activeInHierarchy)
                return;

            source.enabled = true;
            source.Play();
        }
    }
}
