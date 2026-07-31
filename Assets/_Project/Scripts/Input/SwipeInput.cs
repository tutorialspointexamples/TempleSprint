using UnityEngine;

namespace TempleSprint
{
    public enum SwipeDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>Touch/mouse swipe detection + keyboard fallback for Editor.</summary>
    public class SwipeInput : MonoBehaviour
    {
        public static SwipeInput Instance { get; private set; }

        [SerializeField] float minSwipePixels = 50f;
        [SerializeField] float keyboardRepeatBlock = 0.15f;

        public float Sensitivity { get; set; } = 1f;
        public bool TiltEnabled { get; set; }

        Vector2 _startPos;
        bool _tracking;
        float _keyBlock;

        public event System.Action<SwipeDirection> OnSwipe;
        public event System.Action OnTap;

        void Awake() => Instance = this;

        void Update()
        {
            if (_keyBlock > 0f) _keyBlock -= Time.unscaledDeltaTime;
            HandleTouch();
            HandleMouse();
            HandleKeyboard();
        }

        void HandleTouch()
        {
            if (Input.touchCount == 0) return;
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                _tracking = true;
                _startPos = t.position;
            }
            else if (t.phase == TouchPhase.Ended && _tracking)
            {
                _tracking = false;
                Resolve(_startPos, t.position);
            }
        }

        void HandleMouse()
        {
            if (Input.touchCount > 0) return;
            if (Input.GetMouseButtonDown(0))
            {
                _tracking = true;
                _startPos = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0) && _tracking)
            {
                _tracking = false;
                Resolve(_startPos, Input.mousePosition);
            }
        }

        void HandleKeyboard()
        {
            if (_keyBlock > 0f) return;
            SwipeDirection d = SwipeDirection.None;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) d = SwipeDirection.Up;
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) d = SwipeDirection.Down;
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) d = SwipeDirection.Left;
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) d = SwipeDirection.Right;
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                OnTap?.Invoke();
                return;
            }

            if (d != SwipeDirection.None)
            {
                _keyBlock = keyboardRepeatBlock;
                OnSwipe?.Invoke(d);
            }
        }

        void Resolve(Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            float threshold = minSwipePixels / Mathf.Max(0.25f, Sensitivity);
            if (delta.magnitude < threshold)
            {
                OnTap?.Invoke();
                return;
            }

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                OnSwipe?.Invoke(delta.x > 0 ? SwipeDirection.Right : SwipeDirection.Left);
            else
                OnSwipe?.Invoke(delta.y > 0 ? SwipeDirection.Up : SwipeDirection.Down);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
