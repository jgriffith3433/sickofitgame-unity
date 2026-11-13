using System;
using UnityEditor;
using UnityEditor.Build;

namespace SickOfIt.Tools.Editor
{
    /// <summary>
    /// The WebGL player settings every deploy builds with, set here in code so they can't drift in ProjectSettings.asset
    /// (an LFS file agents can't read) and are written to the build log. <see cref="Apply"/> sets them and returns a scope
    /// that puts the project's own values back after the build, so a deploy never dirties ProjectSettings.asset.
    ///
    /// Compression: gzip with Unity's decompression fallback. The builds host (sites.hawkesgen.ai) and the game pages
    /// (Hawkes Games' /play/sickofit, the template page) ask for SickOfIt.*.unityweb and the host sends those without a
    /// Content-Encoding, so the browser can't decode them and Unity's loader does it in JavaScript. Measured on v17
    /// (2026-11-08): Brotli would save 3.9 MB of 25.2 MB, but its JavaScript decoder is several times slower than gzip's on
    /// a phone, which costs more than the download it saves. Brotli pays only without the fallback (SickOfIt.*.br served
    /// with Content-Encoding: br), which needs the hosted page to ask for .br files first (Docs/BuildSize.md).
    /// </summary>
    public static class WebGLPlayerSettings
    {
        public sealed class Restore : IDisposable
        {
            readonly WebGLCompressionFormat compression;
            readonly bool fallback, strip, dataCaching, nameAsHashes, development;
            readonly UnityEditor.WebGL.WasmCodeOptimization codeOptimization;
            readonly Il2CppCodeGeneration codeGeneration;
            readonly WebGLExceptionSupport exceptions;
            bool done;

            internal Restore()
            {
                compression = PlayerSettings.WebGL.compressionFormat;
                fallback = PlayerSettings.WebGL.decompressionFallback;
                strip = PlayerSettings.stripEngineCode;
                dataCaching = PlayerSettings.WebGL.dataCaching;
                nameAsHashes = PlayerSettings.WebGL.nameFilesAsHashes;
                development = EditorUserBuildSettings.development;
                codeOptimization = UnityEditor.WebGL.UserBuildSettings.codeOptimization;
                codeGeneration = PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL);
                exceptions = PlayerSettings.WebGL.exceptionSupport;
            }

            public void Dispose()
            {
                if (done)
                    return;
                done = true;
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                PlayerSettings.stripEngineCode = strip;
                PlayerSettings.WebGL.dataCaching = dataCaching;
                PlayerSettings.WebGL.nameFilesAsHashes = nameAsHashes;
                EditorUserBuildSettings.development = development;
                UnityEditor.WebGL.UserBuildSettings.codeOptimization = codeOptimization;
                PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, codeGeneration);
                PlayerSettings.WebGL.exceptionSupport = exceptions;
            }
        }

        /// <summary>Sets the deploy's WebGL settings (logging each one, before and after); dispose the result after the build.</summary>
        public static Restore Apply(Action<string> log)
        {
            var restore = new Restore();
            log("  WebGL player settings for this build (the project's own values come back after it):");
            Set(log, "compression", PlayerSettings.WebGL.compressionFormat, WebGLCompressionFormat.Gzip,
                v => PlayerSettings.WebGL.compressionFormat = v);
            Set(log, "decompression fallback (.unityweb names, decoded by the loader)", PlayerSettings.WebGL.decompressionFallback, true,
                v => PlayerSettings.WebGL.decompressionFallback = v);
            // The pages ask for SickOfIt.data.unityweb and friends by name.
            Set(log, "file names as hashes", PlayerSettings.WebGL.nameFilesAsHashes, false, v => PlayerSettings.WebGL.nameFilesAsHashes = v);
            // Unused engine modules out of the wasm (Addressables writes a link.xml for the types its bundles use).
            Set(log, "strip engine code", PlayerSettings.stripEngineCode, true, v => PlayerSettings.stripEngineCode = v);
            // A second visit reads SickOfIt.data from the browser's cache instead of downloading it again.
            Set(log, "data caching", PlayerSettings.WebGL.dataCaching, true, v => PlayerSettings.WebGL.dataCaching = v);
            Set(log, "development build", EditorUserBuildSettings.development, false, v => EditorUserBuildSettings.development = v);
            // The wasm is the biggest thing a phone downloads before play (10.6 MB, ~50 s at Slow 4G): build it for disk size
            // (clang -Oz with link-time optimisation) and let IL2CPP share generic code. Phone-load / first-five report
            // the seconds, the phone twin's fpsP5 says whether the shared generics cost frame time.
            Set(log, "wasm code optimization", UnityEditor.WebGL.UserBuildSettings.codeOptimization,
                UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO, v => UnityEditor.WebGL.UserBuildSettings.codeOptimization = v);
            Set(log, "IL2CPP code generation", PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL),
                Il2CppCodeGeneration.OptimizeSize, v => PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, v));
            // Exception support: explicit `throw`s stay catchable (the JSON importer and loaders use them); only the implicit
            // null/bounds checks lose their try/catch landing pads, which is a few percent of the wasm.
            Set(log, "exception support", PlayerSettings.WebGL.exceptionSupport,
                WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly, v => PlayerSettings.WebGL.exceptionSupport = v);
            // Not changed on purpose: managed stripping (the JSON content types are found by reflection, so a higher level
            // needs Assets/link.xml checked in a WebGL build first).
            log($"    managed stripping {PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL)} (unchanged)");
            return restore;
        }

        static void Set<T>(Action<string> log, string name, T current, T wanted, Action<T> set)
        {
            if (Equals(current, wanted))
            {
                log($"    {name}: {wanted}");
                return;
            }
            set(wanted);
            log($"    {name}: {current} -> {wanted}");
        }
    }
}
