using UnityEngine;
using System.Collections.Generic;

namespace MobileControllerFramework
{
    /// <summary>
    /// Cycles through a list of audio sources (songs) when a button is pressed.
    /// Each song starts from the beginning when switched.
    /// </summary>
    public class MusicCycler : MonoBehaviour
    {
        [Header("Button")]
        [Tooltip("The button from nappin's framework that triggers song cycling")]
        public Button cycleButton;

        [Header("Songs")]
        [Tooltip("List of AudioSources containing the songs to cycle through")]
        public List<AudioSource> songs = new List<AudioSource>();

        [Header("Settings")]
        [Tooltip("Auto-play the first song on start")]
        public bool autoPlayOnStart = true;

        private int currentSongIndex = -1;

        private void Start()
        {
            if (cycleButton == null)
            {
                Debug.LogWarning("MusicCycler: No button assigned!");
                return;
            }

            // Subscribe to button tap event (fires when button is released)
            cycleButton.TapEndEvent.AddListener(CycleToNextSong);

            // Auto-play first song if enabled
            if (autoPlayOnStart && songs.Count > 0)
            {
                PlaySong(0);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from button events to prevent memory leaks
            if (cycleButton != null)
            {
                cycleButton.TapEndEvent.RemoveListener(CycleToNextSong);
            }
        }

        /// <summary>
        /// Cycles to the next song in the list
        /// </summary>
        public void CycleToNextSong()
        {
            if (songs.Count == 0)
            {
                Debug.LogWarning("MusicCycler: No songs in the list!");
                return;
            }

            // Stop current song if playing
            if (currentSongIndex >= 0 && currentSongIndex < songs.Count)
            {
                AudioSource currentSong = songs[currentSongIndex];
                if (currentSong != null)
                {
                    currentSong.Stop();
                }
            }

            // Move to next song (loop back to 0 if at the end)
            currentSongIndex = (currentSongIndex + 1) % songs.Count;

            // Play the new song
            PlaySong(currentSongIndex);
        }

        /// <summary>
        /// Plays a specific song by index
        /// </summary>
        private void PlaySong(int index)
        {
            if (index < 0 || index >= songs.Count)
            {
                Debug.LogWarning($"MusicCycler: Song index {index} is out of range!");
                return;
            }

            AudioSource song = songs[index];
            if (song == null)
            {
                Debug.LogWarning($"MusicCycler: Song at index {index} is null!");
                return;
            }

            // Start the song from the beginning
            song.time = 0f;
            song.Play();

            Debug.Log($"MusicCycler: Now playing song {index + 1}/{songs.Count}");
        }

        /// <summary>
        /// Plays a specific song by index (can be called from UI or other scripts)
        /// </summary>
        public void PlaySongAtIndex(int index)
        {
            // Stop current song
            if (currentSongIndex >= 0 && currentSongIndex < songs.Count)
            {
                AudioSource currentSong = songs[currentSongIndex];
                if (currentSong != null)
                {
                    currentSong.Stop();
                }
            }

            currentSongIndex = index;
            PlaySong(index);
        }

        /// <summary>
        /// Stops all songs
        /// </summary>
        public void StopAllSongs()
        {
            foreach (AudioSource song in songs)
            {
                if (song != null)
                {
                    song.Stop();
                }
            }
            currentSongIndex = -1;
        }
    }
}
