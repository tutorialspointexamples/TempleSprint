using UnityEngine;

namespace TempleSprint
{
    /// <summary>
    /// Visible ghost rival that races the player's best timed distance samples.
    /// Original silhouette — not a clone of any third-party character.
    /// </summary>
    public class GhostRivalRunner : MonoBehaviour
    {
        public static GhostRivalRunner Instance { get; private set; }

        Transform _body;
        Material _ghostMat;
        float _elapsed;
        bool _active;
        float _ghostDist;
        int _ghostLane = 1;
        float _laneOffset;
        string _label = "";

        public float GhostDistance => _ghostDist;
        public string HudLabel => _label;
        public bool IsRacing => _active;

        public static GhostRivalRunner Ensure(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("GhostRival");
            go.transform.SetParent(parent, false);
            return go.AddComponent<GhostRivalRunner>();
        }

        void Awake()
        {
            Instance = this;
            BuildSilhouette();
            SetVisible(false);
        }

        void BuildSilhouette()
        {
            _ghostMat = JunglePalette.Mat(new Color(0.45f, 0.85f, 1f, 0.45f), 0.05f, 0.9f);
            // Soft translucent cyan so it reads as a rival, not a second solid runner.
            if (_ghostMat.HasProperty("_Surface")) _ghostMat.SetFloat("_Surface", 1f);
            if (_ghostMat.HasProperty("_BaseColor"))
                _ghostMat.SetColor("_BaseColor", new Color(0.45f, 0.85f, 1f, 0.55f));

            _body = new GameObject("GhostBody").transform;
            _body.SetParent(transform, false);

            void Part(PrimitiveType p, string name, Vector3 pos, Vector3 scale)
            {
                var go = GameObject.CreatePrimitive(p);
                go.name = name;
                go.transform.SetParent(_body, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = _ghostMat;
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Object.Destroy(go.GetComponent<Collider>());
            }

            Part(PrimitiveType.Capsule, "Torso", new Vector3(0f, 1f, 0f), new Vector3(0.5f, 0.95f, 0.45f));
            Part(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.95f, 0.05f), new Vector3(0.45f, 0.45f, 0.45f));
            Part(PrimitiveType.Cube, "Pack", new Vector3(0f, 1.15f, -0.28f), new Vector3(0.35f, 0.45f, 0.18f));
            Part(PrimitiveType.Cube, "Scarf", new Vector3(0f, 1.55f, 0.22f), new Vector3(0.4f, 0.12f, 0.08f));
        }

        public void BeginRun()
        {
            _elapsed = 0f;
            _ghostDist = 0f;
            _ghostLane = 1;
            _laneOffset = 0f;
            _active = GhostRunService.TryLoadGhost(out _, out _, out _);
            SetVisible(_active);
            _label = _active ? "Ghost racing" : "";
            if (_active)
                GhostRunService.SampleAt(0f, out _ghostDist, out _ghostLane);
        }

        public void Stop()
        {
            _active = false;
            SetVisible(false);
            _label = "";
        }

        void SetVisible(bool on)
        {
            if (_body != null) _body.gameObject.SetActive(on);
        }

        void LateUpdate()
        {
            if (!_active || RunSession.Instance == null || !RunSession.Instance.IsAlive)
            {
                if (_body != null && _body.gameObject.activeSelf) SetVisible(false);
                return;
            }

            _elapsed += Time.deltaTime;
            GhostRunService.SampleAt(_elapsed, out _ghostDist, out _ghostLane);

            float playerDist = PlayerController.Instance != null ? PlayerController.Instance.PathDistance : 0f;
            float delta = _ghostDist - playerDist;
            if (Mathf.Abs(delta) < 1.5f) _label = "Ghost neck-and-neck";
            else if (delta > 0f) _label = $"Ghost +{delta:0}m ahead";
            else _label = $"Ghost {Mathf.Abs(delta):0}m behind";

            // Keep ghost on the same route pose as the live track.
            PathPose pose = new PathPose(transform.position, 0f, _ghostDist);
            var tile = TileSpawner.Instance != null
                ? TileSpawner.Instance.FindTileAtPathDistance(_ghostDist)
                : null;
            if (tile != null) pose = tile.SampleAtPathDistance(_ghostDist);

            float targetLane = (_ghostLane - 1) * PlayerController.LaneWidth;
            _laneOffset = Mathf.Lerp(_laneOffset, targetLane, 1f - Mathf.Exp(-10f * Time.deltaTime));

            Vector3 pos = pose.position + pose.Right * _laneOffset;
            pos.y = 0f;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, pose.yaw, 0f));

            // Soft bob so the rival feels alive.
            if (_body != null)
            {
                float bob = Mathf.Abs(Mathf.Sin(Time.time * 11f)) * 0.05f;
                _body.localPosition = new Vector3(0f, bob, 0f);
            }

            SetVisible(true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
