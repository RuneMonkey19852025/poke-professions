using UnityEngine;
using System.Collections.Generic;

namespace PokeProfessions.Audio
{
    /// <summary>
    /// Procedural SFX: all sounds are synthesized at boot into AudioClips,
    /// so the project needs zero audio assets.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx I { get; private set; }

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlay = new Dictionary<string, float>();
        AudioSource src;

        void Awake()
        {
            I = this;
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            BakeAll();
        }

        public void Play(string id)
        {
            AudioClip clip;
            if (!clips.TryGetValue(id, out clip) || clip == null) return;

            float now = Time.unscaledTime;
            float last;
            if (lastPlay.TryGetValue(id, out last) && now - last < 0.05f) return;
            lastPlay[id] = now;

            src.PlayOneShot(clip, 0.8f);
        }

        void BakeAll()
        {
            Bake("hit", 0.10f, 220, 60, Wf.Square, 0.9f);
            Bake("faint", 0.35f, 300, 40, Wf.Triangle, 0.8f);
            Bake("catch", 0.45f, 500, 900, Wf.Sine, 0.9f);
            Bake("levelup", 0.35f, 520, 1040, Wf.Sine, 0.8f);
            Bake("gather", 0.12f, 700, 900, Wf.Sine, 0.5f);
            Bake("craft", 0.2f, 300, 500, Wf.Square, 0.45f);
            Bake("coin", 0.15f, 900, 1300, Wf.Sine, 0.5f);
            Bake("ui", 0.05f, 600, 600, Wf.Sine, 0.3f);
            Bake("quest", 0.4f, 400, 800, Wf.Triangle, 0.6f);
            Bake("evolve", 0.6f, 300, 1200, Wf.Saw, 0.5f);
        }

        void Bake(string id, float dur, int f0, int f1, Wf wave, float vol)
        {
            const int rate = 22050;
            int n = Mathf.Max(1, Mathf.RoundToInt(dur * rate));
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, t);
                phase += 2f * Mathf.PI * f / rate;
                float s = wave == Wf.Sine ? Mathf.Sin(phase)
                        : wave == Wf.Square ? Mathf.Sign(Mathf.Sin(phase)) * 0.7f
                        : wave == Wf.Triangle ? (2f / Mathf.PI) * Mathf.Asin(Mathf.Sin(phase))
                        : Mathf.PerlinNoise(phase * 0.15f, 0.3f) * 1.2f - 0.6f;

                float env = Mathf.Clamp01(t < 0.08f ? t / 0.08f : 1f);
                env *= (1f - t);
                env *= (1f - t);
                data[i] = Mathf.Clamp(s * env * vol, -1f, 1f);
            }

            var clip = AudioClip.Create("sfx_" + id, n, 1, rate, false);
            clip.SetData(data, 0);
            clips[id] = clip;
        }

        enum Wf { Sine, Square, Triangle, Saw }
    }
}
