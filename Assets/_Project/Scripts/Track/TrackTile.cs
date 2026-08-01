using UnityEngine;

namespace TempleSprint
{
    public enum TileKind
    {
        Straight,
        ObstacleCluster,
        CoinLane,
        HazardGap,
        Branch,
        DynamicHazard,
        EnvironmentZone,
        RiverCrossing,
        FireCrossing,
        Zipline,
        MineCart,
        IceSurf,
        WallRun,
        LedgeGrab,
        TreeBridge,
        CanopyRope,
        WaterfallPlunge,
        WaterSlide,
        TempleHall,
        RuinFork,
        LavaRiver,
        BiomeTransitionTunnel,
        TurnLeft,
        TurnRight,
        TJunction
    }

    public class TrackTile : MonoBehaviour
    {
        public const float Length = 12f;
        public const float DeckWidth = 4.6f;
        /// <summary>Quarter-circle turn radius (genre-style banked curves, not sharp L corners).</summary>
        public const float CurveRadius = 7.2f;
        public const int CurveSegments = 12;
        public const float MaxBankDegrees = 20f;
        // Legacy aliases — arm length ≈ radius for spacing estimates.
        public const float TurnArmIn = CurveRadius;
        public const float TurnArmOut = CurveRadius;
        public const float JunctionApproach = 7f;
        public const float JunctionArm = 6f;
        public static float CurvePathLength => CurveRadius * (Mathf.PI * 0.5f);

        public TileKind Kind { get; private set; }
        public PathPose EntryPose { get; private set; }
        public PathPose ExitPose { get; private set; }
        public float PathStartDistance => EntryPose.pathDistance;
        public float PathLength { get; private set; }
        public float StartZ => PathStartDistance; // compat for older callers

        public bool IsTurn => Kind == TileKind.TurnLeft || Kind == TileKind.TurnRight;
        public bool IsJunction => Kind == TileKind.TJunction || Kind == TileKind.RuinFork;
        public bool JunctionResolved { get; private set; }
        public bool JunctionChoseLeft { get; private set; }

        public void Recycle()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            var marker = GetComponent<RiverCrossingMarker>();
            if (marker != null) Destroy(marker);
            var fireMarker = GetComponent<FireCrossingMarker>();
            if (fireMarker != null) Destroy(fireMarker);
            var special = GetComponent<SpecialStageMarker>();
            if (special != null) Destroy(special);
            JunctionResolved = false;
            JunctionChoseLeft = false;
            gameObject.SetActive(false);
        }

        RunDifficulty _runDifficulty = RunDifficulty.Medium;

        /// <summary>Legacy straight-only spawn (menu preview).</summary>
        public void Build(TileKind kind, float startZ, float obstacleChance, int difficultyTier)
        {
            Build(kind, new PathPose(new Vector3(0f, 0f, startZ), 0f, startZ), obstacleChance, difficultyTier, RunDifficulty.Medium);
        }

        public void Build(TileKind kind, PathPose entry, float obstacleChance, int difficultyTier)
        {
            Build(kind, entry, obstacleChance, difficultyTier, RunDifficulty.Medium);
        }

        public void Build(TileKind kind, PathPose entry, float obstacleChance, int difficultyTier, RunDifficulty runDifficulty)
        {
            Kind = kind;
            _runDifficulty = runDifficulty;
            EntryPose = entry;
            JunctionResolved = false;
            JunctionChoseLeft = false;
            transform.SetPositionAndRotation(entry.position, entry.Rotation);
            gameObject.SetActive(true);

            if (kind == TileKind.TurnLeft || kind == TileKind.TurnRight)
            {
                BuildTurn(kind == TileKind.TurnLeft);
                return;
            }

            if (kind == TileKind.TJunction)
            {
                BuildTJunction();
                return;
            }

            if (kind == TileKind.RuinFork)
            {
                BuildRuinFork();
                return;
            }

            // Straight content tiles
            PathLength = Length;
            ExitPose = entry.AdvanceStraight(Length);

            if (kind == TileKind.HazardGap || kind == TileKind.RiverCrossing || kind == TileKind.FireCrossing
                || kind == TileKind.LavaRiver
                || kind == TileKind.Zipline || kind == TileKind.MineCart || kind == TileKind.IceSurf
                || kind == TileKind.WallRun || kind == TileKind.LedgeGrab || kind == TileKind.TreeBridge
                || kind == TileKind.CanopyRope || kind == TileKind.WaterfallPlunge
                || kind == TileKind.WaterSlide)
            {
                if (kind == TileKind.HazardGap)
                    BuildFloorWithGap();
                else if (kind == TileKind.FireCrossing || kind == TileKind.LavaRiver)
                    BuildFloorWithFireChannel();
                else if (kind == TileKind.Zipline || kind == TileKind.MineCart || kind == TileKind.IceSurf
                         || kind == TileKind.WallRun || kind == TileKind.LedgeGrab || kind == TileKind.TreeBridge
                         || kind == TileKind.CanopyRope || kind == TileKind.WaterfallPlunge
                         || kind == TileKind.WaterSlide)
                    BuildFloorWithSpecialChannel();
                else
                    BuildFloorWithRiverChannel();
                BuildRailsForGap();
                BuildSupportsForGap();
            }
            else
            {
                BuildFloor();
                BuildRails();
                BuildSupports();
            }

            if (kind == TileKind.RiverCrossing)
                BuildForestEdgeForRiver();
            else if (kind == TileKind.FireCrossing)
                BuildForestEdgeForFire();
            else if (kind == TileKind.LavaRiver)
                BuildForestEdgeForFire();
            else if (kind == TileKind.Zipline || kind == TileKind.WallRun || kind == TileKind.LedgeGrab
                     || kind == TileKind.TreeBridge || kind == TileKind.CanopyRope)
                BuildForestEdgeForSpecial();
            else if (kind == TileKind.WaterfallPlunge || kind == TileKind.WaterSlide)
                BuildForestEdgeForRiver();
            else if (kind == TileKind.MineCart)
                BuildCaveTunnelShell();
            else if (kind == TileKind.IceSurf)
                BuildIceSlopeShell();
            else if (kind == TileKind.TempleHall)
                BuildTempleHallInterior();
            else if (kind == TileKind.BiomeTransitionTunnel)
                BuildBiomeTransitionTunnelShell();
            else
                BuildForestEdge(transform, 0f, Length);

            switch (kind)
            {
                case TileKind.Straight:
                    if (Random.value < 0.5f) ScatterCoins(4);
                    if (Random.value < 0.12f) SpawnRelicOrGem();
                    if (Random.value < 0.22f) BuildArch();
                    if (Random.value < 0.45f) BuildHangingVines(Random.Range(1, 3));
                    if (BiomeSystem.Current == BiomeId.CaveMines && Random.value < 0.4f) BuildMineTimberFrame();
                    if (BiomeSystem.Current == BiomeId.CaveMines && Random.value < 0.45f) BuildCaveRunwayProps();
                    if (BiomeSystem.Current == BiomeId.VolcanicCrater && Random.value < 0.35f) BuildVolcanicEdgeGlow();
                    if (BiomeSystem.Current == BiomeId.IceCaverns && Random.value < 0.4f) BuildIceRunwayFrost();
                    if (BiomeSystem.Current == BiomeId.DesertTombs && Random.value < 0.55f) BuildDesertRunwayProps();
                    if (BiomeSystem.Current == BiomeId.IceCaverns && Random.value < 0.5f) BuildIceRunwayProps();
                    if (BiomeSystem.Current == BiomeId.NightSummit && Random.value < 0.55f) BuildNightSummitProps();
                    break;
                case TileKind.CoinLane:
                    ScatterCoins(10);
                    if (Random.value < 0.3f) SpawnPowerUp();
                    if (Random.value < 0.35f) BuildHangingVines(1);
                    if (Random.value < 0.55f) SpawnBreakableIdol();
                    break;
                case TileKind.ObstacleCluster:
                    SpawnObstacles(obstacleChance, difficultyTier);
                    if (Random.value < 0.4f) ScatterCoins(3);
                    if (Random.value < 0.2f) BuildArch();
                    if (Random.value < 0.4f) BuildHangingVines(Random.Range(1, 3));
                    if (Random.value < 0.35f) SpawnBreakableIdol();
                    break;
                case TileKind.HazardGap:
                    BuildGap();
                    break;
                case TileKind.Branch:
                    BuildBranch();
                    break;
                case TileKind.DynamicHazard:
                    SpawnDynamic(difficultyTier);
                    if (Random.value < 0.18f) BuildArch();
                    break;
                case TileKind.EnvironmentZone:
                    ScatterCoins(5);
                    if (Random.value < 0.5f) SpawnPowerUp();
                    if (Random.value < 0.25f) BuildArch();
                    BuildHangingVines(2);
                    PlaceEnvironmentPulseTrigger();
                    break;
                case TileKind.RiverCrossing:
                    BuildRiverCrossing();
                    break;
                case TileKind.FireCrossing:
                    BuildFireCrossing();
                    break;
                case TileKind.Zipline:
                    BuildZiplineStage();
                    break;
                case TileKind.MineCart:
                    BuildMineCartStage();
                    break;
                case TileKind.IceSurf:
                    BuildIceSurfStage();
                    break;
                case TileKind.WallRun:
                    BuildWallRunStage();
                    break;
                case TileKind.LedgeGrab:
                    BuildLedgeGrabStage();
                    break;
                case TileKind.TreeBridge:
                    BuildTreeBridgeStage();
                    break;
                case TileKind.CanopyRope:
                    BuildCanopyRopeStage();
                    break;
                case TileKind.WaterfallPlunge:
                    BuildWaterfallPlungeStage();
                    break;
                case TileKind.WaterSlide:
                    BuildWaterSlideStage();
                    break;
                case TileKind.TempleHall:
                    BuildTempleHallContents();
                    break;
                case TileKind.LavaRiver:
                    BuildLavaRiverCrossing();
                    break;
                case TileKind.BiomeTransitionTunnel:
                    BuildBiomeTransitionTunnelContents();
                    break;
            }
        }

        public bool ContainsPathDistance(float pathDist)
        {
            return pathDist >= PathStartDistance - 0.05f
                   && pathDist <= PathStartDistance + PathLength + 0.05f;
        }

        /// <summary>Sample centerline pose for a world path distance.</summary>
        public PathPose SampleAtPathDistance(float pathDist)
        {
            float local = Mathf.Clamp(pathDist - PathStartDistance, 0f, PathLength);
            return SampleLocal(local);
        }

        PathPose SampleLocal(float s)
        {
            if (IsTurn)
            {
                bool left = Kind == TileKind.TurnLeft;
                float arcLen = CurvePathLength;
                float clamped = Mathf.Clamp(s, 0f, arcLen);
                float thetaDeg = (clamped / Mathf.Max(0.001f, arcLen)) * 90f;
                float thetaRad = thetaDeg * Mathf.Deg2Rad;
                float R = CurveRadius;

                Vector3 center = left
                    ? EntryPose.position - EntryPose.Right * R
                    : EntryPose.position + EntryPose.Right * R;
                Vector3 fromCenter = left
                    ? EntryPose.Right * (Mathf.Cos(thetaRad) * R) + EntryPose.Forward * (Mathf.Sin(thetaRad) * R)
                    : -EntryPose.Right * (Mathf.Cos(thetaRad) * R) + EntryPose.Forward * (Mathf.Sin(thetaRad) * R);

                float yaw = NormalizeYaw(EntryPose.yaw + (left ? -thetaDeg : thetaDeg));
                // Peak bank mid-curve; lean into the turn (left = negative roll).
                float bank = Mathf.Sin(thetaRad * 2f) * MaxBankDegrees * (left ? -1f : 1f);
                return new PathPose(center + fromCenter, yaw, PathStartDistance + clamped, bank);
            }

            if (IsJunction)
            {
                if (!JunctionResolved || s <= JunctionApproach)
                {
                    float along = Mathf.Min(s, JunctionApproach);
                    return new PathPose(EntryPose.position + EntryPose.Forward * along, EntryPose.yaw,
                        PathStartDistance + along);
                }

                float u = s - JunctionApproach;
                float t = Mathf.Clamp01(u / JunctionArm);
                float yaw = Mathf.LerpAngle(EntryPose.yaw, ExitPose.yaw, t);
                var corner = EntryPose.position + EntryPose.Forward * JunctionApproach;
                Vector3 dir = JunctionChoseLeft ? -EntryPose.Right : EntryPose.Right;
                // Soft bank into the chosen arm so forks feel less like a hard L.
                float bank = Mathf.Sin(t * Mathf.PI) * (MaxBankDegrees * 0.55f) * (JunctionChoseLeft ? -1f : 1f);
                return new PathPose(corner + dir * u, yaw, PathStartDistance + s, bank);
            }

            // Straight
            return new PathPose(EntryPose.position + EntryPose.Forward * s, EntryPose.yaw, PathStartDistance + s);
        }

        static float NormalizeYaw(float yaw) => PathPose.NormalizeYaw(yaw);

        /// <summary>Commit T-junction choice. Returns new exit pose for the spawner.</summary>
        public PathPose ResolveJunction(bool chooseLeft)
        {
            if (!IsJunction || JunctionResolved) return ExitPose;
            JunctionResolved = true;
            JunctionChoseLeft = chooseLeft;
            PathLength = JunctionApproach + JunctionArm;

            var corner = EntryPose.position + EntryPose.Forward * JunctionApproach;
            float exitYaw = PathPose.NormalizeYaw(EntryPose.yaw + (chooseLeft ? -90f : 90f));
            Vector3 dir = chooseLeft ? -EntryPose.Right : EntryPose.Right;
            ExitPose = new PathPose(corner + dir * JunctionArm, exitYaw, PathStartDistance + PathLength);

            // Hide unused arm visuals
            var unused = transform.Find(chooseLeft ? "ArmRight" : "ArmLeft");
            if (unused != null) unused.gameObject.SetActive(false);

            return ExitPose;
        }

        void BuildTurn(bool left)
        {
            PathLength = CurvePathLength;
            ExitPose = SampleLocal(PathLength);

            var curveRoot = new GameObject(left ? "BankedCurveL" : "BankedCurveR").transform;
            curveRoot.SetParent(transform, false);

            float segArc = PathLength / CurveSegments;
            for (int i = 0; i < CurveSegments; i++)
            {
                float sMid = (i + 0.5f) * segArc;
                var pose = SampleLocal(sMid);
                Vector3 localPos = transform.InverseTransformPoint(pose.position);
                float localYaw = NormalizeYaw(pose.yaw - EntryPose.yaw);

                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.name = "CurveSlab_" + i;
                slab.transform.SetParent(curveRoot, false);
                slab.transform.localPosition = localPos + new Vector3(0f, -0.12f, 0f);
                // Bank the deck into the turn so the outer rail reads higher.
                slab.transform.localRotation = Quaternion.Euler(0f, localYaw, pose.bank);
                slab.transform.localScale = new Vector3(DeckWidth, 0.42f, segArc * 1.12f);
                slab.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
                Object.Destroy(slab.GetComponent<Collider>());

                // Outer curb raised slightly for banked-turn silhouette.
                float curbSide = left ? 1f : -1f;
                var curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                curb.name = "BankCurb_" + i;
                curb.transform.SetParent(curveRoot, false);
                curb.transform.localPosition = localPos
                    + Quaternion.Euler(0f, localYaw, 0f) * new Vector3(curbSide * (DeckWidth * 0.5f - 0.12f), 0.18f, 0f);
                curb.transform.localRotation = Quaternion.Euler(0f, localYaw, pose.bank);
                curb.transform.localScale = new Vector3(0.28f, 0.35f, segArc * 1.05f);
                curb.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                Object.Destroy(curb.GetComponent<Collider>());

                if (i % 2 == 0)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        rail.transform.SetParent(curveRoot, false);
                        rail.transform.localPosition = localPos
                            + Quaternion.Euler(0f, localYaw, 0f) * new Vector3(side * (DeckWidth * 0.5f), 0.28f, 0f);
                        rail.transform.localRotation = Quaternion.Euler(0f, localYaw, pose.bank);
                        rail.transform.localScale = new Vector3(0.28f, 0.5f, segArc * 1.05f);
                        rail.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                        Object.Destroy(rail.GetComponent<Collider>());
                    }
                }

                if (i % 3 == 0)
                {
                    var supportAnchor = new GameObject("CurveSupport_" + i).transform;
                    supportAnchor.SetParent(curveRoot, false);
                    supportAnchor.localPosition = localPos;
                    supportAnchor.localRotation = Quaternion.Euler(0f, localYaw, 0f);
                    SpawnSupportPairOn(supportAnchor, 0f);
                }
            }

            if (Random.value < 0.55f)
            {
                for (int c = 0; c < 3; c++)
                {
                    float s = PathLength * (0.2f + c * 0.25f);
                    var pose = SampleLocal(s);
                    Vector3 local = transform.InverseTransformPoint(pose.position);
                    CollectibleCoin.Create(transform, local + Vector3.up * 1.25f);
                }
            }

            // Plant only on the outside of the curve; keep the inside clear for camera.
            int outerSign = left ? 1 : -1;
            for (int i = 0; i <= 5; i++)
            {
                float s = PathLength * (i / 5f);
                var pose = SampleLocal(s);
                Vector3 worldOuter = pose.position + pose.Right * (outerSign * (DeckWidth * 0.5f + 3.2f));
                Vector3 localOuter = transform.InverseTransformPoint(worldOuter);
                localOuter.y = ForestFloorY;
                if (Random.value < 0.7f)
                    SpawnForestTree(transform, localOuter, Random.Range(0.9f, 1.35f), Random.value < 0.55f);
                if (Random.value < 0.5f)
                    SpawnShrub(transform, localOuter + new Vector3(outerSign * 1.2f, 0f, Random.Range(-0.4f, 0.4f)));
            }

            StripHazardsFromTile();
        }

        void BuildTJunction()
        {
            // Until resolved, path only covers the approach (player must choose).
            PathLength = JunctionApproach;
            ExitPose = EntryPose.AdvanceStraight(JunctionApproach); // placeholder

            BuildFloorSegment(new Vector3(0f, -0.12f, JunctionApproach * 0.5f),
                new Vector3(DeckWidth, 0.4f, JunctionApproach));
            BuildRailSegment(0.3f, JunctionApproach - 0.6f);
            SpawnSupportPair(2f);
            SpawnSupportPair(5f);

            // Overgrown dead end: the route continues left or right, never straight on.
            BuildForestEdge(transform, 0f, JunctionApproach, 0, false);
            BuildJunctionThicket();

            BuildJunctionArm("ArmLeft", -90f);
            BuildJunctionArm("ArmRight", 90f);

            // Swipe hint coins at fork mouth
            CollectibleCoin.Create(transform, new Vector3(-1.1f, 1.4f, JunctionApproach - 1.2f));
            CollectibleCoin.Create(transform, new Vector3(1.1f, 1.4f, JunctionApproach - 1.2f));

            StripHazardsFromTile();
        }

        /// <summary>Enclosed temple fork — two ruin corridors branch left/right (genre multi-path halls).</summary>
        void BuildRuinFork()
        {
            PathLength = JunctionApproach;
            ExitPose = EntryPose.AdvanceStraight(JunctionApproach);

            BuildFloorSegment(new Vector3(0f, -0.12f, JunctionApproach * 0.5f),
                new Vector3(DeckWidth, 0.4f, JunctionApproach));
            BuildRailSegment(0.3f, JunctionApproach - 0.6f);
            SpawnSupportPair(2f);
            SpawnSupportPair(5f);

            // Sealed dead-end gate ahead — path continues only into the side halls.
            BuildRuinForkGate();
            BuildEnclosedHallSegment(transform, 0f, JunctionApproach, "ApproachHall");

            BuildRuinForkArm("ArmLeft", -90f);
            BuildRuinForkArm("ArmRight", 90f);

            CollectibleCoin.Create(transform, new Vector3(-1.15f, 1.45f, JunctionApproach - 1.1f));
            CollectibleCoin.Create(transform, new Vector3(1.15f, 1.45f, JunctionApproach - 1.1f));
            if (Random.value < 0.45f)
                GemPickup.Create(transform, new Vector3(
                    (Random.value < 0.5f ? -1f : 1f) * PlayerController.LaneWidth,
                    1.35f, JunctionApproach * 0.45f));

            StripHazardsFromTile();
        }

        void BuildRuinForkGate()
        {
            var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "RuinForkGate";
            gate.transform.SetParent(transform, false);
            gate.transform.localPosition = new Vector3(0f, 1.8f, JunctionApproach + 0.35f);
            gate.transform.localScale = new Vector3(DeckWidth + 1.8f, 3.6f, 0.55f);
            gate.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
            StripCollider(gate);

            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "RuinForkLintel";
            lintel.transform.SetParent(transform, false);
            lintel.transform.localPosition = new Vector3(0f, 3.6f, JunctionApproach + 0.1f);
            lintel.transform.localScale = new Vector3(DeckWidth + 2.4f, 0.45f, 1.1f);
            lintel.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            StripCollider(lintel);

            var idol = GameObject.CreatePrimitive(PrimitiveType.Cube);
            idol.name = "GateIdol";
            idol.transform.SetParent(transform, false);
            idol.transform.localPosition = new Vector3(0f, 2.4f, JunctionApproach + 0.05f);
            idol.transform.localScale = new Vector3(0.55f, 1.1f, 0.4f);
            idol.GetComponent<Renderer>().sharedMaterial = JunglePalette.Gold;
            StripCollider(idol);
        }

        void BuildRuinForkArm(string name, float yaw)
        {
            var arm = new GameObject(name).transform;
            arm.SetParent(transform, false);
            arm.localPosition = new Vector3(0f, 0f, JunctionApproach);
            arm.localRotation = Quaternion.Euler(0f, yaw, 0f);
            BuildFloorOn(arm, new Vector3(0f, -0.12f, JunctionArm * 0.5f), new Vector3(DeckWidth, 0.4f, JunctionArm));
            BuildRailsOn(arm, 0.3f, JunctionArm - 0.6f);
            SpawnSupportPairOn(arm, JunctionArm * 0.4f);
            SpawnSupportPairOn(arm, JunctionArm * 0.8f);
            BuildEnclosedHallSegment(arm, 0f, JunctionArm, "ForkHall");

            // Light corridor challenge on one random arm only (readable choice reward).
            if (Random.value < 0.55f)
            {
                float z = JunctionArm * Random.Range(0.35f, 0.7f);
                if (Random.value < 0.5f)
                    Obstacle.CreateLowBeam(arm, z, Random.Range(0, 3));
                else
                    CollectibleCoin.Create(arm, new Vector3(0f, 1.4f, z));
            }
        }

        void BuildEnclosedHallSegment(Transform parent, float zStart, float length, string label)
        {
            float mid = zStart + length * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = label + "Wall";
                wall.transform.SetParent(parent, false);
                wall.transform.localPosition = new Vector3(side * (DeckWidth * 0.5f + 1.45f), 2.2f, mid);
                wall.transform.localScale = new Vector3(2.2f, 4.4f, length * 0.96f);
                wall.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                StripCollider(wall);

                for (int i = 0; i < 2; i++)
                {
                    float z = zStart + length * ((i + 1f) / 3f);
                    var niche = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    niche.transform.SetParent(parent, false);
                    niche.transform.localPosition = new Vector3(side * (DeckWidth * 0.5f + 0.5f), 1.45f, z);
                    niche.transform.localScale = new Vector3(0.5f, 1.2f, 0.95f);
                    niche.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
                    StripCollider(niche);

                    var torch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    torch.name = "ForkTorch";
                    torch.transform.SetParent(parent, false);
                    torch.transform.localPosition = new Vector3(side * (DeckWidth * 0.48f + 0.3f), 2.45f, z);
                    torch.transform.localScale = Vector3.one * 0.26f;
                    torch.GetComponent<Renderer>().sharedMaterial = JunglePalette.Flame;
                    StripCollider(torch);
                }
            }

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = label + "Ceiling";
            ceiling.transform.SetParent(parent, false);
            ceiling.transform.localPosition = new Vector3(0f, 4.25f, mid);
            ceiling.transform.localScale = new Vector3(DeckWidth + 3.4f, 0.5f, length * 0.96f);
            ceiling.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            StripCollider(ceiling);

            var inlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
            inlay.name = label + "Inlay";
            inlay.transform.SetParent(parent, false);
            inlay.transform.localPosition = new Vector3(0f, 0.02f, mid);
            inlay.transform.localScale = new Vector3(1f, 0.04f, length * 0.85f);
            inlay.GetComponent<Renderer>().sharedMaterial = JunglePalette.Gold;
            StripCollider(inlay);
        }

        void BuildJunctionArm(string name, float yaw)
        {
            var arm = new GameObject(name).transform;
            arm.SetParent(transform, false);
            arm.localPosition = new Vector3(0f, 0f, JunctionApproach);
            arm.localRotation = Quaternion.Euler(0f, yaw, 0f);
            BuildFloorOn(arm, new Vector3(0f, -0.12f, JunctionArm * 0.5f), new Vector3(DeckWidth, 0.4f, JunctionArm));
            BuildRailsOn(arm, 0.3f, JunctionArm - 0.6f);
            SpawnSupportPairOn(arm, JunctionArm * 0.45f);
            SpawnSupportPairOn(arm, JunctionArm * 0.85f);

            // Plant only on the far side of the arm so the approach view stays open.
            int outer = yaw < 0f ? 1 : -1;
            BuildForestEdge(arm, 0f, JunctionArm, outer);
        }

        void BuildFloorOn(Transform parent, Vector3 localPos, Vector3 scale)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "FloorSlab";
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = localPos;
            floor.transform.localScale = scale;
            floor.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
            Object.Destroy(floor.GetComponent<Collider>());
        }

        void BuildFloorSegment(Vector3 localPos, Vector3 scale) => BuildFloorOn(transform, localPos, scale);

        void BuildRailsOn(Transform parent, float zStart, float segLength)
        {
            float zMid = zStart + segLength * 0.5f;
            float x = DeckWidth * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.transform.SetParent(parent, false);
                rail.transform.localPosition = new Vector3(side * x, 0.32f, zMid);
                rail.transform.localScale = new Vector3(0.32f, 0.55f, segLength);
                rail.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                Object.Destroy(rail.GetComponent<Collider>());
            }
        }

        void SpawnSupportPairOn(Transform parent, float z)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "Support";
                pillar.transform.SetParent(parent, false);
                pillar.transform.localPosition = new Vector3(side * (DeckWidth * 0.38f), -3.4f, z);
                pillar.transform.localScale = new Vector3(0.45f, 6.6f, 0.45f);
                pillar.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                Object.Destroy(pillar.GetComponent<Collider>());
            }
        }

        void PlaceEnvironmentPulseTrigger()
        {
            var go = new GameObject("EnvPulse");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1f, Length * 0.5f);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(DeckWidth + 1f, 3f, 2.5f);
            go.AddComponent<EnvironmentZonePulse>();
        }

        void BuildSupports()
        {
            for (int i = 0; i < 3; i++)
                SpawnSupportPair(2f + i * 4f);
        }

        void BuildSupportsForGap()
        {
            SpawnSupportPair(2f);
            SpawnSupportPair(10f);
        }

        void SpawnSupportPair(float z) => SpawnSupportPairOn(transform, z);

        void BuildFloorWithGap()
        {
            var before = GameObject.CreatePrimitive(PrimitiveType.Cube);
            before.name = "FloorBefore";
            before.transform.SetParent(transform, false);
            before.transform.localPosition = new Vector3(0f, -0.12f, 2f);
            before.transform.localScale = new Vector3(DeckWidth, 0.38f, 4f);
            before.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
            Object.Destroy(before.GetComponent<Collider>());

            var after = GameObject.CreatePrimitive(PrimitiveType.Cube);
            after.name = "FloorAfter";
            after.transform.SetParent(transform, false);
            after.transform.localPosition = new Vector3(0f, -0.12f, 10f);
            after.transform.localScale = new Vector3(DeckWidth, 0.38f, 4f);
            after.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
            Object.Destroy(after.GetComponent<Collider>());

            // Dark ravine floor sits above the world ground so the gap still reads as lethal.
            var ravine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ravine.name = "RavineFloor";
            ravine.transform.SetParent(transform, false);
            ravine.transform.localPosition = new Vector3(0f, -3.4f, Length * 0.5f);
            ravine.transform.localScale = new Vector3(DeckWidth + 5f, 0.4f, 5.4f);
            ravine.GetComponent<Renderer>().sharedMaterial = RavineMat;
            Object.Destroy(ravine.GetComponent<Collider>());

            for (int i = 0; i < 4; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "RavineRock";
                shard.transform.SetParent(transform, false);
                shard.transform.localPosition = new Vector3(
                    Random.Range(-2f, 2f), Random.Range(-3.2f, -2.4f), Length * 0.5f + Random.Range(-1.8f, 1.8f));
                shard.transform.localRotation = Quaternion.Euler(
                    Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f));
                float s = Random.Range(0.5f, 1.1f);
                shard.transform.localScale = new Vector3(s, s * 0.8f, s * 1.2f);
                shard.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
                Object.Destroy(shard.GetComponent<Collider>());
            }
        }

        static Material _ravineMat;
        static Material RavineMat => _ravineMat ??= JunglePalette.Mat(new Color(0.05f, 0.06f, 0.05f), 0.1f);

        void BuildFloor()
        {
            const int slabs = 4;
            float slabLen = Length / slabs;
            for (int s = 0; s < slabs; s++)
            {
                // Split each slab into irregular paving stones for carved-path silhouette.
                int stones = 3;
                float stoneW = DeckWidth / stones;
                for (int x = 0; x < stones; x++)
                {
                    float jitterX = Random.Range(-0.06f, 0.06f);
                    float jitterZ = Random.Range(-0.08f, 0.08f);
                    float chip = Random.Range(0.88f, 0.98f);
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.name = "FloorStone";
                    floor.transform.SetParent(transform, false);
                    floor.transform.localPosition = new Vector3(
                        (x - (stones - 1) * 0.5f) * stoneW + jitterX,
                        -0.12f + Random.Range(-0.02f, 0.02f),
                        slabLen * (s + 0.5f) + jitterZ);
                    floor.transform.localScale = new Vector3(stoneW * chip, 0.4f, slabLen * chip * 0.92f);
                    floor.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
                    Object.Destroy(floor.GetComponent<Collider>());
                }

                // Center crack line every other slab.
                if (s % 2 == 0)
                {
                    var crack = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    crack.name = "PathCrack";
                    crack.transform.SetParent(transform, false);
                    crack.transform.localPosition = new Vector3(Random.Range(-0.4f, 0.4f), 0.05f, slabLen * (s + 0.5f));
                    crack.transform.localScale = new Vector3(0.08f, 0.05f, slabLen * 0.55f);
                    crack.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                    Object.Destroy(crack.GetComponent<Collider>());
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                var curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                curb.name = "PathCurb";
                curb.transform.SetParent(transform, false);
                curb.transform.localPosition = new Vector3(side * (DeckWidth * 0.5f + 0.08f), 0.08f, Length * 0.5f);
                curb.transform.localScale = new Vector3(0.22f, 0.22f, Length * 0.94f);
                curb.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                Object.Destroy(curb.GetComponent<Collider>());

                var moss = GameObject.CreatePrimitive(PrimitiveType.Cube);
                moss.name = "MossEdge";
                moss.transform.SetParent(transform, false);
                moss.transform.localPosition = new Vector3(side * (DeckWidth * 0.48f), 0.02f, Length * 0.5f);
                moss.transform.localScale = new Vector3(0.28f, 0.1f, Length * 0.92f);
                moss.GetComponent<Renderer>().sharedMaterial =
                    BiomeSystem.Current == BiomeId.DesertTombs ? BiomeSystem.FoliageMat
                    : BiomeSystem.Current == BiomeId.IceCaverns ? JunglePalette.Mat(new Color(0.78f, 0.9f, 0.98f), 0.4f)
                    : JunglePalette.StoneMoss;
                Object.Destroy(moss.GetComponent<Collider>());
            }
        }

        void BuildArch()
        {
            float z = Length * Random.Range(0.4f, 0.7f);
            // Outside the lane envelope (player outer extent ≈ 2.56).
            float halfW = 3.25f;
            for (int side = -1; side <= 1; side += 2)
            {
                var col = GameObject.CreatePrimitive(PrimitiveType.Cube);
                col.name = "ArchPost";
                col.transform.SetParent(transform, false);
                col.transform.localPosition = new Vector3(side * halfW, 1.45f, z);
                col.transform.localScale = new Vector3(0.55f, 2.9f, 0.55f);
                col.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
                Object.Destroy(col.GetComponent<Collider>());

                var baseBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseBlock.transform.SetParent(transform, false);
                baseBlock.transform.localPosition = new Vector3(side * halfW, 0.2f, z);
                baseBlock.transform.localScale = new Vector3(0.75f, 0.4f, 0.75f);
                baseBlock.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                Object.Destroy(baseBlock.GetComponent<Collider>());
            }

            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "ArchLintel";
            lintel.transform.SetParent(transform, false);
            lintel.transform.localPosition = new Vector3(0f, 3.05f, z);
            lintel.transform.localScale = new Vector3(halfW * 2f + 0.9f, 0.4f, 0.55f);
            lintel.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
            Object.Destroy(lintel.GetComponent<Collider>());

            SpawnRopeVine(new Vector3(Random.Range(-0.8f, 0.8f), 2.9f, z), Random.Range(1.4f, 2.2f), Random.Range(-6f, 6f));
        }

        void BuildHangingVines(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                float z = Length * Random.Range(0.15f, 0.85f);
                float x = side * Random.Range(3.1f, 3.8f);
                SpawnRopeVine(new Vector3(x, 3.2f, z), Random.Range(1.5f, 2.6f), side * Random.Range(4f, 12f));
            }
        }

        void SpawnRopeVine(Vector3 top, float length, float sway)
        {
            var root = new GameObject("RopeVine").transform;
            root.SetParent(transform, false);
            root.localPosition = top;
            root.localRotation = Quaternion.Euler(0f, 0f, sway);

            var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rope.transform.SetParent(root, false);
            rope.transform.localPosition = new Vector3(0f, -length * 0.5f, 0f);
            rope.transform.localScale = new Vector3(0.05f, length * 0.5f, 0.05f);
            rope.GetComponent<Renderer>().sharedMaterial = JunglePalette.Rope;
            Object.Destroy(rope.GetComponent<Collider>());

            int leaves = Random.Range(1, 3);
            for (int i = 0; i < leaves; i++)
            {
                var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leaf.transform.SetParent(root, false);
                leaf.transform.localPosition = new Vector3(Random.Range(-0.08f, 0.08f), -length * Random.Range(0.35f, 0.95f), 0f);
                leaf.transform.localScale = new Vector3(0.28f, 0.18f, 0.28f);
                leaf.GetComponent<Renderer>().sharedMaterial = JunglePalette.FoliageDark;
                Object.Destroy(leaf.GetComponent<Collider>());
            }
        }

        // Forest floor sits below the causeway so the deck still reads as a raised path.
        // Outer player extent ≈ LaneWidth + capsule radius = 2.56; keep trunks well clear.
        const float ForestFloorY = -2.4f;
        const float ForestInner = DeckWidth * 0.5f + 1.4f;
        const float ForestOuter = 18f;
        const float TrunkClearance = 3.5f;

        /// <summary>
        /// Ground bank plus trees flanking a path segment, in segment-local space so the
        /// forest turns with the route instead of drifting across it.
        /// </summary>
        /// <param name="onlySide">0 = both sides, -1 / +1 = that local X side only.</param>
        void BuildForestEdge(Transform parent, float zStart, float segLength, int onlySide = 0, bool plant = true)
        {
            if (segLength <= 0.5f) return;
            for (int side = -1; side <= 1; side += 2)
            {
                BuildForestFloor(parent, zStart, segLength, side);
                if (plant && (onlySide == 0 || side == onlySide))
                    PlantForestStrip(parent, zStart, segLength, side);
            }
        }

        void BuildForestFloor(Transform parent, float zStart, float segLength, int side)
        {
            float width = ForestOuter - ForestInner;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ForestFloor";
            floor.transform.SetParent(parent, false);
            // Tiny height jitter keeps overlapping banks at corners from z-fighting.
            float top = ForestFloorY + Random.Range(-0.05f, 0.05f);
            floor.transform.localPosition = new Vector3(
                side * (ForestInner + width * 0.5f), top - 2.5f, zStart + segLength * 0.5f);
            floor.transform.localScale = new Vector3(width, 5f, segLength);
            Material bankMat = BiomeSystem.Current switch
            {
                BiomeId.DesertTombs => BiomeSystem.PathMat,
                BiomeId.IceCaverns => JunglePalette.Mat(new Color(0.82f, 0.9f, 0.96f), 0.5f),
                BiomeId.CaveMines => JunglePalette.Charcoal,
                BiomeId.VolcanicCrater => JunglePalette.Dirt,
                _ => JunglePalette.Grass
            };
            floor.GetComponent<Renderer>().sharedMaterial = bankMat;
            StripCollider(floor);
        }

        /// <summary>
        /// Scenery colliders are stripped immediately; a deferred Destroy would leave them
        /// live for one physics step and shove the runner.
        /// </summary>
        static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col == null) return;
            col.enabled = false;
            Object.Destroy(col);
        }

        void PlantForestStrip(Transform parent, float zStart, float segLength, int side)
        {
            int trees = Mathf.Max(2, Mathf.RoundToInt(segLength / 5.5f));
            for (int i = 0; i < trees; i++)
            {
                float z = zStart + (i + Random.Range(0.15f, 0.85f)) * (segLength / trees);
                // Nearer trees stay smaller so nothing looms over the lanes.
                float x = ForestInner + TrunkClearance + Random.Range(0f, ForestOuter - ForestInner - TrunkClearance - 1.5f);
                float near = Mathf.InverseLerp(ForestInner + TrunkClearance, ForestOuter, x);
                float scale = Mathf.Lerp(0.75f, 1.45f, near) * Random.Range(0.85f, 1.15f);
                SpawnForestTree(parent, new Vector3(side * x, ForestFloorY, z), scale, Random.value < 0.55f);
            }

            int shrubs = Mathf.Max(2, Mathf.RoundToInt(segLength / 6.5f));
            for (int i = 0; i < shrubs; i++)
            {
                float z = zStart + Random.Range(0f, segLength);
                float x = ForestInner + TrunkClearance * 0.55f + Random.Range(0f, 5.5f);
                if (Random.value < 0.32f)
                    SpawnMossRock(parent, new Vector3(side * x, ForestFloorY, z));
                else
                    SpawnShrub(parent, new Vector3(side * x, ForestFloorY, z));
            }

            // Bank waterfall cascading into a pool beside the path — tall enough to read from chase cam.
            // Skip arid / sealed biomes where cascading water would break the look.
            bool allowFalls = BiomeSystem.Current == BiomeId.JungleRuins
                              || BiomeSystem.Current == BiomeId.IceCaverns
                              || BiomeSystem.Current == BiomeId.CaveMines;
            if (allowFalls && segLength > 5f && Random.value < 0.28f)
            {
                float wz = zStart + Random.Range(segLength * 0.25f, segLength * 0.75f);
                float wx = side * (ForestInner + Random.Range(6.5f, 10f));
                SpawnWaterfall(parent, new Vector3(wx, ForestFloorY, wz), Random.Range(6.2f, 9f), side);
            }
        }

        void SpawnForestTree(Transform parent, Vector3 basePos, float scale, bool conifer)
        {
            // Biome props: desert cacti / ice pillars / cave spires / volcanic ash trunks.
            if (BiomeSystem.Current == BiomeId.DesertTombs)
            {
                SpawnDesertCactus(parent, basePos, scale);
                return;
            }
            if (BiomeSystem.Current == BiomeId.IceCaverns)
            {
                SpawnIcePillar(parent, basePos, scale);
                return;
            }
            if (BiomeSystem.Current == BiomeId.CaveMines)
            {
                SpawnCaveSpire(parent, basePos, scale);
                return;
            }
            if (BiomeSystem.Current == BiomeId.VolcanicCrater)
            {
                SpawnAshTrunk(parent, basePos, scale);
                return;
            }

            var root = new GameObject(conifer ? "Pine" : "BroadleafTree").transform;
            root.SetParent(parent, false);
            root.localPosition = basePos;
            root.localRotation = Quaternion.Euler(Random.Range(-2.5f, 2.5f), Random.Range(0f, 360f), Random.Range(-2.5f, 2.5f));

            float trunkH = (conifer ? Random.Range(2.9f, 3.9f) : Random.Range(2.4f, 3.2f)) * scale;
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root, false);
            // Sunk slightly so the base never floats above uneven ground.
            trunk.transform.localPosition = new Vector3(0f, trunkH * 0.5f - 0.25f, 0f);
            trunk.transform.localScale = new Vector3(0.26f * scale, trunkH * 0.5f, 0.26f * scale);
            trunk.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
            StripCollider(trunk);

            const int tiers = 3;
            for (int i = 0; i < tiers; i++)
            {
                float t = i / (float)(tiers - 1);
                var piece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                piece.transform.SetParent(root, false);

                if (conifer)
                {
                    // Tiers overlap vertically so the canopy reads solid, never gapped.
                    float w = Mathf.Lerp(2.3f, 0.8f, t) * scale;
                    piece.transform.localPosition = new Vector3(0f, trunkH * 0.6f + i * 0.95f * scale, 0f);
                    piece.transform.localScale = new Vector3(w, w * 0.95f, w);
                }
                else
                {
                    float w = Random.Range(1.7f, 2.4f) * scale;
                    piece.transform.localPosition = new Vector3(
                        Random.Range(-0.5f, 0.5f) * scale,
                        trunkH * 0.85f + Random.Range(-0.1f, 0.6f) * scale,
                        Random.Range(-0.5f, 0.5f) * scale);
                    piece.transform.localScale = new Vector3(w, w * 0.82f, w);
                }

                piece.GetComponent<Renderer>().sharedMaterial =
                    i == 0 ? JunglePalette.FoliageDark
                    : i == tiers - 1 ? JunglePalette.FoliageLight
                    : JunglePalette.Foliage;
                StripCollider(piece);
            }
        }

        void SpawnDesertCactus(Transform parent, Vector3 basePos, float scale)
        {
            // Mix cactus with sandstone obelisks / dune mounds so desert reads beyond recolored trees.
            float roll = Random.value;
            if (roll < 0.35f)
            {
                SpawnDesertObelisk(parent, basePos, scale);
                return;
            }
            if (roll < 0.55f)
            {
                SpawnDesertDune(parent, basePos, scale);
                return;
            }

            var root = new GameObject("Cactus").transform;
            root.SetParent(parent, false);
            root.localPosition = basePos;
            float h = Random.Range(2.2f, 3.4f) * scale;
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            trunk.transform.SetParent(root, false);
            trunk.transform.localPosition = new Vector3(0f, h * 0.45f, 0f);
            trunk.transform.localScale = new Vector3(0.55f * scale, h * 0.45f, 0.55f * scale);
            trunk.GetComponent<Renderer>().sharedMaterial = BiomeSystem.FoliageMat;
            StripCollider(trunk);
            for (int a = 0; a < 2; a++)
            {
                float side = a == 0 ? -1f : 1f;
                var arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                arm.transform.SetParent(root, false);
                arm.transform.localPosition = new Vector3(side * 0.55f * scale, h * 0.55f, 0f);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, side * -55f);
                arm.transform.localScale = new Vector3(0.35f * scale, 0.55f * scale, 0.35f * scale);
                arm.GetComponent<Renderer>().sharedMaterial = BiomeSystem.FoliageMat;
                StripCollider(arm);
            }
        }

        void SpawnDesertObelisk(Transform parent, Vector3 basePos, float scale)
        {
            var root = new GameObject("DesertObelisk").transform;
            root.SetParent(parent, false);
            root.localPosition = basePos;
            float h = Random.Range(2.8f, 4.6f) * scale;
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shaft.transform.SetParent(root, false);
            shaft.transform.localPosition = new Vector3(0f, h * 0.45f, 0f);
            shaft.transform.localScale = new Vector3(0.55f * scale, h * 0.9f, 0.55f * scale);
            shaft.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
            StripCollider(shaft);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.transform.SetParent(root, false);
            cap.transform.localPosition = new Vector3(0f, h * 0.95f, 0f);
            cap.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            cap.transform.localScale = new Vector3(0.7f * scale, 0.35f * scale, 0.7f * scale);
            cap.GetComponent<Renderer>().sharedMaterial = BiomeSystem.AccentMat;
            StripCollider(cap);
        }

        void SpawnDesertDune(Transform parent, Vector3 basePos, float scale)
        {
            var dune = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dune.name = "SandDune";
            dune.transform.SetParent(parent, false);
            dune.transform.localPosition = basePos + new Vector3(0f, 0.35f * scale, 0f);
            dune.transform.localScale = new Vector3(2.4f * scale, 0.9f * scale, 1.8f * scale);
            dune.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
            StripCollider(dune);
        }

        void SpawnIcePillar(Transform parent, Vector3 basePos, float scale)
        {
            if (Random.value < 0.4f)
            {
                SpawnIceCrystalCluster(parent, basePos, scale);
                return;
            }

            var root = new GameObject("IcePillar").transform;
            root.SetParent(parent, false);
            root.localPosition = basePos;
            float h = Random.Range(2.4f, 4.2f) * scale;
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.transform.SetParent(root, false);
            pillar.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            pillar.transform.localScale = new Vector3(0.45f * scale, h * 0.5f, 0.45f * scale);
            pillar.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.72f, 0.88f, 0.98f), 0.65f, 0.2f);
            StripCollider(pillar);
            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.transform.SetParent(root, false);
            tip.transform.localPosition = new Vector3(0f, h + 0.15f * scale, 0f);
            tip.transform.localScale = Vector3.one * (0.55f * scale);
            tip.GetComponent<Renderer>().sharedMaterial = BiomeSystem.AccentMat;
            StripCollider(tip);
        }

        void SpawnIceCrystalCluster(Transform parent, Vector3 basePos, float scale)
        {
            var root = new GameObject("IceCrystals").transform;
            root.SetParent(parent, false);
            root.localPosition = basePos;
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.transform.SetParent(root, false);
                float h = Random.Range(0.9f, 2.2f) * scale;
                shard.transform.localPosition = new Vector3(
                    Random.Range(-0.55f, 0.55f) * scale, h * 0.45f, Random.Range(-0.55f, 0.55f) * scale);
                shard.transform.localRotation = Quaternion.Euler(
                    Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-18f, 18f));
                shard.transform.localScale = new Vector3(0.22f * scale, h, 0.22f * scale);
                shard.GetComponent<Renderer>().sharedMaterial =
                    JunglePalette.Mat(new Color(0.65f, 0.85f, 0.98f), 0.8f, 0.35f);
                StripCollider(shard);
            }
        }

        void SpawnCaveSpire(Transform parent, Vector3 basePos, float scale)
        {
            var root = new GameObject("CaveSpire").transform;
            root.SetParent(parent, false);
            root.localPosition = basePos;
            float h = Random.Range(2f, 3.6f) * scale;
            var spire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spire.transform.SetParent(root, false);
            spire.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            spire.transform.localScale = new Vector3(0.7f * scale, h * 0.5f, 0.7f * scale);
            spire.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
            StripCollider(spire);
        }

        void SpawnAshTrunk(Transform parent, Vector3 basePos, float scale)
        {
            var root = new GameObject("AshTrunk").transform;
            root.SetParent(parent, false);
            root.localPosition = basePos;
            float h = Random.Range(2.2f, 3.5f) * scale;
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(root, false);
            trunk.transform.localPosition = new Vector3(0f, h * 0.45f, 0f);
            trunk.transform.localScale = new Vector3(0.35f * scale, h * 0.45f, 0.35f * scale);
            trunk.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            StripCollider(trunk);
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.transform.SetParent(root, false);
            canopy.transform.localPosition = new Vector3(0f, h * 0.95f, 0f);
            canopy.transform.localScale = Vector3.one * (1.4f * scale);
            canopy.GetComponent<Renderer>().sharedMaterial = BiomeSystem.FoliageMat;
            StripCollider(canopy);
            if (Random.value < 0.4f)
            {
                var ember = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ember.transform.SetParent(root, false);
                ember.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                ember.transform.localScale = Vector3.one * (0.4f * scale);
                ember.GetComponent<Renderer>().sharedMaterial = JunglePalette.Ember;
                StripCollider(ember);
            }
        }

        void SpawnShrub(Transform parent, Vector3 basePos)
        {
            if (BiomeSystem.Current == BiomeId.DesertTombs)
            {
                SpawnDesertDune(parent, basePos, Random.Range(0.55f, 0.9f));
                return;
            }
            if (BiomeSystem.Current == BiomeId.IceCaverns)
            {
                SpawnIceCrystalCluster(parent, basePos, Random.Range(0.55f, 0.95f));
                return;
            }

            var root = new GameObject("Shrub").transform;
            root.SetParent(parent, false);
            root.localPosition = basePos;
            root.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            int blobs = Random.Range(2, 4);
            for (int i = 0; i < blobs; i++)
            {
                float w = Random.Range(0.6f, 1.15f);
                var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blob.transform.SetParent(root, false);
                blob.transform.localPosition = new Vector3(
                    Random.Range(-0.4f, 0.4f), w * 0.3f, Random.Range(-0.4f, 0.4f));
                blob.transform.localScale = new Vector3(w, w * 0.7f, w);
                blob.GetComponent<Renderer>().sharedMaterial =
                    Random.value < 0.5f ? JunglePalette.Undergrowth : JunglePalette.FoliageDark;
                StripCollider(blob);
            }
        }

        void SpawnMossRock(Transform parent, Vector3 basePos)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "MossRock";
            rock.transform.SetParent(parent, false);
            float s = Random.Range(0.5f, 1.3f);
            rock.transform.localPosition = basePos + new Vector3(0f, s * 0.25f, 0f);
            rock.transform.localRotation = Quaternion.Euler(
                Random.Range(-18f, 18f), Random.Range(0f, 360f), Random.Range(-18f, 18f));
            rock.transform.localScale = new Vector3(s, s * 0.65f, s * Random.Range(0.8f, 1.4f));
            Material mat = BiomeSystem.Current switch
            {
                BiomeId.DesertTombs => BiomeSystem.StoneMat,
                BiomeId.IceCaverns => JunglePalette.Mat(new Color(0.75f, 0.88f, 0.98f), 0.6f, 0.25f),
                BiomeId.VolcanicCrater => JunglePalette.Charcoal,
                _ => Random.value < 0.5f ? JunglePalette.StoneMoss : JunglePalette.Stone
            };
            rock.GetComponent<Renderer>().sharedMaterial = mat;
            StripCollider(rock);
        }

        /// <summary>
        /// Closes off the straight-ahead option at a T-junction with a lethal kerb and
        /// forest beyond the dead-end — trees stay clear of the approach lanes.
        /// </summary>
        /// <summary>
        /// Purely visual dead end. Nothing may sit inside the corner sweep: while the yaw
        /// lerps through the turn the lane offset rotates into +Z, carrying the runner up to
        /// JunctionApproach + LaneWidth + capsule radius past the corner.
        /// </summary>
        void BuildJunctionThicket()
        {
            float sweep = JunctionApproach + PlayerController.LaneWidth + 0.8f;
            float z = sweep + 1.2f;

            for (int i = 0; i < 5; i++)
            {
                float x = -6.5f + i * 3.25f;
                SpawnForestTree(transform,
                    new Vector3(x, ForestFloorY, z + Random.Range(0f, 2.2f)),
                    Random.Range(1.05f, 1.45f), i % 2 == 0);
            }
            for (int i = 0; i < 5; i++)
                SpawnShrub(transform, new Vector3(Random.Range(-5.5f, 5.5f), ForestFloorY, z + Random.Range(-0.6f, 2.6f)));
            for (int i = 0; i < 3; i++)
                SpawnMossRock(transform, new Vector3(Random.Range(-4f, 4f), ForestFloorY, z + Random.Range(-0.8f, 1.4f)));
        }

        /// <summary>
        /// Hard guarantee that turns and junctions carry no lethal content, whatever the
        /// decoration helpers spawned.
        /// </summary>
        void StripHazardsFromTile()
        {
            foreach (var obs in GetComponentsInChildren<Obstacle>(true))
                Destroy(obs);
            foreach (var dyn in GetComponentsInChildren<DynamicHazard>(true))
                Destroy(dyn);
            foreach (var kill in GetComponentsInChildren<GapKillZone>(true))
                Destroy(kill.gameObject);
        }

        void BuildFloorWithRiverChannel()
        {
            GetRiverChannel(out float channelStart, out float channelEnd, out _);
            float beforeLen = channelStart;
            float afterLen = Length - channelEnd;

            var before = GameObject.CreatePrimitive(PrimitiveType.Cube);
            before.name = "FloorBeforeRiver";
            before.transform.SetParent(transform, false);
            before.transform.localPosition = new Vector3(0f, -0.12f, beforeLen * 0.5f);
            before.transform.localScale = new Vector3(DeckWidth, 0.38f, beforeLen);
            before.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
            Object.Destroy(before.GetComponent<Collider>());

            var after = GameObject.CreatePrimitive(PrimitiveType.Cube);
            after.name = "FloorAfterRiver";
            after.transform.SetParent(transform, false);
            after.transform.localPosition = new Vector3(0f, -0.12f, channelEnd + afterLen * 0.5f);
            after.transform.localScale = new Vector3(DeckWidth, 0.38f, afterLen);
            after.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
            Object.Destroy(after.GetComponent<Collider>());
        }

        void BuildForestEdgeForRiver()
        {
            GetRiverChannel(out float channelStart, out float channelEnd, out _);
            BuildForestEdge(transform, 0f, channelStart);
            BuildForestEdge(transform, channelEnd, Length - channelEnd);
        }

        void GetRiverChannel(out float channelStart, out float channelEnd, out float channelLen)
        {
            float profileSpeed = DifficultyProfile.For(_runDifficulty).BaseSpeed;
            channelLen = Mathf.Clamp(6.5f + profileSpeed * 0.22f, 8f, 9.2f);
            channelStart = (Length - channelLen) * 0.5f;
            channelEnd = channelStart + channelLen;
        }

        void BuildRiverCrossing()
        {
            WaterFlow.Ensure();
            GetRiverChannel(out float channelStart, out float channelEnd, out float channelLen);
            float channelMid = (channelStart + channelEnd) * 0.5f;

            var mode = TileSpawner.Instance != null
                ? TileSpawner.Instance.PickRiverMode(_runDifficulty)
                : RiverCrossingMode.Jump;

            var marker = gameObject.AddComponent<RiverCrossingMarker>();
            marker.Configure(mode, channelStart, channelEnd, PathStartDistance, _runDifficulty);

            BuildNaturalRiverShell(channelStart, channelEnd, channelLen, channelMid);

            switch (mode)
            {
                case RiverCrossingMode.Boat:
                    BuildRiverBoatCrossing(marker, channelStart, channelEnd, channelMid);
                    break;
                case RiverCrossingMode.Rope:
                    BuildRiverRopeCrossing(marker, channelStart, channelEnd);
                    break;
                case RiverCrossingMode.Swim:
                    BuildRiverSwimCrossing(marker, channelStart, channelEnd, channelMid, channelLen);
                    break;
                default:
                    BuildRiverJumpCrossing(channelStart, channelEnd, channelLen);
                    break;
            }
        }

        void BuildRiverSwimCrossing(RiverCrossingMarker marker, float channelStart, float channelEnd,
            float channelMid, float channelLen)
        {
            // Submerged coin lane under the surface sheet.
            for (int i = 0; i < 5; i++)
            {
                float z = Mathf.Lerp(channelStart + 0.8f, channelEnd - 0.6f, i / 4f);
                CollectibleCoin.Create(transform, new Vector3(0f, ForestFloorY + 0.55f, z));
            }

            // Floating barrels / logs that force lane changes while swimming.
            int debris = _runDifficulty == RunDifficulty.Easy ? 2
                : _runDifficulty == RunDifficulty.Hard ? 4 : 3;
            int usedMask = 0;
            for (int i = 0; i < debris; i++)
            {
                float z = Mathf.Lerp(channelStart + 1.2f, channelEnd - 1.2f, (i + 1f) / (debris + 1f));
                int lane = Random.Range(0, 3);
                int tries = 0;
                while ((usedMask & (1 << lane)) != 0 && tries++ < 5)
                    lane = Random.Range(0, 3);
                usedMask |= 1 << lane;
                Obstacle.CreateBoatDebris(transform, z, lane);
            }

            // Bubbles rising from the swim channel.
            for (int i = 0; i < 8; i++)
            {
                var bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bubble.name = "SwimBubble";
                bubble.transform.SetParent(transform, false);
                bubble.transform.localPosition = new Vector3(
                    Random.Range(-DeckWidth * 0.35f, DeckWidth * 0.35f),
                    ForestFloorY + Random.Range(0.2f, 1.1f),
                    channelStart + channelLen * Random.Range(0.1f, 0.9f));
                float s = Random.Range(0.18f, 0.38f);
                bubble.transform.localScale = new Vector3(s, s, s);
                bubble.GetComponent<Renderer>().sharedMaterial = JunglePalette.Foam;
                StripCollider(bubble);
            }

            var mountGo = new GameObject("SwimMount");
            mountGo.transform.SetParent(transform, false);
            mountGo.transform.localPosition = new Vector3(0f, 0.6f, channelStart + 0.15f);
            var mountCol = mountGo.AddComponent<BoxCollider>();
            mountCol.isTrigger = true;
            mountCol.size = new Vector3(DeckWidth + 0.5f, 2.2f, 1.6f);
            var mount = mountGo.AddComponent<RiverSwimMount>();
            mount.Marker = marker;
            mount.SwimDepth = ForestFloorY + 0.35f;

            if (_runDifficulty != RunDifficulty.Easy && Random.value < 0.4f)
                GemPickup.Create(transform, new Vector3(-PlayerController.LaneWidth, ForestFloorY + 0.7f, channelMid));
        }

        void BuildNaturalRiverShell(float channelStart, float channelEnd, float channelLen, float channelMid)
        {
            // Deep riverbed under a wide flowing surface.
            float riverWidth = ForestOuter * 2.4f + DeckWidth;
            var bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bed.name = "RiverBed";
            bed.transform.SetParent(transform, false);
            bed.transform.localPosition = new Vector3(0f, ForestFloorY - 1.1f, channelMid);
            bed.transform.localScale = new Vector3(riverWidth, 1.4f, channelLen + 2.2f);
            bed.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.08f, 0.14f, 0.16f), 0.2f);
            StripCollider(bed);

            var river = GameObject.CreatePrimitive(PrimitiveType.Cube);
            river.name = "RiverSurface";
            river.transform.SetParent(transform, false);
            river.transform.localPosition = new Vector3(0f, ForestFloorY - 0.2f, channelMid);
            river.transform.localScale = new Vector3(riverWidth, 0.45f, channelLen + 1.8f);
            river.GetComponent<Renderer>().sharedMaterial = JunglePalette.RiverWater;
            StripCollider(river);

            // Extra near-surface sheet so depth/flow reads from the chase camera.
            var flow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flow.name = "RiverFlowSheet";
            flow.transform.SetParent(transform, false);
            flow.transform.localPosition = new Vector3(0f, ForestFloorY + 0.05f, channelMid);
            flow.transform.localScale = new Vector3(riverWidth * 0.92f, 0.08f, channelLen + 0.8f);
            flow.GetComponent<Renderer>().sharedMaterial = JunglePalette.RiverWater;
            StripCollider(flow);

            int fallSide = Random.value < 0.5f ? -1 : 1;
            SpawnWaterfall(transform,
                new Vector3(fallSide * (ForestInner + 8.5f), ForestFloorY, channelStart + channelLen * 0.2f),
                Random.Range(7f, 9.5f), fallSide);

            // Shore foam strips along both banks — reads as whitewater from the chase cam.
            for (int side = -1; side <= 1; side += 2)
            {
                var shore = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shore.name = "ShoreFoam";
                shore.transform.SetParent(transform, false);
                shore.transform.localPosition = new Vector3(
                    side * (DeckWidth * 0.5f + 1.1f),
                    ForestFloorY + 0.12f,
                    channelMid);
                shore.transform.localScale = new Vector3(1.4f, 0.12f, channelLen + 0.6f);
                shore.GetComponent<Renderer>().sharedMaterial = JunglePalette.Foam;
                StripCollider(shore);
            }

            var foamRoot = new GameObject("RiverFoamBurst").transform;
            foamRoot.SetParent(transform, false);
            foamRoot.localPosition = new Vector3(0f, ForestFloorY + 0.1f, channelMid);
            var fx = foamRoot.gameObject.AddComponent<RiverFoamAnimator>();
            fx.Build(foamRoot, riverWidth, channelLen);

            // Soft river mist so boat/swim channels read as humid crossings.
            var mistRoot = new GameObject("RiverMist").transform;
            mistRoot.SetParent(transform, false);
            mistRoot.localPosition = new Vector3(0f, ForestFloorY + 0.55f, channelMid);
            var mist = mistRoot.gameObject.AddComponent<RiverMistAnimator>();
            mist.Build(mistRoot, riverWidth, channelLen, 9);

            // Second thinner flow sheet for layered current.
            var flow2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flow2.name = "RiverFlowSheetFast";
            flow2.transform.SetParent(transform, false);
            flow2.transform.localPosition = new Vector3(0f, ForestFloorY + 0.12f, channelMid);
            flow2.transform.localScale = new Vector3(riverWidth * 0.7f, 0.05f, channelLen * 0.85f);
            flow2.GetComponent<Renderer>().sharedMaterial = JunglePalette.Foam;
            StripCollider(flow2);

            // Scroll the surface sheet so the crossing never looks like a static cube.
            var flowAnim = river.AddComponent<RiverSurfaceScroll>();
            flowAnim.Bind(river.GetComponent<Renderer>(), flow.GetComponent<Renderer>());

            for (int i = 0; i < 6; i++)
            {
                int side = Random.value < 0.5f ? -1 : 1;
                float z = channelStart + Random.Range(-0.4f, channelLen + 0.4f);
                SpawnMossRock(transform, new Vector3(side * Random.Range(ForestInner + 1.5f, ForestOuter - 1f), ForestFloorY, z));
                if (Random.value < 0.65f)
                    SpawnShrub(transform, new Vector3(side * Random.Range(ForestInner + 2f, ForestOuter - 2f), ForestFloorY, z + Random.Range(-0.8f, 0.8f)));
            }
        }

        void BuildRiverBoatCrossing(RiverCrossingMarker marker, float channelStart, float channelEnd, float channelMid)
        {
            // Near-bank dock.
            var dock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dock.name = "BoatDock";
            dock.transform.SetParent(transform, false);
            dock.transform.localPosition = new Vector3(0f, 0.08f, channelStart - 0.35f);
            dock.transform.localScale = new Vector3(DeckWidth * 0.85f, 0.18f, 1.4f);
            dock.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
            StripCollider(dock);

            var farDock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            farDock.name = "BoatDockFar";
            farDock.transform.SetParent(transform, false);
            farDock.transform.localPosition = new Vector3(0f, 0.08f, channelEnd + 0.35f);
            farDock.transform.localScale = new Vector3(DeckWidth * 0.85f, 0.18f, 1.4f);
            farDock.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
            StripCollider(farDock);

            var rideGo = new GameObject("BoatRide");
            rideGo.transform.SetParent(transform, false);
            var ride = rideGo.AddComponent<RiverBoatRide>();
            ride.Marker = marker;
            ride.BuildVisual(rideGo.transform);

            var mountGo = new GameObject("BoatMount");
            mountGo.transform.SetParent(transform, false);
            mountGo.transform.localPosition = new Vector3(0f, 1f, channelStart + 0.2f);
            var mountCol = mountGo.AddComponent<BoxCollider>();
            mountCol.isTrigger = true;
            mountCol.size = new Vector3(DeckWidth + 0.5f, 2.5f, 1.8f);
            var mount = mountGo.AddComponent<RiverBoatMount>();
            mount.Marker = marker;
            mount.Ride = ride;

            // Water kills under the channel — ignored while mounted.
            for (int lane = 0; lane < 3; lane++)
                GapKillZone.Create(transform, channelMid, lane, channelEnd - channelStart);

            // Lane debris the player must dodge from the boat.
            int hazardCount = _runDifficulty == RunDifficulty.Easy ? 1
                : _runDifficulty == RunDifficulty.Hard ? 3 : 2;
            int usedMask = 0;
            for (int i = 0; i < hazardCount; i++)
            {
                int lane = Random.Range(0, 3);
                int tries = 0;
                while ((usedMask & (1 << lane)) != 0 && tries++ < 5)
                    lane = Random.Range(0, 3);
                // Keep at least one open lane across the cluster.
                if (i == hazardCount - 1)
                {
                    if (usedMask == 0b011) lane = 2;
                    else if (usedMask == 0b101) lane = 1;
                    else if (usedMask == 0b110) lane = 0;
                }
                usedMask |= 1 << lane;
                float z = Mathf.Lerp(channelStart + 1.5f, channelEnd - 1.5f, (i + 1f) / (hazardCount + 1f));
                Obstacle.CreateBoatDebris(transform, z, lane);
            }

            CollectibleCoin.Create(transform, new Vector3(0f, 1.4f, channelStart + 0.8f));
            CollectibleCoin.Create(transform, new Vector3(0f, 1.5f, channelMid));
            CollectibleCoin.Create(transform, new Vector3(0f, 1.4f, channelEnd - 0.5f));
        }

        void BuildRiverRopeCrossing(RiverCrossingMarker marker, float channelStart, float channelEnd)
        {
            float grabZ = channelStart + 0.35f;
            float landZ = channelEnd - 0.25f;

            var swingGo = new GameObject("RopeSwing");
            swingGo.transform.SetParent(transform, false);
            swingGo.transform.localPosition = new Vector3(0f, 1.2f, grabZ);
            var swingCol = swingGo.AddComponent<BoxCollider>();
            swingCol.isTrigger = true;
            swingCol.size = new Vector3(DeckWidth + 0.6f, 3f, 1.6f);
            var swing = swingGo.AddComponent<RiverRopeSwing>();
            swing.Marker = marker;
            swing.SwingDuration = _runDifficulty == RunDifficulty.Hard ? 1.0f
                : _runDifficulty == RunDifficulty.Easy ? 1.35f : 1.15f;
            swing.ReleaseWindowStart = _runDifficulty == RunDifficulty.Easy ? 0.45f : 0.52f;
            swing.ReleaseWindowEnd = _runDifficulty == RunDifficulty.Hard ? 0.78f : 0.85f;
            swing.ApexHeight = 2.7f;
            swing.BuildVisual(transform, grabZ, landZ);

            // Landing pad on far bank.
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "RopeLanding";
            pad.transform.SetParent(transform, false);
            pad.transform.localPosition = new Vector3(0f, 0.06f, landZ);
            pad.transform.localScale = new Vector3(DeckWidth * 0.9f, 0.16f, 1.6f);
            pad.GetComponent<Renderer>().sharedMaterial = JunglePalette.StoneMoss;
            StripCollider(pad);

            float mid = (channelStart + channelEnd) * 0.5f;
            for (int lane = 0; lane < 3; lane++)
                GapKillZone.Create(transform, mid, lane, channelEnd - channelStart);

            // Coin arc telegraphing the swing apex.
            CollectibleCoin.Create(transform, new Vector3(0f, 1.3f, grabZ));
            CollectibleCoin.Create(transform, new Vector3(0f, 2.8f, mid));
            CollectibleCoin.Create(transform, new Vector3(0f, 1.4f, landZ));
        }

        void BuildRiverJumpCrossing(float channelStart, float channelEnd, float channelLen)
        {
            float row1 = channelStart + channelLen * 0.35f;
            float row2 = channelStart + channelLen * 0.7f;
            bool[,] stones = PickRiverStoneLanes();

            for (int lane = 0; lane < 3; lane++)
            {
                if (stones[0, lane]) SpawnSteppingStone(lane, row1);
                else GapKillZone.Create(transform, row1, lane, 1.8f);

                if (stones[1, lane]) SpawnSteppingStone(lane, row2);
                else GapKillZone.Create(transform, row2, lane, 1.8f);
            }

            float gapA = (channelStart + row1) * 0.5f;
            float gapB = (row1 + row2) * 0.5f;
            float gapC = (row2 + channelEnd) * 0.5f;
            float segA = Mathf.Max(1.2f, row1 - channelStart - 0.6f);
            float segB = Mathf.Max(1.2f, row2 - row1 - 0.6f);
            float segC = Mathf.Max(1.2f, channelEnd - row2 - 0.6f);
            for (int lane = 0; lane < 3; lane++)
            {
                GapKillZone.Create(transform, gapA, lane, segA);
                GapKillZone.Create(transform, gapC, lane, segC);
                if (!stones[0, lane] || !stones[1, lane])
                    GapKillZone.Create(transform, gapB, lane, segB);
            }

            int guideLane = 1;
            for (int r = 0; r < 2; r++)
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    if (stones[r, lane]) { guideLane = lane; break; }
                }
            }
            float gx = (guideLane - 1) * PlayerController.LaneWidth;
            CollectibleCoin.Create(transform, new Vector3(gx, 1.35f, channelStart + 0.6f));
            CollectibleCoin.Create(transform, new Vector3(gx, 1.55f, row1));
            CollectibleCoin.Create(transform, new Vector3(
                (PickGuideLane(stones, 1) - 1) * PlayerController.LaneWidth, 1.55f, row2));
            CollectibleCoin.Create(transform, new Vector3(gx, 1.35f, channelEnd - 0.6f));
        }

        static int PickGuideLane(bool[,] stones, int row)
        {
            for (int lane = 0; lane < 3; lane++)
                if (stones[row, lane]) return lane;
            return 1;
        }

        bool[,] PickRiverStoneLanes()
        {
            var stones = new bool[2, 3];
            if (_runDifficulty == RunDifficulty.Easy)
            {
                for (int r = 0; r < 2; r++)
                    for (int l = 0; l < 3; l++)
                        stones[r, l] = true;
                return stones;
            }

            if (_runDifficulty == RunDifficulty.Medium)
            {
                int skip1 = Random.Range(0, 3);
                int skip2 = (skip1 + Random.Range(1, 3)) % 3;
                for (int l = 0; l < 3; l++)
                {
                    stones[0, l] = l != skip1;
                    stones[1, l] = l != skip2;
                }
                return stones;
            }

            // Hard: one or two stones per row, offset to force a lane change.
            int a = Random.Range(0, 3);
            int b = (a + (Random.value < 0.55f ? 1 : 2)) % 3;
            stones[0, a] = true;
            if (Random.value < 0.4f) stones[0, (a + 1) % 3] = true;
            stones[1, b] = true;
            if (Random.value < 0.35f) stones[1, (b + 2) % 3] = true;
            return stones;
        }

        void SpawnSteppingStone(int lane, float z)
        {
            float x = (lane - 1) * PlayerController.LaneWidth;
            float pillarH = -ForestFloorY + 0.05f;

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "StonePillar";
            pillar.transform.SetParent(transform, false);
            pillar.transform.localPosition = new Vector3(x, ForestFloorY + pillarH * 0.5f, z);
            pillar.transform.localScale = new Vector3(1.15f, pillarH * 0.5f, 1.15f);
            pillar.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
            StripCollider(pillar);

            var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            top.name = "StoneTop";
            top.transform.SetParent(transform, false);
            top.transform.localPosition = new Vector3(x, 0.08f, z);
            top.transform.localScale = new Vector3(1.55f, 0.12f, 1.55f);
            top.GetComponent<Renderer>().sharedMaterial = JunglePalette.StoneMoss;
            StripCollider(top);
        }

        const string FireDeath = "Burned in the fire";

        void GetFireChannel(out float channelStart, out float channelEnd, out float channelLen)
        {
            float profileSpeed = DifficultyProfile.For(_runDifficulty).BaseSpeed;
            channelLen = Mathf.Clamp(6.2f + profileSpeed * 0.2f, 7.5f, 9f);
            channelStart = (Length - channelLen) * 0.5f;
            channelEnd = channelStart + channelLen;
        }

        void BuildFloorWithFireChannel()
        {
            GetFireChannel(out float channelStart, out float channelEnd, out _);
            float beforeLen = channelStart;
            float afterLen = Length - channelEnd;

            var before = GameObject.CreatePrimitive(PrimitiveType.Cube);
            before.name = "FloorBeforeFire";
            before.transform.SetParent(transform, false);
            before.transform.localPosition = new Vector3(0f, -0.12f, beforeLen * 0.5f);
            before.transform.localScale = new Vector3(DeckWidth, 0.38f, beforeLen);
            before.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
            Object.Destroy(before.GetComponent<Collider>());

            var after = GameObject.CreatePrimitive(PrimitiveType.Cube);
            after.name = "FloorAfterFire";
            after.transform.SetParent(transform, false);
            after.transform.localPosition = new Vector3(0f, -0.12f, channelEnd + afterLen * 0.5f);
            after.transform.localScale = new Vector3(DeckWidth, 0.38f, afterLen);
            after.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
            Object.Destroy(after.GetComponent<Collider>());
        }

        void BuildForestEdgeForFire()
        {
            GetFireChannel(out float channelStart, out float channelEnd, out _);
            BuildForestEdge(transform, 0f, channelStart);
            BuildForestEdge(transform, channelEnd, Length - channelEnd);
        }

        void BuildFireCrossing()
        {
            GetFireChannel(out float channelStart, out float channelEnd, out float channelLen);
            float channelMid = (channelStart + channelEnd) * 0.5f;

            var mode = TileSpawner.Instance != null
                ? TileSpawner.Instance.PickFireMode(_runDifficulty)
                : FireCrossingMode.Jump;

            var marker = gameObject.AddComponent<FireCrossingMarker>();
            marker.Configure(mode, channelStart, channelEnd, PathStartDistance, _runDifficulty);

            BuildNaturalFireShell(channelStart, channelEnd, channelLen, channelMid);

            switch (mode)
            {
                case FireCrossingMode.Vine:
                    BuildFireVineCrossing(marker, channelStart, channelEnd);
                    break;
                case FireCrossingMode.WaterDunk:
                    BuildFireWaterDunkCrossing(marker, channelStart, channelEnd, channelMid);
                    break;
                default:
                    BuildFireJumpCrossing(channelStart, channelEnd, channelLen);
                    break;
            }
        }

        const string LavaDeath = "Fell into the lava river";

        /// <summary>Volcano magma river — flowing lava channel distinct from ember fire pits.</summary>
        void BuildLavaRiverCrossing()
        {
            GetFireChannel(out float channelStart, out float channelEnd, out float channelLen);
            float channelMid = (channelStart + channelEnd) * 0.5f;

            // Jump stones most of the time; occasional vine swing — never water dunk on lava.
            var mode = FireCrossingMode.Jump;
            if (_runDifficulty != RunDifficulty.Easy && Random.value < 0.35f)
                mode = FireCrossingMode.Vine;

            var marker = gameObject.AddComponent<FireCrossingMarker>();
            marker.Configure(mode, channelStart, channelEnd, PathStartDistance, _runDifficulty);

            BuildNaturalLavaShell(channelStart, channelEnd, channelLen, channelMid);

            if (mode == FireCrossingMode.Vine)
                BuildLavaVineCrossing(marker, channelStart, channelEnd);
            else
                BuildLavaJumpCrossing(channelStart, channelEnd, channelLen);
        }

        void BuildNaturalLavaShell(float channelStart, float channelEnd, float channelLen, float channelMid)
        {
            float riverWidth = ForestOuter * 2.1f + DeckWidth;

            var bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bed.name = "LavaBed";
            bed.transform.SetParent(transform, false);
            bed.transform.localPosition = new Vector3(0f, ForestFloorY - 1.05f, channelMid);
            bed.transform.localScale = new Vector3(riverWidth, 1.35f, channelLen + 2f);
            bed.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            StripCollider(bed);

            var magma = GameObject.CreatePrimitive(PrimitiveType.Cube);
            magma.name = "LavaSurface";
            magma.transform.SetParent(transform, false);
            magma.transform.localPosition = new Vector3(0f, ForestFloorY - 0.18f, channelMid);
            magma.transform.localScale = new Vector3(riverWidth, 0.4f, channelLen + 1.6f);
            magma.GetComponent<Renderer>().sharedMaterial = JunglePalette.Ember;
            StripCollider(magma);

            var flow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flow.name = "LavaFlowSheet";
            flow.transform.SetParent(transform, false);
            flow.transform.localPosition = new Vector3(0f, ForestFloorY + 0.06f, channelMid);
            flow.transform.localScale = new Vector3(riverWidth * 0.9f, 0.1f, channelLen + 0.9f);
            flow.GetComponent<Renderer>().sharedMaterial = JunglePalette.Flame;
            StripCollider(flow);

            var scroll = magma.AddComponent<RiverSurfaceScroll>();
            scroll.Bind(magma.GetComponent<Renderer>(), flow.GetComponent<Renderer>());

            // Hot bank crusts along both shores.
            for (int side = -1; side <= 1; side += 2)
            {
                var crust = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crust.name = "LavaCrust";
                crust.transform.SetParent(transform, false);
                crust.transform.localPosition = new Vector3(
                    side * (DeckWidth * 0.5f + 1.25f), ForestFloorY + 0.1f, channelMid);
                crust.transform.localScale = new Vector3(1.5f, 0.22f, channelLen + 0.5f);
                crust.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
                StripCollider(crust);
            }

            // Low magma plumes (wider/shorter than fire-pit columns).
            int plumes = Mathf.Max(4, Mathf.RoundToInt(channelLen * 0.85f));
            for (int i = 0; i < plumes; i++)
            {
                float z = channelStart + channelLen * ((i + 0.5f) / plumes);
                float x = Random.Range(-DeckWidth * 0.6f, DeckWidth * 0.6f);
                SpawnFlameColumn(new Vector3(x, ForestFloorY, z), Random.Range(0.9f, 1.7f));
            }

            // Heat haze spheres above the river.
            for (int i = 0; i < 7; i++)
            {
                var haze = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                haze.name = "LavaHaze";
                haze.transform.SetParent(transform, false);
                haze.transform.localPosition = new Vector3(
                    Random.Range(-riverWidth * 0.28f, riverWidth * 0.28f),
                    ForestFloorY + Random.Range(1.6f, 3.8f),
                    channelStart + channelLen * Random.Range(0.12f, 0.88f));
                float s = Random.Range(1.4f, 2.6f);
                haze.transform.localScale = new Vector3(s, s * 0.55f, s);
                haze.GetComponent<Renderer>().sharedMaterial = JunglePalette.Smoke;
                StripCollider(haze);
            }

            var lightGo = new GameObject("LavaGlow");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.6f, channelMid);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.4f, 0.12f);
            light.range = 16f;
            light.intensity = 3.2f;
            light.shadows = LightShadows.None;

            var sparks = new GameObject("LavaSparks");
            sparks.transform.SetParent(transform, false);
            sparks.transform.localPosition = new Vector3(0f, ForestFloorY + 0.35f, channelMid);
            var sparkFx = sparks.AddComponent<EmberSparkAnimator>();
            sparkFx.Build(sparks.transform, riverWidth * 0.35f, channelLen * 0.4f);
        }

        void BuildLavaJumpCrossing(float channelStart, float channelEnd, float channelLen)
        {
            float row1 = channelStart + channelLen * 0.32f;
            float row2 = channelStart + channelLen * 0.68f;
            bool[,] stones = PickRiverStoneLanes();

            for (int lane = 0; lane < 3; lane++)
            {
                if (stones[0, lane]) SpawnLavaStone(lane, row1);
                else GapKillZone.Create(transform, row1, lane, 1.8f, LavaDeath);

                if (stones[1, lane]) SpawnLavaStone(lane, row2);
                else GapKillZone.Create(transform, row2, lane, 1.8f, LavaDeath);
            }

            float gapA = (channelStart + row1) * 0.5f;
            float gapB = (row1 + row2) * 0.5f;
            float gapC = (row2 + channelEnd) * 0.5f;
            float segA = Mathf.Max(1.2f, row1 - channelStart - 0.6f);
            float segB = Mathf.Max(1.2f, row2 - row1 - 0.6f);
            float segC = Mathf.Max(1.2f, channelEnd - row2 - 0.6f);
            for (int lane = 0; lane < 3; lane++)
            {
                GapKillZone.Create(transform, gapA, lane, segA, LavaDeath);
                GapKillZone.Create(transform, gapC, lane, segC, LavaDeath);
                if (!stones[0, lane] || !stones[1, lane])
                    GapKillZone.Create(transform, gapB, lane, segB, LavaDeath);
            }

            int guideLane = PickGuideLane(stones, 0);
            float gx = (guideLane - 1) * PlayerController.LaneWidth;
            CollectibleCoin.Create(transform, new Vector3(gx, 1.4f, channelStart + 0.55f));
            CollectibleCoin.Create(transform, new Vector3(gx, 1.55f, row1));
            CollectibleCoin.Create(transform, new Vector3(
                (PickGuideLane(stones, 1) - 1) * PlayerController.LaneWidth, 1.55f, row2));
            CollectibleCoin.Create(transform, new Vector3(gx, 1.35f, channelEnd - 0.55f));
        }

        void SpawnLavaStone(int lane, float z)
        {
            float x = (lane - 1) * PlayerController.LaneWidth;
            float pillarH = -ForestFloorY + 0.05f;

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "LavaStonePillar";
            pillar.transform.SetParent(transform, false);
            pillar.transform.localPosition = new Vector3(x, ForestFloorY + pillarH * 0.5f, z);
            pillar.transform.localScale = new Vector3(1.2f, pillarH * 0.5f, 1.2f);
            pillar.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            StripCollider(pillar);

            var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            top.name = "LavaStoneTop";
            top.transform.SetParent(transform, false);
            top.transform.localPosition = new Vector3(x, 0.1f, z);
            top.transform.localScale = new Vector3(1.6f, 0.14f, 1.6f);
            top.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
            StripCollider(top);

            // Glowing rim so stones read against the magma.
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.transform.SetParent(transform, false);
            rim.transform.localPosition = new Vector3(x, 0.02f, z);
            rim.transform.localScale = new Vector3(1.75f, 0.06f, 1.75f);
            rim.GetComponent<Renderer>().sharedMaterial = JunglePalette.Ember;
            StripCollider(rim);
        }

        void BuildLavaVineCrossing(FireCrossingMarker marker, float channelStart, float channelEnd)
        {
            float grabZ = channelStart + 0.35f;
            float landZ = channelEnd - 0.25f;

            var swingGo = new GameObject("LavaVineSwing");
            swingGo.transform.SetParent(transform, false);
            swingGo.transform.localPosition = new Vector3(0f, 1.2f, grabZ);
            var swingCol = swingGo.AddComponent<BoxCollider>();
            swingCol.isTrigger = true;
            swingCol.size = new Vector3(DeckWidth + 0.6f, 3f, 1.6f);
            var swing = swingGo.AddComponent<FireVineSwing>();
            swing.Marker = marker;
            swing.SwingDuration = _runDifficulty == RunDifficulty.Hard ? 1.0f
                : _runDifficulty == RunDifficulty.Easy ? 1.35f : 1.15f;
            swing.ReleaseWindowStart = _runDifficulty == RunDifficulty.Easy ? 0.45f : 0.52f;
            swing.ReleaseWindowEnd = _runDifficulty == RunDifficulty.Hard ? 0.78f : 0.85f;
            swing.ApexHeight = 2.9f;
            swing.BuildVisual(transform, grabZ, landZ);

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "LavaVineLanding";
            pad.transform.SetParent(transform, false);
            pad.transform.localPosition = new Vector3(0f, 0.06f, landZ);
            pad.transform.localScale = new Vector3(DeckWidth * 0.9f, 0.16f, 1.6f);
            pad.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            StripCollider(pad);

            float mid = (channelStart + channelEnd) * 0.5f;
            float len = channelEnd - channelStart;
            for (int lane = 0; lane < 3; lane++)
                GapKillZone.Create(transform, mid, lane, len, LavaDeath);

            CollectibleCoin.Create(transform, new Vector3(0f, 2.4f, mid));
            if (_runDifficulty != RunDifficulty.Easy && Random.value < 0.35f)
                GemPickup.Create(transform, new Vector3(PlayerController.LaneWidth, 2.2f, mid + 0.5f));
        }

        void BuildNaturalFireShell(float channelStart, float channelEnd, float channelLen, float channelMid)
        {
            float pitWidth = ForestOuter * 1.8f + DeckWidth;

            var bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bed.name = "FireBed";
            bed.transform.SetParent(transform, false);
            bed.transform.localPosition = new Vector3(0f, ForestFloorY - 0.85f, channelMid);
            bed.transform.localScale = new Vector3(pitWidth, 1.1f, channelLen + 1.6f);
            bed.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            StripCollider(bed);

            var ember = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ember.name = "EmberSheet";
            ember.transform.SetParent(transform, false);
            ember.transform.localPosition = new Vector3(0f, ForestFloorY - 0.15f, channelMid);
            ember.transform.localScale = new Vector3(pitWidth * 0.95f, 0.22f, channelLen + 1.1f);
            ember.GetComponent<Renderer>().sharedMaterial = JunglePalette.Ember;
            StripCollider(ember);

            int columns = Mathf.Max(5, Mathf.RoundToInt(channelLen * 1.1f));
            for (int i = 0; i < columns; i++)
            {
                float z = channelStart + channelLen * ((i + 0.5f) / columns);
                float x = Random.Range(-DeckWidth * 0.55f, DeckWidth * 0.55f);
                SpawnFlameColumn(new Vector3(x, ForestFloorY, z), Random.Range(1.4f, 2.6f));
            }

            for (int i = 0; i < 5; i++)
            {
                var smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                smoke.name = "SmokeWisp";
                smoke.transform.SetParent(transform, false);
                smoke.transform.localPosition = new Vector3(
                    Random.Range(-pitWidth * 0.25f, pitWidth * 0.25f),
                    ForestFloorY + Random.Range(2.2f, 4.5f),
                    channelStart + channelLen * Random.Range(0.15f, 0.85f));
                float s = Random.Range(1.2f, 2.4f);
                smoke.transform.localScale = new Vector3(s, s * 0.7f, s);
                smoke.GetComponent<Renderer>().sharedMaterial = JunglePalette.Smoke;
                StripCollider(smoke);
            }

            for (int i = 0; i < 5; i++)
            {
                int side = Random.value < 0.5f ? -1 : 1;
                float z = channelStart + Random.Range(-0.3f, channelLen + 0.3f);
                SpawnMossRock(transform,
                    new Vector3(side * Random.Range(ForestInner + 1.2f, ForestOuter - 1.5f), ForestFloorY, z));
            }

            // Warm point light so the channel reads from chase cam.
            var lightGo = new GameObject("FireGlow");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.8f, channelMid);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.2f);
            light.range = 14f;
            light.intensity = 2.4f;
            light.shadows = LightShadows.None;

            // Rising ember sparks so the pit feels alive (not a flat orange slab).
            var sparks = new GameObject("EmberSparks");
            sparks.transform.SetParent(transform, false);
            sparks.transform.localPosition = new Vector3(0f, ForestFloorY + 0.4f, channelMid);
            var sparkFx = sparks.AddComponent<EmberSparkAnimator>();
            sparkFx.Build(sparks.transform, pitWidth * 0.4f, channelLen * 0.4f);
        }

        void SpawnFlameColumn(Vector3 basePos, float height)
        {
            var root = new GameObject("FlameColumn").transform;
            root.SetParent(transform, false);
            root.localPosition = basePos;

            var core = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            core.transform.SetParent(root, false);
            core.transform.localPosition = new Vector3(0f, height * 0.45f, 0f);
            core.transform.localScale = new Vector3(0.55f, height * 0.45f, 0.55f);
            core.GetComponent<Renderer>().sharedMaterial = JunglePalette.FlameCore;
            StripCollider(core);

            var outer = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            outer.transform.SetParent(root, false);
            outer.transform.localPosition = new Vector3(0f, height * 0.55f, 0f);
            outer.transform.localScale = new Vector3(0.95f, height * 0.55f, 0.95f);
            outer.GetComponent<Renderer>().sharedMaterial = JunglePalette.Flame;
            StripCollider(outer);

            var flicker = root.gameObject.AddComponent<FlameFlicker>();
            flicker.Configure(height);
        }

        void PlaceFireKillZones(float channelStart, float channelEnd)
        {
            float mid = (channelStart + channelEnd) * 0.5f;
            float len = channelEnd - channelStart;
            for (int lane = 0; lane < 3; lane++)
                GapKillZone.Create(transform, mid, lane, len, FireDeath);
        }

        void BuildFireJumpCrossing(float channelStart, float channelEnd, float channelLen)
        {
            float row1 = channelStart + channelLen * 0.35f;
            float row2 = channelStart + channelLen * 0.7f;
            bool[,] stones = PickRiverStoneLanes();

            for (int lane = 0; lane < 3; lane++)
            {
                if (stones[0, lane]) SpawnSteppingStone(lane, row1);
                else GapKillZone.Create(transform, row1, lane, 1.8f, FireDeath);

                if (stones[1, lane]) SpawnSteppingStone(lane, row2);
                else GapKillZone.Create(transform, row2, lane, 1.8f, FireDeath);
            }

            float gapA = (channelStart + row1) * 0.5f;
            float gapB = (row1 + row2) * 0.5f;
            float gapC = (row2 + channelEnd) * 0.5f;
            float segA = Mathf.Max(1.2f, row1 - channelStart - 0.6f);
            float segB = Mathf.Max(1.2f, row2 - row1 - 0.6f);
            float segC = Mathf.Max(1.2f, channelEnd - row2 - 0.6f);
            for (int lane = 0; lane < 3; lane++)
            {
                GapKillZone.Create(transform, gapA, lane, segA, FireDeath);
                GapKillZone.Create(transform, gapC, lane, segC, FireDeath);
                if (!stones[0, lane] || !stones[1, lane])
                    GapKillZone.Create(transform, gapB, lane, segB, FireDeath);
            }

            int guideLane = PickGuideLane(stones, 0);
            float gx = (guideLane - 1) * PlayerController.LaneWidth;
            CollectibleCoin.Create(transform, new Vector3(gx, 1.35f, channelStart + 0.6f));
            CollectibleCoin.Create(transform, new Vector3(gx, 1.55f, row1));
            CollectibleCoin.Create(transform, new Vector3(
                (PickGuideLane(stones, 1) - 1) * PlayerController.LaneWidth, 1.55f, row2));
            CollectibleCoin.Create(transform, new Vector3(gx, 1.35f, channelEnd - 0.6f));
        }

        void BuildFireVineCrossing(FireCrossingMarker marker, float channelStart, float channelEnd)
        {
            float grabZ = channelStart + 0.35f;
            float landZ = channelEnd - 0.25f;

            var swingGo = new GameObject("VineSwing");
            swingGo.transform.SetParent(transform, false);
            swingGo.transform.localPosition = new Vector3(0f, 1.2f, grabZ);
            var swingCol = swingGo.AddComponent<BoxCollider>();
            swingCol.isTrigger = true;
            swingCol.size = new Vector3(DeckWidth + 0.6f, 3f, 1.6f);
            var swing = swingGo.AddComponent<FireVineSwing>();
            swing.Marker = marker;
            swing.SwingDuration = _runDifficulty == RunDifficulty.Hard ? 1.0f
                : _runDifficulty == RunDifficulty.Easy ? 1.35f : 1.15f;
            swing.ReleaseWindowStart = _runDifficulty == RunDifficulty.Easy ? 0.45f : 0.52f;
            swing.ReleaseWindowEnd = _runDifficulty == RunDifficulty.Hard ? 0.78f : 0.85f;
            swing.ApexHeight = 2.9f;
            swing.BuildVisual(transform, grabZ, landZ);

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "VineLanding";
            pad.transform.SetParent(transform, false);
            pad.transform.localPosition = new Vector3(0f, 0.06f, landZ);
            pad.transform.localScale = new Vector3(DeckWidth * 0.9f, 0.16f, 1.6f);
            pad.GetComponent<Renderer>().sharedMaterial = JunglePalette.StoneMoss;
            StripCollider(pad);

            PlaceFireKillZones(channelStart, channelEnd);

            float mid = (channelStart + channelEnd) * 0.5f;
            CollectibleCoin.Create(transform, new Vector3(0f, 1.3f, grabZ));
            CollectibleCoin.Create(transform, new Vector3(0f, 2.9f, mid));
            CollectibleCoin.Create(transform, new Vector3(0f, 1.4f, landZ));
        }

        void BuildFireWaterDunkCrossing(FireCrossingMarker marker, float channelStart, float channelEnd, float channelMid)
        {
            float grabZ = channelStart + 0.25f;

            // Hanging water barrel visual.
            var barrelRoot = new GameObject("WaterBarrel").transform;
            barrelRoot.SetParent(transform, false);
            barrelRoot.localPosition = new Vector3(0f, 2.6f, grabZ);

            var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rope.transform.SetParent(barrelRoot, false);
            rope.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            rope.transform.localScale = new Vector3(0.08f, 0.55f, 0.08f);
            rope.GetComponent<Renderer>().sharedMaterial = JunglePalette.Rope;
            StripCollider(rope);

            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Barrel";
            barrel.transform.SetParent(barrelRoot, false);
            barrel.transform.localPosition = new Vector3(0f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.7f, 0.55f, 0.7f);
            barrel.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
            StripCollider(barrel);

            var waterCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            waterCap.transform.SetParent(barrelRoot, false);
            waterCap.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            waterCap.transform.localScale = new Vector3(0.62f, 0.08f, 0.62f);
            waterCap.GetComponent<Renderer>().sharedMaterial = JunglePalette.RiverWater;
            StripCollider(waterCap);

            float safe = _runDifficulty == RunDifficulty.Hard ? 1.2f
                : _runDifficulty == RunDifficulty.Easy ? 1.6f : 1.4f;

            var mountGo = new GameObject("WaterDunkMount");
            mountGo.transform.SetParent(transform, false);
            mountGo.transform.localPosition = new Vector3(0f, 1.1f, grabZ);
            var mountCol = mountGo.AddComponent<BoxCollider>();
            mountCol.isTrigger = true;
            mountCol.size = new Vector3(DeckWidth + 0.5f, 2.8f, 1.8f);
            var mount = mountGo.AddComponent<FireWaterDunkMount>();
            mount.Marker = marker;
            mount.SafeDuration = safe;

            PlaceFireKillZones(channelStart, channelEnd);

            // Steam / extinguish hint coins along the channel.
            CollectibleCoin.Create(transform, new Vector3(0f, 1.4f, grabZ));
            CollectibleCoin.Create(transform, new Vector3(0f, 1.5f, channelMid));
            CollectibleCoin.Create(transform, new Vector3(0f, 1.4f, channelEnd - 0.4f));
        }

        /// <summary>
        /// Cascading waterfall sheet with plunge foam. side = bank facing (-1 / +1).
        /// </summary>
        void SpawnWaterfall(Transform parent, Vector3 basePos, float height, int side)
        {
            WaterFlow.Ensure();
            var root = new GameObject("Waterfall").transform;
            root.SetParent(parent, false);
            root.localPosition = basePos;

            var sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sheet.name = "FallSheet";
            sheet.transform.SetParent(root, false);
            sheet.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            sheet.transform.localScale = new Vector3(2.2f, height, 0.22f);
            sheet.GetComponent<Renderer>().sharedMaterial = JunglePalette.FallingWater;
            StripCollider(sheet);

            // Second sheet slightly offset so the cascade reads from the chase camera.
            var sheet2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sheet2.name = "FallSheetB";
            sheet2.transform.SetParent(root, false);
            sheet2.transform.localPosition = new Vector3(-side * 0.35f, height * 0.45f, 0.35f);
            sheet2.transform.localScale = new Vector3(1.4f, height * 0.85f, 0.16f);
            sheet2.GetComponent<Renderer>().sharedMaterial = JunglePalette.FallingWater;
            StripCollider(sheet2);

            var mist = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mist.name = "PlungeMist";
            mist.transform.SetParent(root, false);
            mist.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            mist.transform.localScale = new Vector3(3.2f, 1.2f, 3.2f);
            mist.GetComponent<Renderer>().sharedMaterial = JunglePalette.Foam;
            StripCollider(mist);

            var pool = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pool.name = "PlungePool";
            pool.transform.SetParent(root, false);
            pool.transform.localPosition = new Vector3(-side * 1.1f, 0.08f, 0f);
            pool.transform.localScale = new Vector3(4.2f, 0.14f, 3.2f);
            pool.GetComponent<Renderer>().sharedMaterial = JunglePalette.RiverWater;
            StripCollider(pool);

            // Cliff face behind the fall so it reads as cascading off rock.
            var cliff = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cliff.name = "FallCliff";
            cliff.transform.SetParent(root, false);
            cliff.transform.localPosition = new Vector3(side * 0.85f, height * 0.48f, 0f);
            cliff.transform.localScale = new Vector3(1.6f, height * 0.98f, 3.4f);
            cliff.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
            StripCollider(cliff);
        }

        void BuildRails() => BuildRailSegment(0.4f, Length - 0.8f);

        void BuildRailsForGap()
        {
            BuildRailSegment(0.4f, 3.5f);
            BuildRailSegment(8.5f, 3.5f);
        }

        void BuildRailSegment(float zStart, float segLength)
        {
            float zMid = zStart + segLength * 0.5f;
            // Outside outer-lane envelope so rails never ghost through the runner.
            float x = 3.15f;
            for (int side = -1; side <= 1; side += 2)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.transform.SetParent(transform, false);
                rail.transform.localPosition = new Vector3(side * x, 0.32f, zMid);
                rail.transform.localScale = new Vector3(0.32f, 0.55f, segLength);
                rail.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                Object.Destroy(rail.GetComponent<Collider>());

                int merlons = Mathf.Max(2, Mathf.RoundToInt(segLength / 1.8f));
                for (int m = 0; m < merlons; m++)
                {
                    float mz = zStart + (m + 0.5f) * (segLength / merlons);
                    var merlon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    merlon.transform.SetParent(transform, false);
                    merlon.transform.localPosition = new Vector3(side * x, 0.72f, mz);
                    merlon.transform.localScale = new Vector3(0.36f, 0.35f, 0.55f);
                    merlon.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
                    Object.Destroy(merlon.GetComponent<Collider>());
                }
            }
        }

        void ScatterCoins(int count)
        {
            int lane = Random.Range(0, 3);
            bool weave = count >= 6 && Random.value < 0.55f;
            bool zig = count >= 8 && Random.value < 0.35f;
            for (int i = 0; i < count; i++)
            {
                if (zig)
                    lane = (i / 2) % 3;
                else if (weave && i > 0 && i % 2 == 0)
                    lane = Mathf.Clamp(lane + (Random.value < 0.5f ? 1 : -1), 0, 2);

                float z = 1.2f + (Length - 2.4f) * (i + 1) / (count + 1);
                float x = (lane - 1) * PlayerController.LaneWidth;
                CollectibleCoin.Create(transform, new Vector3(x, 1.25f, z));
            }
        }

        void SpawnPowerUp()
        {
            int lane = Random.Range(0, 3);
            float x = (lane - 1) * PlayerController.LaneWidth;
            var types = new[]
            {
                PowerUpType.Magnet, PowerUpType.Shield, PowerUpType.ScoreMultiplier,
                PowerUpType.SpeedBoost, PowerUpType.SlowMo
            };
            PowerUpPickup.Create(transform, new Vector3(x, 1.3f, Length * 0.55f), types[Random.Range(0, types.Length)]);
        }

        void SpawnRelicOrGem()
        {
            int lane = Random.Range(0, 3);
            float x = (lane - 1) * PlayerController.LaneWidth;
            if (Random.value < 0.35f)
                RelicPickup.Create(transform, new Vector3(x, 1.35f, Length * 0.4f));
            else
                GemPickup.Create(transform, new Vector3(x, 1.35f, Length * 0.4f));
        }

        void SpawnBreakableIdol()
        {
            // Park urns beside the runway so they read as smashables, not lane blockers.
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = side * (DeckWidth * 0.5f + 0.55f);
            float z = Length * Random.Range(0.35f, 0.75f);
            var kind = Random.value < 0.28f
                ? BreakableIdol.IdolKind.GemIdol
                : BreakableIdol.IdolKind.CoinUrn;
            BreakableIdol.Create(transform, new Vector3(x, 0.15f, z), kind);
        }

        void SpawnObstacles(float chance, int tier)
        {
            int max = Mathf.RoundToInt(DifficultyProfile.For(_runDifficulty).MaxObstaclesInCluster);
            int count = 1;
            if (_runDifficulty != RunDifficulty.Easy && tier >= 2) count++;
            if (Random.value < chance) count++;
            count = Mathf.Clamp(count, 1, max);

            // Never block all 3 lanes at the same depth
            int usedLaneMask = 0;
            for (int i = 0; i < count; i++)
            {
                int lane = Random.Range(0, 3);
                int tries = 0;
                while ((usedLaneMask & (1 << lane)) != 0 && tries < 6)
                {
                    lane = Random.Range(0, 3);
                    tries++;
                }
                // Keep at least one open lane across the whole cluster
                if (i == count - 1 && usedLaneMask == 0b011) lane = 2;
                else if (i == count - 1 && usedLaneMask == 0b101) lane = 1;
                else if (i == count - 1 && usedLaneMask == 0b110) lane = 0;
                usedLaneMask |= 1 << lane;

                float z = 3f + i * (_runDifficulty == RunDifficulty.Easy ? 4.5f : 3.5f);
                // Occasional full-width wall that requires a slide — clearly lethal.
                if (i == 0 && _runDifficulty != RunDifficulty.Easy && Random.value < 0.22f)
                {
                    Obstacle.CreateBlockingWall(transform, z);
                    continue;
                }
                int roll = Random.Range(0, _runDifficulty == RunDifficulty.Hard ? 4 : 3);
                if (roll == 0) Obstacle.CreateLowBeam(transform, z, lane);
                else if (roll == 1) Obstacle.CreateSpike(transform, z, lane);
                else if (roll == 2) Obstacle.CreateFirePit(transform, z, lane);
                else Obstacle.CreateCollapsingBridge(transform, z, lane);
            }
        }

        void SpawnDynamic(int tier)
        {
            // Jungle / canopy bias readable vine sweeps.
            bool vineBiome = BiomeSystem.Current == BiomeId.JungleRuins
                             || BiomeSystem.Current == BiomeId.NightSummit;
            if (vineBiome && Random.value < (_runDifficulty == RunDifficulty.Easy ? 0.28f : 0.4f))
            {
                DynamicHazard.CreateVineSweep(transform, Length * 0.55f, Random.Range(0, 3));
                if (_runDifficulty == RunDifficulty.Hard && Random.value < 0.28f)
                    DynamicHazard.CreateLogPendulum(transform, Length * 0.82f, Random.Range(0, 3));
                return;
            }

            // Cave / temple bias swinging log pendulums with arc telegraphs.
            bool logBiome = BiomeSystem.Current == BiomeId.CaveMines
                            || BiomeSystem.Current == BiomeId.DesertTombs
                            || BiomeSystem.Current == BiomeId.IceCaverns;
            if (logBiome && Random.value < (_runDifficulty == RunDifficulty.Easy ? 0.26f : 0.38f))
            {
                DynamicHazard.CreateLogPendulum(transform, Length * 0.52f, Random.Range(0, 3));
                if (_runDifficulty != RunDifficulty.Easy && Random.value < 0.3f)
                    DynamicHazard.CreateLogPendulum(transform, Length * 0.8f, Random.Range(0, 3));
                return;
            }

            // Cave / desert / night bias telegraphing stone crushers.
            bool crusherBiome = BiomeSystem.Current == BiomeId.CaveMines
                                || BiomeSystem.Current == BiomeId.DesertTombs
                                || BiomeSystem.Current == BiomeId.NightSummit;
            if (crusherBiome && _runDifficulty != RunDifficulty.Easy
                && Random.value < (_runDifficulty == RunDifficulty.Hard ? 0.4f : 0.32f))
            {
                DynamicHazard.CreateStoneCrusher(transform, Length * 0.55f, Random.Range(0, 3));
                if (_runDifficulty == RunDifficulty.Hard && Random.value < 0.3f)
                    DynamicHazard.CreateStoneCrusher(transform, Length * 0.78f, Random.Range(0, 3));
                return;
            }

            // Cave / volcano bias rolling boulders.
            bool boulderBiome = BiomeSystem.Current == BiomeId.CaveMines
                                || BiomeSystem.Current == BiomeId.VolcanicCrater
                                || BiomeSystem.Current == BiomeId.DesertTombs;
            // Temple / night bias spinning spike wheels (distinct silhouette from boulders).
            bool wheelBiome = BiomeSystem.Current == BiomeId.JungleRuins
                              || BiomeSystem.Current == BiomeId.NightSummit
                              || BiomeSystem.Current == BiomeId.DesertTombs;
            if (wheelBiome && Random.value < (_runDifficulty == RunDifficulty.Easy ? 0.22f : 0.38f))
            {
                DynamicHazard.CreateSpikeWheel(transform, Length * 0.55f, Random.Range(0, 3));
                if (_runDifficulty == RunDifficulty.Hard && Random.value < 0.35f)
                    DynamicHazard.CreateSpikeWheel(transform, Length * 0.78f, Random.Range(0, 3));
                return;
            }
            if (boulderBiome && Random.value < (_runDifficulty == RunDifficulty.Easy ? 0.28f : 0.42f))
            {
                DynamicHazard.CreateRollingBoulder(transform, Length * 0.85f, Random.Range(0, 3));
                if (_runDifficulty == RunDifficulty.Hard && Random.value < 0.3f)
                    DynamicHazard.CreateRollingBoulder(transform, Length * 0.95f, Random.Range(0, 3));
                return;
            }

            int roll = Random.Range(0, _runDifficulty == RunDifficulty.Easy ? 4 : 10);
            if (roll == 0) DynamicHazard.CreatePendulum(transform, Length * 0.5f, 1);
            else if (roll == 1) DynamicHazard.CreateArrow(transform, Length * 0.3f, Random.Range(0, 3));
            else if (roll == 2) DynamicHazard.CreateGate(transform, Length * 0.55f);
            else if (roll == 3) DynamicHazard.CreateVineSweep(transform, Length * 0.55f, Random.Range(0, 3));
            else if (roll == 4) Obstacle.CreateBlockingWall(transform, Length * 0.55f);
            else if (roll == 5) DynamicHazard.CreateCrumbling(transform, Length * 0.5f, Random.Range(0, 3));
            else if (roll == 6) DynamicHazard.CreateSpikeWheel(transform, Length * 0.6f, Random.Range(0, 3));
            else if (roll == 7) DynamicHazard.CreateStoneCrusher(transform, Length * 0.58f, Random.Range(0, 3));
            else if (roll == 8) DynamicHazard.CreateLogPendulum(transform, Length * 0.55f, Random.Range(0, 3));
            else DynamicHazard.CreateRollingBoulder(transform, Length * 0.88f, Random.Range(0, 3));

            if (_runDifficulty == RunDifficulty.Hard && tier >= 2 && Random.value < 0.35f)
                Obstacle.CreateSpike(transform, Length * 0.75f, Random.Range(0, 3));
        }

        void BuildBranch()
        {
            var divider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            divider.name = "BranchDivider";
            divider.transform.SetParent(transform, false);
            divider.transform.localPosition = new Vector3(0f, 0.85f, Length * 0.5f);
            divider.transform.localScale = new Vector3(0.35f, 1.7f, Length * 0.7f);
            divider.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
            var dCol = divider.GetComponent<Collider>();
            dCol.isTrigger = true;
            var dObs = divider.AddComponent<Obstacle>();
            dObs.DeathMessage = "Hit the branch divider";

            // Easy: reward route. Medium: one hazard side. Hard: risk/reward split.
            for (int i = 0; i < 6; i++)
                CollectibleCoin.Create(transform, new Vector3(-PlayerController.LaneWidth, 1.25f, 1.8f + i * 1.6f));
            if (Random.value < 0.65f)
                BreakableIdol.Create(transform,
                    new Vector3(-DeckWidth * 0.5f - 0.5f, 0.15f, Length * 0.45f),
                    BreakableIdol.IdolKind.CoinUrn);

            if (_runDifficulty == RunDifficulty.Easy)
            {
                GemPickup.Create(transform, new Vector3(PlayerController.LaneWidth, 1.35f, 5.5f));
                if (Random.value < 0.35f)
                    Obstacle.CreateSpike(transform, 7f, 1);
                return;
            }

            if (_runDifficulty == RunDifficulty.Medium)
            {
                Obstacle.CreateSpike(transform, 5f, 1);
                Obstacle.CreateLowBeam(transform, 9f, 1);
                GemPickup.Create(transform, new Vector3(PlayerController.LaneWidth, 1.35f, 6f));
                return;
            }

            Obstacle.CreateSpike(transform, 3.5f, 1);
            Obstacle.CreateSpike(transform, 7f, 1);
            Obstacle.CreateLowBeam(transform, 10f, 1);
            Obstacle.CreateSpike(transform, 4f, 2);
            Obstacle.CreateFirePit(transform, 7f, 2);
            DynamicHazard.CreatePendulum(transform, 9f, 2);
            GemPickup.Create(transform, new Vector3(PlayerController.LaneWidth, 1.35f, 5.5f));
            RelicPickup.Create(transform, new Vector3(PlayerController.LaneWidth, 1.35f, 9.5f));
        }

        void BuildGap()
        {
            int safeLane = Random.Range(0, 3);
            // Easy/Medium: two safe lanes. Hard: one safe lane.
            int extraSafe = _runDifficulty == RunDifficulty.Hard ? -1 : Random.Range(0, 3);
            if (extraSafe == safeLane) extraSafe = (safeLane + 1) % 3;

            for (int lane = 0; lane < 3; lane++)
            {
                bool safe = lane == safeLane || lane == extraSafe;
                if (safe)
                {
                    var bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bridge.transform.SetParent(transform, false);
                    float x = (lane - 1) * PlayerController.LaneWidth;
                    bridge.transform.localPosition = new Vector3(x, 0.05f, Length * 0.5f);
                    bridge.transform.localScale = new Vector3(1.7f, 0.22f, 4.2f);
                    bridge.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                    Object.Destroy(bridge.GetComponent<Collider>());
                    if (_runDifficulty == RunDifficulty.Hard && Random.value < 0.2f)
                        Obstacle.CreateCollapsingBridge(transform, Length * 0.5f, lane);
                }
                else
                {
                    GapKillZone.Create(transform, Length * 0.5f, lane);
                }
            }

            float sx = (safeLane - 1) * PlayerController.LaneWidth;
            CollectibleCoin.Create(transform, new Vector3(sx, 1.25f, Length * 0.5f));
        }

        void GetSpecialChannel(out float channelStart, out float channelEnd, out float channelLen)
        {
            float profileSpeed = DifficultyProfile.For(_runDifficulty).BaseSpeed;
            channelLen = Mathf.Clamp(7.2f + profileSpeed * 0.2f, 8.2f, 10f);
            channelStart = (Length - channelLen) * 0.5f;
            channelEnd = channelStart + channelLen;
        }

        void BuildFloorWithSpecialChannel()
        {
            GetSpecialChannel(out float channelStart, out float channelEnd, out _);
            float beforeLen = channelStart;
            float afterLen = Length - channelEnd;

            var before = GameObject.CreatePrimitive(PrimitiveType.Cube);
            before.name = "FloorBeforeSpecial";
            before.transform.SetParent(transform, false);
            before.transform.localPosition = new Vector3(0f, -0.12f, beforeLen * 0.5f);
            before.transform.localScale = new Vector3(DeckWidth, 0.38f, beforeLen);
            before.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
            Object.Destroy(before.GetComponent<Collider>());

            var after = GameObject.CreatePrimitive(PrimitiveType.Cube);
            after.name = "FloorAfterSpecial";
            after.transform.SetParent(transform, false);
            after.transform.localPosition = new Vector3(0f, -0.12f, channelEnd + afterLen * 0.5f);
            after.transform.localScale = new Vector3(DeckWidth, 0.38f, afterLen);
            after.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
            Object.Destroy(after.GetComponent<Collider>());
        }

        void BuildForestEdgeForSpecial()
        {
            GetSpecialChannel(out float channelStart, out float channelEnd, out _);
            BuildForestEdge(transform, 0f, channelStart);
            BuildForestEdge(transform, channelEnd, Length - channelEnd);
        }

        void BuildCaveTunnelShell()
        {
            GetSpecialChannel(out float channelStart, out float channelEnd, out float channelLen);
            float mid = (channelStart + channelEnd) * 0.5f;

            // Rock walls framing the mine corridor.
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "CaveWall";
                wall.transform.SetParent(transform, false);
                wall.transform.localPosition = new Vector3(side * (DeckWidth * 0.5f + 2.2f), 2.2f, mid);
                wall.transform.localScale = new Vector3(3.2f, 5.2f, channelLen + 2f);
                wall.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                StripCollider(wall);
            }

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "CaveCeiling";
            ceiling.transform.SetParent(transform, false);
            ceiling.transform.localPosition = new Vector3(0f, 4.6f, mid);
            ceiling.transform.localScale = new Vector3(DeckWidth + 5f, 0.7f, channelLen + 1.5f);
            ceiling.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            StripCollider(ceiling);

            for (int i = 0; i < 3; i++)
            {
                float z = channelStart + channelLen * ((i + 1f) / 4f);
                BuildMineTimberAt(z);
            }
        }

        void BuildMineTimberFrame()
        {
            BuildMineTimberAt(Length * Random.Range(0.35f, 0.7f));
        }

        void BuildMineTimberAt(float z)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.transform.SetParent(transform, false);
                post.transform.localPosition = new Vector3(side * (DeckWidth * 0.48f + 0.15f), 1.6f, z);
                post.transform.localScale = new Vector3(0.28f, 3.2f, 0.28f);
                post.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
                StripCollider(post);
            }
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = new Vector3(0f, 3.15f, z);
            beam.transform.localScale = new Vector3(DeckWidth + 0.6f, 0.28f, 0.28f);
            beam.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
            StripCollider(beam);
        }

        void BuildVolcanicEdgeGlow()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var glow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                glow.transform.SetParent(transform, false);
                glow.transform.localPosition = new Vector3(side * (DeckWidth * 0.55f + 0.8f), -0.4f, Length * 0.5f);
                glow.transform.localScale = new Vector3(1.2f, 0.2f, Length * 0.7f);
                glow.GetComponent<Renderer>().sharedMaterial = JunglePalette.Ember;
                StripCollider(glow);
            }
        }

        void BuildZiplineStage()
        {
            GetSpecialChannel(out float channelStart, out float channelEnd, out float channelLen);
            float mid = (channelStart + channelEnd) * 0.5f;

            var marker = gameObject.AddComponent<SpecialStageMarker>();
            marker.Configure(SpecialStageKind.Zipline, channelStart, channelEnd, PathStartDistance, _runDifficulty);

            // Deep cliff void under the cable.
            var voidBed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            voidBed.name = "ZiplineVoid";
            voidBed.transform.SetParent(transform, false);
            voidBed.transform.localPosition = new Vector3(0f, ForestFloorY - 1.5f, mid);
            voidBed.transform.localScale = new Vector3(ForestOuter * 2.2f + DeckWidth, 2f, channelLen + 2f);
            voidBed.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.1f, 0.14f, 0.12f), 0.2f);
            StripCollider(voidBed);

            for (int lane = 0; lane < 3; lane++)
                GapKillZone.Create(transform, mid, lane, channelLen, "Fell from the zipline");

            // Twin posts + cable.
            var nearPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nearPost.transform.SetParent(transform, false);
            nearPost.transform.localPosition = new Vector3(0f, 2.2f, channelStart);
            nearPost.transform.localScale = new Vector3(0.35f, 2.2f, 0.35f);
            nearPost.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
            StripCollider(nearPost);

            var farPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            farPost.transform.SetParent(transform, false);
            farPost.transform.localPosition = new Vector3(0f, 2.2f, channelEnd);
            farPost.transform.localScale = new Vector3(0.35f, 2.2f, 0.35f);
            farPost.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
            StripCollider(farPost);

            var cable = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cable.name = "ZiplineCable";
            cable.transform.SetParent(transform, false);
            cable.transform.localPosition = new Vector3(0f, 3.9f, mid);
            cable.transform.localScale = new Vector3(0.08f, 0.08f, channelLen);
            cable.GetComponent<Renderer>().sharedMaterial = JunglePalette.Rope;
            StripCollider(cable);

            var mountGo = new GameObject("ZiplineMount");
            mountGo.transform.SetParent(transform, false);
            mountGo.transform.localPosition = new Vector3(0f, 1.2f, channelStart + 0.25f);
            var mountCol = mountGo.AddComponent<BoxCollider>();
            mountCol.isTrigger = true;
            mountCol.size = new Vector3(DeckWidth + 0.6f, 3f, 1.8f);
            var mount = mountGo.AddComponent<ZiplineMount>();
            mount.Marker = marker;
            mount.RideHeight = 2.55f;

            // Overhead beams that require a slide while riding.
            int beams = _runDifficulty == RunDifficulty.Easy ? 1 : _runDifficulty == RunDifficulty.Hard ? 3 : 2;
            for (int i = 0; i < beams; i++)
            {
                float z = Mathf.Lerp(channelStart + 1.4f, channelEnd - 1.4f, (i + 1f) / (beams + 1f));
                Obstacle.CreateLowBeam(transform, z, 1);
            }

            CollectibleCoin.Create(transform, new Vector3(0f, 2.8f, channelStart + 1f));
            CollectibleCoin.Create(transform, new Vector3(0f, 2.9f, mid));
            CollectibleCoin.Create(transform, new Vector3(0f, 2.8f, channelEnd - 0.8f));
        }

        void BuildMineCartStage()
        {
            GetSpecialChannel(out float channelStart, out float channelEnd, out float channelLen);
            float mid = (channelStart + channelEnd) * 0.5f;

            var marker = gameObject.AddComponent<SpecialStageMarker>();
            marker.Configure(SpecialStageKind.MineCart, channelStart, channelEnd, PathStartDistance, _runDifficulty);

            // Dual-track rail beds (left lane 0 / right lane 2) across the channel.
            var rails = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rails.name = "MineRails";
            rails.transform.SetParent(transform, false);
            rails.transform.localPosition = new Vector3(0f, -0.05f, mid);
            rails.transform.localScale = new Vector3(DeckWidth * 0.92f, 0.18f, channelLen);
            rails.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            StripCollider(rails);

            float trackX = PlayerController.LaneWidth;
            for (int side = -1; side <= 1; side += 2)
            {
                // Outer + inner rail for each track.
                for (int inner = 0; inner < 2; inner++)
                {
                    float x = side * trackX + (inner == 0 ? -0.35f : 0.35f);
                    var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rail.name = side < 0 ? "LeftTrackRail" : "RightTrackRail";
                    rail.transform.SetParent(transform, false);
                    rail.transform.localPosition = new Vector3(x, 0.05f, mid);
                    rail.transform.localScale = new Vector3(0.12f, 0.1f, channelLen);
                    rail.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
                    StripCollider(rail);
                }

                // Switch ties between dual tracks.
                for (int t = 0; t < 4; t++)
                {
                    float z = Mathf.Lerp(channelStart + 0.8f, channelEnd - 0.8f, (t + 0.5f) / 4f);
                    var tie = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tie.transform.SetParent(transform, false);
                    tie.transform.localPosition = new Vector3(side * trackX, 0.02f, z);
                    tie.transform.localScale = new Vector3(1.1f, 0.08f, 0.28f);
                    tie.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
                    StripCollider(tie);
                }
            }

            // Cross-switch plate mid-channel — telegraph that lanes matter.
            var switchPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            switchPlate.name = "TrackSwitchPlate";
            switchPlate.transform.SetParent(transform, false);
            switchPlate.transform.localPosition = new Vector3(0f, 0.08f, mid);
            switchPlate.transform.localScale = new Vector3(DeckWidth * 0.85f, 0.06f, 1.2f);
            switchPlate.GetComponent<Renderer>().sharedMaterial = JunglePalette.Gold;
            StripCollider(switchPlate);

            var rideGo = new GameObject("MineCartRide");
            rideGo.transform.SetParent(transform, false);
            var ride = rideGo.AddComponent<MineCartRide>();
            ride.Marker = marker;
            ride.TrackSpacing = trackX;
            ride.BuildVisual(rideGo.transform);

            var mountGo = new GameObject("MineCartMount");
            mountGo.transform.SetParent(transform, false);
            mountGo.transform.localPosition = new Vector3(0f, 1f, channelStart + 0.2f);
            var mountCol = mountGo.AddComponent<BoxCollider>();
            mountCol.isTrigger = true;
            mountCol.size = new Vector3(DeckWidth + 0.5f, 2.5f, 1.8f);
            var mount = mountGo.AddComponent<MineCartMount>();
            mount.Marker = marker;
            mount.Ride = ride;

            // Broken-rail telegraphs — must switch dual tracks before impact.
            int breaks = _runDifficulty == RunDifficulty.Easy ? 1
                : _runDifficulty == RunDifficulty.Hard ? 3 : 2;
            int lastBroken = -1;
            for (int i = 0; i < breaks; i++)
            {
                float z = Mathf.Lerp(channelStart + 1.6f, channelEnd - 1.4f, (i + 1f) / (breaks + 1f));
                int brokenLane = Random.value < 0.5f ? 0 : 2;
                if (brokenLane == lastBroken) brokenLane = brokenLane == 0 ? 2 : 0;
                lastBroken = brokenLane;
                ride.AddBrokenRail(transform, z, brokenLane);
            }

            // Low beams + side rock walls — dodge lanes / slide.
            int hazards = _runDifficulty == RunDifficulty.Easy ? 1
                : _runDifficulty == RunDifficulty.Hard ? 3 : 2;
            int usedMask = 0;
            for (int i = 0; i < hazards; i++)
            {
                float z = Mathf.Lerp(channelStart + 1.3f, channelEnd - 1.3f, (i + 0.5f) / (hazards + 0.5f));
                if (i % 2 == 0)
                {
                    Obstacle.CreateLowBeam(transform, z, Random.Range(0, 3));
                }
                else
                {
                    int lane = Random.Range(0, 3);
                    int tries = 0;
                    while ((usedMask & (1 << lane)) != 0 && tries++ < 5)
                        lane = Random.Range(0, 3);
                    usedMask |= 1 << lane;
                    Obstacle.CreateBoatDebris(transform, z, lane);
                }
            }

            CollectibleCoin.Create(transform, new Vector3(-trackX, 1.5f, channelStart + 0.9f));
            CollectibleCoin.Create(transform, new Vector3(trackX, 1.6f, mid));
            CollectibleCoin.Create(transform, new Vector3(-trackX, 1.5f, channelEnd - 0.6f));
            if (_runDifficulty != RunDifficulty.Easy && Random.value < 0.45f)
                GemPickup.Create(transform, new Vector3(trackX, 1.55f, mid + 1f));
        }

        void BuildIceSlopeShell()
        {
            // Snow banks flanking the iced slope.
            for (int side = -1; side <= 1; side += 2)
            {
                var bank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bank.name = "SnowBank";
                bank.transform.SetParent(transform, false);
                bank.transform.localPosition = new Vector3(side * (DeckWidth * 0.5f + 2.4f), 0.4f, Length * 0.5f);
                bank.transform.localScale = new Vector3(3.2f, 1.6f, Length * 0.95f);
                bank.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.85f, 0.92f, 0.98f), 0.35f);
                StripCollider(bank);

                for (int i = 0; i < 3; i++)
                {
                    var spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    spike.transform.SetParent(transform, false);
                    spike.transform.localPosition = new Vector3(
                        side * (DeckWidth * 0.5f + 1.6f + Random.Range(0f, 1.2f)),
                        1.1f + Random.Range(0f, 0.6f),
                        Length * (0.2f + i * 0.25f));
                    spike.transform.localScale = new Vector3(0.35f, Random.Range(0.8f, 1.4f), 0.35f);
                    spike.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.7f, 0.85f, 0.95f), 0.55f, 0.2f);
                    StripCollider(spike);
                }
            }
        }

        void BuildIceRunwayFrost()
        {
            var frost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frost.name = "IceFrostSheet";
            frost.transform.SetParent(transform, false);
            frost.transform.localPosition = new Vector3(0f, 0.06f, Length * 0.5f);
            frost.transform.localScale = new Vector3(DeckWidth * 0.95f, 0.05f, Length * 0.85f);
            frost.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.78f, 0.9f, 0.98f), 0.7f, 0.15f);
            StripCollider(frost);
        }

        /// <summary>Cave runway dressing — overhanging stalactites, rock shelves, torch sconces, ore glints.</summary>
        void BuildCaveRunwayProps()
        {
            for (int i = 0; i < 4; i++)
            {
                float z = Length * ((i + 0.5f) / 4f);
                float side = (i % 2 == 0 ? -1f : 1f);
                var spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spike.name = "Stalactite";
                spike.transform.SetParent(transform, false);
                spike.transform.localPosition = new Vector3(side * Random.Range(0.6f, DeckWidth * 0.4f), 3.6f, z);
                spike.transform.localScale = new Vector3(0.22f, Random.Range(0.7f, 1.4f), 0.22f);
                spike.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                StripCollider(spike);
            }

            // Side rock shelves so the corridor reads enclosed (not open jungle walls).
            for (int side = -1; side <= 1; side += 2)
            {
                var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shelf.name = "CaveRockShelf";
                shelf.transform.SetParent(transform, false);
                shelf.transform.localPosition = new Vector3(side * (DeckWidth * 0.5f + 1.5f), 1.6f, Length * 0.5f);
                shelf.transform.localScale = new Vector3(2.4f, 3.4f, Length * 0.85f);
                shelf.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
                StripCollider(shelf);

                var torch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                torch.name = "CaveTorch";
                torch.transform.SetParent(transform, false);
                torch.transform.localPosition = new Vector3(side * (DeckWidth * 0.5f + 0.55f), 1.7f, Length * Random.Range(0.3f, 0.7f));
                torch.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
                torch.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
                StripCollider(torch);

                var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flame.transform.SetParent(torch.transform, false);
                flame.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                flame.transform.localScale = new Vector3(1.6f, 2.2f, 1.6f);
                flame.GetComponent<Renderer>().sharedMaterial = JunglePalette.Flame;
                StripCollider(flame);

                var light = new GameObject("TorchLight").AddComponent<Light>();
                light.transform.SetParent(torch.transform, false);
                light.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.55f, 0.25f);
                light.intensity = 1.4f;
                light.range = 6.5f;
            }

            if (Random.value < 0.5f)
            {
                var ore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ore.name = "OreGlint";
                ore.transform.SetParent(transform, false);
                ore.transform.localPosition = new Vector3(
                    (Random.value < 0.5f ? -1f : 1f) * (DeckWidth * 0.5f + 1.1f),
                    1.2f,
                    Length * Random.Range(0.3f, 0.7f));
                ore.transform.localScale = Vector3.one * 0.35f;
                ore.GetComponent<Renderer>().sharedMaterial = JunglePalette.GoldBright;
                StripCollider(ore);
            }

            // Occasional floor mist so cave runs feel damp vs desert/jungle.
            if (Random.value < 0.55f)
            {
                for (int i = 0; i < 3; i++)
                {
                    var mist = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    mist.name = "CaveMist";
                    mist.transform.SetParent(transform, false);
                    mist.transform.localPosition = new Vector3(
                        Random.Range(-1.2f, 1.2f), 0.35f, Length * ((i + 1f) / 4f));
                    mist.transform.localScale = new Vector3(1.8f, 0.45f, 1.4f);
                    mist.GetComponent<Renderer>().sharedMaterial =
                        JunglePalette.Mat(new Color(0.35f, 0.4f, 0.45f, 0.35f), 0.05f);
                    StripCollider(mist);
                }
            }
        }

        /// <summary>Desert-specific deck dressing — sand drifts + broken sandstone arch.</summary>
        void BuildDesertRunwayProps()
        {
            for (int i = 0; i < 3; i++)
            {
                var drift = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                drift.name = "SandDrift";
                drift.transform.SetParent(transform, false);
                float side = Random.value < 0.5f ? -1f : 1f;
                drift.transform.localPosition = new Vector3(
                    side * (DeckWidth * 0.28f + Random.Range(0f, 0.45f)),
                    0.08f,
                    Length * Random.Range(0.2f, 0.85f));
                drift.transform.localScale = new Vector3(
                    Random.Range(0.9f, 1.5f), Random.Range(0.18f, 0.32f), Random.Range(1.1f, 1.8f));
                drift.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
                StripCollider(drift);
            }

            if (Random.value < 0.55f)
            {
                float z = Length * Random.Range(0.35f, 0.7f);
                for (int side = -1; side <= 1; side += 2)
                {
                    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pillar.name = "SandstonePillar";
                    pillar.transform.SetParent(transform, false);
                    pillar.transform.localPosition = new Vector3(side * (DeckWidth * 0.52f + 0.2f), 1.4f, z);
                    pillar.transform.localScale = new Vector3(0.45f, 2.8f, 0.45f);
                    pillar.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                    StripCollider(pillar);
                }
                var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = "SandstoneLintel";
                lintel.transform.SetParent(transform, false);
                lintel.transform.localPosition = new Vector3(0f, 2.85f, z);
                lintel.transform.localScale = new Vector3(DeckWidth + 0.9f, 0.35f, 0.4f);
                lintel.GetComponent<Renderer>().sharedMaterial = BiomeSystem.AccentMat;
                StripCollider(lintel);
            }
        }

        /// <summary>Ice-specific deck dressing — packed snow banks + frozen arch crystals.</summary>
        void BuildIceRunwayProps()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var bank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bank.name = "PackedSnowBank";
                bank.transform.SetParent(transform, false);
                bank.transform.localPosition = new Vector3(side * (DeckWidth * 0.55f + 0.7f), 0.35f, Length * 0.5f);
                bank.transform.localScale = new Vector3(1.4f, 0.9f, Length * 0.75f);
                bank.GetComponent<Renderer>().sharedMaterial =
                    JunglePalette.Mat(new Color(0.88f, 0.94f, 1f), 0.45f);
                StripCollider(bank);
            }

            if (Random.value < 0.6f)
            {
                float z = Length * Random.Range(0.3f, 0.75f);
                for (int side = -1; side <= 1; side += 2)
                {
                    var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    crystal.transform.SetParent(transform, false);
                    crystal.transform.localPosition = new Vector3(side * (DeckWidth * 0.5f + 0.35f), 1.5f, z);
                    crystal.transform.localRotation = Quaternion.Euler(0f, 0f, side * 12f);
                    crystal.transform.localScale = new Vector3(0.35f, 2.6f, 0.35f);
                    crystal.GetComponent<Renderer>().sharedMaterial =
                        JunglePalette.Mat(new Color(0.7f, 0.88f, 1f), 0.75f, 0.3f);
                    StripCollider(crystal);
                }
                var arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arch.transform.SetParent(transform, false);
                arch.transform.localPosition = new Vector3(0f, 2.9f, z);
                arch.transform.localScale = new Vector3(DeckWidth + 0.7f, 0.28f, 0.28f);
                arch.GetComponent<Renderer>().sharedMaterial = BiomeSystem.AccentMat;
                StripCollider(arch);
            }

            SpawnIceCrystalCluster(transform, new Vector3(
                (Random.value < 0.5f ? -1f : 1f) * (DeckWidth * 0.5f + 1.6f),
                ForestFloorY,
                Length * Random.Range(0.25f, 0.8f)), Random.Range(0.85f, 1.2f));
        }

        /// <summary>Enclosed ruins hallway — carved walls, torch sconces, idol niches.</summary>
        void BuildTempleHallInterior()
        {
            float mid = Length * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "HallWall";
                wall.transform.SetParent(transform, false);
                wall.transform.localPosition = new Vector3(side * (DeckWidth * 0.5f + 1.55f), 2.35f, mid);
                wall.transform.localScale = new Vector3(2.4f, 4.8f, Length * 0.98f);
                wall.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                StripCollider(wall);

                // Recessed niches with small idol blocks.
                for (int i = 0; i < 3; i++)
                {
                    float z = Length * ((i + 1f) / 4f);
                    var niche = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    niche.transform.SetParent(transform, false);
                    niche.transform.localPosition = new Vector3(side * (DeckWidth * 0.5f + 0.55f), 1.55f, z);
                    niche.transform.localScale = new Vector3(0.55f, 1.4f, 1.1f);
                    niche.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
                    StripCollider(niche);

                    var idol = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    idol.transform.SetParent(transform, false);
                    idol.transform.localPosition = new Vector3(side * (DeckWidth * 0.5f + 0.35f), 1.55f, z);
                    idol.transform.localScale = new Vector3(0.35f, 0.85f, 0.35f);
                    idol.GetComponent<Renderer>().sharedMaterial = JunglePalette.Gold;
                    StripCollider(idol);
                }
            }

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "HallCeiling";
            ceiling.transform.SetParent(transform, false);
            ceiling.transform.localPosition = new Vector3(0f, 4.5f, mid);
            ceiling.transform.localScale = new Vector3(DeckWidth + 3.6f, 0.55f, Length * 0.98f);
            ceiling.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            StripCollider(ceiling);

            // Column pairs + torch sconces along the hall.
            for (int i = 0; i < 4; i++)
            {
                float z = Length * ((i + 0.5f) / 4f);
                for (int side = -1; side <= 1; side += 2)
                {
                    var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    col.name = "HallColumn";
                    col.transform.SetParent(transform, false);
                    col.transform.localPosition = new Vector3(side * (DeckWidth * 0.48f + 0.05f), 1.9f, z);
                    col.transform.localScale = new Vector3(0.42f, 1.9f, 0.42f);
                    col.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                    StripCollider(col);

                    var torch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    torch.name = "TorchFlame";
                    torch.transform.SetParent(transform, false);
                    torch.transform.localPosition = new Vector3(side * (DeckWidth * 0.48f + 0.35f), 2.6f, z);
                    torch.transform.localScale = Vector3.one * 0.28f;
                    torch.GetComponent<Renderer>().sharedMaterial = JunglePalette.Flame;
                    StripCollider(torch);
                }

                var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.transform.SetParent(transform, false);
                beam.transform.localPosition = new Vector3(0f, 3.85f, z);
                beam.transform.localScale = new Vector3(DeckWidth + 0.8f, 0.22f, 0.28f);
                beam.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
                StripCollider(beam);
            }

            // Floor inlay mosaic strip.
            var inlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
            inlay.name = "HallInlay";
            inlay.transform.SetParent(transform, false);
            inlay.transform.localPosition = new Vector3(0f, 0.02f, mid);
            inlay.transform.localScale = new Vector3(1.1f, 0.04f, Length * 0.9f);
            inlay.GetComponent<Renderer>().sharedMaterial = JunglePalette.Gold;
            StripCollider(inlay);
        }

        void BuildTempleHallContents()
        {
            ScatterCoins(6);
            if (Random.value < 0.35f) SpawnRelicOrGem();
            if (Random.value < 0.4f) SpawnPowerUp();

            // Occasional slide beam mid-hall — classic enclosed-corridor challenge.
            if (Random.value < 0.55f)
                Obstacle.CreateLowBeam(transform, Length * Random.Range(0.4f, 0.7f), Random.Range(0, 3));
            if (_runDifficulty != RunDifficulty.Easy && Random.value < 0.35f)
                Obstacle.CreateSpike(transform, Length * Random.Range(0.55f, 0.85f), Random.Range(0, 3));
            // Ceiling slam traps with telegraph windows in enclosed halls.
            if (_runDifficulty != RunDifficulty.Easy && Random.value < 0.4f)
                DynamicHazard.CreateCeilingSlam(transform, Length * Random.Range(0.45f, 0.75f), Random.Range(0, 3));
        }

        void BuildBiomeTransitionTunnelShell()
        {
            BiomeId from = BiomeSystem.Current;
            BiomeId to = BiomeSystem.PendingTransitionTarget;
            if (!BiomeSystem.TransitionActive || to == from)
                to = BiomeSystem.PeekNextUnlockedBiome();

            float mid = Length * 0.5f;
            // Near half keeps the current biome, far half previews the destination.
            for (int side = -1; side <= 1; side += 2)
            {
                var nearWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                nearWall.name = "TransitionWallNear";
                nearWall.transform.SetParent(transform, false);
                nearWall.transform.localPosition = new Vector3(side * (DeckWidth * 0.5f + 1.9f), 2.1f, mid * 0.55f);
                nearWall.transform.localScale = new Vector3(2.6f, 4.4f, Length * 0.55f);
                nearWall.GetComponent<Renderer>().sharedMaterial = BiomePalette.Stone(from);
                StripCollider(nearWall);

                var farWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                farWall.name = "TransitionWallFar";
                farWall.transform.SetParent(transform, false);
                farWall.transform.localPosition = new Vector3(side * (DeckWidth * 0.5f + 1.9f), 2.1f, mid + Length * 0.22f);
                farWall.transform.localScale = new Vector3(2.6f, 4.4f, Length * 0.45f);
                farWall.GetComponent<Renderer>().sharedMaterial = BiomePalette.Stone(to);
                StripCollider(farWall);
            }

            var nearCeil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nearCeil.name = "TransitionCeilNear";
            nearCeil.transform.SetParent(transform, false);
            nearCeil.transform.localPosition = new Vector3(0f, 4.3f, mid * 0.55f);
            nearCeil.transform.localScale = new Vector3(DeckWidth + 4.2f, 0.55f, Length * 0.55f);
            nearCeil.GetComponent<Renderer>().sharedMaterial = BiomePalette.Stone(from);
            StripCollider(nearCeil);

            var farCeil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            farCeil.name = "TransitionCeilFar";
            farCeil.transform.SetParent(transform, false);
            farCeil.transform.localPosition = new Vector3(0f, 4.3f, mid + Length * 0.22f);
            farCeil.transform.localScale = new Vector3(DeckWidth + 4.2f, 0.55f, Length * 0.45f);
            farCeil.GetComponent<Renderer>().sharedMaterial = BiomePalette.Stone(to);
            StripCollider(farCeil);

            // Crossfade path inlays — current → destination.
            var nearPath = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nearPath.name = "TransitionPathNear";
            nearPath.transform.SetParent(transform, false);
            nearPath.transform.localPosition = new Vector3(0f, 0.03f, mid * 0.55f);
            nearPath.transform.localScale = new Vector3(DeckWidth * 0.92f, 0.05f, Length * 0.5f);
            nearPath.GetComponent<Renderer>().sharedMaterial = BiomePalette.Path(from);
            StripCollider(nearPath);

            var farPath = GameObject.CreatePrimitive(PrimitiveType.Cube);
            farPath.name = "TransitionPathFar";
            farPath.transform.SetParent(transform, false);
            farPath.transform.localPosition = new Vector3(0f, 0.03f, mid + Length * 0.22f);
            farPath.transform.localScale = new Vector3(DeckWidth * 0.92f, 0.05f, Length * 0.45f);
            farPath.GetComponent<Renderer>().sharedMaterial = BiomePalette.Path(to);
            StripCollider(farPath);

            // Gold threshold arch at the biome seam.
            var seam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seam.name = "BiomeSeam";
            seam.transform.SetParent(transform, false);
            seam.transform.localPosition = new Vector3(0f, 2.2f, mid);
            seam.transform.localScale = new Vector3(DeckWidth + 1.2f, 0.22f, 0.35f);
            seam.GetComponent<Renderer>().sharedMaterial = JunglePalette.GoldBright;
            StripCollider(seam);
        }

        void BuildBiomeTransitionTunnelContents()
        {
            BiomeId target = BiomeSystem.PendingTransitionTarget;
            if (!BiomeSystem.TransitionActive || target == BiomeSystem.Current)
            {
                target = BiomeSystem.PeekNextUnlockedBiome();
                BiomeSystem.BeginRunTransition(target);
            }

            ScatterCoins(5);
            if (Random.value < 0.45f) SpawnPowerUp();

            var trigger = new GameObject("BiomeTransitionCommit");
            trigger.transform.SetParent(transform, false);
            trigger.transform.localPosition = new Vector3(0f, 1f, Length - 1.35f);
            var col = trigger.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(DeckWidth + 0.8f, 2.6f, 1.5f);
            var commit = trigger.AddComponent<BiomeTransitionCommitTrigger>();
            commit.Target = target;
        }

        void BuildIceSurfStage()
        {
            GetSpecialChannel(out float channelStart, out float channelEnd, out float channelLen);
            float mid = (channelStart + channelEnd) * 0.5f;

            var marker = gameObject.AddComponent<SpecialStageMarker>();
            marker.Configure(SpecialStageKind.IceSurf, channelStart, channelEnd, PathStartDistance, _runDifficulty);

            var slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slope.name = "IceSlope";
            slope.transform.SetParent(transform, false);
            slope.transform.localPosition = new Vector3(0f, -0.08f, mid);
            slope.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
            slope.transform.localScale = new Vector3(DeckWidth * 0.98f, 0.22f, channelLen);
            slope.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.72f, 0.86f, 0.96f), 0.65f, 0.25f);
            StripCollider(slope);

            for (int side = -1; side <= 1; side += 2)
            {
                GapKillZone.Create(transform, mid, side < 0 ? 0 : 2, channelLen * 0.35f, "Slid off the ice");
            }

            var rideGo = new GameObject("IceBoardRide");
            rideGo.transform.SetParent(transform, false);
            var ride = rideGo.AddComponent<IceBoardRide>();
            ride.Marker = marker;
            ride.BuildVisual(rideGo.transform);

            var mountGo = new GameObject("IceSurfMount");
            mountGo.transform.SetParent(transform, false);
            mountGo.transform.localPosition = new Vector3(0f, 0.9f, channelStart + 0.2f);
            var mountCol = mountGo.AddComponent<BoxCollider>();
            mountCol.isTrigger = true;
            mountCol.size = new Vector3(DeckWidth + 0.5f, 2.4f, 1.8f);
            var mount = mountGo.AddComponent<IceSurfMount>();
            mount.Marker = marker;
            mount.Ride = ride;

            int hazards = _runDifficulty == RunDifficulty.Easy ? 2
                : _runDifficulty == RunDifficulty.Hard ? 4 : 3;
            for (int i = 0; i < hazards; i++)
            {
                float z = Mathf.Lerp(channelStart + 1.2f, channelEnd - 1.2f, (i + 1f) / (hazards + 1f));
                if (i % 2 == 0)
                    Obstacle.CreateLowBeam(transform, z, Random.Range(0, 3));
                else
                    Obstacle.CreateBoatDebris(transform, z, Random.Range(0, 3));
            }

            CollectibleCoin.Create(transform, new Vector3(0f, 1.2f, channelStart + 0.8f));
            CollectibleCoin.Create(transform, new Vector3(0f, 1.15f, mid));
            CollectibleCoin.Create(transform, new Vector3(0f, 1.1f, channelEnd - 0.7f));
            if (_runDifficulty != RunDifficulty.Easy && Random.value < 0.4f)
                GemPickup.Create(transform, new Vector3(-PlayerController.LaneWidth, 1.25f, mid + 0.8f));
        }

        void BuildWallRunStage()
        {
            GetSpecialChannel(out float channelStart, out float channelEnd, out float channelLen);
            float mid = (channelStart + channelEnd) * 0.5f;

            var marker = gameObject.AddComponent<SpecialStageMarker>();
            marker.Configure(SpecialStageKind.WallRun, channelStart, channelEnd, PathStartDistance, _runDifficulty);

            var voidBed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            voidBed.name = "WallRunVoid";
            voidBed.transform.SetParent(transform, false);
            voidBed.transform.localPosition = new Vector3(0f, ForestFloorY - 1.2f, mid);
            voidBed.transform.localScale = new Vector3(ForestOuter * 1.8f + DeckWidth, 2f, channelLen + 1.5f);
            voidBed.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.12f, 0.14f, 0.12f), 0.2f);
            StripCollider(voidBed);

            for (int lane = 0; lane < 3; lane++)
                GapKillZone.Create(transform, mid, lane, channelLen, "Fell during wall run");

            for (int side = -1; side <= 1; side += 2)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = side < 0 ? "WallRunLeft" : "WallRunRight";
                wall.transform.SetParent(transform, false);
                wall.transform.localPosition = new Vector3(side * 2.55f, 1.6f, mid);
                wall.transform.localScale = new Vector3(0.55f, 3.4f, channelLen + 0.4f);
                wall.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                StripCollider(wall);

                // Climable face strip for silhouette readability.
                var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
                face.transform.SetParent(transform, false);
                face.transform.localPosition = new Vector3(side * 2.2f, 1.4f, mid);
                face.transform.localScale = new Vector3(0.12f, 2.6f, channelLen * 0.95f);
                face.GetComponent<Renderer>().sharedMaterial = BiomeSystem.AccentMat;
                StripCollider(face);
            }

            var mountGo = new GameObject("WallRunMount");
            mountGo.transform.SetParent(transform, false);
            mountGo.transform.localPosition = new Vector3(0f, 1.1f, channelStart + 0.25f);
            var mountCol = mountGo.AddComponent<BoxCollider>();
            mountCol.isTrigger = true;
            mountCol.size = new Vector3(DeckWidth + 0.8f, 3f, 1.8f);
            var mount = mountGo.AddComponent<WallRunMount>();
            mount.Marker = marker;
            mount.WallHeight = 1.85f;
            mount.WallOffset = 2.2f;

            int overhangs = _runDifficulty == RunDifficulty.Easy ? 1 : _runDifficulty == RunDifficulty.Hard ? 3 : 2;
            for (int i = 0; i < overhangs; i++)
            {
                float z = Mathf.Lerp(channelStart + 1.3f, channelEnd - 1.3f, (i + 1f) / (overhangs + 1f));
                int side = (i % 2 == 0) ? -1 : 1;
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.name = "WallOverhang";
                beam.transform.SetParent(transform, false);
                beam.transform.localPosition = new Vector3(side * 1.6f, 2.55f, z);
                beam.transform.localScale = new Vector3(1.8f, 0.35f, 0.55f);
                beam.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
                StripCollider(beam);
                Obstacle.CreateLowBeam(transform, z, side < 0 ? 0 : 2);
            }

            CollectibleCoin.Create(transform, new Vector3(-2.1f, 2.2f, channelStart + 1f));
            CollectibleCoin.Create(transform, new Vector3(2.1f, 2.3f, mid));
            CollectibleCoin.Create(transform, new Vector3(-2.1f, 2.2f, channelEnd - 0.8f));
        }

        void BuildLedgeGrabStage()
        {
            GetSpecialChannel(out float channelStart, out float channelEnd, out float channelLen);
            float mid = (channelStart + channelEnd) * 0.5f;

            var marker = gameObject.AddComponent<SpecialStageMarker>();
            marker.Configure(SpecialStageKind.LedgeGrab, channelStart, channelEnd, PathStartDistance, _runDifficulty);

            var voidBed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            voidBed.name = "LedgeVoid";
            voidBed.transform.SetParent(transform, false);
            voidBed.transform.localPosition = new Vector3(0f, ForestFloorY - 1.4f, mid);
            voidBed.transform.localScale = new Vector3(ForestOuter * 1.6f + DeckWidth, 2f, channelLen + 1.5f);
            voidBed.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.08f, 0.1f, 0.09f), 0.15f);
            StripCollider(voidBed);

            for (int lane = 0; lane < 3; lane++)
                GapKillZone.Create(transform, mid, lane, channelLen, "Lost grip on the ledge");

            // Three staggered hang-ledges across the ravine.
            float[] zs = { channelStart + channelLen * 0.28f, mid, channelEnd - channelLen * 0.28f };
            float[] xs = { -PlayerController.LaneWidth, 0f, PlayerController.LaneWidth };
            for (int i = 0; i < zs.Length; i++)
            {
                var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ledge.name = "HangLedge";
                ledge.transform.SetParent(transform, false);
                ledge.transform.localPosition = new Vector3(xs[i], 2.05f, zs[i]);
                ledge.transform.localScale = new Vector3(1.5f, 0.28f, 1.1f);
                ledge.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
                StripCollider(ledge);

                var lip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lip.transform.SetParent(transform, false);
                lip.transform.localPosition = new Vector3(xs[i], 1.85f, zs[i]);
                lip.transform.localScale = new Vector3(1.55f, 0.12f, 0.25f);
                lip.GetComponent<Renderer>().sharedMaterial = BiomeSystem.AccentMat;
                StripCollider(lip);
            }

            var mountGo = new GameObject("LedgeGrabMount");
            mountGo.transform.SetParent(transform, false);
            mountGo.transform.localPosition = new Vector3(0f, 1.4f, channelStart + 0.2f);
            var mountCol = mountGo.AddComponent<BoxCollider>();
            mountCol.isTrigger = true;
            mountCol.size = new Vector3(DeckWidth + 0.6f, 3.2f, 1.8f);
            var mount = mountGo.AddComponent<LedgeGrabMount>();
            mount.Marker = marker;
            mount.LedgeHeight = 1.75f;

            CollectibleCoin.Create(transform, new Vector3(xs[0], 2.5f, zs[0]));
            CollectibleCoin.Create(transform, new Vector3(xs[1], 2.55f, zs[1]));
            CollectibleCoin.Create(transform, new Vector3(xs[2], 2.5f, zs[2]));
            if (_runDifficulty != RunDifficulty.Easy && Random.value < 0.35f)
                RelicPickup.Create(transform, new Vector3(0f, 2.6f, mid));
        }

        void BuildTreeBridgeStage()
        {
            GetSpecialChannel(out float channelStart, out float channelEnd, out float channelLen);
            float mid = (channelStart + channelEnd) * 0.5f;

            var marker = gameObject.AddComponent<SpecialStageMarker>();
            marker.Configure(SpecialStageKind.TreeBridge, channelStart, channelEnd, PathStartDistance, _runDifficulty);

            var voidBed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            voidBed.name = "TreeBridgeVoid";
            voidBed.transform.SetParent(transform, false);
            voidBed.transform.localPosition = new Vector3(0f, ForestFloorY - 1.3f, mid);
            voidBed.transform.localScale = new Vector3(ForestOuter * 1.7f + DeckWidth, 2f, channelLen + 1.8f);
            voidBed.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.1f, 0.16f, 0.1f), 0.15f);
            StripCollider(voidBed);

            // Mist under the canopy crossing.
            var mist = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mist.name = "CanopyMist";
            mist.transform.SetParent(transform, false);
            mist.transform.localPosition = new Vector3(0f, ForestFloorY + 0.6f, mid);
            mist.transform.localScale = new Vector3(DeckWidth + 6f, 0.4f, channelLen * 0.9f);
            mist.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.55f, 0.7f, 0.5f, 0.35f), 0.1f);
            StripCollider(mist);

            int safeLaneA = Random.Range(0, 3);
            int safeLaneB = _runDifficulty == RunDifficulty.Hard ? safeLaneA : (safeLaneA + 1 + Random.Range(0, 2)) % 3;

            for (int lane = 0; lane < 3; lane++)
            {
                bool safe = lane == safeLaneA || lane == safeLaneB;
                if (!safe)
                {
                    GapKillZone.Create(transform, mid, lane, channelLen, "Fell from the tree bridge");
                    continue;
                }

                // Multi-log causeway segments.
                int segs = _runDifficulty == RunDifficulty.Easy ? 3 : 4;
                for (int s = 0; s < segs; s++)
                {
                    float z = Mathf.Lerp(channelStart + 0.6f, channelEnd - 0.6f, (s + 0.5f) / segs);
                    float x = (lane - 1) * PlayerController.LaneWidth;
                    var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    log.name = "BridgeLog";
                    log.transform.SetParent(transform, false);
                    log.transform.localPosition = new Vector3(x, 0.28f, z);
                    log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    log.transform.localScale = new Vector3(0.55f, 0.95f, 0.55f);
                    log.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
                    StripCollider(log);

                    var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    plank.transform.SetParent(transform, false);
                    plank.transform.localPosition = new Vector3(x, 0.12f, z);
                    plank.transform.localScale = new Vector3(1.55f, 0.18f, channelLen / segs * 0.85f);
                    plank.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.45f, 0.32f, 0.18f), 0.25f);
                    StripCollider(plank);

                    // Hard: some mid segments crumble underfoot.
                    if (_runDifficulty == RunDifficulty.Hard && s > 0 && s < segs - 1 && Random.value < 0.35f)
                        Obstacle.CreateCollapsingBridge(transform, z, lane);
                }
            }

            // Support trunks at the banks.
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? channelStart : channelEnd;
                for (int i = -1; i <= 1; i++)
                {
                    var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    trunk.transform.SetParent(transform, false);
                    trunk.transform.localPosition = new Vector3(i * 1.4f, 1.1f, z);
                    trunk.transform.localScale = new Vector3(0.45f, 1.2f, 0.45f);
                    trunk.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
                    StripCollider(trunk);
                }
            }

            int vines = _runDifficulty == RunDifficulty.Easy ? 1 : 2;
            for (int i = 0; i < vines; i++)
            {
                float z = Mathf.Lerp(channelStart + 1.2f, channelEnd - 1.2f, (i + 1f) / (vines + 1f));
                Obstacle.CreateLowBeam(transform, z, safeLaneA);
            }

            float sx = (safeLaneA - 1) * PlayerController.LaneWidth;
            CollectibleCoin.Create(transform, new Vector3(sx, 1.2f, channelStart + 0.9f));
            CollectibleCoin.Create(transform, new Vector3(sx, 1.25f, mid));
            CollectibleCoin.Create(transform, new Vector3(sx, 1.2f, channelEnd - 0.7f));
            if (_runDifficulty != RunDifficulty.Easy && Random.value < 0.4f)
                GemPickup.Create(transform, new Vector3(sx, 1.35f, mid + 1f));
        }

        void BuildCanopyRopeStage()
        {
            GetSpecialChannel(out float channelStart, out float channelEnd, out float channelLen);
            float mid = (channelStart + channelEnd) * 0.5f;

            var marker = gameObject.AddComponent<SpecialStageMarker>();
            marker.Configure(SpecialStageKind.CanopyRope, channelStart, channelEnd, PathStartDistance, _runDifficulty);

            var voidBed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            voidBed.name = "CanopyVoid";
            voidBed.transform.SetParent(transform, false);
            voidBed.transform.localPosition = new Vector3(0f, ForestFloorY - 1.4f, mid);
            voidBed.transform.localScale = new Vector3(ForestOuter * 2f + DeckWidth, 2f, channelLen + 2f);
            voidBed.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.08f, 0.14f, 0.1f), 0.15f);
            StripCollider(voidBed);

            for (int lane = 0; lane < 3; lane++)
                GapKillZone.Create(transform, mid, lane, channelLen, "Fell from the canopy rope");

            // Twin canopy trunks + thick rope.
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? channelStart : channelEnd;
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.transform.SetParent(transform, false);
                trunk.transform.localPosition = new Vector3(0f, 2.4f, z);
                trunk.transform.localScale = new Vector3(0.55f, 2.5f, 0.55f);
                trunk.GetComponent<Renderer>().sharedMaterial = JunglePalette.Bark;
                StripCollider(trunk);

                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.transform.SetParent(transform, false);
                crown.transform.localPosition = new Vector3(0f, 4.6f, z);
                crown.transform.localScale = Vector3.one * 2.2f;
                crown.GetComponent<Renderer>().sharedMaterial = BiomeSystem.FoliageMat;
                StripCollider(crown);
            }

            var rope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rope.name = "CanopyRope";
            rope.transform.SetParent(transform, false);
            rope.transform.localPosition = new Vector3(0f, 3.55f, mid);
            rope.transform.localScale = new Vector3(0.12f, 0.12f, channelLen);
            rope.GetComponent<Renderer>().sharedMaterial = JunglePalette.Rope;
            StripCollider(rope);

            // Hanging vine fringe for a denser canopy read.
            int vines = _runDifficulty == RunDifficulty.Easy ? 3 : 5;
            for (int i = 0; i < vines; i++)
            {
                float z = Mathf.Lerp(channelStart + 0.8f, channelEnd - 0.8f, (i + 0.5f) / vines);
                float x = ((i % 3) - 1) * 0.7f;
                var vine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                vine.transform.SetParent(transform, false);
                vine.transform.localPosition = new Vector3(x, 2.6f, z);
                vine.transform.localScale = new Vector3(0.08f, 0.9f + (i % 2) * 0.35f, 0.08f);
                vine.GetComponent<Renderer>().sharedMaterial = JunglePalette.FoliageDark;
                StripCollider(vine);
            }

            var mountGo = new GameObject("CanopyRopeMount");
            mountGo.transform.SetParent(transform, false);
            mountGo.transform.localPosition = new Vector3(0f, 1.2f, channelStart + 0.25f);
            var mountCol = mountGo.AddComponent<BoxCollider>();
            mountCol.isTrigger = true;
            mountCol.size = new Vector3(DeckWidth + 0.6f, 3f, 1.8f);
            var mount = mountGo.AddComponent<CanopyRopeMount>();
            mount.Marker = marker;
            mount.RideHeight = 2.35f;

            int beams = _runDifficulty == RunDifficulty.Easy ? 1 : _runDifficulty == RunDifficulty.Hard ? 3 : 2;
            for (int i = 0; i < beams; i++)
            {
                float z = Mathf.Lerp(channelStart + 1.5f, channelEnd - 1.5f, (i + 1f) / (beams + 1f));
                Obstacle.CreateLowBeam(transform, z, 1);
            }

            CollectibleCoin.Create(transform, new Vector3(0f, 2.6f, channelStart + 1f));
            CollectibleCoin.Create(transform, new Vector3(0f, 2.7f, mid));
            CollectibleCoin.Create(transform, new Vector3(0f, 2.6f, channelEnd - 0.8f));
            if (_runDifficulty != RunDifficulty.Easy && Random.value < 0.35f)
                GemPickup.Create(transform, new Vector3(0f, 2.9f, mid + 0.6f));
        }

        void BuildWaterfallPlungeStage()
        {
            GetSpecialChannel(out float channelStart, out float channelEnd, out float channelLen);
            float mid = (channelStart + channelEnd) * 0.5f;

            var marker = gameObject.AddComponent<SpecialStageMarker>();
            marker.Configure(SpecialStageKind.WaterfallPlunge, channelStart, channelEnd, PathStartDistance, _runDifficulty);

            // Plunge pool under the sheet.
            var pool = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pool.name = "PlungePool";
            pool.transform.SetParent(transform, false);
            pool.transform.localPosition = new Vector3(0f, ForestFloorY + 0.15f, mid);
            pool.transform.localScale = new Vector3(ForestOuter * 1.6f + DeckWidth, 0.35f, channelLen + 1.5f);
            pool.GetComponent<Renderer>().sharedMaterial = JunglePalette.Water;
            StripCollider(pool);
            pool.AddComponent<RiverSurfaceScroll>();

            for (int lane = 0; lane < 3; lane++)
                GapKillZone.Create(transform, mid, lane, channelLen, "Swept over the waterfall");

            // Tall cascading sheets framing the plunge curtain.
            float sheetZ = channelStart + channelLen * 0.35f;
            SpawnWaterfall(transform, new Vector3(-DeckWidth * 0.55f, ForestFloorY, sheetZ), 5.5f, -1);
            SpawnWaterfall(transform, new Vector3(DeckWidth * 0.55f, ForestFloorY, sheetZ), 5.5f, 1);

            var sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sheet.name = "WaterfallSheet";
            sheet.transform.SetParent(transform, false);
            sheet.transform.localPosition = new Vector3(0f, 2.4f, channelStart + channelLen * 0.32f);
            sheet.transform.localScale = new Vector3(DeckWidth + 1.8f, 4.6f, 0.55f);
            sheet.GetComponent<Renderer>().sharedMaterial = JunglePalette.Water;
            StripCollider(sheet);
            sheet.AddComponent<RiverSurfaceScroll>();

            // Plunge foam + mist.
            var foam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            foam.name = "PlungeFoam";
            foam.transform.SetParent(transform, false);
            foam.transform.localPosition = new Vector3(0f, 0.35f, mid);
            foam.transform.localScale = new Vector3(DeckWidth + 2.5f, 0.25f, channelLen * 0.55f);
            foam.GetComponent<Renderer>().sharedMaterial = JunglePalette.Foam;
            StripCollider(foam);

            var mist = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mist.name = "PlungeMist";
            mist.transform.SetParent(transform, false);
            mist.transform.localPosition = new Vector3(0f, 1.4f, mid);
            mist.transform.localScale = new Vector3(DeckWidth + 3f, 1.8f, channelLen * 0.7f);
            mist.GetComponent<Renderer>().sharedMaterial = JunglePalette.Mat(new Color(0.7f, 0.85f, 0.9f, 0.28f), 0.05f);
            StripCollider(mist);

            var mountGo = new GameObject("WaterfallPlungeMount");
            mountGo.transform.SetParent(transform, false);
            mountGo.transform.localPosition = new Vector3(0f, 1.4f, channelStart + 0.2f);
            var mountCol = mountGo.AddComponent<BoxCollider>();
            mountCol.isTrigger = true;
            mountCol.size = new Vector3(DeckWidth + 0.8f, 3.5f, 2f);
            var mount = mountGo.AddComponent<WaterfallPlungeMount>();
            mount.Marker = marker;
            mount.DiveHeight = 3.6f;
            mount.PoolDepth = -0.35f;

            // Surface rocks after the plunge require a late jump/lane weave.
            int rocks = _runDifficulty == RunDifficulty.Easy ? 1 : 2;
            for (int i = 0; i < rocks; i++)
            {
                float z = Mathf.Lerp(mid + 0.5f, channelEnd - 1f, (i + 1f) / (rocks + 1f));
                int lane = (i + (_runDifficulty == RunDifficulty.Hard ? 0 : 1)) % 3;
                Obstacle.CreateBoatDebris(transform, z, lane);
            }

            CollectibleCoin.Create(transform, new Vector3(0f, 3.2f, channelStart + 0.8f));
            CollectibleCoin.Create(transform, new Vector3(0f, 0.9f, mid));
            CollectibleCoin.Create(transform, new Vector3(0f, 1.1f, channelEnd - 0.7f));
            if (Random.value < 0.45f)
                GemPickup.Create(transform, new Vector3(PlayerController.LaneWidth, 1.2f, mid + 0.8f));
        }

        /// <summary>Aqueduct water-slide — steer lanes down a flowing stone channel.</summary>
        void BuildWaterSlideStage()
        {
            GetSpecialChannel(out float channelStart, out float channelEnd, out float channelLen);
            float mid = (channelStart + channelEnd) * 0.5f;

            var marker = gameObject.AddComponent<SpecialStageMarker>();
            marker.Configure(SpecialStageKind.WaterSlide, channelStart, channelEnd, PathStartDistance, _runDifficulty);

            var trough = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trough.name = "AqueductTrough";
            trough.transform.SetParent(transform, false);
            trough.transform.localPosition = new Vector3(0f, -0.12f, mid);
            trough.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
            trough.transform.localScale = new Vector3(DeckWidth * 0.98f, 0.28f, channelLen);
            trough.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
            StripCollider(trough);

            var flow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flow.name = "SlideFlow";
            flow.transform.SetParent(transform, false);
            flow.transform.localPosition = new Vector3(0f, 0.02f, mid);
            flow.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
            flow.transform.localScale = new Vector3(DeckWidth * 0.88f, 0.12f, channelLen * 0.98f);
            var flowMat = new Material(JunglePalette.Water);
            flow.GetComponent<Renderer>().sharedMaterial = flowMat;
            StripCollider(flow);
            flow.AddComponent<RiverSurfaceScroll>();

            // Stone aqueduct walls + arches.
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "AqueductWall";
                wall.transform.SetParent(transform, false);
                wall.transform.localPosition = new Vector3(side * (DeckWidth * 0.52f), 0.55f, mid);
                wall.transform.localScale = new Vector3(0.35f, 1.1f, channelLen);
                wall.GetComponent<Renderer>().sharedMaterial = BiomeSystem.StoneMat;
                StripCollider(wall);
            }
            for (int i = 0; i < 3; i++)
            {
                float z = Mathf.Lerp(channelStart + 0.6f, channelEnd - 0.6f, (i + 0.5f) / 3f);
                var arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arch.transform.SetParent(transform, false);
                arch.transform.localPosition = new Vector3(0f, 1.45f, z);
                arch.transform.localScale = new Vector3(DeckWidth + 0.4f, 0.28f, 0.35f);
                arch.GetComponent<Renderer>().sharedMaterial = BiomeSystem.AccentMat;
                StripCollider(arch);
            }

            for (int side = -1; side <= 1; side += 2)
                GapKillZone.Create(transform, mid, side < 0 ? 0 : 2, channelLen * 0.3f, "Swept out of the aqueduct");

            var rideGo = new GameObject("WaterSlideRide");
            rideGo.transform.SetParent(transform, false);
            var ride = rideGo.AddComponent<WaterSlideRide>();
            ride.Marker = marker;
            ride.BuildVisual(rideGo.transform);
            ride.BindFlowMaterial(flowMat);

            var mountGo = new GameObject("WaterSlideMount");
            mountGo.transform.SetParent(transform, false);
            mountGo.transform.localPosition = new Vector3(0f, 0.9f, channelStart + 0.2f);
            var mountCol = mountGo.AddComponent<BoxCollider>();
            mountCol.isTrigger = true;
            mountCol.size = new Vector3(DeckWidth + 0.5f, 2.4f, 1.8f);
            var mount = mountGo.AddComponent<WaterSlideMount>();
            mount.Marker = marker;
            mount.Ride = ride;

            int hazards = _runDifficulty == RunDifficulty.Easy ? 2
                : _runDifficulty == RunDifficulty.Hard ? 4 : 3;
            for (int i = 0; i < hazards; i++)
            {
                float z = Mathf.Lerp(channelStart + 1.4f, channelEnd - 1.3f, (i + 1f) / (hazards + 1f));
                if (i % 2 == 0)
                    Obstacle.CreateBoatDebris(transform, z, Random.Range(0, 3));
                else
                    Obstacle.CreateLowBeam(transform, z, Random.Range(0, 3));
            }

            CollectibleCoin.Create(transform, new Vector3(0f, 1.1f, channelStart + 0.9f));
            CollectibleCoin.Create(transform, new Vector3(-PlayerController.LaneWidth, 1.2f, mid));
            CollectibleCoin.Create(transform, new Vector3(PlayerController.LaneWidth, 1.1f, channelEnd - 0.7f));
            if (_runDifficulty != RunDifficulty.Easy && Random.value < 0.4f)
                GemPickup.Create(transform, new Vector3(0f, 1.35f, mid + 0.8f));
        }

        /// <summary>Night summit runway dressing — lantern posts + starlit stone markers.</summary>
        void BuildNightSummitProps()
        {
            for (int i = 0; i < 3; i++)
            {
                float z = Length * ((i + 0.5f) / 3f);
                float side = i % 2 == 0 ? -1f : 1f;
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "LanternPost";
                post.transform.SetParent(transform, false);
                post.transform.localPosition = new Vector3(side * (DeckWidth * 0.55f + 0.35f), 1.1f, z);
                post.transform.localScale = new Vector3(0.18f, 1.1f, 0.18f);
                post.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
                StripCollider(post);

                var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lamp.name = "LanternGlow";
                lamp.transform.SetParent(transform, false);
                lamp.transform.localPosition = new Vector3(side * (DeckWidth * 0.55f + 0.35f), 2.25f, z);
                lamp.transform.localScale = Vector3.one * 0.32f;
                lamp.GetComponent<Renderer>().sharedMaterial = JunglePalette.GoldBright;
                StripCollider(lamp);
            }

            if (Random.value < 0.5f)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "SummitMarker";
                marker.transform.SetParent(transform, false);
                marker.transform.localPosition = new Vector3(0f, 0.35f, Length * 0.5f);
                marker.transform.localScale = new Vector3(0.8f, 0.7f, 0.35f);
                marker.GetComponent<Renderer>().sharedMaterial = BiomeSystem.AccentMat;
                StripCollider(marker);
            }
        }
    }
}
