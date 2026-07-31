using UnityEngine;

namespace TempleSprint
{
    public enum FireCrossingMode
    {
        Jump = 0,
        Vine = 1,
        WaterDunk = 2
    }

    /// <summary>Per-tile fire crossing metadata; never destroyed while IsOccupied.</summary>
    public class FireCrossingMarker : MonoBehaviour
    {
        public FireCrossingMode Mode;
        public float ChannelStart;
        public float ChannelEnd;
        public float PathStartDistance;
        public RunDifficulty Difficulty;
        public bool IsOccupied { get; private set; }
        public bool Completed { get; private set; }

        public float ChannelLength => ChannelEnd - ChannelStart;

        public void Configure(FireCrossingMode mode, float channelStart, float channelEnd,
            float pathStart, RunDifficulty difficulty)
        {
            Mode = mode;
            ChannelStart = channelStart;
            ChannelEnd = channelEnd;
            PathStartDistance = pathStart;
            Difficulty = difficulty;
            IsOccupied = false;
            Completed = false;
        }

        public void SetOccupied(bool occupied) => IsOccupied = occupied;

        public void MarkCompleted()
        {
            Completed = true;
            IsOccupied = false;
        }

        void OnDisable() => IsOccupied = false;
    }

    /// <summary>Auto-grab vine swing across the fire channel.</summary>
    public class FireVineSwing : MonoBehaviour
    {
        public FireCrossingMarker Marker;
        public float SwingDuration = 1.15f;
        public float ApexHeight = 2.8f;
        public float ReleaseWindowStart = 0.52f;
        public float ReleaseWindowEnd = 0.82f;

        Transform _vineVisual;
        bool _grabbed;

        void OnDisable() => _grabbed = false;

        public void BuildVisual(Transform parent, float grabZ, float landZ)
        {
            var nearPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nearPost.name = "VineAnchorNear";
            nearPost.transform.SetParent(parent, false);
            nearPost.transform.localPosition = new Vector3(0f, 1.7f, grabZ);
            nearPost.transform.localScale = new Vector3(0.32f, 1.7f, 0.32f);
            nearPost.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
            Object.Destroy(nearPost.GetComponent<Collider>());

            var farPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            farPost.name = "VineAnchorFar";
            farPost.transform.SetParent(parent, false);
            farPost.transform.localPosition = new Vector3(0f, 1.7f, landZ);
            farPost.transform.localScale = new Vector3(0.32f, 1.7f, 0.32f);
            farPost.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
            Object.Destroy(farPost.GetComponent<Collider>());

            _vineVisual = new GameObject("SwingVine").transform;
            _vineVisual.SetParent(parent, false);

            var vine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vine.transform.SetParent(_vineVisual, false);
            vine.transform.localPosition = new Vector3(0f, -1.35f, 0f);
            vine.transform.localScale = new Vector3(0.1f, 1.4f, 0.1f);
            vine.GetComponent<Renderer>().sharedMaterial = JunglePalette.FoliageDark;
            Object.Destroy(vine.GetComponent<Collider>());

            var handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handle.transform.SetParent(_vineVisual, false);
            handle.transform.localPosition = new Vector3(0f, -2.55f, 0f);
            handle.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            handle.GetComponent<Renderer>().sharedMaterial = JunglePalette.Leather;
            Object.Destroy(handle.GetComponent<Collider>());
        }

        void OnTriggerEnter(Collider other)
        {
            if (_grabbed || Marker == null) return;
            if (other.GetComponent<PlayerController>() == null
                && other.GetComponentInParent<PlayerController>() == null)
                return;
            var player = PlayerController.Instance;
            if (player == null || player.Traversal != TraversalMode.None) return;
            _grabbed = true;
            player.BeginVineSwing(Marker, this);
        }

        public void UpdateVineVisual(float t, Vector3 worldPos)
        {
            if (_vineVisual == null) return;
            _vineVisual.position = worldPos + Vector3.up * 2.5f;
            float sway = Mathf.Lerp(-20f, 20f, t);
            _vineVisual.rotation = Quaternion.Euler(0f, 0f, sway);
        }

        public bool InReleaseWindow(float t) => t >= ReleaseWindowStart && t <= ReleaseWindowEnd;
    }

    /// <summary>Auto-grab water barrel that grants a short flame-safe dunk window.</summary>
    public class FireWaterDunkMount : MonoBehaviour
    {
        public FireCrossingMarker Marker;
        public float SafeDuration = 1.4f;
        bool _used;

        void OnDisable() => _used = false;

        void OnTriggerEnter(Collider other)
        {
            if (_used || Marker == null) return;
            if (other.GetComponent<PlayerController>() == null
                && other.GetComponentInParent<PlayerController>() == null)
                return;
            var player = PlayerController.Instance;
            if (player == null || player.Traversal != TraversalMode.None) return;
            _used = true;
            player.BeginWaterDunk(Marker, SafeDuration);
        }
    }
}
