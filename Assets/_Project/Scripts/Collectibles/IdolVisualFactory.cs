using UnityEngine;

namespace TempleSprint
{
    /// <summary>Shared procedural idol geometry for pickups and the opening theft beat (no Imangi IP).</summary>
    public static class IdolVisualFactory
    {
        public static Transform BuildStolenIdol(Transform parent, Vector3 localPos, float scale = 1f)
        {
            var root = new GameObject("StolenIdol").transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            root.localScale = Vector3.one * scale;

            var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torso.name = "IdolTorso";
            torso.transform.SetParent(root, false);
            torso.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            torso.transform.localScale = new Vector3(0.42f, 0.55f, 0.28f);
            torso.GetComponent<Renderer>().sharedMaterial = JunglePalette.Gold;
            Object.Destroy(torso.GetComponent<Collider>());

            var crest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crest.name = "Headdress";
            crest.transform.SetParent(root, false);
            crest.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            crest.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            crest.transform.localScale = new Vector3(0.28f, 0.28f, 0.12f);
            crest.GetComponent<Renderer>().sharedMaterial = JunglePalette.GoldBright;
            Object.Destroy(crest.GetComponent<Collider>());

            foreach (float sx in new[] { -0.1f, 0.1f })
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.transform.SetParent(root, false);
                eye.transform.localPosition = new Vector3(sx, 0.38f, 0.16f);
                eye.transform.localScale = Vector3.one * 0.1f;
                eye.GetComponent<Renderer>().sharedMaterial = JunglePalette.Accent;
                Object.Destroy(eye.GetComponent<Collider>());
            }

            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.transform.SetParent(root, false);
            glow.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            glow.transform.localScale = Vector3.one * 0.85f;
            var gr = glow.GetComponent<Renderer>();
            gr.sharedMaterial = JunglePalette.GoldBright;
            gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(glow.GetComponent<Collider>());

            var spin = root.gameObject.AddComponent<IdolSpin>();
            spin.DegreesPerSecond = 90f;
            return root;
        }

        public static Transform BuildPedestal(Transform parent, Vector3 localPos)
        {
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "IdolPedestal";
            pedestal.transform.SetParent(parent, false);
            pedestal.transform.localPosition = localPos;
            pedestal.transform.localScale = new Vector3(0.7f, 0.14f, 0.7f);
            pedestal.GetComponent<Renderer>().sharedMaterial = JunglePalette.Stone;
            Object.Destroy(pedestal.GetComponent<Collider>());
            return pedestal.transform;
        }

        /// <summary>Quieter held idol for the early-run "stolen treasure" prop.</summary>
        public static void ConfigureAsHeldProp(Transform idol)
        {
            if (idol == null) return;
            idol.name = "HeldStolenIdol";
            var spin = idol.GetComponent<IdolSpin>();
            if (spin != null) spin.DegreesPerSecond = 28f;
        }
    }

    public class IdolSpin : MonoBehaviour
    {
        public float DegreesPerSecond = 90f;
        void Update() => transform.Rotate(0f, DegreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
