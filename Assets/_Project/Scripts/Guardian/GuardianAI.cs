using UnityEngine;

namespace TempleSprint
{
    /// <summary>Rubber-band Demon Monkey pack — hunched gallop chase.</summary>
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

        void Awake()
        {
            Instance = this;
            BuildVisual();
        }

        void BuildVisual()
        {
            var root = new GameObject("DemonPack").transform;
            root.SetParent(transform, false);
            _pack = new Transform[3];
            _phase = new float[3];
            float[] xOff = { -0.95f, 0f, 0.95f };
            float[] zOff = { -0.55f, 0.4f, -0.7f };
            for (int i = 0; i < 3; i++)
            {
                _pack[i] = BuildMonkey(root, new Vector3(xOff[i], 0f, zOff[i]), i == 1 ? 1.2f : 1.0f);
                _phase[i] = i * 0.7f;
            }
        }

        static Transform BuildMonkey(Transform parent, Vector3 localPos, float scale)
        {
            var root = new GameObject("DemonMonkey").transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            root.localScale = Vector3.one * scale;

            // Hunched torso
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, 0.7f, 0.1f);
            body.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            body.transform.localScale = new Vector3(0.75f, 0.7f, 0.55f);
            body.GetComponent<Renderer>().sharedMaterial = JunglePalette.Guardian;
            Object.Destroy(body.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root, false);
            head.transform.localPosition = new Vector3(0f, 1.15f, 0.45f);
            head.transform.localScale = new Vector3(0.55f, 0.45f, 0.55f);
            head.GetComponent<Renderer>().sharedMaterial = JunglePalette.Guardian;
            Object.Destroy(head.GetComponent<Collider>());

            // Snout
            var snout = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            snout.transform.SetParent(head.transform, false);
            snout.transform.localPosition = new Vector3(0f, -0.1f, 0.35f);
            snout.transform.localScale = new Vector3(0.55f, 0.4f, 0.5f);
            snout.GetComponent<Renderer>().sharedMaterial = JunglePalette.Hazard;
            Object.Destroy(snout.GetComponent<Collider>());

            // Glowing white eyes
            foreach (float sx in new[] { -0.18f, 0.18f })
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.transform.SetParent(head.transform, false);
                eye.transform.localPosition = new Vector3(sx, 0.08f, 0.38f);
                eye.transform.localScale = new Vector3(0.2f, 0.16f, 0.12f);
                eye.GetComponent<Renderer>().sharedMaterial = JunglePalette.EyeWhite;
                Object.Destroy(eye.GetComponent<Collider>());
            }

            // Long reaching arms
            foreach (var side in new[] { -1f, 1f })
            {
                var arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                arm.name = side < 0 ? "ArmL" : "ArmR";
                arm.transform.SetParent(root, false);
                arm.transform.localPosition = new Vector3(side * 0.45f, 0.75f, 0.25f);
                arm.transform.localRotation = Quaternion.Euler(55f, side * 15f, side * 35f);
                arm.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
                arm.GetComponent<Renderer>().sharedMaterial = JunglePalette.Guardian;
                Object.Destroy(arm.GetComponent<Collider>());

                var hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hand.transform.SetParent(arm.transform, false);
                hand.transform.localPosition = new Vector3(0f, -0.85f, 0f);
                hand.transform.localScale = new Vector3(1.4f, 0.9f, 1.4f);
                hand.GetComponent<Renderer>().sharedMaterial = JunglePalette.Hazard;
                Object.Destroy(hand.GetComponent<Collider>());
            }

            // Legs
            foreach (var side in new[] { -1f, 1f })
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                leg.name = side < 0 ? "LegL" : "LegR";
                leg.transform.SetParent(root, false);
                leg.transform.localPosition = new Vector3(side * 0.18f, 0.28f, -0.05f);
                leg.transform.localScale = new Vector3(0.2f, 0.28f, 0.2f);
                leg.GetComponent<Renderer>().sharedMaterial = JunglePalette.Guardian;
                Object.Destroy(leg.GetComponent<Collider>());
            }

            return root;
        }

        float _lastGrowlTime = -10f;

        public void BeginRun()
        {
            _active = true;
            _gap = 16f;
            if (PlayerController.Instance != null)
            {
                var p = PlayerController.Instance.transform;
                transform.position = p.position - p.forward * _gap;
                transform.rotation = Quaternion.LookRotation(p.forward, Vector3.up);
            }
        }

        public void Stop() => _active = false;

        void Update()
        {
            if (!_active || PlayerController.Instance == null || RunSession.Instance == null || !RunSession.Instance.IsAlive)
                return;

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

            _gap = Mathf.MoveTowards(_gap, targetGap, Time.deltaTime * 2.5f);
            _gap = Mathf.Max(0.8f, _gap);

            var pt = player.transform;
            Vector3 desired = pt.position - pt.forward * _gap;
            desired.y = 0f;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-6f * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(pt.forward, Vector3.up),
                1f - Mathf.Exp(-8f * Time.deltaTime));

            // Gallop cycle
            for (int i = 0; i < _pack.Length; i++)
            {
                if (_pack[i] == null) continue;
                _phase[i] += Time.deltaTime * 11f;
                float s = Mathf.Sin(_phase[i]);
                var lp = _pack[i].localPosition;
                _pack[i].localPosition = new Vector3(lp.x, Mathf.Abs(s) * 0.18f, lp.z);
                _pack[i].localRotation = Quaternion.Euler(s * 12f, 0f, s * 4f);

                var armL = _pack[i].Find("ArmL");
                var armR = _pack[i].Find("ArmR");
                if (armL != null) armL.localRotation = Quaternion.Euler(55f + s * 25f, -15f, -35f);
                if (armR != null) armR.localRotation = Quaternion.Euler(55f - s * 25f, 15f, 35f);
            }

            // Warning growl while the pack is closing in
            if (_gap <= catchDistance + 3.5f && Time.time - _lastGrowlTime > 1.4f)
            {
                _lastGrowlTime = Time.time;
                AudioHooks.Instance?.PlayGuardian();
            }

            if (_gap <= catchDistance)
                RunSession.Instance.EndRun("Caught by Demon Monkeys");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
