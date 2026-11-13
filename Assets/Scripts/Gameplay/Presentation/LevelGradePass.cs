using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Camera fullscreen grade (Docs/lighting.md): bloom on emissive (~threshold 1), vignette, colour grade, film grain
    /// on desktop. Phone tier uses half-res bloom and skips grain. Built-in pipeline OnRenderImage; presentation only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelGradePass : MonoBehaviour
    {
        static readonly int GradeId = Shader.PropertyToID("_Grade");
        static readonly int GradeStrengthId = Shader.PropertyToID("_GradeStrength");
        static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        static readonly int BloomThresholdId = Shader.PropertyToID("_BloomThreshold");
        static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        static readonly int GrainId = Shader.PropertyToID("_Grain");
        static readonly int TimeSeedId = Shader.PropertyToID("_TimeSeed");
        static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        static readonly int UseBloomId = Shader.PropertyToID("_UseBloom");

        Material material;
        RenderTexture bloomA, bloomB;
        int bloomW, bloomH;
        bool halfRes;
        bool grainOn = false;
        Color grade = Color.white;
        float gradeStrength = 0.35f;
        float vignette = 0.35f;
        float bloomThreshold = 1f;
        float bloomIntensity = 0.55f;
        float grain = 0f;

        public Color Grade => grade;
        public float GradeStrength => gradeStrength;
        public float BloomIntensity => bloomIntensity;
        public bool GrainOn => grainOn;
        public bool HalfResBloom => halfRes;
        public bool Ready => material != null;

        public static LevelGradePass Attach(Camera camera)
        {
            if (camera == null)
                return null;
            if (!camera.TryGetComponent(out LevelGradePass pass))
                pass = camera.gameObject.AddComponent<LevelGradePass>();
            pass.EnsureMaterial();
            return pass;
        }

        void EnsureMaterial()
        {
            if (material != null)
                return;
            var shader = Resources.Load<Shader>("Feel/LevelGrade");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[LevelGrade] Resources/Feel/LevelGrade shader missing or unsupported: grade pass off.");
                enabled = false;
                return;
            }
            material = new Material(shader) { name = "LevelGrade (runtime)", hideFlags = HideFlags.DontSave };
        }

        public void Apply(LevelLightingProfile profile, LightingQuality quality)
        {
            EnsureMaterial();
            if (profile == null || material == null)
            {
                enabled = false;
                return;
            }
            enabled = true;
            grade = profile.grade;
            gradeStrength = profile.gradeStrength;
            vignette = profile.vignette;
            bloomThreshold = profile.bloomThreshold;
            bloomIntensity = profile.bloomIntensity;
            grain = profile.grain;
            halfRes = quality != null && quality.bloomHalfRes;
            grainOn = quality == null || quality.grain;
            if (GameSettings.ReducedFlash)
            {
                bloomIntensity *= 0.55f;
                grain *= 0.4f;
            }
        }

        public void SetGrade(Color color, float strength)
        {
            grade = color;
            gradeStrength = strength;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (material == null || !enabled)
            {
                Graphics.Blit(src, dest);
                return;
            }

            int bw = halfRes ? Mathf.Max(8, src.width / 2) : src.width;
            int bh = halfRes ? Mathf.Max(8, src.height / 2) : src.height;
            EnsureBloom(bw, bh);

            material.SetFloat(BloomThresholdId, bloomThreshold);
            Graphics.Blit(src, bloomA, material, 0);
            Graphics.Blit(bloomA, bloomB, material, 1);
            Graphics.Blit(bloomB, bloomA, material, 2);

            material.SetColor(GradeId, grade);
            material.SetFloat(GradeStrengthId, gradeStrength);
            material.SetFloat(VignetteId, vignette);
            material.SetFloat(BloomIntensityId, bloomIntensity);
            material.SetFloat(GrainId, grainOn ? grain : 0f);
            material.SetFloat(TimeSeedId, Time.unscaledTime * 60f);
            material.SetTexture(BloomTexId, bloomA);
            material.SetFloat(UseBloomId, bloomIntensity > 0.01f ? 1f : 0f);
            Graphics.Blit(src, dest, material, 3);
        }

        void EnsureBloom(int w, int h)
        {
            if (bloomA != null && bloomW == w && bloomH == h)
                return;
            ReleaseBloom();
            bloomW = w;
            bloomH = h;
            bloomA = new RenderTexture(w, h, 0, RenderTextureFormat.Default)
            {
                name = "LevelGradeBloomA",
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            bloomB = new RenderTexture(w, h, 0, RenderTextureFormat.Default)
            {
                name = "LevelGradeBloomB",
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            bloomA.Create();
            bloomB.Create();
        }

        void ReleaseBloom()
        {
            if (bloomA != null)
            {
                bloomA.Release();
                Destroy(bloomA);
                bloomA = null;
            }
            if (bloomB != null)
            {
                bloomB.Release();
                Destroy(bloomB);
                bloomB = null;
            }
        }

        void OnDestroy()
        {
            ReleaseBloom();
            if (material != null)
                Destroy(material);
        }
    }
}
