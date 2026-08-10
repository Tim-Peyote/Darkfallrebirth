using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.Core
{
    public sealed class AudioService : MonoBehaviour
    {
        private AudioSource music;
        private AudioSource effects;
        private readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> effectReadyAt = new Dictionary<string, float>();
        private SaveData settings;
        private bool paused;

        public void Initialize(float volume)
        {
            music = gameObject.AddComponent<AudioSource>();
            effects = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            SetVolume(volume);
        }

        public void ApplySettings(SaveData settings)
        {
            this.settings = settings;
            AudioListener.volume = settings.audioEnabled ? Mathf.Clamp01(settings.masterVolume) : 0;
            music.volume = Mathf.Clamp01(settings.musicVolume) * (paused ? .35f : 1f);
            effects.volume = Mathf.Clamp01(settings.sfxVolume);
        }

        public void SetPaused(bool value)
        {
            paused = value;
            if (settings != null) ApplySettings(settings);
        }

        public void SetVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
        }

        public void PlayMusic(string clipName)
        {
            var clip = Load("Audio/" + clipName);
            if (clip == null || music.clip == clip) return;
            music.clip = clip;
            music.Play();
        }

        public void PlayEffect(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return;
            var now = Time.unscaledTime;
            if (effectReadyAt.TryGetValue(clipName, out var readyAt) && now < readyAt) return;
            var clip = Load("Audio/Fx/" + clipName);
            if (clip == null) return;
            effects.PlayOneShot(clip);
            effectReadyAt[clipName] = now + EffectCooldown(clipName);
        }

        private static float EffectCooldown(string clipName)
        {
            // Combat clips have long tails. A shared limiter prevents a pack from stacking the
            // same full-volume sample dozens of times in one frame without muting distinct events.
            switch (clipName)
            {
                case "Fireball": return .48f;
                case "sword":
                case "Dagger": return .16f;
                case "enemy_hit": return .075f;
                case "heroes_hit": return .12f;
                case "enemy_die": return .09f;
                case "explosion": return .35f;
                default: return .025f;
            }
        }

        private AudioClip Load(string path)
        {
            if (cache.TryGetValue(path, out var loaded)) return loaded;
            loaded = Resources.Load<AudioClip>(path);
            cache[path] = loaded;
            if (loaded == null) Debug.LogWarning("Audio clip is missing from Resources: " + path, this);
            return loaded;
        }
    }
}
