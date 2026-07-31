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
        TurnLeft,
        TurnRight,
        TJunction
    }

    public class TrackTile : MonoBehaviour
    {
        public const float Length = 12f;
        public const float DeckWidth = 4.6f;
        public const float TurnArmIn = 6f;
        public const float TurnArmOut = 6f;
        public const float JunctionApproach = 7f;
        public const float JunctionArm = 6f;

        public TileKind Kind { get; private set; }
        public PathPose EntryPose { get; private set; }
        public PathPose ExitPose { get; private set; }
        public float PathStartDistance => EntryPose.pathDistance;
        public float PathLength { get; private set; }
        public float StartZ => PathStartDistance; // compat for older callers

        public bool IsTurn => Kind == TileKind.TurnLeft || Kind == TileKind.TurnRight;
        public bool IsJunction => Kind == TileKind.TJunction;
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

            // Straight content tiles
            PathLength = Length;
            ExitPose = entry.AdvanceStraight(Length);

            if (kind == TileKind.HazardGap || kind == TileKind.RiverCrossing || kind == TileKind.FireCrossing
                || kind == TileKind.Zipline || kind == TileKind.MineCart || kind == TileKind.IceSurf)
            {
                if (kind == TileKind.HazardGap)
                    BuildFloorWithGap();
                else if (kind == TileKind.FireCrossing)
                    BuildFloorWithFireChannel();
                else if (kind == TileKind.Zipline || kind == TileKind.MineCart || kind == TileKind.IceSurf)
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
            else if (kind == TileKind.Zipline)
                BuildForestEdgeForSpecial();
            else if (kind == TileKind.MineCart)
                BuildCaveTunnelShell();
            else if (kind == TileKind.IceSurf)
                BuildIceSlopeShell();
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
                    if (BiomeSystem.Current == BiomeId.VolcanicCrater && Random.value < 0.35f) BuildVolcanicEdgeGlow();
                    if (BiomeSystem.Current == BiomeId.IceCaverns && Random.value < 0.4f) BuildIceRunwayFrost();
                    break;
                case TileKind.CoinLane:
                    ScatterCoins(10);
                    if (Random.value < 0.3f) SpawnPowerUp();
                    if (Random.value < 0.35f) BuildHangingVines(1);
                    break;
                case TileKind.ObstacleCluster:
                    SpawnObstacles(obstacleChance, difficultyTier);
                    if (Random.value < 0.4f) ScatterCoins(3);
                    if (Random.value < 0.2f) BuildArch();
                    if (Random.value < 0.4f) BuildHangingVines(Random.Range(1, 3));
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
                var corner = EntryPose.position + EntryPose.Forward * TurnArmIn;
                if (s <= TurnArmIn)
                {
                    return new PathPose(EntryPose.position + EntryPose.Forward * s, EntryPose.yaw,
                        PathStartDistance + s);
                }

                float u = s - TurnArmIn;
                float t = Mathf.Clamp01(u / TurnArmOut);
                float yaw = Mathf.LerpAngle(EntryPose.yaw, ExitPose.yaw, t);
                Vector3 dir = left ? -EntryPose.Right : EntryPose.Right;
                return new PathPose(corner + dir * u, yaw, PathStartDistance + s);
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
                return new PathPose(corner + dir * u, yaw, PathStartDistance + s);
            }

            // Straight
            return new PathPose(EntryPose.position + EntryPose.Forward * s, EntryPose.yaw, PathStartDistance + s);
        }

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
            PathLength = TurnArmIn + TurnArmOut;
            var corner = EntryPose.position + EntryPose.Forward * TurnArmIn;
            float exitYaw = PathPose.NormalizeYaw(EntryPose.yaw + (left ? -90f : 90f));
            Vector3 dir = left ? -EntryPose.Right : EntryPose.Right;
            ExitPose = new PathPose(corner + dir * TurnArmOut, exitYaw, PathStartDistance + PathLength);

            // Approach along local +Z
            BuildFloorSegment(new Vector3(0f, -0.12f, TurnArmIn * 0.5f), new Vector3(DeckWidth, 0.4f, TurnArmIn));
            BuildRailSegment(0.3f, TurnArmIn - 0.6f);
            SpawnSupportPair(TurnArmIn * 0.35f);
            SpawnSupportPair(TurnArmIn * 0.75f);

            // Exit arm as child rotated ±90° at corner
            var arm = new GameObject(left ? "TurnArmL" : "TurnArmR").transform;
            arm.SetParent(transform, false);
            arm.localPosition = new Vector3(0f, 0f, TurnArmIn);
            arm.localRotation = Quaternion.Euler(0f, left ? -90f : 90f, 0f);

            BuildFloorOn(arm, new Vector3(0f, -0.12f, TurnArmOut * 0.5f), new Vector3(DeckWidth, 0.4f, TurnArmOut));
            BuildRailsOn(arm, 0.3f, TurnArmOut - 0.6f);
            SpawnSupportPairOn(arm, TurnArmOut * 0.4f);
            SpawnSupportPairOn(arm, TurnArmOut * 0.85f);

            if (Random.value < 0.4f)
            {
                CollectibleCoin.Create(transform, new Vector3(0f, 1.25f, TurnArmIn * 0.5f));
                CollectibleCoin.Create(arm, new Vector3(0f, 1.25f, TurnArmOut * 0.55f));
            }

            // Plant only on the outside of the corner; keep foliage clear of the inside lane.
            int outer = left ? 1 : -1;
            BuildForestEdge(transform, 0f, TurnArmIn, outer);
            BuildForestEdge(arm, 0f, TurnArmOut, outer);

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
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "FloorSlab";
                floor.transform.SetParent(transform, false);
                floor.transform.localPosition = new Vector3(0f, -0.12f, slabLen * (s + 0.5f));
                floor.transform.localScale = new Vector3(DeckWidth, 0.4f, slabLen * 0.95f);
                floor.GetComponent<Renderer>().sharedMaterial = BiomeSystem.PathMat;
                Object.Destroy(floor.GetComponent<Collider>());
            }

            for (int side = -1; side <= 1; side += 2)
            {
                var moss = GameObject.CreatePrimitive(PrimitiveType.Cube);
                moss.name = "MossEdge";
                moss.transform.SetParent(transform, false);
                moss.transform.localPosition = new Vector3(side * (DeckWidth * 0.48f), 0.02f, Length * 0.5f);
                moss.transform.localScale = new Vector3(0.28f, 0.1f, Length * 0.92f);
                moss.GetComponent<Renderer>().sharedMaterial = JunglePalette.StoneMoss;
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
            floor.GetComponent<Renderer>().sharedMaterial = JunglePalette.Grass;
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
            if (segLength > 5f && Random.value < 0.28f)
            {
                float wz = zStart + Random.Range(segLength * 0.25f, segLength * 0.75f);
                float wx = side * (ForestInner + Random.Range(6.5f, 10f));
                SpawnWaterfall(parent, new Vector3(wx, ForestFloorY, wz), Random.Range(6.2f, 9f), side);
            }
        }

        void SpawnForestTree(Transform parent, Vector3 basePos, float scale, bool conifer)
        {
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

        void SpawnShrub(Transform parent, Vector3 basePos)
        {
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
            rock.GetComponent<Renderer>().sharedMaterial =
                Random.value < 0.5f ? JunglePalette.StoneMoss : JunglePalette.Stone;
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

            for (int i = 0; i < 7; i++)
            {
                var foam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                foam.name = "RiverFoam";
                foam.transform.SetParent(transform, false);
                foam.transform.localPosition = new Vector3(
                    Random.Range(-riverWidth * 0.35f, riverWidth * 0.35f),
                    ForestFloorY + 0.08f,
                    channelStart + channelLen * Random.Range(0.08f, 0.92f));
                foam.transform.localScale = new Vector3(Random.Range(1.6f, 3.4f), 0.1f, Random.Range(0.45f, 1.1f));
                foam.GetComponent<Renderer>().sharedMaterial = JunglePalette.Foam;
                StripCollider(foam);
            }

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
            int roll = Random.Range(0, _runDifficulty == RunDifficulty.Easy ? 2 : 5);
            if (roll == 0) DynamicHazard.CreatePendulum(transform, Length * 0.5f, 1);
            else if (roll == 1) DynamicHazard.CreateArrow(transform, Length * 0.3f, Random.Range(0, 3));
            else if (roll == 2) DynamicHazard.CreateGate(transform, Length * 0.55f);
            else if (roll == 3) Obstacle.CreateBlockingWall(transform, Length * 0.55f);
            else DynamicHazard.CreateCrumbling(transform, Length * 0.5f, Random.Range(0, 3));

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

            // Rail bed across the channel (cart rides above kill voids at sides).
            var rails = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rails.name = "MineRails";
            rails.transform.SetParent(transform, false);
            rails.transform.localPosition = new Vector3(0f, -0.05f, mid);
            rails.transform.localScale = new Vector3(DeckWidth * 0.92f, 0.18f, channelLen);
            rails.GetComponent<Renderer>().sharedMaterial = JunglePalette.Charcoal;
            StripCollider(rails);

            for (int side = -1; side <= 1; side += 2)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.transform.SetParent(transform, false);
                rail.transform.localPosition = new Vector3(side * 0.55f, 0.05f, mid);
                rail.transform.localScale = new Vector3(0.12f, 0.1f, channelLen);
                rail.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
                StripCollider(rail);
            }

            var rideGo = new GameObject("MineCartRide");
            rideGo.transform.SetParent(transform, false);
            var ride = rideGo.AddComponent<MineCartRide>();
            ride.Marker = marker;
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

            // Low beams + side rock walls — dodge lanes / slide.
            int hazards = _runDifficulty == RunDifficulty.Easy ? 2
                : _runDifficulty == RunDifficulty.Hard ? 4 : 3;
            int usedMask = 0;
            for (int i = 0; i < hazards; i++)
            {
                float z = Mathf.Lerp(channelStart + 1.3f, channelEnd - 1.3f, (i + 1f) / (hazards + 1f));
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

            CollectibleCoin.Create(transform, new Vector3(0f, 1.5f, channelStart + 0.9f));
            CollectibleCoin.Create(transform, new Vector3(0f, 1.6f, mid));
            CollectibleCoin.Create(transform, new Vector3(0f, 1.5f, channelEnd - 0.6f));
            if (_runDifficulty != RunDifficulty.Easy && Random.value < 0.45f)
                GemPickup.Create(transform, new Vector3(PlayerController.LaneWidth, 1.55f, mid + 1f));
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
    }
}
