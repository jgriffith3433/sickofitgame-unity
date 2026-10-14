#if UNITY_EDITOR
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace SickOfIt.Tools.Editor.Audio
{
    /// <summary>
    /// Minimal ElevenLabs API client for the editor: sound effects (POST /v1/sound-generation) and speech
    /// (POST /v1/text-to-speech/{voice_id}). Both return audio bytes in the requested output format.
    /// Retries rate limits and server errors with backoff; other failures throw <see cref="ElevenLabsException"/>
    /// with ElevenLabs' own message.
    /// </summary>
    public static class ElevenLabsClient
    {
        const string BaseUrl = "https://api.elevenlabs.io";
        const string KeyPref = "SickOfIt.ElevenLabs.ApiKey";
        const string KeyEnvironment = "ELEVENLABS_API_KEY";
        const int MaxAttempts = 4;

        static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        /// <summary>
        /// The API key: the ELEVENLABS_API_KEY environment variable, else the one saved on this machine from the
        /// Sound Generator window (EditorPrefs, never in the project).
        /// </summary>
        public static string ApiKey
        {
            get
            {
                var fromEnvironment = Environment.GetEnvironmentVariable(KeyEnvironment);
                return !string.IsNullOrWhiteSpace(fromEnvironment) ? fromEnvironment.Trim() : EditorPrefs.GetString(KeyPref, "");
            }
        }

        public static bool KeyFromEnvironment => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(KeyEnvironment));

        public static void SaveKey(string key) => EditorPrefs.SetString(KeyPref, (key ?? "").Trim());

        public static Task<byte[]> SoundEffectAsync(string text, float durationSeconds, float promptInfluence, bool loop,
            string outputFormat, CancellationToken cancel)
        {
            var body = new JObject
            {
                ["text"] = text,
                ["prompt_influence"] = Math.Round(Math.Max(0f, Math.Min(1f, promptInfluence)), 2),
                ["model_id"] = "eleven_text_to_sound_v2",
            };
            if (durationSeconds > 0f)
                body["duration_seconds"] = Math.Round(Math.Max(0.5f, Math.Min(30f, durationSeconds)), 2);
            if (loop)
                body["loop"] = true;
            return PostAsync($"/v1/sound-generation?output_format={Uri.EscapeDataString(outputFormat)}", body, cancel);
        }

        public static Task<byte[]> SpeechAsync(string voiceId, string text, string modelId, string outputFormat, CancellationToken cancel)
        {
            var body = new JObject
            {
                ["text"] = text,
                ["model_id"] = string.IsNullOrEmpty(modelId) ? "eleven_multilingual_v2" : modelId,
            };
            return PostAsync($"/v1/text-to-speech/{Uri.EscapeDataString(voiceId)}?output_format={Uri.EscapeDataString(outputFormat)}", body, cancel);
        }

        static async Task<byte[]> PostAsync(string path, JObject body, CancellationToken cancel)
        {
            string key = ApiKey;
            if (string.IsNullOrEmpty(key))
                throw new ElevenLabsException("No ElevenLabs API key. Paste it in SickOfIt > Audio > Sound Generator, or set ELEVENLABS_API_KEY.", 0, false);

            string json = body.ToString(Formatting.None);
            for (int attempt = 1; ; attempt++)
            {
                cancel.ThrowIfCancellationRequested();
                using (var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path))
                {
                    request.Headers.Add("xi-api-key", key);
                    request.Headers.Accept.ParseAdd("audio/mpeg");
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response;
                    try
                    {
                        response = await http.SendAsync(request, cancel).ConfigureAwait(false);
                    }
                    catch (HttpRequestException) when (attempt < MaxAttempts)
                    {
                        // Network hiccup: wait and try again.
                        await Task.Delay(Backoff(attempt), cancel).ConfigureAwait(false);
                        continue;
                    }

                    using (response)
                    {
                        if (response.IsSuccessStatusCode)
                            return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                        int status = (int)response.StatusCode;
                        bool retry = status == 429 || status >= 500;
                        if (retry && attempt < MaxAttempts)
                        {
                            await Task.Delay(Backoff(attempt), cancel).ConfigureAwait(false);
                            continue;
                        }
                        string detail = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new ElevenLabsException(Describe(response.StatusCode, detail), status, retry);
                    }
                }
            }
        }

        static TimeSpan Backoff(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt));

        /// <summary>Turns an error response into a sentence (ElevenLabs puts the reason in "detail").</summary>
        static string Describe(HttpStatusCode status, string body)
        {
            string reason = body;
            try
            {
                var detail = JToken.Parse(body)["detail"];
                if (detail is JObject o)
                    reason = (string)o["message"] ?? o.ToString(Formatting.None);
                else if (detail is JArray a && a.Count > 0)
                    reason = (string)a[0]["msg"] ?? a.ToString(Formatting.None);
                else if (detail != null)
                    reason = detail.ToString();
            }
            catch (JsonException)
            {
            }
            if (reason != null && reason.Length > 300)
                reason = reason.Substring(0, 300) + "...";
            switch ((int)status)
            {
                case 401: return "ElevenLabs rejected the API key (401). Check it in the Sound Generator. " + reason;
                case 402: return "ElevenLabs says the account is out of credits or the plan doesn't allow this (402). " + reason;
                case 422: return "ElevenLabs didn't accept the request (422): " + reason;
                case 429: return "ElevenLabs rate limit (429) after several retries. Try again in a minute. " + reason;
                default: return $"ElevenLabs error {(int)status}: {reason}";
            }
        }
    }

    public sealed class ElevenLabsException : Exception
    {
        public int Status { get; }
        public bool Transient { get; }

        public ElevenLabsException(string message, int status, bool transient) : base(message)
        {
            Status = status;
            Transient = transient;
        }
    }
}
#endif
