using UnityEngine;

namespace TempleSprint
{
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        public const int LaneCount = 3;
        public const float LaneWidth = 2.2f;

        public int Lane { get; private set; } = 1;
        public bool IsSliding { get; private set; }
        public bool IsJumping => !_grounded;
        /// <summary>Distance along the path (replaces world Z as the run metric).</summary>
        public float PathDistance { get; private set; }
        /// <summary>Compat alias — systems that used WorldZ now mean path distance.</summary>
        public float WorldZ => PathDistance;
        public float FacingYaw { get; private set; }
        public Transform Visual { get; private set; }
        public TraversalMode Traversal { get; private set; }
        public float LaneOffset => _laneOffset;

        [SerializeField] float laneLerp = 12f;
        [SerializeField] float jumpVelocity = 9f;
        [SerializeField] float gravity = 28f;
        [SerializeField] float slideDuration = 0.7f;

        float _verticalVel;
        bool _grounded = true;
        bool _airHopUsed;
        float _slideTimer;
        float _stumbleTimer;
        float _laneOffset;
        CapsuleCollider _col;
        Vector3 _baseColCenter;
        float _baseColHeight;
        bool _inputBound;
        ExplorerRunnerVisual _explorer;
        bool _deathFalling;
        float _deathFallVel;
        Light _lantern;

        RiverCrossingMarker _riverMarker;
        RiverBoatRide _boatRide;
        RiverRopeSwing _ropeSwing;
        float _ropeT;
        bool _ropeReleased;

        FireCrossingMarker _fireMarker;
        FireVineSwing _vineSwing;
        float _dunkSafeTimer;
        Transform _dunkSteam;

        SpecialStageMarker _specialMarker;
        MineCartRide _mineCartRide;
        IceBoardRide _iceBoardRide;
        WaterSlideRide _waterSlideRide;
        float _ziplineHeight;
        float _swimDepth;
        Transform _swimBubbles;
        float _wallRunHeight;
        float _wallRunOffset;
        int _wallSide = -1;
        float _ledgeHeight;
        float _canopyRopeHeight;
        float _waterfallDiveHeight;
        float _waterfallPoolDepth;

        public bool IsStumbling => _stumbleTimer > 0f;

        void Awake()
        {
            Instance = this;
            BuildVisual();
            _col = gameObject.AddComponent<CapsuleCollider>();
            _col.height = 1.9f;
            _col.radius = 0.36f;
            _col.center = new Vector3(0f, 0.95f, 0f);
            _col.isTrigger = true;
            _baseColCenter = _col.center;
            _baseColHeight = _col.height;

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            AttachLantern();
        }

        void AttachLantern()
        {
            var go = new GameObject("Lantern");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.6f, 0.4f);
            _lantern = go.AddComponent<Light>();
            _lantern.type = LightType.Point;
            _lantern.color = new Color(1f, 0.82f, 0.55f);
            _lantern.range = 14f;
            _lantern.intensity = 0f;
            _lantern.shadows = LightShadows.None;
        }

        void BuildVisual()
        {
            _explorer = ExplorerRunnerVisual.Create(transform);
            Visual = _explorer.transform;
        }

        public void BindInput()
        {
            if (_inputBound || SwipeInput.Instance == null) return;
            SwipeInput.Instance.OnSwipe += HandleSwipe;
            SwipeInput.Instance.OnTap += HandleTap;
            _inputBound = true;
        }

        public void ResetAtStart()
        {
            Lane = 1;
            PathDistance = 0f;
            FacingYaw = 0f;
            _laneOffset = 0f;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _verticalVel = 0f;
            _grounded = true;
            _airHopUsed = false;
            IsSliding = false;
            _slideTimer = 0f;
            _stumbleTimer = 0f;
            _deathFalling = false;
            _deathFallVel = 0f;
            ClearTraversal();
            RestoreCollider();
            _explorer?.ClearHeldIdol();
            _explorer?.ClearOutcomePose();
            _explorer?.ResetPose();
            _explorer?.SetPoseFlags(false, false);
            ApplyCharacterColors();
        }

        /// <summary>Place the runner back on the deck, further along the path, after a revive.</summary>
        public void AdvanceAfterRevive(float meters)
        {
            ClearTraversal();
            ClearOutcomePose();
            _deathFalling = false;
            _deathFallVel = 0f;
            PathDistance += Mathf.Max(0f, meters);
            _verticalVel = 0f;
            _grounded = true;
            _airHopUsed = false;
            IsSliding = false;
            _slideTimer = 0f;
            var p = transform.position;
            p.y = 0f;
            transform.position = p;
            RestoreCollider();
            ApplyPathPose();
        }

        void ClearTraversal()
        {
            if (_riverMarker != null) _riverMarker.SetOccupied(false);
            if (_fireMarker != null) _fireMarker.SetOccupied(false);
            if (_specialMarker != null) _specialMarker.SetOccupied(false);
            Traversal = TraversalMode.None;
            _riverMarker = null;
            _boatRide = null;
            _ropeSwing = null;
            _ropeT = 0f;
            _ropeReleased = false;
            _fireMarker = null;
            _vineSwing = null;
            _dunkSafeTimer = 0f;
            _specialMarker = null;
            _mineCartRide = null;
            _iceBoardRide = null;
            _waterSlideRide = null;
            _ziplineHeight = 0f;
            _swimDepth = 0f;
            _wallRunHeight = 0f;
            _wallRunOffset = 0f;
            _wallSide = -1;
            _ledgeHeight = 0f;
            _canopyRopeHeight = 0f;
            _waterfallDiveHeight = 0f;
            _waterfallPoolDepth = 0f;
            if (_dunkSteam != null)
            {
                Destroy(_dunkSteam.gameObject);
                _dunkSteam = null;
            }
            if (_swimBubbles != null)
            {
                Destroy(_swimBubbles.gameObject);
                _swimBubbles = null;
            }
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
        }

        public void BeginBoatRide(RiverCrossingMarker marker, RiverBoatRide ride)
        {
            if (marker == null || ride == null || Traversal != TraversalMode.None) return;
            Traversal = TraversalMode.Boat;
            _riverMarker = marker;
            _boatRide = ride;
            marker.SetOccupied(true);
            Lane = 1;
            _laneOffset = 0f;
            _grounded = true;
            _verticalVel = 0f;
            IsSliding = false;
            RestoreCollider();
            AudioHooks.Instance?.PlayBoatMount();
            ChaseCamera.Instance?.SetTraversalBias(0.35f, 4f);
        }

        public void BeginRopeSwing(RiverCrossingMarker marker, RiverRopeSwing rope)
        {
            if (marker == null || rope == null || Traversal != TraversalMode.None) return;
            Traversal = TraversalMode.Rope;
            _riverMarker = marker;
            _ropeSwing = rope;
            marker.SetOccupied(true);
            _ropeT = 0f;
            _ropeReleased = false;
            Lane = 1;
            _laneOffset = 0f;
            _grounded = false;
            _verticalVel = 0f;
            IsSliding = false;
            RestoreCollider();
            AudioHooks.Instance?.PlayRopeGrab();
            ChaseCamera.Instance?.SetTraversalBias(0.55f, 6f);
        }

        public void BeginVineSwing(FireCrossingMarker marker, FireVineSwing vine)
        {
            if (marker == null || vine == null || Traversal != TraversalMode.None) return;
            Traversal = TraversalMode.Vine;
            _fireMarker = marker;
            _vineSwing = vine;
            marker.SetOccupied(true);
            _ropeT = 0f;
            _ropeReleased = false;
            Lane = 1;
            _laneOffset = 0f;
            _grounded = false;
            _verticalVel = 0f;
            IsSliding = false;
            RestoreCollider();
            AudioHooks.Instance?.PlayRopeGrab();
            ChaseCamera.Instance?.SetTraversalBias(0.55f, 6f);
        }

        public void BeginWaterDunk(FireCrossingMarker marker, float safeDuration)
        {
            if (marker == null || Traversal != TraversalMode.None) return;
            Traversal = TraversalMode.WaterDunk;
            _fireMarker = marker;
            marker.SetOccupied(true);
            _dunkSafeTimer = Mathf.Max(0.8f, safeDuration);
            _grounded = true;
            _verticalVel = 0f;
            IsSliding = false;
            RestoreCollider();
            EnsureDunkSteam();
            AudioHooks.Instance?.PlaySplash();
            ChaseCamera.Instance?.SetTraversalBias(0.25f, 3f);
        }

        public void BeginZipline(SpecialStageMarker marker, float rideHeight)
        {
            if (marker == null || Traversal != TraversalMode.None) return;
            Traversal = TraversalMode.Zipline;
            _specialMarker = marker;
            marker.SetOccupied(true);
            _ziplineHeight = rideHeight;
            Lane = 1;
            _laneOffset = 0f;
            _grounded = false;
            _verticalVel = 0f;
            IsSliding = false;
            RestoreCollider();
            AudioHooks.Instance?.PlayRopeGrab();
            ChaseCamera.Instance?.SetTraversalBias(0.65f, 7f);
        }

        public void BeginMineCart(SpecialStageMarker marker, MineCartRide ride)
        {
            if (marker == null || ride == null || Traversal != TraversalMode.None) return;
            Traversal = TraversalMode.MineCart;
            _specialMarker = marker;
            _mineCartRide = ride;
            marker.SetOccupied(true);
            Lane = 1;
            _laneOffset = 0f;
            _grounded = true;
            _verticalVel = 0f;
            IsSliding = false;
            RestoreCollider();
            AudioHooks.Instance?.PlayBoatMount();
            ChaseCamera.Instance?.SetTraversalBias(0.4f, 5f);
        }

        public void BeginSwim(RiverCrossingMarker marker, float swimDepth)
        {
            if (marker == null || Traversal != TraversalMode.None) return;
            Traversal = TraversalMode.Swim;
            _riverMarker = marker;
            marker.SetOccupied(true);
            _swimDepth = swimDepth;
            _grounded = false;
            _verticalVel = 0f;
            IsSliding = false;
            RestoreCollider();
            EnsureSwimBubbles();
            AudioHooks.Instance?.PlaySplash();
            ChaseCamera.Instance?.SetTraversalBias(0.35f, 4f);
        }

        public void BeginIceSurf(SpecialStageMarker marker, IceBoardRide ride)
        {
            if (marker == null || ride == null || Traversal != TraversalMode.None) return;
            Traversal = TraversalMode.IceSurf;
            _specialMarker = marker;
            _iceBoardRide = ride;
            marker.SetOccupied(true);
            Lane = 1;
            _laneOffset = 0f;
            _grounded = true;
            _verticalVel = 0f;
            IsSliding = false;
            RestoreCollider();
            AudioHooks.Instance?.PlayBoatMount();
            ChaseCamera.Instance?.SetTraversalBias(0.5f, 6f);
        }

        public void BeginWallRun(SpecialStageMarker marker, float wallHeight, float wallOffset)
        {
            if (marker == null || Traversal != TraversalMode.None) return;
            Traversal = TraversalMode.WallRun;
            _specialMarker = marker;
            marker.SetOccupied(true);
            _wallRunHeight = wallHeight;
            _wallRunOffset = wallOffset;
            _wallSide = Lane <= 1 ? -1 : 1;
            Lane = _wallSide < 0 ? 0 : 2;
            _laneOffset = _wallSide * _wallRunOffset;
            _grounded = false;
            _verticalVel = 0f;
            IsSliding = false;
            RestoreCollider();
            AudioHooks.Instance?.PlayJump();
            ChaseCamera.Instance?.SetTraversalBias(0.45f, 5f);
        }

        public void BeginLedgeGrab(SpecialStageMarker marker, float ledgeHeight)
        {
            if (marker == null || Traversal != TraversalMode.None) return;
            Traversal = TraversalMode.LedgeGrab;
            _specialMarker = marker;
            marker.SetOccupied(true);
            _ledgeHeight = ledgeHeight;
            Lane = 1;
            _laneOffset = 0f;
            _grounded = false;
            _verticalVel = 0f;
            IsSliding = false;
            RestoreCollider();
            AudioHooks.Instance?.PlayRopeGrab();
            ChaseCamera.Instance?.SetTraversalBias(0.4f, 4.5f);
        }

        public void BeginCanopyRope(SpecialStageMarker marker, float rideHeight)
        {
            if (marker == null || Traversal != TraversalMode.None) return;
            Traversal = TraversalMode.CanopyRope;
            _specialMarker = marker;
            marker.SetOccupied(true);
            _canopyRopeHeight = rideHeight;
            Lane = 1;
            _laneOffset = 0f;
            _grounded = false;
            _verticalVel = 0f;
            IsSliding = false;
            RestoreCollider();
            AudioHooks.Instance?.PlayRopeGrab();
            ChaseCamera.Instance?.SetTraversalBias(0.55f, 6f);
        }

        public void BeginWaterfallPlunge(SpecialStageMarker marker, float diveHeight, float poolDepth)
        {
            if (marker == null || Traversal != TraversalMode.None) return;
            Traversal = TraversalMode.WaterfallPlunge;
            _specialMarker = marker;
            marker.SetOccupied(true);
            _waterfallDiveHeight = diveHeight;
            _waterfallPoolDepth = poolDepth;
            Lane = 1;
            _laneOffset = 0f;
            _grounded = false;
            _verticalVel = 0f;
            IsSliding = false;
            RestoreCollider();
            EnsureSwimBubbles();
            AudioHooks.Instance?.PlaySplash();
            ChaseCamera.Instance?.SetTraversalBias(0.7f, 8f);
        }

        public void BeginWaterSlide(SpecialStageMarker marker, WaterSlideRide ride)
        {
            if (marker == null || ride == null || Traversal != TraversalMode.None) return;
            Traversal = TraversalMode.WaterSlide;
            _specialMarker = marker;
            _waterSlideRide = ride;
            marker.SetOccupied(true);
            Lane = 1;
            _laneOffset = 0f;
            _grounded = true;
            _verticalVel = 0f;
            IsSliding = false;
            RestoreCollider();
            AudioHooks.Instance?.PlaySplash();
            ChaseCamera.Instance?.SetTraversalBias(0.55f, 6.5f);
        }

        void EnsureSwimBubbles()
        {
            if (_swimBubbles != null) return;
            var go = new GameObject("SwimBubbles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            _swimBubbles = go.transform;
            for (int i = 0; i < 4; i++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.transform.SetParent(_swimBubbles, false);
                puff.transform.localPosition = new Vector3(
                    Random.Range(-0.3f, 0.3f), 0.2f + i * 0.25f, Random.Range(-0.15f, 0.15f));
                float s = Random.Range(0.2f, 0.4f);
                puff.transform.localScale = new Vector3(s, s, s);
                puff.GetComponent<Renderer>().sharedMaterial = JunglePalette.Foam;
                Object.Destroy(puff.GetComponent<Collider>());
            }
        }

        void EnsureDunkSteam()
        {
            if (_dunkSteam != null) return;
            var go = new GameObject("DunkSteam");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            _dunkSteam = go.transform;
            for (int i = 0; i < 3; i++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.transform.SetParent(_dunkSteam, false);
                puff.transform.localPosition = new Vector3(
                    Random.Range(-0.35f, 0.35f), 0.4f + i * 0.35f, Random.Range(-0.2f, 0.2f));
                float s = Random.Range(0.45f, 0.8f);
                puff.transform.localScale = new Vector3(s, s * 0.7f, s);
                puff.GetComponent<Renderer>().sharedMaterial = JunglePalette.Foam;
                Object.Destroy(puff.GetComponent<Collider>());
            }
        }

        /// <summary>Drop the runner out of the world after a gap death so the fall is visible.</summary>
        public void BeginDeathFall()
        {
            if (_deathFalling) return;
            ClearTraversal();
            _deathFalling = true;
            _deathFallVel = 1.5f;
            _explorer?.SetPoseFlags(false, true);
            _explorer?.SetOutcomePose(RunnerOutcomePose.FallDeath);
        }

        /// <summary>Impact / catch death pose before EndRun tears down the session.</summary>
        public void BeginOutcomeDeath(RunnerOutcomePose pose)
        {
            _explorer?.SetOutcomePose(pose);
        }

        public void ClearOutcomePose() => _explorer?.ClearOutcomePose();

        void Update()
        {
            if (!_deathFalling) return;
            _deathFallVel -= gravity * Time.deltaTime;
            var p = transform.position;
            p.y += _deathFallVel * Time.deltaTime;
            transform.position = p;
            if (p.y < -14f) _deathFalling = false;
        }

        void HandleSwipe(SwipeDirection dir)
        {
            if (RunSession.Instance == null || !RunSession.Instance.IsAlive) return;

            if (GuardianAI.Instance != null && GuardianAI.Instance.TryStruggleInput(dir))
                return;

            if (Traversal == TraversalMode.Rope || Traversal == TraversalMode.Vine)
            {
                if (dir == SwipeDirection.Up || dir == SwipeDirection.Down)
                    TryRopeRelease();
                return;
            }

            if (Traversal == TraversalMode.Boat || Traversal == TraversalMode.MineCart
                || Traversal == TraversalMode.IceSurf || Traversal == TraversalMode.WaterSlide)
            {
                if (dir == SwipeDirection.Left) Lane = Mathf.Max(0, Lane - 1);
                else if (dir == SwipeDirection.Right) Lane = Mathf.Min(LaneCount - 1, Lane + 1);
                else if ((Traversal == TraversalMode.MineCart || Traversal == TraversalMode.IceSurf
                          || Traversal == TraversalMode.WaterSlide)
                         && dir == SwipeDirection.Down)
                    TrySlide();
                else if (Traversal == TraversalMode.WaterSlide && dir == SwipeDirection.Up)
                    TryJump();
                return;
            }

            if (Traversal == TraversalMode.Zipline || Traversal == TraversalMode.CanopyRope)
            {
                if (dir == SwipeDirection.Left) Lane = Mathf.Max(0, Lane - 1);
                else if (dir == SwipeDirection.Right) Lane = Mathf.Min(LaneCount - 1, Lane + 1);
                else if (dir == SwipeDirection.Down) TrySlide();
                return;
            }

            if (Traversal == TraversalMode.WaterfallPlunge)
            {
                if (dir == SwipeDirection.Left) Lane = Mathf.Max(0, Lane - 1);
                else if (dir == SwipeDirection.Right) Lane = Mathf.Min(LaneCount - 1, Lane + 1);
                else if (dir == SwipeDirection.Up) TryJump();
                else if (dir == SwipeDirection.Down) TrySlide();
                return;
            }

            if (Traversal == TraversalMode.WallRun)
            {
                if (dir == SwipeDirection.Left)
                {
                    _wallSide = -1;
                    Lane = 0;
                }
                else if (dir == SwipeDirection.Right)
                {
                    _wallSide = 1;
                    Lane = 2;
                }
                else if (dir == SwipeDirection.Down) TrySlide();
                else if (dir == SwipeDirection.Up) TryJump();
                return;
            }

            if (Traversal == TraversalMode.LedgeGrab)
            {
                if (dir == SwipeDirection.Left) Lane = Mathf.Max(0, Lane - 1);
                else if (dir == SwipeDirection.Right) Lane = Mathf.Min(LaneCount - 1, Lane + 1);
                else if (dir == SwipeDirection.Up)
                {
                    // Vault early onto the far bank if past mid-channel.
                    if (_specialMarker != null)
                    {
                        float local = PathDistance - _specialMarker.PathStartDistance;
                        float mid = (_specialMarker.ChannelStart + _specialMarker.ChannelEnd) * 0.5f;
                        if (local >= mid) EndLedgeGrab(true);
                    }
                }
                return;
            }

            if (Traversal == TraversalMode.WaterDunk || Traversal == TraversalMode.Swim)
            {
                if (dir == SwipeDirection.Left) Lane = Mathf.Max(0, Lane - 1);
                else if (dir == SwipeDirection.Right) Lane = Mathf.Min(LaneCount - 1, Lane + 1);
                else if (dir == SwipeDirection.Up && Traversal == TraversalMode.WaterDunk) TryJump();
                else if (dir == SwipeDirection.Down && Traversal == TraversalMode.Swim) TrySlide();
                return;
            }

            // At a T-junction, Left/Right chooses the path instead of changing lanes.
            if ((dir == SwipeDirection.Left || dir == SwipeDirection.Right) && IsInJunctionDecisionWindow())
            {
                bool left = dir == SwipeDirection.Left;
                if (TileSpawner.Instance != null && TileSpawner.Instance.TryChooseJunction(left))
                {
                    AudioHooks.Instance?.PlayJump(); // short confirm feedback
                    return;
                }
            }

            switch (dir)
            {
                case SwipeDirection.Left:
                    Lane = Mathf.Max(0, Lane - 1);
                    break;
                case SwipeDirection.Right:
                    Lane = Mathf.Min(LaneCount - 1, Lane + 1);
                    break;
                case SwipeDirection.Up:
                    TryJump();
                    break;
                case SwipeDirection.Down:
                    TrySlide();
                    break;
            }
        }

        bool IsInJunctionDecisionWindow()
        {
            var spawner = TileSpawner.Instance;
            if (spawner == null || !spawner.AwaitingJunctionChoice || spawner.PendingJunction == null)
                return false;
            var j = spawner.PendingJunction;
            float local = PathDistance - j.PathStartDistance;
            // Open the choice once the player is well onto the approach
            return local >= TrackTile.JunctionApproach * 0.25f;
        }

        void HandleTap()
        {
            if (RunSession.Instance == null || !RunSession.Instance.IsAlive) return;
            if (GuardianAI.Instance != null && GuardianAI.Instance.TryStruggleTap())
                return;
            PowerUpController.Instance?.UseEquipped();
        }

        /// <summary>Canopy Ace skill — restore a free mid-air hop.</summary>
        public void GrantAirHop()
        {
            _airHopUsed = false;
            if (!_grounded)
            {
                _verticalVel = Mathf.Max(_verticalVel, jumpVelocity * 0.72f);
                AudioHooks.Instance?.PlayJump();
                _explorer?.TriggerJump();
                ChaseCamera.Instance?.PunchFov(1.8f);
            }
            else
                TryJump();
        }

        void TryJump()
        {
            if (Traversal == TraversalMode.WallRun)
            {
                // Hop to the opposite wall face.
                _wallSide = -_wallSide;
                Lane = _wallSide < 0 ? 0 : 2;
                AudioHooks.Instance?.PlayJump();
                _explorer?.TriggerJump();
                return;
            }
            if (Traversal != TraversalMode.None && Traversal != TraversalMode.WaterDunk
                && Traversal != TraversalMode.Swim && Traversal != TraversalMode.WaterfallPlunge)
                return;
            if (Traversal == TraversalMode.Swim) return;
            if (IsSliding) return;

            // Mid-air hop for gap recovery (genre staple).
            if (!_grounded)
            {
                if (_airHopUsed || Traversal == TraversalMode.WaterDunk) return;
                _airHopUsed = true;
                _verticalVel = jumpVelocity * 0.72f;
                AudioHooks.Instance?.PlayJump();
                _explorer?.TriggerJump();
                ChaseCamera.Instance?.PunchFov(1.6f);
                return;
            }

            _grounded = false;
            _airHopUsed = false;
            _verticalVel = jumpVelocity;
            AudioHooks.Instance?.PlayJump();
            _explorer?.TriggerJump();
        }

        void TrySlide()
        {
            if (Traversal != TraversalMode.None
                && Traversal != TraversalMode.Zipline
                && Traversal != TraversalMode.CanopyRope
                && Traversal != TraversalMode.MineCart
                && Traversal != TraversalMode.IceSurf
                && Traversal != TraversalMode.WaterSlide
                && Traversal != TraversalMode.Swim
                && Traversal != TraversalMode.WaterfallPlunge
                && Traversal != TraversalMode.WallRun) return;
            if (!_grounded && Traversal != TraversalMode.Zipline && Traversal != TraversalMode.CanopyRope
                && Traversal != TraversalMode.Swim && Traversal != TraversalMode.WaterfallPlunge
                && Traversal != TraversalMode.WallRun) return;
            IsSliding = true;
            _slideTimer = slideDuration;
            _col.height = _baseColHeight * 0.45f;
            _col.center = new Vector3(0f, _col.height * 0.5f, 0f);
            _explorer?.SetPoseFlags(true, false);
            _explorer?.SetOutcomePose(RunnerOutcomePose.Slide);
            ApplySlideVisual(true);
            AudioHooks.Instance?.PlaySlide();
        }

        void ApplySlideVisual(bool sliding)
        {
            if (Visual == null) return;
            if (sliding)
            {
                // Distinct dive-crouch (animator slide still reuses jump clip).
                Visual.localPosition = new Vector3(0f, -0.42f, 0.35f);
                Visual.localRotation = Quaternion.Euler(62f, 0f, 0f);
            }
            else if (Traversal != TraversalMode.WallRun && Traversal != TraversalMode.LedgeGrab)
            {
                Visual.localPosition = Vector3.zero;
                Visual.localRotation = Quaternion.identity;
            }
        }

        void TryRopeRelease()
        {
            if (Traversal == TraversalMode.Vine)
            {
                TryVineRelease();
                return;
            }

            if (Traversal != TraversalMode.Rope || _ropeSwing == null || _ropeReleased) return;
            _ropeReleased = true;
            AudioHooks.Instance?.PlayRopeRelease();

            if (_ropeSwing.InReleaseWindow(_ropeT))
            {
                CompleteRopeLanding(true);
            }
            else
            {
                FailRopeSwing();
            }
        }

        void TryVineRelease()
        {
            if (Traversal != TraversalMode.Vine || _vineSwing == null || _ropeReleased) return;
            _ropeReleased = true;
            AudioHooks.Instance?.PlayRopeRelease();

            if (_vineSwing.InReleaseWindow(_ropeT))
                CompleteVineLanding(true);
            else
                FailVineSwing();
        }

        void CompleteRopeLanding(bool fromRelease)
        {
            if (_riverMarker != null)
            {
                // Snap path to far bank so the runner continues cleanly.
                PathDistance = _riverMarker.PathStartDistance + _riverMarker.ChannelEnd + 0.2f;
                _riverMarker.MarkCompleted();
            }
            Traversal = TraversalMode.None;
            _ropeSwing = null;
            _riverMarker = null;
            _ropeT = 0f;
            _ropeReleased = false;
            _grounded = true;
            _verticalVel = 0f;
            var p = transform.position;
            p.y = 0f;
            transform.position = p;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            ApplyPathPose();
        }

        void FailRopeSwing()
        {
            if (_riverMarker != null) _riverMarker.SetOccupied(false);
            Traversal = TraversalMode.None;
            _ropeSwing = null;
            _riverMarker = null;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            BeginDeathFall();
            RunSession.Instance?.EndRun("Missed the rope landing");
        }

        void CompleteVineLanding(bool fromRelease)
        {
            if (_fireMarker != null)
            {
                PathDistance = _fireMarker.PathStartDistance + _fireMarker.ChannelEnd + 0.2f;
                _fireMarker.MarkCompleted();
            }
            Traversal = TraversalMode.None;
            _vineSwing = null;
            _fireMarker = null;
            _ropeT = 0f;
            _ropeReleased = false;
            _grounded = true;
            _verticalVel = 0f;
            var p = transform.position;
            p.y = 0f;
            transform.position = p;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            ApplyPathPose();
        }

        void FailVineSwing()
        {
            if (_fireMarker != null) _fireMarker.SetOccupied(false);
            Traversal = TraversalMode.None;
            _vineSwing = null;
            _fireMarker = null;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            BeginDeathFall();
            RunSession.Instance?.EndRun("Burned — missed the vine landing");
        }

        void RestoreCollider()
        {
            _col.height = _baseColHeight;
            _col.center = _baseColCenter;
            _explorer?.SetPoseFlags(false, !_grounded);
        }

        public void RegisterStumble(float duration = 0.85f) => _stumbleTimer = duration;
        public void ClearStumble() => _stumbleTimer = 0f;

        public void TickMovement(float speed)
        {
            if (RunSession.Instance == null || !RunSession.Instance.IsAlive) return;

            float powerScale = PowerUpController.Instance != null ? PowerUpController.Instance.SpeedScale : 1f;
            float rainFactor = EnvironmentEffects.Instance != null && EnvironmentEffects.Instance.RainActive ? 0.88f : 1f;
            float effectiveSpeed = (IsStumbling ? speed * 0.55f : speed) * powerScale * rainFactor;
            if (Traversal == TraversalMode.Boat) effectiveSpeed *= 1.05f;
            if (Traversal == TraversalMode.Zipline) effectiveSpeed *= 1.2f;
            if (Traversal == TraversalMode.CanopyRope) effectiveSpeed *= 1.15f;
            if (Traversal == TraversalMode.MineCart) effectiveSpeed *= 1.15f;
            if (Traversal == TraversalMode.IceSurf) effectiveSpeed *= 1.28f;
            if (Traversal == TraversalMode.WaterSlide) effectiveSpeed *= 1.32f;
            if (Traversal == TraversalMode.Swim) effectiveSpeed *= 0.88f;
            if (Traversal == TraversalMode.WaterfallPlunge) effectiveSpeed *= 1.05f;
            if (Traversal == TraversalMode.WallRun) effectiveSpeed *= 1.12f;
            if (Traversal == TraversalMode.LedgeGrab) effectiveSpeed *= 0.82f;
            float dz = effectiveSpeed * Time.deltaTime;

            float targetLane = (Lane - 1) * LaneWidth;
            if (Traversal == TraversalMode.WallRun)
            {
                float wallTarget = _wallSide * _wallRunOffset;
                _laneOffset = Mathf.Lerp(_laneOffset, wallTarget, 1f - Mathf.Exp(-16f * Time.deltaTime));
            }
            else if (Traversal != TraversalMode.Rope && Traversal != TraversalMode.Vine)
            {
                if (SwipeInput.Instance != null && SwipeInput.Instance.TiltEnabled && Traversal == TraversalMode.None)
                {
                    float tilt = Input.acceleration.x;
                    targetLane += Mathf.Clamp(tilt * 1.2f, -0.7f, 0.7f);
                }
                if (EnvironmentEffects.Instance != null && EnvironmentEffects.Instance.WindActive && Traversal == TraversalMode.None)
                    targetLane += EnvironmentEffects.Instance.WindDrift * 0.15f;

                float lerp = laneLerp * (EnvironmentEffects.Instance != null && EnvironmentEffects.Instance.RainActive ? 0.75f : 1f);
                _laneOffset = Mathf.Lerp(_laneOffset, targetLane, 1f - Mathf.Exp(-lerp * Time.deltaTime));
            }
            else
            {
                _laneOffset = Mathf.Lerp(_laneOffset, 0f, 1f - Mathf.Exp(-14f * Time.deltaTime));
            }

            PathDistance += dz;

            if (Traversal == TraversalMode.Rope)
                TickRopeSwing(dz);
            else if (Traversal == TraversalMode.Vine)
                TickVineSwing(dz);
            else
                ApplyPathPose();

            if (Traversal == TraversalMode.Boat)
            {
                _boatRide?.SyncToPlayer(this);
                var p = transform.position;
                p.y = _boatRide != null ? _boatRide.DeckHeight : 0.35f;
                transform.position = p;
                _grounded = true;
                if (_boatRide != null && _boatRide.ReachedFarBank(this))
                    EndBoatRide();
            }
            else if (Traversal == TraversalMode.MineCart)
            {
                _mineCartRide?.SyncToPlayer(this);
                var p = transform.position;
                p.y = _mineCartRide != null ? _mineCartRide.DeckHeight : 0.55f;
                transform.position = p;
                _grounded = true;
                if (_mineCartRide != null && _mineCartRide.ReachedFarBank(this))
                    EndMineCartRide();
            }
            else if (Traversal == TraversalMode.IceSurf)
            {
                _iceBoardRide?.SyncToPlayer(this);
                var p = transform.position;
                p.y = _iceBoardRide != null ? _iceBoardRide.DeckHeight : 0.28f;
                transform.position = p;
                _grounded = true;
                if (_iceBoardRide != null && _iceBoardRide.ReachedFarBank(this))
                    EndIceSurf();
            }
            else if (Traversal == TraversalMode.WaterSlide)
            {
                _waterSlideRide?.SyncToPlayer(this);
                var p = transform.position;
                p.y = _waterSlideRide != null ? _waterSlideRide.DeckHeight : 0.2f;
                // Brief hop support while sliding the aqueduct.
                if (!_grounded)
                {
                    _verticalVel -= gravity * Time.deltaTime;
                    p.y = Mathf.Max(_waterSlideRide != null ? _waterSlideRide.DeckHeight : 0.2f,
                        transform.position.y + _verticalVel * Time.deltaTime);
                    if (p.y <= (_waterSlideRide != null ? _waterSlideRide.DeckHeight : 0.2f) + 0.01f)
                    {
                        p.y = _waterSlideRide != null ? _waterSlideRide.DeckHeight : 0.2f;
                        _verticalVel = 0f;
                        _grounded = true;
                    }
                }
                else
                    _grounded = true;
                transform.position = p;
                if (_waterSlideRide != null && _waterSlideRide.ReachedFarBank(this))
                    EndWaterSlide();
            }
            else if (Traversal == TraversalMode.Zipline)
            {
                TickZipline();
            }
            else if (Traversal == TraversalMode.CanopyRope)
            {
                TickCanopyRope();
            }
            else if (Traversal == TraversalMode.Swim)
            {
                TickSwim();
            }
            else if (Traversal == TraversalMode.WaterfallPlunge)
            {
                TickWaterfallPlunge();
            }
            else if (Traversal == TraversalMode.WallRun)
            {
                TickWallRun();
            }
            else if (Traversal == TraversalMode.LedgeGrab)
            {
                TickLedgeGrab();
            }
            else if (Traversal == TraversalMode.WaterDunk)
            {
                if (!_grounded)
                {
                    _verticalVel -= gravity * Time.deltaTime;
                    var p = transform.position;
                    p.y += _verticalVel * Time.deltaTime;
                    if (p.y <= 0f)
                    {
                        p.y = 0f;
                        _verticalVel = 0f;
                        _grounded = true;
                    }
                    transform.position = p;
                }
                TickWaterDunk();
            }
            else if (Traversal == TraversalMode.None && !_grounded)
            {
                _verticalVel -= gravity * Time.deltaTime;
                var p = transform.position;
                p.y += _verticalVel * Time.deltaTime;
                if (p.y <= 0f)
                {
                    p.y = 0f;
                    _verticalVel = 0f;
                    _grounded = true;
                    _airHopUsed = false;
                }
                transform.position = p;
            }
            else if (Traversal == TraversalMode.None && _grounded)
            {
                _airHopUsed = false;
            }

            if (IsSliding)
            {
                _slideTimer -= Time.deltaTime;
                ApplySlideVisual(true);
                if (_slideTimer <= 0f)
                {
                    IsSliding = false;
                    RestoreCollider();
                    ApplySlideVisual(false);
                    if (_explorer != null && _explorer.CurrentOutcomePose == RunnerOutcomePose.Slide)
                        _explorer.ClearOutcomePose();
                }
            }

            if (_stumbleTimer > 0f)
                _stumbleTimer -= Time.deltaTime;

            RunSession.Instance.Tick(dz, effectiveSpeed);
            EnvironmentEffects.Instance?.Tick(RunSession.Instance.Distance);
            if (_lantern != null)
            {
                float blend = EnvironmentEffects.Instance != null ? EnvironmentEffects.Instance.DarknessBlend : 0f;
                _lantern.intensity = Mathf.Lerp(_lantern.intensity, blend * 2.2f, 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
            _explorer?.SetPoseFlags(IsSliding, !_grounded);
            _explorer?.SetLaneLean(_laneOffset / LaneWidth);

            if (transform.position.y < -5f)
                RunSession.Instance.EndRun("Fell into the abyss");
        }

        void TickRopeSwing(float dz)
        {
            if (_ropeSwing == null || _riverMarker == null)
            {
                ClearTraversal();
                ApplyPathPose();
                return;
            }

            float duration = Mathf.Max(0.7f, _ropeSwing.SwingDuration);
            _ropeT = Mathf.Clamp01(_ropeT + Time.deltaTime / duration);

            // Easy auto-releases in the safe window if the player never swipes.
            if (!_ropeReleased
                && _riverMarker.Difficulty == RunDifficulty.Easy
                && _ropeT >= (_ropeSwing.ReleaseWindowStart + _ropeSwing.ReleaseWindowEnd) * 0.5f)
            {
                _ropeReleased = true;
                AudioHooks.Instance?.PlayRopeRelease();
                CompleteRopeLanding(true);
                return;
            }

            if (!_ropeReleased && _ropeT >= 1f)
            {
                // Past the swing with no release — fail on Medium/Hard, soft land on Easy.
                if (_riverMarker.Difficulty == RunDifficulty.Easy)
                    CompleteRopeLanding(false);
                else
                    FailRopeSwing();
                return;
            }

            float localZ = Mathf.Lerp(_riverMarker.ChannelStart, _riverMarker.ChannelEnd, _ropeT);
            PathDistance = _riverMarker.PathStartDistance + localZ;
            ApplyPathPose();

            float y = Mathf.Sin(_ropeT * Mathf.PI) * _ropeSwing.ApexHeight;
            var pos = transform.position;
            pos.y = y;
            transform.position = pos;
            _ropeSwing.UpdateRopeVisual(_ropeT, pos);
        }

        void TickVineSwing(float dz)
        {
            if (_vineSwing == null || _fireMarker == null)
            {
                ClearTraversal();
                ApplyPathPose();
                return;
            }

            float duration = Mathf.Max(0.7f, _vineSwing.SwingDuration);
            _ropeT = Mathf.Clamp01(_ropeT + Time.deltaTime / duration);

            if (!_ropeReleased
                && _fireMarker.Difficulty == RunDifficulty.Easy
                && _ropeT >= (_vineSwing.ReleaseWindowStart + _vineSwing.ReleaseWindowEnd) * 0.5f)
            {
                _ropeReleased = true;
                AudioHooks.Instance?.PlayRopeRelease();
                CompleteVineLanding(true);
                return;
            }

            if (!_ropeReleased && _ropeT >= 1f)
            {
                if (_fireMarker.Difficulty == RunDifficulty.Easy)
                    CompleteVineLanding(false);
                else
                    FailVineSwing();
                return;
            }

            float localZ = Mathf.Lerp(_fireMarker.ChannelStart, _fireMarker.ChannelEnd, _ropeT);
            PathDistance = _fireMarker.PathStartDistance + localZ;
            ApplyPathPose();

            float y = Mathf.Sin(_ropeT * Mathf.PI) * _vineSwing.ApexHeight;
            var pos = transform.position;
            pos.y = y;
            transform.position = pos;
            _vineSwing.UpdateVineVisual(_ropeT, pos);
        }

        void TickWaterDunk()
        {
            _dunkSafeTimer -= Time.deltaTime;
            if (_dunkSteam != null)
            {
                float pulse = 0.85f + Mathf.Sin(Time.time * 10f) * 0.15f;
                _dunkSteam.localScale = Vector3.one * pulse;
            }

            if (_fireMarker != null)
            {
                float local = PathDistance - _fireMarker.PathStartDistance;
                if (local >= _fireMarker.ChannelEnd - 0.2f)
                {
                    EndWaterDunk(true);
                    return;
                }
            }

            // Safe window expired while still over fire — burn.
            if (_dunkSafeTimer <= 0f)
            {
                if (_fireMarker != null) _fireMarker.SetOccupied(false);
                Traversal = TraversalMode.None;
                _fireMarker = null;
                if (_dunkSteam != null)
                {
                    Destroy(_dunkSteam.gameObject);
                    _dunkSteam = null;
                }
                ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
                BeginDeathFall();
                RunSession.Instance?.EndRun("Burned in the fire");
            }
        }

        void EndWaterDunk(bool success)
        {
            if (_fireMarker != null)
            {
                if (success)
                {
                    PathDistance = Mathf.Max(PathDistance, _fireMarker.PathStartDistance + _fireMarker.ChannelEnd + 0.15f);
                    _fireMarker.MarkCompleted();
                }
                else
                    _fireMarker.SetOccupied(false);
            }
            Traversal = TraversalMode.None;
            _fireMarker = null;
            _dunkSafeTimer = 0f;
            if (_dunkSteam != null)
            {
                Destroy(_dunkSteam.gameObject);
                _dunkSteam = null;
            }
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            ApplyPathPose();
        }

        void EndBoatRide()
        {
            if (_riverMarker != null)
            {
                PathDistance = Mathf.Max(PathDistance, _riverMarker.PathStartDistance + _riverMarker.ChannelEnd + 0.15f);
                _riverMarker.MarkCompleted();
            }
            Traversal = TraversalMode.None;
            _boatRide = null;
            _riverMarker = null;
            _grounded = true;
            var p = transform.position;
            p.y = 0f;
            transform.position = p;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            ApplyPathPose();
        }

        void TickZipline()
        {
            if (_specialMarker == null)
            {
                ClearTraversal();
                ApplyPathPose();
                return;
            }

            float local = PathDistance - _specialMarker.PathStartDistance;
            if (local >= _specialMarker.ChannelEnd - 0.25f)
            {
                EndZipline(true);
                return;
            }

            var pos = transform.position;
            pos.y = _ziplineHeight;
            transform.position = pos;
            _grounded = false;
        }

        void EndZipline(bool success)
        {
            if (_specialMarker != null)
            {
                if (success)
                {
                    PathDistance = Mathf.Max(PathDistance, _specialMarker.PathStartDistance + _specialMarker.ChannelEnd + 0.2f);
                    _specialMarker.MarkCompleted();
                }
                else
                    _specialMarker.SetOccupied(false);
            }
            Traversal = TraversalMode.None;
            _specialMarker = null;
            _ziplineHeight = 0f;
            _grounded = true;
            _verticalVel = 0f;
            var p = transform.position;
            p.y = 0f;
            transform.position = p;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            ApplyPathPose();
        }

        void EndMineCartRide()
        {
            if (_specialMarker != null)
            {
                PathDistance = Mathf.Max(PathDistance, _specialMarker.PathStartDistance + _specialMarker.ChannelEnd + 0.15f);
                _specialMarker.MarkCompleted();
            }
            Traversal = TraversalMode.None;
            _mineCartRide = null;
            _specialMarker = null;
            _grounded = true;
            var p = transform.position;
            p.y = 0f;
            transform.position = p;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            ApplyPathPose();
        }

        void EndWaterSlide()
        {
            if (_specialMarker != null)
            {
                PathDistance = Mathf.Max(PathDistance, _specialMarker.PathStartDistance + _specialMarker.ChannelEnd + 0.15f);
                _specialMarker.MarkCompleted();
            }
            Traversal = TraversalMode.None;
            _waterSlideRide = null;
            _specialMarker = null;
            _grounded = true;
            _verticalVel = 0f;
            var p = transform.position;
            p.y = 0f;
            transform.position = p;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            ApplyPathPose();
        }

        void TickSwim()
        {
            if (_riverMarker == null)
            {
                ClearTraversal();
                ApplyPathPose();
                return;
            }

            float local = PathDistance - _riverMarker.PathStartDistance;
            if (local >= _riverMarker.ChannelEnd - 0.25f)
            {
                EndSwim(true);
                return;
            }

            var pos = transform.position;
            float bob = Mathf.Sin(Time.time * 7f) * 0.12f;
            pos.y = _swimDepth + bob;
            transform.position = pos;
            _grounded = false;
            if (_swimBubbles != null)
            {
                float pulse = 0.9f + Mathf.Sin(Time.time * 9f) * 0.15f;
                _swimBubbles.localScale = Vector3.one * pulse;
            }
        }

        void EndSwim(bool success)
        {
            if (_riverMarker != null)
            {
                if (success)
                {
                    PathDistance = Mathf.Max(PathDistance, _riverMarker.PathStartDistance + _riverMarker.ChannelEnd + 0.15f);
                    _riverMarker.MarkCompleted();
                }
                else
                    _riverMarker.SetOccupied(false);
            }
            Traversal = TraversalMode.None;
            _riverMarker = null;
            _swimDepth = 0f;
            if (_swimBubbles != null)
            {
                Destroy(_swimBubbles.gameObject);
                _swimBubbles = null;
            }
            _grounded = true;
            _verticalVel = 0f;
            var p = transform.position;
            p.y = 0f;
            transform.position = p;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            ApplyPathPose();
        }

        void EndIceSurf()
        {
            if (_specialMarker != null)
            {
                PathDistance = Mathf.Max(PathDistance, _specialMarker.PathStartDistance + _specialMarker.ChannelEnd + 0.15f);
                _specialMarker.MarkCompleted();
            }
            Traversal = TraversalMode.None;
            _iceBoardRide = null;
            _specialMarker = null;
            _grounded = true;
            var p = transform.position;
            p.y = 0f;
            transform.position = p;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            ApplyPathPose();
        }

        void TickWallRun()
        {
            if (_specialMarker == null)
            {
                ClearTraversal();
                ApplyPathPose();
                return;
            }

            float local = PathDistance - _specialMarker.PathStartDistance;
            if (local >= _specialMarker.ChannelEnd - 0.25f)
            {
                EndWallRun(true);
                return;
            }

            var pos = transform.position;
            float bob = Mathf.Sin(Time.time * 14f) * 0.06f;
            pos.y = _wallRunHeight + bob;
            transform.position = pos;
            _grounded = false;
            // Lean into the wall for silhouette readability.
            if (Visual != null)
            {
                float lean = _wallSide * -18f;
                Visual.localRotation = Quaternion.Euler(0f, 0f, lean);
            }
        }

        void EndWallRun(bool success)
        {
            if (_specialMarker != null)
            {
                if (success)
                {
                    PathDistance = Mathf.Max(PathDistance, _specialMarker.PathStartDistance + _specialMarker.ChannelEnd + 0.2f);
                    _specialMarker.MarkCompleted();
                }
                else
                    _specialMarker.SetOccupied(false);
            }
            Traversal = TraversalMode.None;
            _specialMarker = null;
            _wallRunHeight = 0f;
            _wallRunOffset = 0f;
            _wallSide = -1;
            _grounded = true;
            _verticalVel = 0f;
            if (Visual != null) Visual.localRotation = Quaternion.identity;
            var p = transform.position;
            p.y = 0f;
            transform.position = p;
            Lane = 1;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            ApplyPathPose();
        }

        void TickLedgeGrab()
        {
            if (_specialMarker == null)
            {
                ClearTraversal();
                ApplyPathPose();
                return;
            }

            float local = PathDistance - _specialMarker.PathStartDistance;
            if (local >= _specialMarker.ChannelEnd - 0.25f)
            {
                EndLedgeGrab(true);
                return;
            }

            var pos = transform.position;
            float sway = Mathf.Sin(Time.time * 5f) * 0.08f;
            pos.y = _ledgeHeight + sway;
            transform.position = pos;
            _grounded = false;
            if (Visual != null)
                Visual.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        }

        void EndLedgeGrab(bool success)
        {
            if (_specialMarker != null)
            {
                if (success)
                {
                    PathDistance = Mathf.Max(PathDistance, _specialMarker.PathStartDistance + _specialMarker.ChannelEnd + 0.2f);
                    _specialMarker.MarkCompleted();
                }
                else
                    _specialMarker.SetOccupied(false);
            }
            Traversal = TraversalMode.None;
            _specialMarker = null;
            _ledgeHeight = 0f;
            _grounded = true;
            _verticalVel = 0f;
            if (Visual != null) Visual.localRotation = Quaternion.identity;
            var p = transform.position;
            p.y = 0f;
            transform.position = p;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            ApplyPathPose();
        }

        void TickCanopyRope()
        {
            if (_specialMarker == null)
            {
                ClearTraversal();
                ApplyPathPose();
                return;
            }

            float local = PathDistance - _specialMarker.PathStartDistance;
            if (local >= _specialMarker.ChannelEnd - 0.25f)
            {
                EndCanopyRope(true);
                return;
            }

            var pos = transform.position;
            float sway = Mathf.Sin(Time.time * 8f) * 0.1f;
            pos.y = _canopyRopeHeight + sway;
            transform.position = pos;
            _grounded = false;
        }

        void EndCanopyRope(bool success)
        {
            if (_specialMarker != null)
            {
                if (success)
                {
                    PathDistance = Mathf.Max(PathDistance, _specialMarker.PathStartDistance + _specialMarker.ChannelEnd + 0.2f);
                    _specialMarker.MarkCompleted();
                }
                else
                    _specialMarker.SetOccupied(false);
            }
            Traversal = TraversalMode.None;
            _specialMarker = null;
            _canopyRopeHeight = 0f;
            _grounded = true;
            _verticalVel = 0f;
            var p = transform.position;
            p.y = 0f;
            transform.position = p;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            ApplyPathPose();
        }

        void TickWaterfallPlunge()
        {
            if (_specialMarker == null)
            {
                ClearTraversal();
                ApplyPathPose();
                return;
            }

            float local = PathDistance - _specialMarker.PathStartDistance;
            float start = _specialMarker.ChannelStart;
            float end = _specialMarker.ChannelEnd;
            float len = Mathf.Max(0.1f, end - start);
            float u = Mathf.Clamp01((local - start) / len);

            if (local >= end - 0.25f)
            {
                EndWaterfallPlunge(true);
                return;
            }

            // Dive from the lip, then settle into the plunge pool.
            float height;
            if (u < 0.35f)
            {
                float diveT = u / 0.35f;
                height = Mathf.Lerp(_waterfallDiveHeight, _waterfallPoolDepth, diveT * diveT);
            }
            else
            {
                float bob = Mathf.Sin(Time.time * 7f) * 0.1f;
                height = _waterfallPoolDepth + bob;
            }

            var pos = transform.position;
            pos.y = height;
            transform.position = pos;
            _grounded = false;
            if (_swimBubbles != null && u >= 0.3f)
            {
                float pulse = 0.9f + Mathf.Sin(Time.time * 10f) * 0.2f;
                _swimBubbles.localScale = Vector3.one * pulse;
            }
        }

        void EndWaterfallPlunge(bool success)
        {
            if (_specialMarker != null)
            {
                if (success)
                {
                    PathDistance = Mathf.Max(PathDistance, _specialMarker.PathStartDistance + _specialMarker.ChannelEnd + 0.15f);
                    _specialMarker.MarkCompleted();
                }
                else
                    _specialMarker.SetOccupied(false);
            }
            Traversal = TraversalMode.None;
            _specialMarker = null;
            _waterfallDiveHeight = 0f;
            _waterfallPoolDepth = 0f;
            if (_swimBubbles != null)
            {
                Destroy(_swimBubbles.gameObject);
                _swimBubbles = null;
            }
            _grounded = true;
            _verticalVel = 0f;
            var p = transform.position;
            p.y = 0f;
            transform.position = p;
            ChaseCamera.Instance?.SetTraversalBias(0f, 0f);
            ApplyPathPose();
        }

        float _pathBank;

        void ApplyPathPose()
        {
            PathPose pose = new PathPose(transform.position, FacingYaw, PathDistance);
            var tile = TileSpawner.Instance != null
                ? TileSpawner.Instance.FindTileAtPathDistance(PathDistance)
                : null;
            if (tile != null)
                pose = tile.SampleAtPathDistance(PathDistance);

            FacingYaw = pose.yaw;
            _pathBank = Mathf.Lerp(_pathBank, pose.bank, 1f - Mathf.Exp(-10f * Time.deltaTime));
            Vector3 lateral = pose.Right * _laneOffset;
            Vector3 pos = pose.position + lateral;
            pos.y = transform.position.y;
            // Bank into curved turns; slide crouch is applied on Visual separately.
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, FacingYaw, _pathBank));
        }

        void OnTriggerEnter(Collider other)
        {
            if (RunSession.Instance == null || !RunSession.Instance.IsAlive) return;

            if (other.TryGetComponent<Obstacle>(out var obstacle))
            {
                if (obstacle.RequiresSlide && IsSliding)
                {
                    RunSession.Instance.RegisterNearMiss();
                    ChaseCamera.Instance?.PunchFov(1.2f);
                    return;
                }
                if (obstacle.RequiresJump && IsJumping)
                {
                    RunSession.Instance.RegisterNearMiss();
                    ChaseCamera.Instance?.PunchFov(1.2f);
                    return;
                }
                if (PowerUpController.Instance != null && PowerUpController.Instance.TryAbsorbHit())
                {
                    AudioHooks.Instance?.PlayHit();
                    RunSession.Instance.RegisterNearMiss();
                    obstacle.Consume();
                    return;
                }

                if (obstacle.StumbleOnly)
                {
                    RegisterStumble();
                    AudioHooks.Instance?.PlayHit();
                    obstacle.Consume();
                    return;
                }

                AudioHooks.Instance?.PlayHit();
                ClearTraversal();
                BeginOutcomeDeath(RunnerOutcomePose.ImpactDeath);
                RunSession.Instance.EndRun(obstacle.DeathMessage);
                return;
            }

            if (other.TryGetComponent<CollectibleCoin>(out var coin))
            {
                coin.Collect();
                return;
            }

            if (other.TryGetComponent<PowerUpPickup>(out var power))
            {
                power.Collect();
                return;
            }

            if (other.TryGetComponent<GemPickup>(out var gem))
            {
                gem.Collect();
                return;
            }

            if (other.TryGetComponent<RelicPickup>(out var relic))
            {
                relic.Collect();
                return;
            }

            if (other.TryGetComponent<BreakableIdol>(out var idol))
            {
                idol.Smash();
                return;
            }

            if (other.TryGetComponent<GapKillZone>(out var killZone))
            {
                // Mounted boat / active rope / vine / dunk ignore channel kills.
                if (Traversal == TraversalMode.Boat
                    || Traversal == TraversalMode.Rope
                    || Traversal == TraversalMode.Vine
                    || Traversal == TraversalMode.WaterDunk
                    || Traversal == TraversalMode.Zipline
                    || Traversal == TraversalMode.CanopyRope
                    || Traversal == TraversalMode.MineCart
                    || Traversal == TraversalMode.Swim
                    || Traversal == TraversalMode.WaterfallPlunge
                    || Traversal == TraversalMode.IceSurf
                    || Traversal == TraversalMode.WaterSlide
                    || Traversal == TraversalMode.WallRun
                    || Traversal == TraversalMode.LedgeGrab)
                    return;
                if (PowerUpController.Instance != null && PowerUpController.Instance.TryAbsorbHit()) return;
                if (IsJumping && transform.position.y > 0.4f) return;
                BeginDeathFall();
                RunSession.Instance.EndRun(string.IsNullOrEmpty(killZone.DeathMessage)
                    ? "Fell through a gap"
                    : killZone.DeathMessage);
            }
        }

        public void ApplyCharacterColors()
        {
            if (_explorer == null) return;
            bool keepIdol = _explorer.CarryingStolenIdol;
            var c = CharacterRoster.GetSelected();
            _explorer.ApplyCharacterAppearance(c);
            _explorer.ApplyCharacterKit(c.id, c.color);
            _explorer.ApplyCosmetics();
            if (keepIdol) _explorer.EnsureHeldIdol();
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.MainMenu)
                _explorer.ApplyMenuShowcase();
        }

        /// <summary>Keep the stolen idol prop attached through the opening-to-run handoff.</summary>
        public void AttachStolenIdol(Transform idol) => _explorer?.AttachHeldIdol(idol);

        public void ClearStolenIdol() => _explorer?.ClearHeldIdol();

        void OnDestroy()
        {
            if (_inputBound && SwipeInput.Instance != null)
            {
                SwipeInput.Instance.OnSwipe -= HandleSwipe;
                SwipeInput.Instance.OnTap -= HandleTap;
            }
            if (Instance == this) Instance = null;
        }
    }

    public enum RunnerOutcomePose
    {
        None = 0,
        Slide = 1,
        ImpactDeath = 2,
        FallDeath = 3,
        Caught = 4,
        IdolReach = 5
    }

    /// <summary>Kenney CC0 cartoon humanoid — forced-visible materials + Animator Run/Jump/Slide.</summary>
    public class ExplorerRunnerVisual : MonoBehaviour
    {
        public Transform Root { get; private set; }
        public float BodyHeight { get; private set; }
        public int PartCount { get; private set; }
        public bool HasSkinnedMesh { get; private set; }
        public Animator Animator => _anim;

        Animator _anim;
        Transform _model;
        Transform _kitRoot;
        Transform _heldIdol;
        bool _carryStolenIdol;
        float _laneLean;
        string _kitId;
        string _skinKey;
        Material _runtimeSkin;
        RunnerOutcomePose _outcomePose;
        float _outcomeBlend;
        static readonly int RunningHash = Animator.StringToHash("Running");
        static readonly int JumpHash = Animator.StringToHash("Jump");
        static readonly int SlideHash = Animator.StringToHash("Slide");

        public static ExplorerRunnerVisual Create(Transform parent)
        {
            var go = new GameObject("ExplorerRunner");
            go.transform.SetParent(parent, false);
            var explorer = go.AddComponent<ExplorerRunnerVisual>();
            explorer.Build();
            return explorer;
        }

        void Build()
        {
            Root = transform;
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            var prefab = Resources.Load<GameObject>("Characters/CartoonRunner");
            if (prefab == null)
            {
                Debug.LogError("[Player] CartoonRunner prefab missing.");
                BuildEmergencyMarker();
                return;
            }

            _model = Instantiate(prefab, transform).transform;
            _model.name = "CartoonRunner";
            _model.localPosition = Vector3.zero;
            _model.localRotation = Quaternion.identity;
            _model.gameObject.SetActive(true);

            ForceVisibleRenderers();
            SetupAnimator();
            // Sample once so collapse detection sees animated bounds
            if (_anim != null && _anim.enabled)
                _anim.Update(0f);
            if (AnimatorCollapsedMesh())
            {
                Debug.LogWarning("[Player] Animator collapsed skinned mesh — disabling animator, bind pose only");
                if (_anim != null)
                {
                    _anim.runtimeAnimatorController = null;
                    _anim.enabled = false;
                }
                ForceVisibleRenderers();
            }
            GroundAlign();
            Measure();

            if (PartCount == 0 || BodyHeight < 0.5f)
            {
                Debug.LogWarning("[Player] CartoonRunner failed visibility check — bright fallback");
                BuildEmergencyMarker();
            }
            else
            {
                bool animOn = _anim != null && _anim.enabled && _anim.runtimeAnimatorController != null;
                Debug.Log($"[Player] CartoonRunner OK parts={PartCount} height={BodyHeight:0.00} skinned={HasSkinnedMesh} anim={(animOn ? 1 : 0)} bounds={GetWorldBounds()}");
            }
        }

        bool AnimatorCollapsedMesh()
        {
            var smr = _model != null ? _model.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (smr == null || smr.sharedMesh == null) return false;
            smr.updateWhenOffscreen = true;
            var wb = smr.bounds;
            float meshH = smr.sharedMesh.bounds.size.y * Mathf.Abs(smr.transform.lossyScale.y);
            // Collapsed speck OR exploded Generic retarget
            return (meshH > 0.5f && wb.size.y < 0.15f)
                   || wb.size.y > 6f
                   || wb.size.x > 8f
                   || wb.size.z > 8f;
        }

        void ForceVisibleRenderers()
        {
            if (_model == null) return;
            var mat = EnsureRuntimeSkin(CharacterRoster.GetSelected());
            foreach (var r in _model.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                r.gameObject.SetActive(true);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                r.sharedMaterial = mat;
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true; // critical — wrong localBounds otherwise culls the whole mesh
                    smr.skinnedMotionVectors = false;
                    if (smr.sharedMesh != null)
                        smr.localBounds = smr.sharedMesh.bounds;
                }
            }
        }

        Material EnsureRuntimeSkin(CharacterRoster.CharacterDef def)
        {
            string texPath = string.IsNullOrEmpty(def.textureResource)
                ? "Characters/survivorMaleB"
                : def.textureResource;
            string key = def.id + "|" + texPath;
            if (_runtimeSkin != null && _skinKey == key) return _runtimeSkin;

            var tex = Resources.Load<Texture2D>(texPath)
                      ?? Resources.Load<Texture2D>("Characters/survivorMaleB");
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Texture")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Standard");
            _runtimeSkin = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));
            _runtimeSkin.name = "RuntimeCartoonSkin_" + def.id;
            _skinKey = key;
            if (tex != null)
            {
                _runtimeSkin.mainTexture = tex;
                if (_runtimeSkin.HasProperty("_BaseMap")) _runtimeSkin.SetTexture("_BaseMap", tex);
            }
            Color tint = Color.Lerp(new Color(1f, 0.92f, 0.75f, 1f), def.color, 0.42f);
            _runtimeSkin.color = tint;
            if (_runtimeSkin.HasProperty("_BaseColor")) _runtimeSkin.SetColor("_BaseColor", tint);
            return _runtimeSkin;
        }

        void GroundAlign()
        {
            if (_model == null) return;
            _model.localPosition = Vector3.zero;
            var b = GetWorldBounds();
            if (b.size.sqrMagnitude < 1e-6f)
            {
                _groundY = 0f;
                return;
            }
            // Lift so feet sit on the player root (y=0)
            float lift = transform.position.y - b.min.y;
            _groundY = lift;
            _model.localPosition = new Vector3(0f, lift, 0f);
        }

        void SetupAnimator()
        {
            if (_model == null) return;
            _anim = _model.GetComponentInChildren<Animator>();
            if (_anim == null) _anim = _model.gameObject.AddComponent<Animator>();
            if (_anim.runtimeAnimatorController == null)
            {
                var ctrl = Resources.Load<RuntimeAnimatorController>("Characters/CartoonRunnerAnim");
                if (ctrl != null) _anim.runtimeAnimatorController = ctrl;
            }
            _anim.applyRootMotion = false;
            _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _anim.enabled = _anim.runtimeAnimatorController != null;
            if (!_anim.enabled)
                Debug.LogWarning("[Player] CartoonRunnerAnim missing — bind pose only");
        }

        void BuildEmergencyMarker()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = "VISIBLE_RUNNER_FALLBACK";
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, 1f, 0f);
            marker.transform.localScale = new Vector3(0.55f, 1f, 0.55f);
            Object.Destroy(marker.GetComponent<Collider>());
            // Bright gold so it is impossible to miss
            var mat = JunglePalette.Mat(new Color(1f, 0.75f, 0.15f), 0.2f);
            marker.GetComponent<Renderer>().sharedMaterial = mat;
            // Head
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(marker.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            head.transform.localScale = new Vector3(0.7f, 0.35f, 0.7f);
            Object.Destroy(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = JunglePalette.Skin;
            BodyHeight = Mathf.Max(BodyHeight, 2f);
            PartCount = Mathf.Max(PartCount, 2);
        }

        Bounds GetWorldBounds()
        {
            var rends = GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return new Bounds(transform.position, Vector3.zero);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                if (rends[i] != null) b.Encapsulate(rends[i].bounds);
            return b;
        }

        void Measure()
        {
            var rends = GetComponentsInChildren<Renderer>(true);
            PartCount = rends != null ? rends.Length : 0;
            HasSkinnedMesh = GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
            var b = GetWorldBounds();
            BodyHeight = b.size.y;
        }

        public void ApplyCharacterAppearance(CharacterRoster.CharacterDef def)
        {
            EnsureRuntimeSkin(def);
            if (_model != null)
            {
                Vector3 scale = def.modelScale.sqrMagnitude < 0.01f ? Vector3.one : def.modelScale;
                // Mild shoulder bias via non-uniform X so roster reads as distinct silhouettes.
                scale.x += def.shoulderBias;
                scale.z += def.shoulderBias * 0.5f;
                _model.localScale = scale;
            }
            ForceVisibleRenderers();
            GroundAlign();
            Measure();
        }

        public void SetAccentColor(Color accent)
        {
            var def = CharacterRoster.GetSelected();
            def.color = accent;
            EnsureRuntimeSkin(def);
            ForceVisibleRenderers();
        }

        public void ApplyCosmetics()
        {
            CosmeticRoster.ApplyToRunner(transform, _anim);
        }

        public bool CarryingStolenIdol => _carryStolenIdol;

        /// <summary>Socket the stolen idol to a hand/hip so the treasure stays visible early-run.</summary>
        public void AttachHeldIdol(Transform idol)
        {
            if (idol == null) return;
            // Drop any previous held prop without clearing the carry flag mid-swap.
            if (_heldIdol != null && _heldIdol != idol)
                Destroy(_heldIdol.gameObject);

            _heldIdol = idol;
            _carryStolenIdol = true;
            IdolVisualFactory.ConfigureAsHeldProp(_heldIdol);

            Transform hand = null;
            if (_anim != null && _anim.isHuman)
                hand = _anim.GetBoneTransform(HumanBodyBones.RightHand)
                       ?? _anim.GetBoneTransform(HumanBodyBones.RightLowerArm);

            if (hand != null)
            {
                _heldIdol.SetParent(hand, false);
                _heldIdol.localPosition = new Vector3(0.06f, 0.1f, 0.04f);
                _heldIdol.localRotation = Quaternion.Euler(12f, 25f, -18f);
                _heldIdol.localScale = Vector3.one * 0.42f;
            }
            else
            {
                _heldIdol.SetParent(transform, false);
                _heldIdol.localPosition = new Vector3(0.3f, 1.08f, 0.2f);
                _heldIdol.localRotation = Quaternion.Euler(8f, -14f, 8f);
                _heldIdol.localScale = Vector3.one * 0.55f;
            }
            _heldIdol.gameObject.SetActive(true);
        }

        public void EnsureHeldIdol()
        {
            if (!_carryStolenIdol) return;
            if (_heldIdol != null) { AttachHeldIdol(_heldIdol); return; }
            var idol = IdolVisualFactory.BuildStolenIdol(transform, Vector3.zero, 0.55f);
            AttachHeldIdol(idol);
        }

        public void ClearHeldIdol()
        {
            _carryStolenIdol = false;
            if (_heldIdol != null)
            {
                Destroy(_heldIdol.gameObject);
                _heldIdol = null;
            }
        }

        public void SetHeldIdolVisible(bool visible)
        {
            if (_heldIdol != null) _heldIdol.gameObject.SetActive(visible);
        }

        /// <summary>Original per-character silhouette accents (scarf / pack / wraps / cuffs).</summary>
        public void ApplyCharacterKit(string characterId, Color accent)
        {
            if (_kitRoot != null) Destroy(_kitRoot.gameObject);
            _kitId = characterId ?? "scout_default";
            _kitRoot = new GameObject("CharacterKit").transform;
            _kitRoot.SetParent(transform, false);

            bool scarfEquipped = CosmeticRoster.HasEquippedScarf;
            bool capeEquipped = CosmeticRoster.HasEquippedCape;

            void Prim(PrimitiveType p, string name, Vector3 pos, Vector3 scale, Color c, Vector3? euler = null)
            {
                var go = GameObject.CreatePrimitive(p);
                go.name = name;
                go.transform.SetParent(_kitRoot, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
                go.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(c, 0.25f);
                Object.Destroy(go.GetComponent<Collider>());
            }

            switch (_kitId)
            {
                case "desert_runner":
                    if (!scarfEquipped)
                    {
                        Prim(PrimitiveType.Cube, "Scarf", new Vector3(0f, 1.55f, -0.05f), new Vector3(0.55f, 0.12f, 0.35f), accent);
                        Prim(PrimitiveType.Cube, "ScarfTail", new Vector3(0.12f, 1.25f, -0.28f), new Vector3(0.12f, 0.55f, 0.08f),
                            Color.Lerp(accent, Color.white, 0.15f), new Vector3(18f, 0f, 12f));
                    }
                    Prim(PrimitiveType.Cube, "BootL", new Vector3(-0.18f, 0.12f, 0.05f), new Vector3(0.22f, 0.18f, 0.32f),
                        new Color(0.45f, 0.28f, 0.14f));
                    Prim(PrimitiveType.Cube, "BootR", new Vector3(0.18f, 0.12f, 0.05f), new Vector3(0.22f, 0.18f, 0.32f),
                        new Color(0.45f, 0.28f, 0.14f));
                    break;
                case "ice_wraith":
                    if (!capeEquipped)
                    {
                        Prim(PrimitiveType.Cube, "Cloak", new Vector3(0f, 1.15f, -0.22f), new Vector3(0.7f, 0.9f, 0.12f),
                            Color.Lerp(accent, Color.white, 0.35f));
                    }
                    Prim(PrimitiveType.Sphere, "FrostOrb", new Vector3(0.38f, 1.35f, 0.05f), Vector3.one * 0.18f, accent);
                    break;
                case "jungle_ace":
                    Prim(PrimitiveType.Cube, "ArmWrapL", new Vector3(-0.42f, 1.15f, 0f), new Vector3(0.14f, 0.35f, 0.14f),
                        new Color(0.35f, 0.5f, 0.28f));
                    Prim(PrimitiveType.Cube, "ArmWrapR", new Vector3(0.42f, 1.15f, 0f), new Vector3(0.14f, 0.35f, 0.14f),
                        new Color(0.35f, 0.5f, 0.28f));
                    if (!CosmeticRoster.HasEquippedHat())
                        Prim(PrimitiveType.Cube, "LeafBand", new Vector3(0f, 1.72f, 0f), new Vector3(0.42f, 0.1f, 0.42f), accent);
                    break;
                case "cave_miner":
                    Prim(PrimitiveType.Cube, "Pack", new Vector3(0f, 1.2f, -0.32f), new Vector3(0.55f, 0.55f, 0.28f),
                        new Color(0.4f, 0.32f, 0.22f));
                    Prim(PrimitiveType.Cylinder, "Pick", new Vector3(0.45f, 1.1f, -0.1f), new Vector3(0.08f, 0.55f, 0.08f),
                        new Color(0.45f, 0.42f, 0.38f), new Vector3(0f, 0f, 35f));
                    Prim(PrimitiveType.Cube, "Lamp", new Vector3(-0.35f, 1.45f, 0.15f), new Vector3(0.16f, 0.2f, 0.16f),
                        new Color(1f, 0.85f, 0.4f));
                    break;
                case "ember_scout":
                    Prim(PrimitiveType.Cube, "Sash", new Vector3(0f, 1.05f, 0.05f), new Vector3(0.65f, 0.14f, 0.35f), accent);
                    Prim(PrimitiveType.Sphere, "Ember", new Vector3(0f, 1.55f, 0.28f), Vector3.one * 0.16f,
                        new Color(1f, 0.45f, 0.15f));
                    Prim(PrimitiveType.Cube, "CuffL", new Vector3(-0.38f, 0.55f, 0.05f), new Vector3(0.18f, 0.12f, 0.22f),
                        new Color(0.25f, 0.15f, 0.12f));
                    Prim(PrimitiveType.Cube, "CuffR", new Vector3(0.38f, 0.55f, 0.05f), new Vector3(0.18f, 0.12f, 0.22f),
                        new Color(0.25f, 0.15f, 0.12f));
                    break;
                default:
                    if (!CosmeticRoster.HasEquippedHat())
                        Prim(PrimitiveType.Cube, "Bandana", new Vector3(0f, 1.68f, 0.05f), new Vector3(0.38f, 0.1f, 0.38f), accent);
                    Prim(PrimitiveType.Cube, "Satchel", new Vector3(0.32f, 1.05f, -0.05f), new Vector3(0.22f, 0.28f, 0.18f),
                        new Color(0.42f, 0.3f, 0.18f));
                    break;
            }
        }

        public RunnerOutcomePose CurrentOutcomePose => _outcomePose;

        public void SetOutcomePose(RunnerOutcomePose pose)
        {
            _outcomePose = pose;
            _outcomeBlend = 0f;
        }

        public void ClearOutcomePose()
        {
            _outcomePose = RunnerOutcomePose.None;
            _outcomeBlend = 0f;
        }

        public void SetPoseFlags(bool sliding, bool jumping)
        {
            if (_anim == null) return;
            _anim.SetBool(SlideHash, sliding);
            bool running = RunSession.Instance != null && RunSession.Instance.IsAlive && !sliding;
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.MainMenu)
            {
                // Menu uses character showcase poses — light jog, not full sprint.
                running = !sliding && _menuShowcasePhase < 0.55f;
            }
            _anim.SetBool(RunningHash, running);
        }

        public void TriggerJump()
        {
            if (_anim == null) return;
            _anim.SetTrigger(JumpHash);
        }

        public void SetLaneLean(float lean01) => _laneLean = Mathf.Clamp(lean01, -1.2f, 1.2f);

        /// <summary>Refresh menu causeway showcase when locker character changes.</summary>
        public void ApplyMenuShowcase()
        {
            _menuShowcasePhase = 0f;
            _menuPoseSeed = CharacterRoster.SelectedId?.GetHashCode() ?? 0;
            SetPoseFlags(false, false);
        }

        public void ResetPose()
        {
            _laneLean = 0f;
            _menuShowcasePhase = 0f;
            ClearOutcomePose();
            if (_anim == null) return;
            _anim.SetBool(SlideHash, false);
            _anim.SetBool(RunningHash, false);
            _anim.Play("Idle", 0, 0f);
        }

        void LateUpdate()
        {
            bool onMenu = GameManager.Instance != null && GameManager.Instance.State == GameState.MainMenu;
            bool opening = GameManager.Instance != null && GameManager.Instance.State == GameState.Opening;
            bool running = RunSession.Instance != null && RunSession.Instance.IsAlive;
            if (onMenu)
            {
                _menuShowcasePhase = Mathf.Repeat(_menuShowcasePhase + Time.deltaTime * 0.35f, 1f);
                running = _menuShowcasePhase < 0.55f;
            }

            bool animPlaying = _anim != null && _anim.enabled && _anim.runtimeAnimatorController != null;
            if (animPlaying)
            {
                // Safety: if a bad clip collapses the mesh mid-run, fall back
                if (AnimatorCollapsedMesh())
                {
                    Debug.LogWarning("[Player] Animator collapsed mid-run — disabling");
                    _anim.runtimeAnimatorController = null;
                    _anim.enabled = false;
                    ForceVisibleRenderers();
                    animPlaying = false;
                }
                else
                {
                    bool sliding = _anim.GetBool(SlideHash);
                    _anim.SetBool(RunningHash, running && !sliding && !opening);
                    if (_model != null)
                    {
                        var lp = _model.localPosition;
                        lp.y = _groundY;
                        _model.localPosition = lp;
                    }
                }
            }
            if (!animPlaying && _model != null)
            {
                float bob = running ? Mathf.Abs(Mathf.Sin(Time.time * 10f)) * 0.04f : 0f;
                var lp = _model.localPosition;
                lp.y = _groundY + bob;
                _model.localPosition = lp;
            }

            if (_outcomePose != RunnerOutcomePose.None)
                ApplyOutcomePose();
            else if (onMenu)
                ApplyMenuShowcasePose();
            else
                ApplyTraversalPose(running);
        }

        void ApplyOutcomePose()
        {
            _outcomeBlend = Mathf.MoveTowards(_outcomeBlend, 1f, Time.deltaTime * 6f);
            Vector3 targetPos = Vector3.zero;
            Quaternion targetRot = Quaternion.identity;

            switch (_outcomePose)
            {
                case RunnerOutcomePose.Caught:
                    targetPos = new Vector3(0f, 0.05f, -0.2f);
                    targetRot = Quaternion.Euler(-18f, 160f, 8f);
                    break;
                case RunnerOutcomePose.ImpactDeath:
                    targetPos = new Vector3(0f, -0.15f, 0.1f);
                    targetRot = Quaternion.Euler(35f, 25f, -18f);
                    break;
                case RunnerOutcomePose.FallDeath:
                    targetPos = new Vector3(0f, 0.1f, 0f);
                    targetRot = Quaternion.Euler(-25f + Mathf.Sin(Time.time * 8f) * 20f, 40f, Mathf.Sin(Time.time * 6f) * 25f);
                    break;
                case RunnerOutcomePose.IdolReach:
                    targetPos = new Vector3(0f, 0.05f, 0.12f);
                    targetRot = Quaternion.Euler(-12f, 0f, 0f);
                    break;
                case RunnerOutcomePose.Slide:
                    targetPos = new Vector3(0f, -0.42f, 0.35f);
                    targetRot = Quaternion.Euler(62f, 0f, 0f);
                    break;
            }

            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, _outcomeBlend);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, _outcomeBlend);

            // Procedural bone overlays after Animator evaluation for denser silhouette.
            ApplyProceduralBonePose(_outcomePose, _outcomeBlend);
        }

        void ApplyProceduralBonePose(RunnerOutcomePose pose, float blend)
        {
            if (_anim == null || !_anim.isHuman || blend < 0.01f) return;
            Transform spine = _anim.GetBoneTransform(HumanBodyBones.Spine);
            Transform head = _anim.GetBoneTransform(HumanBodyBones.Head);
            Transform lArm = _anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rArm = _anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform hips = _anim.GetBoneTransform(HumanBodyBones.Hips);

            void BlendLocal(Transform t, Quaternion localExtra)
            {
                if (t == null) return;
                t.localRotation = Quaternion.Slerp(t.localRotation, t.localRotation * localExtra, blend);
            }

            switch (pose)
            {
                case RunnerOutcomePose.Caught:
                    BlendLocal(spine, Quaternion.Euler(-25f, 180f, 0f));
                    BlendLocal(head, Quaternion.Euler(10f, 160f, 0f));
                    BlendLocal(lArm, Quaternion.Euler(-40f, 0f, 55f));
                    BlendLocal(rArm, Quaternion.Euler(-40f, 0f, -55f));
                    break;
                case RunnerOutcomePose.ImpactDeath:
                    BlendLocal(spine, Quaternion.Euler(40f, 20f, -15f));
                    BlendLocal(head, Quaternion.Euler(25f, -15f, 10f));
                    BlendLocal(lArm, Quaternion.Euler(-20f, 0f, 70f));
                    BlendLocal(rArm, Quaternion.Euler(30f, 0f, -40f));
                    BlendLocal(hips, Quaternion.Euler(15f, 10f, 0f));
                    break;
                case RunnerOutcomePose.FallDeath:
                    BlendLocal(spine, Quaternion.Euler(-20f, 0f, Mathf.Sin(Time.time * 7f) * 18f));
                    BlendLocal(lArm, Quaternion.Euler(-70f, 0f, 40f));
                    BlendLocal(rArm, Quaternion.Euler(-70f, 0f, -40f));
                    break;
                case RunnerOutcomePose.IdolReach:
                    BlendLocal(spine, Quaternion.Euler(-8f, 0f, 0f));
                    BlendLocal(rArm, Quaternion.Euler(-75f, 20f, -10f));
                    BlendLocal(lArm, Quaternion.Euler(-40f, -10f, 20f));
                    BlendLocal(head, Quaternion.Euler(-5f, 15f, 0f));
                    break;
                case RunnerOutcomePose.Slide:
                    BlendLocal(spine, Quaternion.Euler(50f, 0f, 0f));
                    BlendLocal(hips, Quaternion.Euler(25f, 0f, 0f));
                    BlendLocal(lArm, Quaternion.Euler(-30f, 0f, 25f));
                    BlendLocal(rArm, Quaternion.Euler(-30f, 0f, -25f));
                    break;
            }
        }

        void ApplyMenuShowcasePose()
        {
            // Per-character causeway poses: lean, ready crouch, or proud idle between jogs.
            string id = _kitId ?? CharacterRoster.SelectedId;
            float t = _menuShowcasePhase;
            Vector3 targetPos = Vector3.zero;
            Quaternion targetRot = Quaternion.identity;
            float lean = Mathf.Sin(Time.time * 1.4f + (_menuPoseSeed % 7)) * 0.35f;

            switch (id)
            {
                case "desert_runner":
                    targetPos = new Vector3(0f, t > 0.55f ? -0.08f : 0f, 0.05f);
                    targetRot = Quaternion.Euler(t > 0.7f ? 8f : 0f, lean * 18f, lean * -6f);
                    break;
                case "ice_wraith":
                    targetPos = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 2f)) * 0.04f, 0f);
                    targetRot = Quaternion.Euler(-6f, lean * 22f, lean * 4f);
                    break;
                case "jungle_ace":
                    targetPos = new Vector3(0f, t > 0.6f ? -0.18f : 0f, t > 0.6f ? 0.12f : 0f);
                    targetRot = Quaternion.Euler(t > 0.6f ? 28f : 0f, 0f, lean * -10f);
                    break;
                case "cave_miner":
                    targetPos = new Vector3(0f, 0f, 0.02f);
                    targetRot = Quaternion.Euler(4f, -12f + lean * 10f, 0f);
                    break;
                case "ember_scout":
                    targetPos = new Vector3(0f, 0f, 0.04f);
                    targetRot = Quaternion.Euler(t > 0.65f ? -10f : 2f, lean * 16f, lean * -8f);
                    break;
                default:
                    targetPos = new Vector3(0f, 0f, 0f);
                    targetRot = Quaternion.Euler(0f, lean * 14f, lean * -5f);
                    break;
            }

            // Kit props get a light sway so locker swaps read on the causeway.
            if (_kitRoot != null)
            {
                float sway = Mathf.Sin(Time.time * 2.2f) * 3f;
                _kitRoot.localRotation = Quaternion.Euler(0f, sway, sway * 0.4f);
            }

            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos,
                1f - Mathf.Exp(-8f * Time.deltaTime));
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot,
                1f - Mathf.Exp(-7f * Time.deltaTime));
        }

        void ApplyTraversalPose(bool running)
        {
            var player = PlayerController.Instance;
            bool sliding = player != null && player.IsSliding;
            var mode = player != null ? player.Traversal : TraversalMode.None;

            Vector3 targetPos = Vector3.zero;
            Quaternion targetRot = Quaternion.identity;
            float posLerp = 10f;
            float rotLerp = 8f;

            if (sliding)
            {
                targetPos = new Vector3(0f, -0.42f, 0.35f);
                targetRot = Quaternion.Euler(62f, 0f, 0f);
                posLerp = 12f;
                rotLerp = 12f;
            }
            else if (mode == TraversalMode.Zipline || mode == TraversalMode.CanopyRope)
            {
                targetPos = new Vector3(0f, -0.15f, 0.1f);
                targetRot = Quaternion.Euler(-8f, 0f, _laneLean * -6f);
                float sway = Mathf.Sin(Time.time * 7f) * 4f;
                targetRot *= Quaternion.Euler(0f, 0f, sway);
            }
            else if (mode == TraversalMode.Swim || mode == TraversalMode.WaterfallPlunge)
            {
                float stroke = Mathf.Sin(Time.time * 8f) * 12f;
                bool diving = mode == TraversalMode.WaterfallPlunge
                              && player != null
                              && player.transform.position.y > 1.2f;
                targetPos = diving
                    ? new Vector3(0f, -0.1f, 0.15f)
                    : new Vector3(0f, -0.55f, 0.2f);
                targetRot = diving
                    ? Quaternion.Euler(35f + stroke * 0.1f, 0f, stroke * 0.2f)
                    : Quaternion.Euler(70f + stroke * 0.15f, 0f, stroke * 0.35f);
                posLerp = 8f;
            }
            else if (mode == TraversalMode.WallRun)
            {
                float side = player != null && player.Lane >= 2 ? 1f : -1f;
                targetPos = new Vector3(side * 0.15f, 0.05f, 0f);
                targetRot = Quaternion.Euler(8f, 0f, side * -55f);
                posLerp = 14f;
                rotLerp = 14f;
            }
            else if (mode == TraversalMode.LedgeGrab)
            {
                targetPos = new Vector3(0f, -0.25f, 0.15f);
                targetRot = Quaternion.Euler(-25f, 0f, _laneLean * -8f);
                float hang = Mathf.Sin(Time.time * 5f) * 3f;
                targetRot *= Quaternion.Euler(hang, 0f, 0f);
            }
            else if (mode == TraversalMode.MineCart || mode == TraversalMode.IceSurf
                     || mode == TraversalMode.WaterSlide || mode == TraversalMode.Boat)
            {
                targetPos = new Vector3(0f, 0.05f, 0f);
                targetRot = Quaternion.Euler(mode == TraversalMode.WaterSlide ? 12f : 6f, 0f, _laneLean * -8f);
            }
            else if (mode == TraversalMode.Rope || mode == TraversalMode.Vine)
            {
                targetPos = new Vector3(0f, -0.1f, 0.05f);
                targetRot = Quaternion.Euler(-15f, 0f, Mathf.Sin(Time.time * 6f) * 10f);
            }
            else
            {
                float leanZ = _laneLean * -10f;
                targetRot = Quaternion.Euler(running && player != null && player.IsJumping ? -12f : 0f, 0f, leanZ);
            }

            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos,
                1f - Mathf.Exp(-posLerp * Time.deltaTime));
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot,
                1f - Mathf.Exp(-rotLerp * Time.deltaTime));
        }

        float _groundY;
        float _menuShowcasePhase;
        int _menuPoseSeed;
    }
}
