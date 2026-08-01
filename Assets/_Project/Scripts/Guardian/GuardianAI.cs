using UnityEngine;

namespace TempleSprint
{
    /// <summary>Rubber-band Idol Beast pack — hunched gallop chase with carved idol markings.</summary>
    public class GuardianAI : MonoBehaviour
    {
        public static GuardianAI Instance { get; private set; }

        [SerializeField] float minGap = 8f;
        [SerializeField] float maxGap = 22f;
        [SerializeField] float catchDistance = 1.6f;

        float _gap;
        Transform[] _pack;
        float[] _phase;
        bool _active;
        Renderer[] _eyeGlow;
        Transform _dustWake;
        float _lunge;
        float _lastLungeTime = -10f;
        float _threat01;
        float _lastGrowlTime = -10f;

        enum GrabStruggleState { None, Prompt, Success, Failed }
        GrabStruggleState _struggle;
        SwipeDirection _requiredSwipe;
        float _struggleTimer;
        int _struggleHits;
        const float StruggleWindow = 1.2f;

        // Flank lane pressure — side beasts crowd a lane and force a dodge.
        int _pressureLane = -1;
        float _pressureTimer;
        float _pressureCooldown;
        float _lastPressureHit = -10f;
        float[] _flankX;

        /// <summary>0 far → 1 about to catch (for chase cam pressure / UI).</summary>
        public float Threat01 => _threat01;
        public bool IsLunging => _lunge > 0.05f || _struggle == GrabStruggleState.Prompt;
        public bool InGrabStruggle => _struggle == GrabStruggleState.Prompt;
        /// <summary>Lane currently crowded by flanking beasts (-1 = none).</summary>
        public int PressureLane => _pressureLane;

        void Awake()
        {
            Instance = this;
            BuildVisual();
        }

        void BuildVisual()
        {
            var root = new GameObject("IdolBeastPack").transform;
            root.SetParent(transform, false);
            // Five-beast pack: lead + flanking wings + trailing scouts (stronger chase presence).
            _pack = new Transform[5];
            _phase = new float[5];
            _flankX = new float[5];
            float[] xOff = { -1.35f, -0.7f, 0f, 0.7f, 1.35f };
            float[] zOff = { -0.95f, -0.35f, 0.55f, -0.45f, -1.05f };
            float[] scales = { 0.92f, 1.05f, 1.32f, 1.05f, 0.92f };
            var eyes = new System.Collections.Generic.List<Renderer>();
            for (int i = 0; i < 5; i++)
            {
                _pack[i] = BuildBeast(root, new Vector3(xOff[i], 0f, zOff[i]), scales[i], eyes);
                _phase[i] = i * 0.55f;
                _flankX[i] = xOff[i];
            }
            _eyeGlow = eyes.ToArray();

            _dustWake = new GameObject("PackDustWake").transform;
            _dustWake.SetParent(root, false);
            _dustWake.localPosition = new Vector3(0f, 0.15f, -1.4f);
            for (int i = 0; i < 6; i++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.transform.SetParent(_dustWake, false);
                puff.transform.localPosition = new Vector3(
                    (i % 3 - 1) * 0.45f, Random.Range(0f, 0.25f), -i * 0.22f);
                float s = Random.Range(0.35f, 0.7f);
                puff.transform.localScale = new Vector3(s, s * 0.55f, s);
                puff.GetComponent<Renderer>().sharedMaterial =
                    JunglePalette.Mat(new Color(0.45f, 0.38f, 0.28f, 0.55f), 0.05f);
                Object.Destroy(puff.GetComponent<Collider>());
            }
        }

        static Transform BuildBeast(Transform parent, Vector3 localPos, float scale,
            System.Collections.Generic.List<Renderer> eyeCollect)
        {
            var root = new GameObject("IdolBeast").transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            root.localScale = Vector3.one * scale;

            // Hunched torso with gold idol breastplate
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, 0.72f, 0.12f);
            body.transform.localRotation = Quaternion.Euler(38f, 0f, 0f);
            body.transform.localScale = new Vector3(0.82f, 0.78f, 0.6f);
            body.GetComponent<Renderer>().sharedMaterial = JunglePalette.Guardian;
            Object.Destroy(body.GetComponent<Collider>());

            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "IdolPlate";
            plate.transform.SetParent(root, false);
            plate.transform.localPosition = new Vector3(0f, 0.85f, 0.42f);
            plate.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            plate.transform.localScale = new Vector3(0.55f, 0.55f, 0.12f);
            plate.GetComponent<Renderer>().sharedMaterial = JunglePalette.Gold;
            Object.Destroy(plate.GetComponent<Collider>());

            // Carved diamond glyph on the plate
            var glyph = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glyph.transform.SetParent(plate.transform, false);
            glyph.transform.localPosition = new Vector3(0f, 0f, 0.65f);
            glyph.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            glyph.transform.localScale = new Vector3(0.35f, 0.35f, 0.2f);
            glyph.GetComponent<Renderer>().sharedMaterial = JunglePalette.GoldBright;
            Object.Destroy(glyph.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root, false);
            head.transform.localPosition = new Vector3(0f, 1.22f, 0.52f);
            head.transform.localScale = new Vector3(0.62f, 0.5f, 0.62f);
            head.GetComponent<Renderer>().sharedMaterial = JunglePalette.Guardian;
            Object.Destroy(head.GetComponent<Collider>());

            // Mane crest
            for (int m = 0; m < 4; m++)
            {
                var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spike.name = "Mane_" + m;
                spike.transform.SetParent(head.transform, false);
                spike.transform.localPosition = new Vector3((m - 1.5f) * 0.12f, 0.35f, -0.15f + m * 0.02f);
                spike.transform.localRotation = Quaternion.Euler(-25f - m * 8f, 0f, (m - 1.5f) * 8f);
                spike.transform.localScale = new Vector3(0.08f, 0.35f + m * 0.04f, 0.12f);
                spike.GetComponent<Renderer>().sharedMaterial = JunglePalette.Hazard;
                Object.Destroy(spike.GetComponent<Collider>());
            }

            // Snout
            var snout = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            snout.transform.SetParent(head.transform, false);
            snout.transform.localPosition = new Vector3(0f, -0.12f, 0.38f);
            snout.transform.localScale = new Vector3(0.55f, 0.4f, 0.55f);
            snout.GetComponent<Renderer>().sharedMaterial = JunglePalette.Hazard;
            Object.Destroy(snout.GetComponent<Collider>());

            // Glowing eyes
            foreach (float sx in new[] { -0.2f, 0.2f })
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.transform.SetParent(head.transform, false);
                eye.transform.localPosition = new Vector3(sx, 0.1f, 0.4f);
                eye.transform.localScale = new Vector3(0.22f, 0.18f, 0.14f);
                var er = eye.GetComponent<Renderer>();
                er.sharedMaterial = JunglePalette.EyeWhite;
                eyeCollect?.Add(er);
                Object.Destroy(eye.GetComponent<Collider>());

                var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pupil.transform.SetParent(eye.transform, false);
                pupil.transform.localPosition = new Vector3(0f, 0f, 0.35f);
                pupil.transform.localScale = new Vector3(0.45f, 0.55f, 0.35f);
                pupil.GetComponent<Renderer>().sharedMaterial = JunglePalette.EyeDark;
                Object.Destroy(pupil.GetComponent<Collider>());
            }

            // Long reaching arms with clawed hands
            foreach (var side in new[] { -1f, 1f })
            {
                var arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                arm.name = side < 0 ? "ArmL" : "ArmR";
                arm.transform.SetParent(root, false);
                arm.transform.localPosition = new Vector3(side * 0.5f, 0.78f, 0.28f);
                arm.transform.localRotation = Quaternion.Euler(58f, side * 18f, side * 38f);
                arm.transform.localScale = new Vector3(0.2f, 0.68f, 0.2f);
                arm.GetComponent<Renderer>().sharedMaterial = JunglePalette.Guardian;
                Object.Destroy(arm.GetComponent<Collider>());

                var hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hand.transform.SetParent(arm.transform, false);
                hand.transform.localPosition = new Vector3(0f, -0.9f, 0f);
                hand.transform.localScale = new Vector3(1.5f, 1.0f, 1.5f);
                hand.GetComponent<Renderer>().sharedMaterial = JunglePalette.Hazard;
                Object.Destroy(hand.GetComponent<Collider>());

                for (int c = 0; c < 3; c++)
                {
                    var claw = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    claw.transform.SetParent(hand.transform, false);
                    claw.transform.localPosition = new Vector3((c - 1) * 0.25f, -0.55f, 0.2f);
                    claw.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
                    claw.transform.localScale = new Vector3(0.12f, 0.45f, 0.12f);
                    claw.GetComponent<Renderer>().sharedMaterial = JunglePalette.Gold;
                    Object.Destroy(claw.GetComponent<Collider>());
                }
            }

            // Legs
            foreach (var side in new[] { -1f, 1f })
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                leg.name = side < 0 ? "LegL" : "LegR";
                leg.transform.SetParent(root, false);
                leg.transform.localPosition = new Vector3(side * 0.2f, 0.3f, -0.08f);
                leg.transform.localScale = new Vector3(0.22f, 0.32f, 0.22f);
                leg.GetComponent<Renderer>().sharedMaterial = JunglePalette.Guardian;
                Object.Destroy(leg.GetComponent<Collider>());
            }

            // Tail stump for silhouette
            var tail = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            tail.transform.SetParent(root, false);
            tail.transform.localPosition = new Vector3(0f, 0.55f, -0.45f);
            tail.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            tail.transform.localScale = new Vector3(0.15f, 0.35f, 0.15f);
            tail.GetComponent<Renderer>().sharedMaterial = JunglePalette.Guardian;
            Object.Destroy(tail.GetComponent<Collider>());

            return root;
        }

        public void BeginRun()
        {
            _active = true;
            _gap = 16f;
            _lunge = 0f;
            _threat01 = 0f;
            _lastLungeTime = -10f;
            _struggle = GrabStruggleState.None;
            _struggleTimer = 0f;
            _struggleHits = 0;
            _pressureLane = -1;
            _pressureTimer = 0f;
            _pressureCooldown = 2.5f;
            _lastPressureHit = -10f;
            GameUI.Instance?.HideGuardianStruggle();
            if (PlayerController.Instance != null)
            {
                var p = PlayerController.Instance.transform;
                transform.position = p.position - p.forward * _gap;
                transform.rotation = Quaternion.LookRotation(p.forward, Vector3.up);
            }
        }

        public void Stop()
        {
            _active = false;
            _lunge = 0f;
            _threat01 = 0f;
            _pressureLane = -1;
            _pressureTimer = 0f;
            _struggle = GrabStruggleState.None;
            GameUI.Instance?.HideGuardianStruggle();
        }

        public bool TryStruggleInput(SwipeDirection dir)
        {
            if (_struggle != GrabStruggleState.Prompt) return false;
            // Matching the prompted swipe counts strongest; any frantic swipe/mash still helps.
            if (dir == _requiredSwipe)
                _struggleHits += 2;
            else
                _struggleHits += 1;
            GameUI.Instance?.PulseGuardianStruggle(_struggleHits);
            if (_struggleHits >= 3)
                ResolveStruggleSuccess();
            return true;
        }

        public bool TryStruggleTap()
        {
            if (_struggle != GrabStruggleState.Prompt) return false;
            _struggleHits += 1;
            GameUI.Instance?.PulseGuardianStruggle(_struggleHits);
            if (_struggleHits >= 3)
                ResolveStruggleSuccess();
            return true;
        }

        void BeginGrabStruggle()
        {
            _struggle = GrabStruggleState.Prompt;
            _struggleTimer = StruggleWindow;
            _struggleHits = 0;
            _requiredSwipe = Random.value < 0.5f ? SwipeDirection.Left : SwipeDirection.Right;
            _gap = catchDistance + 0.35f;
            _lunge = 1f;
            _threat01 = 1f;
            GameUI.Instance?.ShowGuardianStruggle(_requiredSwipe, _struggleTimer);
            AudioHooks.Instance?.PlayGuardianLunge();
            ChaseCamera.Instance?.PunchFov(5.5f);
        }

        void TickGrabStruggle()
        {
            _struggleTimer -= Time.deltaTime;
            _lunge = Mathf.Clamp01(_struggleTimer / StruggleWindow);
            _threat01 = 1f;
            GameUI.Instance?.UpdateGuardianStruggle(_struggleTimer, _struggleHits);

            var player = PlayerController.Instance;
            if (player != null)
            {
                float displayGap = Mathf.Max(0.4f, _gap - 1.6f);
                Vector3 desired = player.transform.position - player.transform.forward * displayGap;
                desired.y = 0f;
                transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-10f * Time.deltaTime));
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(player.transform.forward, Vector3.up),
                    1f - Mathf.Exp(-10f * Time.deltaTime));
            }

            if (_struggleTimer <= 0f && _struggle == GrabStruggleState.Prompt)
            {
                _struggle = GrabStruggleState.Failed;
                GameUI.Instance?.HideGuardianStruggle();
                RunSession.Instance?.EndRun("Caught by the Idol Beast");
            }
        }

        void ResolveStruggleSuccess()
        {
            _struggle = GrabStruggleState.Success;
            _gap = minGap + 3.2f;
            _lunge = 0f;
            _threat01 = 0.35f;
            _lastLungeTime = Time.time;
            GameUI.Instance?.HideGuardianStruggle();
            RunSession.Instance?.RegisterNearMiss();
            ChaseCamera.Instance?.PunchFov(3.2f);
            AudioHooks.Instance?.PlayJump();
            GameUI.Instance?.ShowTutorial("Broke free!");
            // Brief cooldown before another grab attempt.
            _struggle = GrabStruggleState.None;
        }

        void Update()
        {
            if (!_active || PlayerController.Instance == null || RunSession.Instance == null || !RunSession.Instance.IsAlive)
                return;

            if (_struggle == GrabStruggleState.Prompt)
            {
                TickGrabStruggle();
                return;
            }

            var player = PlayerController.Instance;
            float playerSpeed = DifficultyDirector.Instance != null ? DifficultyDirector.Instance.CurrentSpeed : 10f;

            float targetGap = Mathf.Lerp(maxGap, minGap, Mathf.Clamp01(RunSession.Instance.Distance / 600f));
            if (player.IsStumbling)
                targetGap = Mathf.Max(minGap * 0.6f, targetGap - 6f);

            float gapError = _gap - targetGap;
            float catchUp = playerSpeed;
            if (gapError > 2f) catchUp = playerSpeed * 1.25f;
            else if (gapError < -2f) catchUp = playerSpeed * 0.75f;
            if (player.IsStumbling) catchUp = playerSpeed * 1.45f;

            // Grab telegraph: when close, snap forward with a clawing lunge then recoil.
            _threat01 = Mathf.Clamp01(Mathf.InverseLerp(maxGap, catchDistance, _gap));
            if (_lunge > 0f)
                _lunge = Mathf.MoveTowards(_lunge, 0f, Time.deltaTime * 1.35f);
            else if (_threat01 > 0.62f && Time.time - _lastLungeTime > 2.4f)
            {
                _lunge = 1f;
                _lastLungeTime = Time.time;
                _gap = Mathf.Max(catchDistance + 0.35f, _gap - 2.8f);
                ChaseCamera.Instance?.PunchFov(4.5f);
                AudioHooks.Instance?.PlayGuardianLunge();
            }

            float closeBoost = _lunge > 0f ? 3.8f : (_threat01 > 0.55f ? 1.4f : 1f);
            _gap = Mathf.MoveTowards(_gap, targetGap, Time.deltaTime * 2.5f * closeBoost);
            _gap = Mathf.Max(0.8f, _gap);

            float displayGap = Mathf.Max(0.55f, _gap - _lunge * 2.2f);
            var pt = player.transform;
            Vector3 desired = pt.position - pt.forward * displayGap;
            desired.y = 0f;
            float chaseSnap = 6f + _threat01 * 4f + _lunge * 8f;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-chaseSnap * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(pt.forward, Vector3.up),
                1f - Mathf.Exp(-8f * Time.deltaTime));

            TickLanePressure(player);

            // Gallop cycle + claw reach during lunge + flank lane crowd
            for (int i = 0; i < _pack.Length; i++)
            {
                if (_pack[i] == null) continue;
                _phase[i] += Time.deltaTime * (11f + _threat01 * 4f + _lunge * 6f);
                float s = Mathf.Sin(_phase[i]);
                bool lead = i == 2;
                float reach = _lunge * (0.55f + (lead ? 0.4f : 0.18f));
                // Preserve staggered base Z from BuildVisual via phase bob only on Y / temp reach.
                float baseZ = i switch { 0 => -0.95f, 1 => -0.35f, 2 => 0.55f, 3 => -0.45f, _ => -1.05f };
                float baseX = i switch { 0 => -1.35f, 1 => -0.7f, 2 => 0f, 3 => 0.7f, _ => 1.35f };
                float targetX = baseX;
                if (_pressureLane >= 0 && !lead)
                {
                    // Side beasts crowd the pressured lane (player-local X).
                    float laneX = (_pressureLane - 1) * PlayerController.LaneWidth;
                    float flankBias = (i < 2) ? -0.35f : 0.35f;
                    targetX = Mathf.Lerp(baseX, laneX + flankBias, Mathf.Clamp01(_pressureTimer * 1.4f));
                }
                _flankX[i] = Mathf.Lerp(_flankX[i], targetX, 1f - Mathf.Exp(-8f * Time.deltaTime));
                _pack[i].localPosition = new Vector3(_flankX[i], Mathf.Abs(s) * 0.18f + _lunge * 0.12f, baseZ + reach);
                float yawBias = _pressureLane >= 0 && !lead
                    ? Mathf.Clamp((_flankX[i] - baseX) * 18f, -22f, 22f) : 0f;
                _pack[i].localRotation = Quaternion.Euler(s * 12f - _lunge * 28f, yawBias, s * 4f);

                var armL = _pack[i].Find("ArmL");
                var armR = _pack[i].Find("ArmR");
                float claw = _lunge * 55f + (_pressureLane >= 0 && !lead ? 18f : 0f);
                if (armL != null) armL.localRotation = Quaternion.Euler(55f + s * 25f - claw, -15f - claw * 0.2f, -35f);
                if (armR != null) armR.localRotation = Quaternion.Euler(55f - s * 25f - claw, 15f + claw * 0.2f, 35f);
            }

            if (_dustWake != null)
            {
                float dustPulse = 0.85f + _threat01 * 0.45f + _lunge * 0.35f;
                _dustWake.localScale = new Vector3(dustPulse, dustPulse * 0.7f, 1f + _threat01);
                _dustWake.localPosition = new Vector3(0f, 0.12f + Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.08f, -1.5f - _lunge * 0.3f);
            }

            // Eyes pulse hotter as the pack closes in
            if (_eyeGlow != null)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * (6f + _threat01 * 8f + _lunge * 10f));
                Color eye = Color.Lerp(new Color(0.95f, 0.95f, 0.95f), new Color(1f, 0.35f, 0.12f),
                    Mathf.Clamp01(_threat01 * pulse + _lunge * 0.35f));
                foreach (var r in _eyeGlow)
                {
                    if (r == null) continue;
                    r.material.color = eye;
                    if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", eye);
                }
            }

            if (_gap <= catchDistance + 3.5f && Time.time - _lastGrowlTime > 1.4f)
            {
                _lastGrowlTime = Time.time;
                AudioHooks.Instance?.PlayGuardian();
            }

            if (_gap <= catchDistance)
                BeginGrabStruggle();
        }

        void TickLanePressure(PlayerController player)
        {
            if (player == null) return;
            if (_pressureCooldown > 0f) _pressureCooldown -= Time.deltaTime;

            if (_pressureLane >= 0)
            {
                _pressureTimer += Time.deltaTime;
                // Peak of the flank shove — staying in the crowded lane stumbles the runner.
                if (_pressureTimer > 0.55f && _pressureTimer < 1.15f
                    && player.Lane == _pressureLane
                    && _threat01 > 0.42f
                    && Time.time - _lastPressureHit > 1.6f
                    && !player.IsSliding
                    && PowerUpController.Instance != null
                    && !PowerUpController.Instance.IsInvulnerable)
                {
                    _lastPressureHit = Time.time;
                    player.RegisterStumble(0.7f);
                    AudioHooks.Instance?.PlayHit();
                    RunSession.Instance?.RegisterNearMiss();
                    ChaseCamera.Instance?.PunchFov(2.2f);
                    GameUI.Instance?.ShowTutorial(_pressureLane == 0 ? "Flank LEFT — dodge!" :
                        _pressureLane == 2 ? "Flank RIGHT — dodge!" : "Flank CENTER — dodge!");
                }

                if (_pressureTimer >= 1.35f)
                {
                    _pressureLane = -1;
                    _pressureTimer = 0f;
                    _pressureCooldown = Mathf.Lerp(2.8f, 1.6f, _threat01);
                }
                return;
            }

            // Start a new flank shove when the pack is close enough to matter.
            if (_threat01 < 0.48f || _pressureCooldown > 0f || _lunge > 0.2f) return;
            if (RunSession.Instance != null && RunSession.Instance.Distance < 80f) return;

            // Prefer crowding the player's current lane so they must leave it.
            _pressureLane = player.Lane;
            if (Random.value < 0.35f)
                _pressureLane = Mathf.Clamp(player.Lane + (Random.value < 0.5f ? -1 : 1), 0, 2);
            _pressureTimer = 0f;
            AudioHooks.Instance?.PlayGuardian();
            ChaseCamera.Instance?.PunchFov(1.6f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
