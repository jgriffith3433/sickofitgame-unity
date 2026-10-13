using System;
using UnityEngine;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// Authoring folders outside the Unity project.
    /// A clip lives at {root}/{Id}/Output/{Id}.mp4.
    /// A still lives at {imageRoot}/{Category}/{File}.png.
    /// </summary>
    [Serializable]
    public class SourceMediaConfig
    {
        public string videoRoot = @"C:\Projects\SickOfItGameVideos";
        public string objectRoot = @"C:\Projects\SickOfItGameObjects";
        public string imageRoot = @"C:\Projects\SickOfItGameImages";
    }
}
