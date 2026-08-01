using UnityEngine;

namespace TempleSprint
{
    public enum PowerUpType
    {
        Magnet,
        Shield,
        ScoreMultiplier,
        SpeedBoost,
        SlowMo
    }

    public class PowerUpController : MonoBehaviour
    {
        public static PowerUpController Instance { get; private set; }

        public const float EnergyMax = 100f;
        public const float EnergyPerCoin = 4.5f;
        public const float ActivateCost = 100f;

        public bool HasShield { get; private set; }
        public bool MagnetActive => _magnetTimer > 0f;
        public bool SpeedBoostActive => _boostTimer > 0f;
        public bool SlowMoActive => _slowTimer > 0f;
        public bool Invulnerable => SpeedBoostActive || _reviveIFrames > 0f;
        public float ScoreMultiplier => _multiplierTimer > 0f ? (_triple ? 3f : 2f) : 1f;
        public float MagnetRadius { get; private set; } = 3f;
        /// <summary>Movement scale only — SlowMo does NOT also change timeScale.</summary>
        public float SpeedScale => SpeedBoostActive ? 1.45f : (SlowMoActive ? 0.55f : 1f);
        public PowerUpType? EquippedConsumable { get; private set; }
        public float Energy { get; private set; }
        public float EnergyFill01 => Mathf.Clamp01(Energy / EnergyMax);
        public bool EnergyReady => Energy >= ActivateCost - 0.01f;

        public string ActiveLabel
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>(4);
                if (_reviveIFrames > 0f) parts.Add("I-Frames");
                if (HasShield) parts.Add("Shield");
                if (MagnetActive) parts.Add($"Magnet {_magnetTimer:0.0}s");
                if (_multiplierTimer > 0f) parts.Add($"{(_triple ? "3x" : "2x")} {_multiplierTimer:0.0}s");
                if (SpeedBoostActive) parts.Add($"Boost {_boostTimer:0.0}s");
                if (SlowMoActive) parts.Add($"Slow {_slowTimer:0.0}s");
                if (CharacterSkillReady && CharacterRoster.ActiveSkill != CharacterActiveSkill.None)
                    parts.Add("Skill READY");
                else if (_characterSkillCd > 0f && CharacterRoster.ActiveSkill != CharacterActiveSkill.None)
                    parts.Add($"Skill {_characterSkillCd:0}s");
                if (EquippedConsumable.HasValue) parts.Add($"Tap:{EquippedConsumable.Value}");
                parts.Add($"PWR {Mathf.FloorToInt(EnergyFill01 * 100f)}%");
                return string.Join(" · ", parts);
            }
        }

        public float CharacterSkillCooldown01 =>
            CharacterRoster.ActiveSkill == CharacterActiveSkill.None
                ? 1f
                : Mathf.Clamp01(_characterSkillCd / CharacterSkillCooldownSeconds);
        public bool CharacterSkillReady =>
            CharacterRoster.ActiveSkill != CharacterActiveSkill.None && _characterSkillCd <= 0f;

        const float CharacterSkillCooldownSeconds = 18f;

        float _magnetTimer, _multiplierTimer, _boostTimer, _slowTimer, _reviveIFrames;
        float _petMagnetCd, _petShieldCd;
        float _characterSkillCd;
        float _baseMagnetRadius = 3f;
        bool _triple;
        MetaProgress _meta;
        PowerUpVfx _magnetVfx;
        PowerUpVfx _shieldVfx;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _meta = MetaProgress.Ensure();
            MagnetRadius = 3f + _meta.MagnetRadiusBonus;
        }

        public void ResetForRun()
        {
            ClearTimers();
            ClearActiveVfx();
            HasShield = false;
            _triple = false;
            EquippedConsumable = PowerUpType.SpeedBoost;
            Energy = 0f;
            _characterSkillCd = 4f; // brief arming delay so tap doesn't waste skill on start
            _baseMagnetRadius = 3f + _meta.MagnetRadiusBonus + CharacterRoster.PassiveMagnetBonus;
            MagnetRadius = _baseMagnetRadius;
            _petMagnetCd = CosmeticRoster.PetPassives.MagnetPulse
                ? CosmeticRoster.PetPassives.MagnetPulseInterval * 0.4f
                : 999f;
            _petShieldCd = CosmeticRoster.PetPassives.ShieldChirp
                ? CosmeticRoster.PetPassives.ShieldChirpInterval * 0.5f
                : 999f;
            if (_meta.Data.reviveLevel >= 1 && Random.value < 0.35f)
                HasShield = true;
            if (CharacterRoster.EmberStartShield)
                HasShield = true;
            SyncActiveVfx();
        }

        /// <summary>Head-start burst: short speed boost + score mult kick for the opening sprint.</summary>
        public void GrantHeadStartBurst()
        {
            _boostTimer = Mathf.Max(_boostTimer, 4.5f + (_meta != null ? _meta.BoostDurationBonus : 0f));
            _multiplierTimer = Mathf.Max(_multiplierTimer, 6f);
            _triple = false;
            HasShield = true;
            GrantReviveIFrames(1.1f);
        }

        public void ClearTimers()
        {
            _magnetTimer = _multiplierTimer = _boostTimer = _slowTimer = _reviveIFrames = 0f;
            _petMagnetCd = _petShieldCd = 0f;
            _characterSkillCd = 0f;
            MagnetRadius = _baseMagnetRadius > 0f ? _baseMagnetRadius : 3f;
            Time.timeScale = 1f;
            ClearActiveVfx();
        }

        public void AddEnergyFromCoins(int coins)
        {
            if (coins <= 0) return;
            float perCoin = EnergyPerCoin + (_meta != null ? _meta.EnergyFillBonus : 0f);
            Energy = Mathf.Min(EnergyMax, Energy + coins * perCoin);
        }

        public void GrantReviveIFrames(float seconds = 1.75f)
        {
            _reviveIFrames = seconds;
        }

        public void Activate(PowerUpType type, bool equipConsumables = true)
        {
            switch (type)
            {
                case PowerUpType.Magnet:
                    _magnetTimer = 8f + (_meta != null ? _meta.MagnetDurationBonus : 0f);
                    MissionSystem.Report(MissionType.UsePowerUps, 1);
                    EnsureMagnetVfx();
                    break;
                case PowerUpType.Shield:
                    HasShield = true;
                    MissionSystem.Report(MissionType.UsePowerUps, 1);
                    EnsureShieldVfx();
                    break;
                case PowerUpType.ScoreMultiplier:
                    _multiplierTimer = 10f + (_meta != null ? _meta.BoostDurationBonus * 0.5f : 0f);
                    _triple = Random.value < 0.25f;
                    MissionSystem.Report(MissionType.UsePowerUps, 1);
                    break;
                case PowerUpType.SpeedBoost:
                    if (equipConsumables)
                        EquippedConsumable = PowerUpType.SpeedBoost;
                    else
                    {
                        _boostTimer = 5f + (_meta != null ? _meta.BoostDurationBonus : 0f);
                        EquippedConsumable = null;
                        MissionSystem.Report(MissionType.UsePowerUps, 1);
                    }
                    break;
                case PowerUpType.SlowMo:
                    if (equipConsumables)
                        EquippedConsumable = PowerUpType.SlowMo;
                    else
                    {
                        _slowTimer = 6f + (_meta != null ? _meta.BoostDurationBonus * 0.6f : 0f);
                        EquippedConsumable = null;
                        MissionSystem.Report(MissionType.UsePowerUps, 1);
                    }
                    break;
            }
        }

        /// <summary>Spend full energy meter to fire the equipped power-up (or magnet fallback).</summary>
        public bool TryActivateFromEnergy()
        {
            if (!EnergyReady) return false;
            Energy = 0f;
            var type = EquippedConsumable ?? PowerUpType.Magnet;
            if (type == PowerUpType.SpeedBoost || type == PowerUpType.SlowMo)
                Activate(type, false);
            else
                Activate(type, true);
            EquippedConsumable = type;
            return true;
        }

        public void UseEquipped()
        {
            if (TryActivateCharacterSkill()) return;
            if (TryActivateFromEnergy()) return;
            if (!EquippedConsumable.HasValue) return;
            // Without a full meter, only spend a ready pickup consumable if already granted via Activate equip path.
            // Keep legacy tap for equipped boost/slow only when energy is empty but player has a free charge — none by default.
        }

        /// <summary>Character-unique active skill (tap when ready). Cooldown shared across roster kits.</summary>
        public bool TryActivateCharacterSkill()
        {
            if (!CharacterSkillReady || PlayerController.Instance == null) return false;
            switch (CharacterRoster.ActiveSkill)
            {
                case CharacterActiveSkill.SandDash:
                    _boostTimer = Mathf.Max(_boostTimer, 2.6f + (_meta != null ? _meta.BoostDurationBonus * 0.35f : 0f));
                    ChaseCamera.Instance?.PunchFov(3f);
                    break;
                case CharacterActiveSkill.FrostMagnetBurst:
                    _magnetTimer = Mathf.Max(_magnetTimer, 4.2f);
                    MagnetRadius = _baseMagnetRadius + 1.35f;
                    EnsureMagnetVfx();
                    break;
                case CharacterActiveSkill.CanopyVault:
                    PlayerController.Instance.GrantAirHop();
                    break;
                case CharacterActiveSkill.MinerHeadlamp:
                    EnvironmentEffects.Instance?.ClearDarkness();
                    break;
                case CharacterActiveSkill.EmberShieldPulse:
                    HasShield = true;
                    EnsureShieldVfx();
                    break;
                default:
                    return false;
            }

            _characterSkillCd = CharacterSkillCooldownSeconds;
            MissionSystem.Report(MissionType.UsePowerUps, 1);
            AudioHooks.Instance?.PlayPower();
            GameUI.Instance?.ShowTutorial(CharacterRoster.ActiveSkillLabel);
            return true;
        }

        public bool TryAbsorbHit()
        {
            if (Invulnerable) return true;
            if (!HasShield) return false;
            HasShield = false;
            ClearShieldVfx();
            return true;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_magnetTimer > 0f) _magnetTimer -= dt;
            if (_multiplierTimer > 0f) _multiplierTimer -= dt;
            if (_boostTimer > 0f) _boostTimer -= dt;
            if (_slowTimer > 0f) _slowTimer -= dt;
            if (_reviveIFrames > 0f) _reviveIFrames -= dt;
            if (_characterSkillCd > 0f) _characterSkillCd -= dt;
            if (_magnetTimer <= 0f && MagnetRadius > _baseMagnetRadius + 0.01f)
                MagnetRadius = _baseMagnetRadius;
            TickPetPassives(dt);
            SyncActiveVfx();
            // Never leave timeScale altered — SlowMo uses SpeedScale only
            if (Time.timeScale < 0.99f) Time.timeScale = 1f;
        }

        public int MagnetUpgradeTier => _meta != null ? _meta.Data.magnetRadiusLevel : 0;

        void EnsureMagnetVfx()
        {
            if (PlayerController.Instance == null) return;
            if (_magnetVfx == null)
                _magnetVfx = PowerUpVfx.AttachMagnetSwirl(
                    PlayerController.Instance.transform, true, MagnetUpgradeTier);
            _magnetVfx.gameObject.SetActive(true);
        }

        void EnsureShieldVfx()
        {
            if (PlayerController.Instance == null) return;
            if (_shieldVfx == null)
                _shieldVfx = PowerUpVfx.AttachShieldBubble(PlayerController.Instance.transform, 1.4f);
            _shieldVfx.gameObject.SetActive(true);
        }

        void ClearShieldVfx()
        {
            if (_shieldVfx != null) _shieldVfx.gameObject.SetActive(false);
        }

        void ClearActiveVfx()
        {
            if (_magnetVfx != null) { Object.Destroy(_magnetVfx.gameObject); _magnetVfx = null; }
            if (_shieldVfx != null) { Object.Destroy(_shieldVfx.gameObject); _shieldVfx = null; }
        }

        void SyncActiveVfx()
        {
            if (MagnetActive) EnsureMagnetVfx();
            else if (_magnetVfx != null) _magnetVfx.gameObject.SetActive(false);

            if (HasShield) EnsureShieldVfx();
            else ClearShieldVfx();
        }

        void TickPetPassives(float dt)
        {
            if (RunSession.Instance == null || !RunSession.Instance.IsAlive) return;

            if (CosmeticRoster.PetPassives.MagnetPulse)
            {
                _petMagnetCd -= dt;
                if (_petMagnetCd <= 0f)
                {
                    _petMagnetCd = CosmeticRoster.PetPassives.MagnetPulseInterval;
                    _magnetTimer = Mathf.Max(_magnetTimer, CosmeticRoster.PetPassives.MagnetPulseDuration);
                    MagnetRadius = 3f + _meta.MagnetRadiusBonus + CharacterRoster.PassiveMagnetBonus
                                   + CosmeticRoster.PetPassives.MagnetPulseRadiusBonus;
                    AudioHooks.Instance?.PlayPickup();
                }
            }

            if (CosmeticRoster.PetPassives.ShieldChirp)
            {
                _petShieldCd -= dt;
                if (_petShieldCd <= 0f)
                {
                    _petShieldCd = CosmeticRoster.PetPassives.ShieldChirpInterval;
                    if (!HasShield)
                    {
                        HasShield = true;
                        AudioHooks.Instance?.PlayPickup();
                    }
                }
            }
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            if (Instance == this) Instance = null;
        }
    }
}
