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
        AudioClip _gemClip;
        AudioClip _powerClip;
        AudioClip _jumpClip;
        AudioClip _slideClip;
        AudioClip _nearMissClip;
        AudioClip _hitClip;
        AudioClip _deathClip;
        AudioClip _fallClip;
        AudioClip _guardianClip;
        AudioClip _guardianLungeClip;
        AudioClip _boatClip;
        AudioClip _ropeGrabClip;
        AudioClip _ropeReleaseClip;
        AudioClip _splashClip;
        AudioClip _whooshClip;
        AudioClip _smashClip;
        AudioClip _brokenRailWarnClip;
        AudioClip _brokenRailFallClip;
        readonly AudioClip[] _biomeBeds = new AudioClip[6];
        BiomeId _ambienceBiome = (BiomeId)(-1);

        float _lastCoinTime = -10f;
        int _coinCombo;

        public bool HasRunClips =>
            _coinClip != null && _hitClip != null && _deathClip != null
            && _fallClip != null && _jumpClip != null && _pickupClip != null
            && _gemClip != null && _slideClip != null && _nearMissClip != null;

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
        public void PlaySlide() => PlaySfx(_slideClip, 0.5f);
        public void PlayNearMiss() => PlaySfx(_nearMissClip, 0.55f);
        public void PlayWhoosh() => PlaySfx(_whooshClip, 0.45f);

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
        public void PlayGem() => PlaySfx(_gemClip, 0.65f);
        public void PlayPower() => PlaySfx(_powerClip, 0.7f);
        public void PlayHit() => PlaySfx(_hitClip, 0.8f);
        public void PlayDeath() => PlaySfx(_deathClip, 0.7f);
        public void PlayFall() => PlaySfx(_fallClip, 0.7f);
        public void PlayGuardian() => PlaySfx(_guardianClip, 0.6f);
        /// <summary>Sharp pack stinger for Idol Beast grab lunges (distinct from ambient growl).</summary>
        public void PlayGuardianLunge() => PlaySfx(_guardianLungeClip, 0.78f);
        public void PlayBoatMount() => PlaySfx(_boatClip, 0.55f);
        public void PlayRopeGrab()
        {
            PlaySfx(_ropeGrabClip, 0.5f);
            PlaySfx(_whooshClip, 0.35f);
        }
        public void PlayRopeRelease() => PlaySfx(_ropeReleaseClip, 0.55f);
        public void PlaySplash() => PlaySfx(_splashClip, 0.6f);
        public void PlaySmash() => PlaySfx(_smashClip, 0.65f);
        /// <summary>Metallic crackle telegraph before a dual-track broken rail gap.</summary>
        public void PlayBrokenRailWarn() => PlaySfx(_brokenRailWarnClip, 0.72f);
        /// <summary>Wood/metal snap when the cart drops through a broken rail.</summary>
        public void PlayBrokenRailFall() => PlaySfx(_brokenRailFallClip, 0.85f);

        /// <summary>Start / crossfade a procedural looping bed for the active biome.</summary>
        public void PlayBiomeAmbience(BiomeId biome)
        {
            if (_music == null || !GameSettings.SfxEnabled) return;
            int idx = Mathf.Clamp((int)biome, 0, _biomeBeds.Length - 1);
            if (_biomeBeds[idx] == null) _biomeBeds[idx] = BuildBiomeBed(biome);
            if (_ambienceBiome == biome && _music.isPlaying && _music.clip == _biomeBeds[idx]) return;
            _ambienceBiome = biome;
            _music.clip = _biomeBeds[idx];
            _music.volume = BedVolume(biome);
            _music.loop = true;
            if (!_music.isPlaying) _music.Play();
        }

        public void StopAmbience()
        {
            if (_music == null) return;
            _music.Stop();
            _music.clip = null;
            _ambienceBiome = (BiomeId)(-1);
        }

        static float BedVolume(BiomeId biome) => biome switch
        {
            BiomeId.VolcanicCrater => 0.28f,
            BiomeId.CaveMines => 0.24f,
            BiomeId.NightSummit => 0.22f,
            BiomeId.IceCaverns => 0.26f,
            BiomeId.DesertTombs => 0.25f,
            _ => 0.27f
        };

        void BuildClips()
        {
            _coinClip = BuildCoin();
            _pickupClip = BuildPickup();
            _gemClip = BuildGem();
            _powerClip = BuildPower();
            _jumpClip = BuildJump();
            _slideClip = BuildSlide();
            _nearMissClip = BuildNearMiss();
            _hitClip = BuildHit();
            _deathClip = BuildDeath();
            _fallClip = BuildFall();
            _guardianClip = BuildGuardian();
            _guardianLungeClip = BuildGuardianLunge();
            _boatClip = BuildBoat();
            _ropeGrabClip = BuildRopeGrab();
            _ropeReleaseClip = BuildRopeRelease();
            _splashClip = BuildSplash();
            _whooshClip = BuildWhoosh();
            _smashClip = BuildSmash();
            _brokenRailWarnClip = BuildBrokenRailWarn();
            _brokenRailFallClip = BuildBrokenRailFall();
            for (int i = 0; i < _biomeBeds.Length; i++)
                _biomeBeds[i] = BuildBiomeBed((BiomeId)i);
        }

        static AudioClip BuildSmash()
        {
            var data = NewBuffer(0.28f, out int n);
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float noise = (Random.value - 0.5f) * 2f;
                float crack = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(420f, 90f, progress) * (i / (float)SampleRate));
                float env = Mathf.Exp(-9f * progress);
                data[i] = (noise * 0.65f + crack * 0.35f) * env * 0.75f;
            }
            return Finish("sfx_smash", data);
        }

        static AudioClip BuildBrokenRailWarn()
        {
            var data = NewBuffer(0.38f, out int n);
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float t = i / (float)SampleRate;
                float ring = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(880f, 240f, progress) * t);
                float spark = (Random.value - 0.5f) * 2f * Mathf.Exp(-6f * progress);
                float grind = Mathf.Sin(2f * Mathf.PI * 55f * t) * (0.4f + progress);
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress * 1.15f));
                data[i] = (ring * 0.45f + spark * 0.35f + grind * 0.3f) * env * 0.8f;
            }
            return Finish("sfx_broken_rail_warn", data);
        }

        static AudioClip BuildBrokenRailFall()
        {
            var data = NewBuffer(0.55f, out int n);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float freq = Mathf.Lerp(320f, 48f, progress);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float snap = (Random.value - 0.5f) * Mathf.Exp(-18f * progress);
                float wood = Mathf.Sin(phase) * 0.7f;
                float env = Mathf.Exp(-3.5f * progress);
                data[i] = (wood + snap) * env * 0.9f;
            }
            return Finish("sfx_broken_rail_fall", data);
        }

        /// <summary>Short seamless loop — distinct tonal beds per biome without external audio assets.</summary>
        static AudioClip BuildBiomeBed(BiomeId biome)
        {
            float seconds = 2.4f;
            var data = NewBuffer(seconds, out int n);
            float droneA;
            float droneB;
            float pulseHz;
            float noiseAmt;
            switch (biome)
            {
                case BiomeId.DesertTombs:
                    droneA = 98f; droneB = 147f; pulseHz = 1.6f; noiseAmt = 0.08f; break;
                case BiomeId.IceCaverns:
                    droneA = 130f; droneB = 196f; pulseHz = 2.4f; noiseAmt = 0.05f; break;
                case BiomeId.CaveMines:
                    droneA = 73f; droneB = 110f; pulseHz = 1.1f; noiseAmt = 0.14f; break;
                case BiomeId.VolcanicCrater:
                    droneA = 55f; droneB = 82f; pulseHz = 0.9f; noiseAmt = 0.18f; break;
                case BiomeId.NightSummit:
                    droneA = 87f; droneB = 174f; pulseHz = 0.7f; noiseAmt = 0.06f; break;
                default:
                    droneA = 110f; droneB = 165f; pulseHz = 2.0f; noiseAmt = 0.1f; break;
            }

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)n;
                float pulse = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * pulseHz * t);
                float a = Mathf.Sin(2f * Mathf.PI * droneA * t);
                float b = Mathf.Sin(2f * Mathf.PI * droneB * t) * 0.55f;
                float shimmer = Mathf.Sin(2f * Mathf.PI * (droneB * 2f) * t) * 0.12f
                                * Mathf.Sin(2f * Mathf.PI * 0.35f * t);
                float noise = (Random.value - 0.5f) * 2f * noiseAmt;
                // Crossfade edges so the loop is seamless.
                float edge = 1f;
                float fade = 0.08f;
                if (progress < fade) edge = progress / fade;
                else if (progress > 1f - fade) edge = (1f - progress) / fade;
                data[i] = (a + b + shimmer + noise) * pulse * edge * 0.35f;
            }
            return Finish("amb_" + biome, data);
        }

        static AudioClip BuildCoin()
        {
            var data = NewBuffer(0.22f, out int n);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)n;
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

        static AudioClip BuildGem()
        {
            var data = NewBuffer(0.42f, out int n);
            float[] steps = { 880f, 1174.7f, 1396.9f, 1760f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)n;
                int step = Mathf.Min(steps.Length - 1, Mathf.FloorToInt(progress * steps.Length));
                float shimmer = Mathf.Sin(2f * Mathf.PI * steps[step] * 2f * t) * 0.35f;
                float env = Mathf.Exp(-2.8f * progress);
                data[i] = (Mathf.Sin(2f * Mathf.PI * steps[step] * t) + shimmer) * env * 0.65f;
            }
            return Finish("sfx_gem", data);
        }

        static AudioClip BuildPower()
        {
            var data = NewBuffer(0.48f, out int n);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float freq = Mathf.Lerp(180f, 640f, progress);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float swirl = Mathf.Sin(2f * Mathf.PI * 12f * progress) * 0.2f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress * 1.2f));
                data[i] = (Mathf.Sin(phase) + swirl) * env * 0.7f;
            }
            return Finish("sfx_power", data);
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

        static AudioClip BuildSlide()
        {
            var data = NewBuffer(0.28f, out int n);
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float noise = (Random.value - 0.5f) * 2f;
                float rumble = Mathf.Sin(2f * Mathf.PI * 70f * (i / (float)SampleRate));
                float env = Mathf.Exp(-5f * progress) * (0.5f + progress);
                data[i] = (noise * 0.55f + rumble * 0.45f) * env * 0.7f;
            }
            return Finish("sfx_slide", data);
        }

        static AudioClip BuildNearMiss()
        {
            var data = NewBuffer(0.18f, out int n);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float freq = Mathf.Lerp(980f, 420f, progress);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float env = Mathf.Exp(-9f * progress);
                data[i] = Mathf.Sin(phase) * env * 0.55f;
            }
            return Finish("sfx_nearmiss", data);
        }

        static AudioClip BuildWhoosh()
        {
            var data = NewBuffer(0.32f, out int n);
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                float noise = (Random.value - 0.5f) * 2f;
                float band = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(900f, 220f, progress) * (i / (float)SampleRate));
                float env = Mathf.Sin(Mathf.PI * progress);
                data[i] = (noise * 0.65f + band * 0.35f) * env * 0.55f;
            }
            return Finish("sfx_whoosh", data);
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

        static AudioClip BuildGuardianLunge()
        {
            var data = NewBuffer(0.42f, out int n);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float progress = i / (float)n;
                // Rising bark into a short pack snarl — reads as a grab telegraph.
                float freq = Mathf.Lerp(140f, 320f, Mathf.Clamp01(progress * 1.8f));
                if (progress > 0.45f) freq = Mathf.Lerp(320f, 90f, (progress - 0.45f) / 0.55f);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float bark = Mathf.Sin(phase);
                float grit = (Random.value - 0.5f) * (progress < 0.35f ? 0.55f : 0.25f);
                float slap = progress < 0.12f ? Mathf.Sin(2f * Mathf.PI * 60f * (i / (float)SampleRate)) * 0.5f : 0f;
                float env = progress < 0.2f
                    ? progress / 0.2f
                    : Mathf.Exp(-5.5f * (progress - 0.2f));
                data[i] = (bark * 0.75f + grit + slap) * env * 0.95f;
            }
            return Finish("sfx_guardian_lunge", data);
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
