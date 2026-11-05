using System;
using UnityEngine;

namespace Zombio
{
    public enum SoundCue { Shot, Hit, Kill, Pickup, Reload, Hurt, Wave, Explosion, Click }

    /// <summary>Small synthesized sound palette: no imported audio or downloads required.</summary>
    public sealed class ProceduralAudio : MonoBehaviour
    {
        readonly AudioSource[] voices = new AudioSource[16];
        AudioClip[] clips;
        int nextVoice;
        public void Initialize()
        {
            clips = new AudioClip[Enum.GetValues(typeof(SoundCue)).Length];
            for (int i = 0; i < clips.Length; i++) clips[i] = MakeClip((SoundCue)i);
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = new GameObject("Voice " + i);
                voice.transform.SetParent(transform, false);
                voices[i] = voice.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = .25f;
                voices[i].minDistance = 5;
                voices[i].maxDistance = 50;
                voices[i].rolloffMode = AudioRolloffMode.Linear;
            }
        }
        public void Play(SoundCue cue, Vector3 position)
        {
            if (clips == null) return;
            var voice = voices[nextVoice++ % voices.Length];
            voice.transform.position = position;
            voice.clip = clips[(int)cue];
            voice.pitch = cue == SoundCue.Shot ? UnityEngine.Random.Range(.94f, 1.06f) : 1;
            voice.volume = cue == SoundCue.Shot ? .38f : .55f;
            voice.Play();
        }
        static AudioClip MakeClip(SoundCue cue)
        {
            const int rate = 22050;
            float duration = cue == SoundCue.Wave ? .65f : cue == SoundCue.Explosion ? .55f : .18f;
            var samples = new float[(int)(rate * duration)];
            var random = new System.Random(81 + (int)cue);
            double phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate, progress = t / duration;
                float noise = (float)(random.NextDouble() * 2 - 1);
                float frequency = 440, mix = .1f;
                switch (cue)
                {
                    case SoundCue.Shot: frequency = Mathf.Lerp(160, 45, progress); mix = .8f; break;
                    case SoundCue.Hit: frequency = 230; mix = .5f; break;
                    case SoundCue.Kill: frequency = Mathf.Lerp(600, 950, progress); break;
                    case SoundCue.Pickup: frequency = progress < .5f ? 660 : 990; break;
                    case SoundCue.Reload: frequency = progress < .5f ? 240 : 480; mix = .6f; break;
                    case SoundCue.Hurt: frequency = Mathf.Lerp(180, 65, progress); mix = .35f; break;
                    case SoundCue.Wave: frequency = progress < .5f ? 220 : 330; mix = .02f; break;
                    case SoundCue.Explosion: frequency = Mathf.Lerp(90, 30, progress); mix = .85f; break;
                    case SoundCue.Click: frequency = 700; mix = .4f; break;
                }
                phase += 2 * Math.PI * frequency / rate;
                float envelope = Mathf.Min(t * 200, 1) * Mathf.Pow(1 - progress, 2);
                samples[i] = ((float)Math.Sin(phase) * (1 - mix) + noise * mix) * envelope * .65f;
            }
            var clip = AudioClip.Create(cue.ToString(), samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        void OnDestroy() { if (clips != null) foreach (var clip in clips) if (clip != null) Destroy(clip); }
    }
}
