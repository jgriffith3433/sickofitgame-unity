#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HawkesGen;

namespace SickOfIt.Tools.Editor.Audio
{
    /// <summary>
    /// The Sound Generator's link to Hawkes Gen (C:\Projects\hawkesgen, the shared generative-AI service), through the
    /// com.hawkes.gen package. Effects are its audio.sfx tool, lines audio.voice, voice design audio.voice-design and
    /// audio.voice-save. Hawkes Gen holds the ElevenLabs key, retries rate limits and keeps every file in its library
    /// with the recipe that made it. Calls must be awaited on the main thread (the package polls from EditorApplication.update).
    /// </summary>
    public static class HawkesGenAudio
    {
        public const string Project = "sickofit";

        /// <summary>Runs one job to the end. Cancelling cancels the job in Hawkes Gen.</summary>
        public static Task<GenJob> RunAsync(string tool, Dictionary<string, object> parameters, CancellationToken cancel,
            Dictionary<string, object> labels = null)
        {
            var tcs = new TaskCompletionSource<GenJob>();
            cancel.ThrowIfCancellationRequested();
            HawkesGenClient.Submit(tool, Project, parameters, null, null, (job, submitError) =>
            {
                if (submitError != null || job == null)
                {
                    // The service is down or refused the request: every other request would fail the same way.
                    tcs.TrySetException(new HawkesGenAudioException(submitError ?? "Hawkes Gen returned no job", true));
                    return;
                }
                var registration = cancel.Register(() => HawkesGenClient.PostJson($"/jobs/{job.Id}/cancel", null, (_, __) => { }));
                HawkesGenClient.Wait(job.Id, null, (finished, error) =>
                {
                    registration.Dispose();
                    if (cancel.IsCancellationRequested)
                        tcs.TrySetCanceled();
                    else if (error != null)
                        tcs.TrySetException(new HawkesGenAudioException($"{error} (job {job.Id})", IsFatal(error)));
                    else
                        tcs.TrySetResult(finished);
                });
            }, labels);
            return tcs.Task;
        }

        /// <summary>A library asset made from exactly this tool version and params, or null (POST /v1/recipes/check).</summary>
        public static Task<string> FindExistingAsync(string tool, Dictionary<string, object> parameters)
        {
            var tcs = new TaskCompletionSource<string>();
            var body = new Dictionary<string, object> { ["tool"] = tool, ["project"] = Project, ["params"] = parameters, ["inputs"] = new Dictionary<string, object>() };
            HawkesGenClient.PostJson("/recipes/check", body, (res, error) =>
            {
                if (error != null)
                {
                    tcs.TrySetException(new HawkesGenAudioException(error, true));
                    return;
                }
                var d = res as Dictionary<string, object>;
                var asset = d != null && d.TryGetValue("asset", out var a) ? a as Dictionary<string, object> : null;
                tcs.TrySetResult(d != null && Equals(d["status"], "current") && asset != null ? asset["id"]?.ToString() : null);
            });
            return tcs.Task;
        }

        /// <summary>Saves a library asset to assetPath (under Assets/) and imports it.</summary>
        public static Task DownloadAsync(string assetId, string assetPath)
        {
            var tcs = new TaskCompletionSource<bool>();
            HawkesGenClient.Download(assetId, assetPath, (path, error) =>
            {
                if (error != null)
                    tcs.TrySetException(new HawkesGenAudioException(error, IsFatal(error)));
                else
                    tcs.TrySetResult(true);
            });
            return tcs.Task;
        }

        /// <summary>Job params for a sound effect (audio.sfx) or a voice line (audio.voice). Same settings, same params, so recipes match.</summary>
        public static (string tool, Dictionary<string, object> parameters) Effect(string text, float durationSeconds, float promptInfluence,
            bool loop, string outputFormat, int count)
        {
            var p = new Dictionary<string, object>
            {
                ["text"] = text,
                ["influence"] = Math.Round((double)Math.Max(0f, Math.Min(1f, promptInfluence)), 2),
                ["loop"] = loop,
                ["format"] = outputFormat,
                ["count"] = (long)count,
            };
            if (durationSeconds > 0f)
                p["duration"] = Math.Round((double)Math.Max(0.5f, Math.Min(30f, durationSeconds)), 2);
            return ("audio.sfx", p);
        }

        public static (string tool, Dictionary<string, object> parameters) Speech(string voiceId, string text, string modelId, float stability,
            float speed, string outputFormat, int count)
        {
            var p = new Dictionary<string, object>
            {
                ["text"] = text,
                ["voice"] = voiceId,
                ["model"] = string.IsNullOrEmpty(modelId) ? "eleven_multilingual_v2" : modelId,
                ["format"] = outputFormat,
                ["settings"] = new Dictionary<string, object>
                {
                    ["stability"] = Math.Round((double)Math.Max(0f, Math.Min(1f, stability)), 2),
                    ["speed"] = Math.Round((double)Math.Max(0.7f, Math.Min(1.2f, speed)), 2),
                },
                ["count"] = (long)count,
            };
            return ("audio.voice", p);
        }

        /// <summary>A candidate voice from Voice Design: its preview in the Hawkes Gen library and the id to save it with.</summary>
        public sealed class VoicePreview
        {
            public string Asset;
            public string GeneratedVoiceId;
        }

        /// <summary>
        /// Voice Design (audio.voice-design): a few candidate voices for a description. 'sampleText' is what they say
        /// (100-1000 characters; empty = ElevenLabs writes it). Nothing is saved until <see cref="SaveDesignedVoiceAsync"/>.
        /// </summary>
        public static async Task<List<VoicePreview>> DesignVoiceAsync(string description, string sampleText, CancellationToken cancel)
        {
            var p = new Dictionary<string, object> { ["description"] = description };
            if (!string.IsNullOrWhiteSpace(sampleText))
                p["text"] = sampleText.Trim();
            var job = await RunAsync("audio.voice-design", p, cancel);
            var ids = (job.Result as Dictionary<string, object>)?["candidates"] as List<object> ?? new List<object>();
            var result = new List<VoicePreview>();
            for (int i = 0; i < job.Outputs.Count && i < ids.Count; i++)
                result.Add(new VoicePreview { Asset = job.Outputs[i].Asset, GeneratedVoiceId = ids[i]?.ToString() });
            return result;
        }

        /// <summary>Saves a designed voice to the ElevenLabs account (audio.voice-save) and returns its voice id.</summary>
        public static async Task<string> SaveDesignedVoiceAsync(string name, string description, string generatedVoiceId, CancellationToken cancel)
        {
            var job = await RunAsync("audio.voice-save", new Dictionary<string, object>
            {
                ["name"] = name,
                ["description"] = description,
                ["generatedVoiceId"] = generatedVoiceId,
            }, cancel);
            return (job.Result as Dictionary<string, object>)?["voiceId"]?.ToString();
        }

        /// <summary>Errors that every following request would hit too (no service, no key, no credits).</summary>
        static bool IsFatal(string error) =>
            error.StartsWith("Can't reach Hawkes Gen", StringComparison.Ordinal) ||
            error.Contains("ELEVENLABS_API_KEY") || error.Contains("ElevenLabs 401") || error.Contains("ElevenLabs 402") ||
            error.Contains("quota");
    }

    public sealed class HawkesGenAudioException : Exception
    {
        /// <summary>True when every following request would fail the same way, so a batch should stop.</summary>
        public bool Fatal { get; }

        public HawkesGenAudioException(string message, bool fatal) : base(message) => Fatal = fatal;
    }
}
#endif
