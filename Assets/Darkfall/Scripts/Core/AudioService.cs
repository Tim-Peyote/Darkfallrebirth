using System.Collections.Generic;
using UnityEngine;

namespace Darkfall.Core
{
    public sealed class AudioService : MonoBehaviour
    {
        private AudioSource music;
        private AudioSource effects;
        private readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
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
            var clip = Load("Audio/Fx/" + clipName);
            if (clip != null) effects.PlayOneShot(clip);
        }

        private AudioClip Load(string path)
        {
            if (cache.TryGetValue(path, out var loaded)) return loaded;
            loaded = Resources.Load<AudioClip>(path);
            cache[path] = loaded;
            return loaded;
        }
    }
}
