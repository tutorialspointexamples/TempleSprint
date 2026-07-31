using UnityEngine;

namespace TempleSprint
{
    /// <summary>
    /// Run SFX. Clips are synthesized at runtime so the game ships audio without
    /// binary assets (OneDrive sync drops newly added .wav files).
    /// </summary>
    public class AudioHooks : MonoBehaviour
    {
        public static AudioHooks Instance { get; private set; }

        const int SampleRate = 44100;
        const float CoinComboWindow = 0.6f;

        AudioSource _sfx;
        AudioSource _coinSource;
        AudioSource _music;

        AudioClip _coinClip;
        AudioClip _pickupClip;
        AudioClip _jumpClip;
        AudioClip _hitClip;
        AudioClip _deathClip;
        AudioClip _fallClip;
        AudioClip _guardianClip;
        AudioClip _boatClip;
        AudioClip _ropeGrabClip;
        AudioClip _ropeReleaseClip;
        AudioClip _splashClip;

        float _lastCoinTime = -10f;
        int _coinCombo;

        public bool HasRunClips =>
            _coinClip != null && _hitClip != null && _deathClip != null
            && _fallClip != null && _jumpClip != null && _pickupClip != null;

        void Awake()
        {
            Instance = this;

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;

            _coinSource = gameObject.AddComponent<AudioSource>();
            _coinSource.playOnAwake = false;
            _coinSource.spatialBlend = 0f;

            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.volume = 0.35f;

            BuildClips();
        }

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _sfx == null || !GameSettings.SfxEnabled) return;
            _sfx.PlayOneShot(clip, volume);
        }

        public void PlayJump() => PlaySfx(_jumpClip, 0.45f);

        /// <summary>Coin chime; pitch steps up while collecting a streak.</summary>
        public void PlayCoin()
        {
            if (_coinSource == null || _coinClip == null || !GameSettings.SfxEnabled) return;

            _coinCombo = Time.unscaledTime - _lastCoinTime <= CoinComboWindow
                ? Mathf.Min(_coinCombo + 1, 7)
                : 0;
            _lastCoinTime = Time.unscaledTime;

            _coinSource.pitch = 1f + _coinCombo * 0.06f;
            _coinSource.PlayOneShot(_coinClip, 0.55f);
        }

        public void PlayPickup() => PlaySfx(_pickupClip, 0.6f);
        public void PlayHit() => PlaySfx(_hitClip, 0.8f);
        public void PlayDeath() => PlaySfx(_deathClip, 0.7f);
        public void PlayFall() => PlaySfx(_fallClip, 0.7f);
        public void PlayGuardian() => PlaySfx(_guardianClip, 0.6f);
        public void PlayBoatMount() => PlaySfx(_boatClip, 0.55f);
        public void PlayRopeGrab() => PlaySfx(_ropeGrabClip, 0.5f);
        public void PlayRopeRelease() => PlaySfx(_ropeReleaseClip, 0.55f);
        public void PlaySplash() => PlaySfx(_splashClip, 0.6f);

        void BuildClips()
        {
            _coinClip = BuildCoin();
            _pickupClip = BuildPickup();
            _jumpClip = BuildJump();
            _hitClip = BuildHit();
            _deathClip = BuildDeath();
            _fallClip = BuildFall();
            _guardianClip = BuildGuardian();
            _boatClip = BuildBoat();
            _ropeGrabClip = BuildRopeGrab();
            _ropeReleaseClip = BuildRopeRelease();
            _splashClip = BuildSplash();
        }

        static AudioClip BuildCoin()
        {
            var data = NewBuffer(0.22f, out int n);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)n;
                // Two-note sparkle: the upper octave enters slightly late.
                float bell = Mathf.Sin(2f * Mathf.PI * 1568f * t);
                float shimmer = Mathf.Sin(2f * Mathf.PI * 2349f * t) * Mathf.Clamp01(progress * 4f);
                float env = Mathf.Exp(-14f * progress);
                data[i] = (bell * 0.6f + shimmer * 0.4f) * env * 0.8f;
            }
            return Finish("sfx_coin", data);
        }

        static AudioClip BuildPickup()
        {
            var data = NewBuffer(0.36f, out int n);
            float[] steps = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)n;
                int step = Mathf.Min(steps.Length - 1, Mathf.FloorToInt(progress * steps.Length));
                float env = Mathf.Exp(-3.5f * progress);
                data[i] = Mathf.Sin(2f * Mathf.PI * steps[step] * t) * env * 0.7f;
            }
            return Finish("sfx_pickup", data);
        }

        static AudioClip BuildJump()
        {
            var data = NewBuffer(0.2f, out int n);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float freq = Mathf.Lerp(240f, 720f, progress);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float air = (Random.value - 0.5f) * 0.5f;
                float env = Mathf.Sin(Mathf.PI * progress);
                data[i] = (Mathf.Sin(phase) * 0.7f + air * 0.3f) * env * 0.7f;
            }
            return Finish("sfx_jump", data);
        }

        static AudioClip BuildHit()
        {
            var data = NewBuffer(0.35f, out int n);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float freq = Mathf.Lerp(180f, 55f, progress);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float crack = (Random.value - 0.5f) * Mathf.Exp(-28f * progress);
                float env = Mathf.Exp(-8f * progress);
                data[i] = (Mathf.Sin(phase) * 0.8f + crack) * env;
            }
            return Finish("sfx_hit", data);
        }

        static AudioClip BuildDeath()
        {
            var data = NewBuffer(0.75f, out int n);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float freq = Mathf.Lerp(420f, 90f, progress * progress);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float wobble = 1f + Mathf.Sin(2f * Mathf.PI * 7f * progress) * 0.12f;
                float env = Mathf.Exp(-3.2f * progress);
                data[i] = Mathf.Sin(phase) * wobble * env * 0.85f;
            }
            return Finish("sfx_death", data);
        }

        static AudioClip BuildFall()
        {
            var data = NewBuffer(0.9f, out int n);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float freq = Mathf.Lerp(760f, 70f, progress);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float wind = (Random.value - 0.5f) * 0.25f * progress;
                float env = Mathf.Min(1f, (1f - progress) * 2.2f);
                data[i] = (Mathf.Sin(phase) * 0.75f + wind) * env * 0.85f;
            }
            return Finish("sfx_fall", data);
        }

        static AudioClip BuildGuardian()
        {
            var data = NewBuffer(0.6f, out int n);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float growl = 78f + Mathf.Sin(2f * Mathf.PI * 18f * progress) * 22f;
                phase += 2f * Mathf.PI * growl / SampleRate;
                float grit = (Random.value - 0.5f) * 0.35f;
                float env = Mathf.Sin(Mathf.PI * progress);
                data[i] = (Mathf.Sin(phase) * 0.8f + grit) * env * 0.9f;
            }
            return Finish("sfx_guardian", data);
        }

        static AudioClip BuildBoat()
        {
            var data = NewBuffer(0.35f, out int n);
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float t = i / (float)SampleRate;
                float thud = Mathf.Sin(2f * Mathf.PI * 120f * t) * Mathf.Exp(-8f * progress);
                float splash = (Random.value - 0.5f) * Mathf.Exp(-5f * progress);
                data[i] = (thud * 0.7f + splash * 0.5f) * 0.85f;
            }
            return Finish("sfx_boat", data);
        }

        static AudioClip BuildRopeGrab()
        {
            var data = NewBuffer(0.18f, out int n);
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float t = i / (float)SampleRate;
                float twang = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(420f, 180f, progress) * t);
                data[i] = twang * Mathf.Exp(-10f * progress) * 0.75f;
            }
            return Finish("sfx_rope_grab", data);
        }

        static AudioClip BuildRopeRelease()
        {
            var data = NewBuffer(0.22f, out int n);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float freq = Mathf.Lerp(280f, 90f, progress);
                phase += 2f * Mathf.PI * freq / SampleRate;
                data[i] = Mathf.Sin(phase) * Mathf.Exp(-7f * progress) * 0.7f;
            }
            return Finish("sfx_rope_release", data);
        }

        static AudioClip BuildSplash()
        {
            var data = NewBuffer(0.4f, out int n);
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float noise = (Random.value - 0.5f) * 2f;
                float env = Mathf.Exp(-4f * progress) * (1f - progress * 0.3f);
                data[i] = noise * env * 0.55f;
            }
            return Finish("sfx_splash", data);
        }

        static float[] NewBuffer(float seconds, out int sampleCount)
        {
            sampleCount = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            return new float[sampleCount];
        }

        static AudioClip Finish(string name, float[] data)
        {
            // Short fade-out prevents the click a truncated waveform would make.
            int fade = Mathf.Min(data.Length, SampleRate / 200);
            for (int i = 0; i < fade; i++)
                data[data.Length - 1 - i] *= i / (float)fade;

            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
