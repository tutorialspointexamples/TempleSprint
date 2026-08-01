using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TempleSprint
{
    /// <summary>Temple Run chase cam — follows player facing through turns.</summary>
    public class ChaseCamera : MonoBehaviour
    {
        public static ChaseCamera Instance { get; private set; }

        Transform _target;
        Vector3 _localOffset = new Vector3(0f, 2.6f, -5.0f);
        float _baseFov = 58f;
        float _punchFov;
        float _bob;
        Camera _cam;
        bool _snapped;
        float _pullback;
        float _lookLift;
        float _threatPush;
        bool _cinematic;
        Vector3 _cinematicPos;
        Vector3 _cinematicLook;
        float _cinematicFov = 52f;

        void Awake()
        {
            Instance = this;
            _cam = gameObject.GetComponent<Camera>();
            if (_cam == null) _cam = gameObject.AddComponent<Camera>();
            _cam.fieldOfView = _baseFov;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 160f;
            _cam.clearFlags = CameraClearFlags.Skybox;
            _cam.backgroundColor = new Color(0.35f, 0.48f, 0.52f, 1f);
            _cam.depth = 0;
            _cam.enabled = true;
            gameObject.tag = "MainCamera";
            if (GetComponent<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
            if (GetComponent<UniversalAdditionalCameraData>() == null)
                gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            _snapped = false;
            if (_target != null)
                SnapNow();
        }

        /// <summary>Boat pullback / rope apex framing. pullback meters back, lookLift meters up.</summary>
        public void SetTraversalBias(float pullback, float lookLift)
        {
            _pullback = pullback;
            _lookLift = lookLift;
        }

        public void SnapNow()
        {
            if (_target == null) return;
            Vector3 desired = _target.TransformPoint(_localOffset);
            transform.position = desired;
            Vector3 look = _target.position + Vector3.up * 1.15f + _target.forward * 2.0f;
            transform.rotation = Quaternion.LookRotation(look - transform.position, Vector3.up);
            if (_cam != null) _cam.fieldOfView = _baseFov;
            _snapped = true;
        }

        public void PunchFov(float amount = 3f) => _punchFov = amount;

        /// <summary>Brief opening framing — idol theft beat before chase cam resumes.</summary>
        public void SetCinematicPose(Vector3 worldPos, Vector3 lookAt, float fov = 52f)
        {
            _cinematic = true;
            _cinematicPos = worldPos;
            _cinematicLook = lookAt;
            _cinematicFov = fov;
        }

        public void ClearCinematic()
        {
            _cinematic = false;
            _snapped = false;
        }

        void LateUpdate()
        {
            if (_cinematic)
            {
                transform.position = Vector3.Lerp(transform.position, _cinematicPos, 1f - Mathf.Exp(-10f * Time.deltaTime));
                Vector3 dir = _cinematicLook - transform.position;
                if (dir.sqrMagnitude > 0.001f)
                {
                    var lookRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 1f - Mathf.Exp(-10f * Time.deltaTime));
                }
                if (_cam != null)
                    _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _cinematicFov, Time.deltaTime * 5f);
                return;
            }

            LateUpdateChase();
        }

        void LateUpdateChase()
        {
            if (_target == null) return;
            if (!_snapped) SnapNow();

            float speed = DifficultyDirector.Instance != null ? DifficultyDirector.Instance.CurrentSpeed : 10f;
            bool running = RunSession.Instance != null && RunSession.Instance.IsAlive;
            if (running)
                _bob += Time.deltaTime * (6.5f + speed * 0.25f);
            float bobY = running ? Mathf.Sin(_bob) * 0.03f : 0f;

            // Idol Beast pressure: nudge closer + widen FOV so the chase packs the frame.
            float threat = GuardianAI.Instance != null ? GuardianAI.Instance.Threat01 : 0f;
            float lunge = GuardianAI.Instance != null && GuardianAI.Instance.IsLunging ? 1f : 0f;
            float threatTarget = threat * 0.85f + lunge * 0.55f;
            _threatPush = Mathf.Lerp(_threatPush, threatTarget, 1f - Mathf.Exp(-5f * Time.deltaTime));

            Vector3 offset = _localOffset + new Vector3(0f, bobY + _lookLift * 0.15f - _threatPush * 0.18f,
                -_pullback + _threatPush * 1.35f);
            Vector3 desired = _target.TransformPoint(offset);
            // Stay at deck height when the player plummets into a gap so the fall stays on screen.
            desired.y = Mathf.Max(desired.y, 1.2f);
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-18f * Time.deltaTime));

            Vector3 look = _target.position + Vector3.up * (1.15f + _lookLift * 0.35f - _threatPush * 0.08f)
                           + _target.forward * (2.0f - _threatPush * 0.45f);
            // Slight rear glance bias when the pack lunges so the threat reads in-frame.
            if (_threatPush > 0.35f)
                look -= _target.forward * (_threatPush * 0.35f);
            var lookRot = Quaternion.LookRotation(look - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 1f - Mathf.Exp(-16f * Time.deltaTime));

            float targetFov = _baseFov + Mathf.Clamp((speed - 10f) * 0.35f, 0f, 8f)
                              + _punchFov + _pullback * 2f + _threatPush * 6f;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, Time.deltaTime * 4f);
            _punchFov = Mathf.MoveTowards(_punchFov, 0f, Time.deltaTime * 9f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
