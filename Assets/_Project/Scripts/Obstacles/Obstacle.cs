using UnityEngine;

namespace TempleSprint
{
    public class Obstacle : MonoBehaviour
    {
        public bool RequiresJump;
        public bool RequiresSlide;
        public bool StumbleOnly;
        public string DeathMessage = "Hit an obstacle";

        public void Consume()
        {
            gameObject.SetActive(false);
        }

        public static Obstacle CreateLowBeam(Transform parent, float localZ, int lane)
        {
            var root = new GameObject("LowBeam");
            root.transform.SetParent(parent, false);
            float x = (lane - 1) * PlayerController.LaneWidth;
            root.transform.localPosition = new Vector3(x, 0.45f, localZ);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(root.transform, false);
            go.transform.localScale = new Vector3(1.7f, 0.85f, 0.45f);
            go.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
            Object.Destroy(go.GetComponent<Collider>());

            var moss = GameObject.CreatePrimitive(PrimitiveType.Cube);
            moss.transform.SetParent(root.transform, false);
            moss.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            moss.transform.localScale = new Vector3(1.75f, 0.12f, 0.5f);
            moss.GetComponent<Renderer>().sharedMaterial = JunglePalette.StoneMoss;
            Object.Destroy(moss.GetComponent<Collider>());

            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1.7f, 0.9f, 0.5f);
            var obs = root.AddComponent<Obstacle>();
            obs.RequiresSlide = true;
            obs.DeathMessage = "Hit a low beam";
            return obs;
        }

        public static Obstacle CreateFirePit(Transform parent, float localZ, int lane)
        {
            var root = new GameObject("FirePit");
            root.transform.SetParent(parent, false);
            float x = (lane - 1) * PlayerController.LaneWidth;
            root.transform.localPosition = new Vector3(x, 0.15f, localZ);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.transform.SetParent(root.transform, false);
            ring.transform.localScale = new Vector3(1.5f, 0.15f, 1.5f);
            ring.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
            Object.Destroy(ring.GetComponent<Collider>());

            var fire = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fire.transform.SetParent(root.transform, false);
            fire.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            fire.transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);
            fire.GetComponent<Renderer>().sharedMaterial = JunglePalette.Hazard;
            Object.Destroy(fire.GetComponent<Collider>());

            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1.4f, 1.2f, 1.4f);
            col.center = new Vector3(0f, 0.4f, 0f);
            var obs = root.AddComponent<Obstacle>();
            obs.RequiresJump = true;
            obs.DeathMessage = "Burned in a fire pit";
            return obs;
        }

        public static Obstacle CreateSpike(Transform parent, float localZ, int lane)
        {
            var root = new GameObject("Spikes");
            root.transform.SetParent(parent, false);
            float x = (lane - 1) * PlayerController.LaneWidth;
            root.transform.localPosition = new Vector3(x, 0f, localZ);

            var baseBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseBlock.transform.SetParent(root.transform, false);
            baseBlock.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            baseBlock.transform.localScale = new Vector3(1.3f, 0.3f, 0.7f);
            baseBlock.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
            Object.Destroy(baseBlock.GetComponent<Collider>());

            for (int i = -1; i <= 1; i++)
            {
                var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spike.transform.SetParent(root.transform, false);
                spike.transform.localPosition = new Vector3(i * 0.35f, 0.7f, 0f);
                spike.transform.localRotation = Quaternion.Euler(0f, 0f, i * 8f);
                spike.transform.localScale = new Vector3(0.22f, 1.1f, 0.22f);
                spike.GetComponent<Renderer>().sharedMaterial = JunglePalette.StoneMoss;
                Object.Destroy(spike.GetComponent<Collider>());
            }

            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1.3f, 1.3f, 0.7f);
            col.center = new Vector3(0f, 0.65f, 0f);
            var obs = root.AddComponent<Obstacle>();
            obs.DeathMessage = "Impaled by spikes";
            return obs;
        }

        /// <summary>Full-width vertical wall — must slide under or die. Distinct from decorative arches.</summary>
        public static Obstacle CreateBlockingWall(Transform parent, float localZ)
        {
            var root = new GameObject("BlockingWall");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0f, localZ);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            wall.transform.localScale = new Vector3(TrackTile.DeckWidth + 0.6f, 2.5f, 0.45f);
            wall.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
            Object.Destroy(wall.GetComponent<Collider>());

            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.transform.SetParent(root.transform, false);
            lintel.transform.localPosition = new Vector3(0f, 2.75f, 0f);
            lintel.transform.localScale = new Vector3(TrackTile.DeckWidth + 1.2f, 0.35f, 0.55f);
            lintel.GetComponent<Renderer>().sharedMaterial = JunglePalette.StoneMoss;
            Object.Destroy(lintel.GetComponent<Collider>());

            // Open gap under the wall for slides — collider covers the solid face only.
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(TrackTile.DeckWidth + 0.4f, 1.65f, 0.55f);
            col.center = new Vector3(0f, 1.55f, 0f);
            var obs = root.AddComponent<Obstacle>();
            obs.RequiresSlide = true;
            obs.DeathMessage = "Hit a stone wall";
            return obs;
        }

        /// <summary>Floating debris / rock in a boat lane — dodge left/right.</summary>
        public static Obstacle CreateBoatDebris(Transform parent, float localZ, int lane)
        {
            var root = new GameObject("BoatDebris");
            root.transform.SetParent(parent, false);
            float x = (lane - 1) * PlayerController.LaneWidth;
            root.transform.localPosition = new Vector3(x, 0.45f, localZ);

            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.transform.SetParent(root.transform, false);
            rock.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            rock.transform.localRotation = Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
            rock.transform.localScale = new Vector3(Random.Range(1.1f, 1.5f), Random.Range(0.7f, 1.1f), Random.Range(1.0f, 1.4f));
            rock.GetComponent<Renderer>().sharedMaterial =
                Random.value < 0.5f ? JunglePalette.Stone : JunglePalette.StoneMoss;
            Object.Destroy(rock.GetComponent<Collider>());

            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1.4f, 1.2f, 1.3f);
            col.center = new Vector3(0f, 0.35f, 0f);
            var obs = root.AddComponent<Obstacle>();
            obs.DeathMessage = "Hit river debris";
            return obs;
        }

        public static Obstacle CreateCollapsingBridge(Transform parent, float localZ, int lane)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CollapsingBridge";
            go.transform.SetParent(parent, false);
            float x = (lane - 1) * PlayerController.LaneWidth;
            go.transform.localPosition = new Vector3(x, 0.15f, localZ);
            go.transform.localScale = new Vector3(1.8f, 0.25f, 2.8f);
            go.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
            go.GetComponent<Collider>().isTrigger = true;
            var obs = go.AddComponent<Obstacle>();
            obs.RequiresJump = true;
            obs.DeathMessage = "Bridge collapsed";
            var dyn = go.AddComponent<DynamicHazard>();
            dyn.HazardKind = DynamicHazard.Kind.CollapsingBridge;
            dyn.DeathMessage = obs.DeathMessage;
            var trigger = go.AddComponent<CrumbleTrigger>();
            trigger.lane = lane;
            trigger.localZ = localZ;
            return obs;
        }
    }

    public class GapKillZone : MonoBehaviour
    {
        public string DeathMessage = "Fell through a gap";

        public static GapKillZone Create(Transform parent, float localZ, int lane)
            => Create(parent, localZ, lane, 5.0f);

        public static GapKillZone Create(Transform parent, float localZ, int lane, float lengthZ)
            => Create(parent, localZ, lane, lengthZ, "Fell through a gap");

        public static GapKillZone Create(Transform parent, float localZ, int lane, float lengthZ, string deathMessage)
        {
            var go = new GameObject("GapKill");
            go.transform.SetParent(parent, false);
            float x = (lane - 1) * PlayerController.LaneWidth;
            go.transform.localPosition = new Vector3(x, -1.2f, localZ);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.15f, 4.5f, lengthZ);
            box.center = new Vector3(0f, 0.2f, 0f);
            var zone = go.AddComponent<GapKillZone>();
            zone.DeathMessage = deathMessage;
            return zone;
        }
    }
}
