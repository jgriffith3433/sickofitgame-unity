#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.VideoFrameExtractor.Editor;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// Bakes character mp4 clips into sliced sprite sheets and rebuilds the animator prefab.
    ///
    /// Player clips live at {PlayerSourceRoot}/{State}/Output/{State}.mp4
    /// (Idle, Walk, Jump, Fall, Fire).
    /// Enemy clips live at {EnemySourceRoot}/{EnemyType}/{State}/Output/{State}.mp4,
    /// using that enemy's animation state names.
    ///
    /// Sheets are imported as multiple sprites so AnimationGenerator can key them.
    /// This does not go through the addressable video-chunk pipeline.
    /// </summary>
    public static class CharacterSpriteBaker
    {
        public const string PlayerSourceRoot = @"C:\Projects\SickOfItGamePlayer";
        public const string EnemySourceRoot = @"C:\Projects\SickOfItGameEnemies";

        const int MaxSheetSize = 4096;
        const float SpritePixelsPerUnit = 100f;
        const int DefaultClearCutoff = 28;

        static int activeMaxSheet = MaxSheetSize;
        static float activePixelsPerUnit = SpritePixelsPerUnit;
        static int activeClearCutoff = DefaultClearCutoff;

        static void UseArt(GameDefinition gd)
        {
            var art = gd != null && gd.characterArt != null ? gd.characterArt : null;
            activeMaxSheet = art != null && art.maxSheetSize > 0 ? art.maxSheetSize : MaxSheetSize;
            activePixelsPerUnit = art != null && art.pixelsPerUnit > 0f ? art.pixelsPerUnit : SpritePixelsPerUnit;
            activeClearCutoff = art != null ? art.clearCutoff : DefaultClearCutoff;
        }

        public static string PlayerRoot(GameDefinition gd)
        {
            var art = gd != null ? gd.characterArt : null;
            return art != null && !string.IsNullOrEmpty(art.playerSourceRoot) ? art.playerSourceRoot : PlayerSourceRoot;
        }

        public static string EnemyRoot(GameDefinition gd)
        {
            var art = gd != null ? gd.characterArt : null;
            return art != null && !string.IsNullOrEmpty(art.enemySourceRoot) ? art.enemySourceRoot : EnemySourceRoot;
        }
        const string PlayerSheetFolder = "Assets/Art/Sprites/Player";

        class BakedClip
        {
            public string StateName;
            public Texture2D Sheet;
            public int FrameCount;
            public string AssetPath;
        }

        [MenuItem("SickOfIt/Regenerate Player Sprites", false, 1001)]
        static void MenuRegeneratePlayer()
        {
            CharacterSpriteRegenWindow.OpenPlayer();
        }

        [MenuItem("SickOfIt/Regenerate Enemy Sprites", false, 1002)]
        static void MenuRegenerateEnemies()
        {
            CharacterSpriteRegenWindow.OpenEnemies();
        }

        static GameDefinition LoadGameDefinition()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDefinition");
            if (guids.Length == 0)
                return null;
            return AssetDatabase.LoadAssetAtPath<GameDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public static void RegeneratePlayer(GameDefinition gd, string onlyState = null)
        {
            if (gd == null || gd.player == null)
            {
                EditorUtility.DisplayDialog("Regenerate Player Sprites", "No GameDefinition found. Create one via Assets → Create → Sick Of It → Game Definition.", "OK");
                return;
            }

            if (!EnsureFfmpeg())
                return;

            UseArt(gd);
            EnsurePlayerAnimConfig(gd);
            var states = gd.player.animationConfig.states;
            int fallbackFps = gd.player.animationFps > 0 ? gd.player.animationFps : 12;

            try
            {
                var baked = BakeStates(
                    PlayerRoot(gd),
                    states,
                    PlayerSheetFolder,
                    fallbackFps,
                    requireAll: true,
                    "Player",
                    onlyState,
                    out string error);

                if (baked == null)
                {
                    EditorUtility.DisplayDialog("Regenerate Player Sprites", error, "OK");
                    return;
                }

                Undo.RecordObject(gd, "Regenerate Player Sprites");
                AssignSheets(states, baked, out Texture2D fallback);
                bool bakedIdle = baked.Any(b => string.Equals(b.StateName, "Idle", StringComparison.OrdinalIgnoreCase));
                if (bakedIdle || gd.player.spriteSheet == null)
                    gd.player.spriteSheet = fallback;
                EditorUtility.SetDirty(gd);
                AssetDatabase.SaveAssets();

                var prefab = PlayerPrefabGenerator.Generate(gd);
                AssetDatabase.SaveAssets();

                string summary = DescribeBake("Character prefab", prefab != null, baked);
                Debug.Log($"[CharacterSpriteBaker] {summary}");
                EditorUtility.DisplayDialog("Regenerate Player Sprites", summary, "OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Regenerate Player Sprites", e.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void RegenerateAllEnemies(GameDefinition gd)
        {
            if (gd == null)
            {
                EditorUtility.DisplayDialog("Regenerate Enemy Sprites", "No GameDefinition found. Create one via Assets → Create → Sick Of It → Game Definition.", "OK");
                return;
            }

            if (gd.enemyTypes == null || gd.enemyTypes.Count == 0)
            {
                EditorUtility.DisplayDialog("Regenerate Enemy Sprites", "GameDefinition has no enemy types.", "OK");
                return;
            }

            if (!EnsureFfmpeg())
                return;

            var lines = new List<string>();
            try
            {
                foreach (var config in gd.enemyTypes)
                {
                    if (config == null)
                        continue;
                    string line;
                    try
                    {
                        line = RegenerateEnemyInternal(gd, config);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        line = $"{config.type}: {e.Message}";
                    }

                    lines.Add(line);
                    if (line.EndsWith("Cancelled.", StringComparison.Ordinal))
                        break;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string summary = string.Join("\n\n", lines);
            Debug.Log($"[CharacterSpriteBaker]\n{summary}");
            EditorUtility.DisplayDialog("Regenerate Enemy Sprites", summary, "OK");
        }

        public static void RegenerateEnemy(GameDefinition gd, EnemyTypeConfig config, string onlyState = null)
        {
            if (gd == null || config == null)
            {
                EditorUtility.DisplayDialog("Regenerate Enemy Sprites", "Missing GameDefinition or enemy config.", "OK");
                return;
            }

            if (!EnsureFfmpeg())
                return;

            string summary;
            try
            {
                summary = RegenerateEnemyInternal(gd, config, onlyState);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Regenerate Enemy Sprites", e.Message, "OK");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (string.IsNullOrEmpty(summary))
                return;

            Debug.Log($"[CharacterSpriteBaker] {summary}");
            EditorUtility.DisplayDialog("Regenerate Enemy Sprites", summary, "OK");
        }

        static string RegenerateEnemyInternal(GameDefinition gd, EnemyTypeConfig config, string onlyState = null)
        {
            EnsureEnemyAnimConfig(config);
            if (config.animationConfig == null || config.animationConfig.states == null || config.animationConfig.states.Count == 0)
                return $"{config.type}: no animation states.";

            UseArt(gd);
            string sourceRoot = Path.Combine(EnemyRoot(gd), config.type.ToString());
            string outputFolder = $"Assets/Art/Sprites/Enemies/{config.type}";
            int fallbackFps = config.animationFps > 0 ? config.animationFps : 12;
            var states = config.animationConfig.states;

            var baked = BakeStates(
                sourceRoot,
                states,
                outputFolder,
                fallbackFps,
                requireAll: false,
                config.type.ToString(),
                onlyState,
                out string error);

            if (baked == null)
                return $"{config.type}: {error}";

            Undo.RecordObject(gd, $"Regenerate {config.type} Sprites");
            AssignSheets(states, baked, out Texture2D fallback);
            string defaultState = config.animationConfig.defaultStateName;
            var defaultClip = baked.FirstOrDefault(b => string.Equals(b.StateName, defaultState, StringComparison.OrdinalIgnoreCase));
            if (defaultClip != null)
                config.spriteSheet = defaultClip.Sheet;
            else if (config.spriteSheet == null)
                config.spriteSheet = fallback;

            EditorUtility.SetDirty(gd);
            AssetDatabase.SaveAssets();

            var prefab = EnemyPrefabGenerator.GenerateFromConfig(config);
            AssetDatabase.SaveAssets();
            return DescribeBake($"{config.type} prefab", prefab != null, baked);
        }

        static void EnsurePlayerAnimConfig(GameDefinition gd)
        {
            if (gd.player.animationConfig == null || gd.player.animationConfig.states == null || gd.player.animationConfig.states.Count == 0)
                gd.player.animationConfig = AnimationConfigPopulator.CreatePlayerAnimConfig(gd.player);
        }

        static void EnsureEnemyAnimConfig(EnemyTypeConfig config)
        {
            if (config.animationConfig == null || config.animationConfig.states == null || config.animationConfig.states.Count == 0)
                config.animationConfig = AnimationConfigPopulator.CreateEnemyAnimConfig(config);
        }

        static bool EnsureFfmpeg()
        {
            if (FFmpegHelper.IsFFmpegAvailable())
                return true;

            EditorUtility.DisplayDialog(
                "FFmpeg Not Found",
                "FFmpeg was not found.\n\nInstall it with:\nwinget install ffmpeg\n\nThen restart Unity.",
                "OK");
            return false;
        }

        static List<BakedClip> BakeStates(
            string sourceRoot,
            List<AnimationStateConfig> states,
            string outputFolder,
            int fallbackFps,
            bool requireAll,
            string progressPrefix,
            string onlyState,
            out string error)
        {
            error = null;
            var jobs = new List<(AnimationStateConfig state, string mp4, int fps)>();
            var missing = new List<string>();

            foreach (var state in states)
            {
                if (state == null || string.IsNullOrEmpty(state.stateName))
                    continue;
                if (!string.IsNullOrEmpty(onlyState) &&
                    !string.Equals(state.stateName, onlyState, StringComparison.OrdinalIgnoreCase))
                    continue;

                string mp4 = Path.Combine(sourceRoot, state.stateName, "Output", state.stateName + ".mp4");
                if (!File.Exists(mp4))
                {
                    // A sheet baked elsewhere (the scene clips made on the server) is kept as it is.
                    if (state.spriteSheet != null && string.IsNullOrEmpty(onlyState))
                        Debug.Log($"[CharacterSpriteBaker] No {mp4}; keeping {AssetDatabase.GetAssetPath(state.spriteSheet)}.");
                    else
                        missing.Add(mp4);
                    continue;
                }

                int fps = state.fps > 0 ? state.fps : fallbackFps;
                jobs.Add((state, mp4, fps));
            }

            if (requireAll && missing.Count > 0)
            {
                error = "Missing clips:\n\n" + string.Join("\n", missing);
                return null;
            }

            if (jobs.Count == 0)
            {
                error = missing.Count > 0
                    ? "No clips found. Expected files such as:\n\n" + missing[0]
                    : "No animation states to bake.";
                return null;
            }

            var baked = new List<BakedClip>();
            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                float progress = (float)i / jobs.Count;
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Baking Character Sprites",
                        $"{progressPrefix} / {job.state.stateName}",
                        progress))
                {
                    error = "Cancelled.";
                    return null;
                }

                var clip = BakeClip(job.mp4, job.state.stateName, outputFolder, job.fps, out string bakeError);
                if (clip == null)
                {
                    error = $"{job.state.stateName}: {bakeError}";
                    return null;
                }

                baked.Add(clip);
            }

            return baked;
        }

        static BakedClip BakeClip(string mp4Path, string stateName, string outputFolder, int fps, out string error)
        {
            error = null;
            string tempDir = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "Temp", "CharacterFrames",
                outputFolder.Replace("Assets/", "").Replace("/", "_"),
                stateName));

            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);

                bool ok = false;
                string message = null;
                FFmpegHelper.ExtractFrames(mp4Path, fps, 0, tempDir, (success, msg) =>
                {
                    ok = success;
                    message = msg;
                });

                if (!ok)
                {
                    error = message ?? "ffmpeg failed.";
                    return null;
                }

                var frames = Directory.GetFiles(tempDir, "frame_*.jpg")
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                if (frames.Length == 0)
                {
                    error = "ffmpeg wrote no frames.";
                    return null;
                }

                ProjectGenerator.EnsureFolder(outputFolder);
                string assetPath = $"{outputFolder}/{stateName}.png";
                var sheet = PackSheet(frames, assetPath, stateName, out error);
                if (sheet == null)
                    return null;

                return new BakedClip
                {
                    StateName = stateName,
                    Sheet = sheet,
                    FrameCount = frames.Length,
                    AssetPath = assetPath
                };
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); }
                    catch (Exception e) { Debug.LogWarning($"[CharacterSpriteBaker] Could not delete {tempDir}: {e.Message}"); }
                }
            }
        }

        static Texture2D PackSheet(string[] framePaths, string assetPath, string stateName, out string error)
        {
            error = null;
            var probe = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!probe.LoadImage(File.ReadAllBytes(framePaths[0])))
            {
                UnityEngine.Object.DestroyImmediate(probe);
                error = "Could not read the first frame.";
                return null;
            }

            int srcW = probe.width;
            int srcH = probe.height;
            UnityEngine.Object.DestroyImmediate(probe);

            if (!TryFitGrid(framePaths.Length, srcW, srcH, activeMaxSheet, out int cols, out int rows, out int cellW, out int cellH))
            {
                error = $"Could not fit {framePaths.Length} frames of {srcW}x{srcH} into a {activeMaxSheet} sheet. Lower the animation FPS.";
                return null;
            }

            int texW = cols * cellW;
            int texH = rows * cellH;
            var sheet = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            sheet.SetPixels32(new Color32[texW * texH]);

            var metas = new SpriteMetaData[framePaths.Length];
            try
            {
                for (int i = 0; i < framePaths.Length; i++)
                {
                    var source = LoadFrame(framePaths[i], out string frameError);
                    if (source == null)
                    {
                        error = $"Frame {i}: {frameError}";
                        return null;
                    }

                    RemoveBlackBackground(source);
                    var scaled = ScaleTexture(source, cellW, cellH);
                    UnityEngine.Object.DestroyImmediate(source);

                    int col = i % cols;
                    int row = i / cols;
                    int x = col * cellW;
                    int y = texH - (row + 1) * cellH;
                    sheet.SetPixels(x, y, cellW, cellH, scaled.GetPixels());
                    UnityEngine.Object.DestroyImmediate(scaled);

                    metas[i] = new SpriteMetaData
                    {
                        name = $"{stateName}_{i:D4}",
                        rect = new Rect(x, y, cellW, cellH),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    };
                }

                sheet.Apply();
                string absolutePath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
                File.WriteAllBytes(absolutePath, sheet.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sheet);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                error = $"Importer missing for {assetPath}.";
                return null;
            }

            int maxSize = 32;
            int maxDim = Mathf.Max(texW, texH);
            while (maxSize < maxDim)
                maxSize *= 2;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = maxSize;
            importer.spritePixelsPerUnit = activePixelsPerUnit;
            importer.isReadable = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMode = (int)SpriteImportMode.Multiple;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spritePixelsPerUnit = activePixelsPerUnit;
            settings.alphaIsTransparency = true;
            settings.mipmapEnabled = false;
            settings.readable = false;
            settings.npotScale = TextureImporterNPOTScale.None;
            importer.SetTextureSettings(settings);
            importer.spritesheet = metas;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                error = $"Failed to load {assetPath}.";
                return null;
            }

            return texture;
        }

        /// <summary>
        /// JPEG loads replace the texture with RGB24, which throws away alpha.
        /// Copy into RGBA32 before the black plate is keyed out.
        /// </summary>
        static Texture2D LoadFrame(string path, out string error)
        {
            error = null;
            var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!loaded.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.DestroyImmediate(loaded);
                error = "Could not read the image.";
                return null;
            }

            if (loaded.format == TextureFormat.RGBA32)
                return loaded;

            var copy = new Texture2D(loaded.width, loaded.height, TextureFormat.RGBA32, false);
            copy.SetPixels32(loaded.GetPixels32());
            copy.Apply();
            UnityEngine.Object.DestroyImmediate(loaded);
            return copy;
        }

        static bool TryFitGrid(int frameCount, int srcW, int srcH, int maxSheet, out int cols, out int rows, out int cellW, out int cellH)
        {
            cols = rows = cellW = cellH = 0;
            if (frameCount <= 0 || srcW < 4 || srcH < 4 || maxSheet < 4)
                return false;

            float aspect = (float)srcW / srcH;
            int bestArea = -1;

            for (int c = 1; c <= frameCount; c++)
            {
                int r = (frameCount + c - 1) / c;
                int maxCellW = Mathf.Min(srcW, maxSheet / c) & ~3;
                if (maxCellW < 4)
                    continue;

                int cellHFromW = ((int)(maxCellW / aspect)) & ~3;
                cellHFromW = Mathf.Min(cellHFromW, srcH & ~3);
                if (cellHFromW < 4)
                    continue;

                int candidateW;
                int candidateH;
                if (r * cellHFromW <= maxSheet)
                {
                    candidateW = maxCellW;
                    candidateH = cellHFromW;
                }
                else
                {
                    int maxCellH = Mathf.Min(srcH, maxSheet / r) & ~3;
                    if (maxCellH < 4)
                        continue;
                    candidateW = ((int)(maxCellH * aspect)) & ~3;
                    candidateW = Mathf.Min(candidateW, maxCellW);
                    candidateH = maxCellH;
                    if (candidateW < 4 || c * candidateW > maxSheet || r * candidateH > maxSheet)
                        continue;
                }

                int area = candidateW * candidateH;
                if (area > bestArea)
                {
                    bestArea = area;
                    cols = c;
                    rows = r;
                    cellW = candidateW;
                    cellH = candidateH;
                }
            }

            return bestArea > 0;
        }

        /// <summary>
        /// Clears the black plate behind the character. Only near-black pixels
        /// connected to the frame edge are removed, so dark armor and the gun
        /// interior stay. Edge color is then bled into the cleared pixels so
        /// scaling does not put a dark fringe back.
        /// </summary>
        static void RemoveBlackBackground(Texture2D texture)
        {
            byte ClearCutoff = (byte)Mathf.Clamp(activeClearCutoff, 0, 255);

            int w = texture.width;
            int h = texture.height;
            var pixels = texture.GetPixels32();
            var cleared = new bool[pixels.Length];
            var queue = new Queue<int>(w * 2);

            void Consider(int x, int y)
            {
                if ((uint)x >= (uint)w || (uint)y >= (uint)h)
                    return;
                int i = y * w + x;
                if (cleared[i])
                    return;
                var p = pixels[i];
                if (p.r > ClearCutoff || p.g > ClearCutoff || p.b > ClearCutoff)
                    return;
                cleared[i] = true;
                queue.Enqueue(i);
            }

            for (int x = 0; x < w; x++)
            {
                Consider(x, 0);
                Consider(x, h - 1);
            }
            for (int y = 0; y < h; y++)
            {
                Consider(0, y);
                Consider(w - 1, y);
            }

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                var p = pixels[i];
                p.a = 0;
                pixels[i] = p;
                int x = i % w;
                int y = i / w;
                Consider(x - 1, y);
                Consider(x + 1, y);
                Consider(x, y - 1);
                Consider(x, y + 1);
            }

            BleedColorIntoClearedPixels(pixels, cleared, w, h);
            texture.SetPixels32(pixels);
            texture.Apply();
        }

        static void BleedColorIntoClearedPixels(Color32[] pixels, bool[] cleared, int w, int h)
        {
            var hasColor = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                hasColor[i] = !cleared[i];

            var scratch = new Color32[pixels.Length];
            var scratchHas = new bool[pixels.Length];

            for (int pass = 0; pass < 4; pass++)
            {
                Array.Copy(pixels, scratch, pixels.Length);
                Array.Copy(hasColor, scratchHas, hasColor.Length);

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (hasColor[i])
                            continue;

                        int r = 0, g = 0, b = 0, n = 0;
                        AddNeighbor(pixels, hasColor, w, h, x - 1, y, ref r, ref g, ref b, ref n);
                        AddNeighbor(pixels, hasColor, w, h, x + 1, y, ref r, ref g, ref b, ref n);
                        AddNeighbor(pixels, hasColor, w, h, x, y - 1, ref r, ref g, ref b, ref n);
                        AddNeighbor(pixels, hasColor, w, h, x, y + 1, ref r, ref g, ref b, ref n);
                        if (n == 0)
                            continue;

                        scratch[i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 0);
                        scratchHas[i] = true;
                    }
                }

                Array.Copy(scratch, pixels, pixels.Length);
                Array.Copy(scratchHas, hasColor, hasColor.Length);
            }
        }

        static void AddNeighbor(Color32[] pixels, bool[] hasColor, int w, int h, int x, int y, ref int r, ref int g, ref int b, ref int n)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h)
                return;
            int i = y * w + x;
            if (!hasColor[i])
                return;
            var s = pixels[i];
            r += s.r;
            g += s.g;
            b += s.b;
            n++;
        }

        static Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            var src = source.GetPixels32();
            int sw = source.width;
            int sh = source.height;
            if (sw == targetWidth && sh == targetHeight)
            {
                var same = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
                same.SetPixels32(src);
                same.Apply();
                return same;
            }

            var dst = new Color32[targetWidth * targetHeight];
            for (int y = 0; y < targetHeight; y++)
            {
                float v = (y + 0.5f) * sh / targetHeight - 0.5f;
                int y0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, sh - 1);
                int y1 = Mathf.Min(y0 + 1, sh - 1);
                float fy = Mathf.Clamp01(v - y0);
                int row = y * targetWidth;
                for (int x = 0; x < targetWidth; x++)
                {
                    float u = (x + 0.5f) * sw / targetWidth - 0.5f;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, sw - 1);
                    int x1 = Mathf.Min(x0 + 1, sw - 1);
                    float fx = Mathf.Clamp01(u - x0);
                    var c00 = src[y0 * sw + x0];
                    var c10 = src[y0 * sw + x1];
                    var c01 = src[y1 * sw + x0];
                    var c11 = src[y1 * sw + x1];
                    dst[row + x] = Lerp(Lerp(c00, c10, fx), Lerp(c01, c11, fx), fy);
                }
            }

            var scaled = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            scaled.SetPixels32(dst);
            scaled.Apply();
            return scaled;
        }

        static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                (byte)(a.a + (b.a - a.a) * t));
        }

        static void AssignSheets(List<AnimationStateConfig> states, List<BakedClip> baked, out Texture2D fallback)
        {
            foreach (var state in states)
            {
                var match = baked.FirstOrDefault(b => string.Equals(b.StateName, state.stateName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    state.spriteSheet = match.Sheet;
            }

            var idle = baked.FirstOrDefault(b => string.Equals(b.StateName, "Idle", StringComparison.OrdinalIgnoreCase));
            fallback = idle != null ? idle.Sheet : baked[0].Sheet;
        }

        static string DescribeBake(string label, bool prefabRebuilt, List<BakedClip> baked)
        {
            var lines = new List<string>
            {
                prefabRebuilt
                    ? $"Rebuilt the {label} from {baked.Count} clip{(baked.Count == 1 ? "" : "s")}."
                    : $"Packed {baked.Count} clip{(baked.Count == 1 ? "" : "s")}, but the prefab was not rebuilt."
            };
            foreach (var clip in baked)
                lines.Add($"{clip.StateName}: {clip.FrameCount} frames → {clip.AssetPath}");
            return string.Join("\n", lines);
        }
    }

    public class CharacterSpriteRegenWindow : EditorWindow
    {
        enum Mode
        {
            Player,
            Enemies
        }

        Mode mode;
        Vector2 scroll;

        public static void OpenPlayer()
        {
            Open(Mode.Player, "Regenerate Player Sprites");
        }

        public static void OpenEnemies()
        {
            Open(Mode.Enemies, "Regenerate Enemy Sprites");
        }

        static void Open(Mode mode, string title)
        {
            var window = GetWindow<CharacterSpriteRegenWindow>(true, title, true);
            window.mode = mode;
            window.minSize = new Vector2(420, 220);
            window.Show();
        }

        void OnGUI()
        {
            var gd = LoadGameDefinition();
            if (gd == null)
            {
                EditorGUILayout.HelpBox("No GameDefinition found.", MessageType.Error);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (mode == Mode.Player)
                DrawPlayer(gd);
            else
                DrawEnemies(gd);
            EditorGUILayout.EndScrollView();
        }

        void DrawPlayer(GameDefinition gd)
        {
            if (gd.player == null)
            {
                EditorGUILayout.HelpBox("GameDefinition has no player config.", MessageType.Error);
                return;
            }

            if (gd.player.animationConfig == null || gd.player.animationConfig.states == null || gd.player.animationConfig.states.Count == 0)
                gd.player.animationConfig = AnimationConfigPopulator.CreatePlayerAnimConfig(gd.player);

            EditorGUILayout.LabelField("Player", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(CharacterSpriteBaker.PlayerRoot(gd), EditorStyles.miniLabel);
            EditorGUILayout.Space(6);
            DrawStateButtons(gd.player.animationConfig.states, stateName =>
            {
                Run(() => CharacterSpriteBaker.RegeneratePlayer(gd, stateName));
            });
        }

        void DrawEnemies(GameDefinition gd)
        {
            if (gd.enemyTypes == null || gd.enemyTypes.Count == 0)
            {
                EditorGUILayout.HelpBox("GameDefinition has no enemy types.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(CharacterSpriteBaker.EnemyRoot(gd) + "\\{Type}\\{State}\\Output", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);

            foreach (var config in gd.enemyTypes)
            {
                if (config == null)
                    continue;

                if (config.animationConfig == null || config.animationConfig.states == null || config.animationConfig.states.Count == 0)
                    config.animationConfig = AnimationConfigPopulator.CreateEnemyAnimConfig(config);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(string.IsNullOrEmpty(config.displayName) ? config.type.ToString() : $"{config.displayName} ({config.type})", EditorStyles.boldLabel);
                var enemy = config;
                if (enemy.animationConfig == null || enemy.animationConfig.states == null || enemy.animationConfig.states.Count == 0)
                    EditorGUILayout.LabelField("No animation states.");
                else
                {
                    DrawStateButtons(enemy.animationConfig.states, stateName =>
                    {
                        Run(() => CharacterSpriteBaker.RegenerateEnemy(gd, enemy, stateName));
                    });
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("All Enemies", GUILayout.Height(32)))
                Run(() => CharacterSpriteBaker.RegenerateAllEnemies(gd));
        }

        static void DrawStateButtons(List<AnimationStateConfig> states, Action<string> bake)
        {
            EditorGUILayout.BeginHorizontal();
            int across = 0;
            foreach (var state in states)
            {
                if (state == null || string.IsNullOrEmpty(state.stateName))
                    continue;
                if (across > 0 && across % 4 == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }

                string stateName = state.stateName;
                if (GUILayout.Button(stateName, GUILayout.Height(28)))
                    bake(stateName);
                across++;
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("All", GUILayout.Height(28)))
                bake(null);
        }

        static GameDefinition LoadGameDefinition()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameDefinition");
            if (guids.Length == 0)
                return null;
            return AssetDatabase.LoadAssetAtPath<GameDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        void Run(Action bake)
        {
            Close();
            EditorApplication.delayCall += () => bake();
        }
    }
}
#endif
