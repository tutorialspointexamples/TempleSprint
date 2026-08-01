using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace TempleSprint
{
    public static class UiFactory
    {
        static Font _cachedFont;

        public static Canvas CreateCanvas(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Device Simulator is portrait — match phone first, landscape Game view still OK
            bool portrait = Screen.height >= Screen.width;
            scaler.referenceResolution = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = portrait ? 0.6f : 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        public static Font ResolveUiFont(int fontSize = 32)
        {
            if (_cachedFont != null) return _cachedFont;

            // Prefer carved-plaque display faces, then readable UI fonts (never depend on Inter/Roboto).
            try
            {
                _cachedFont = Font.CreateDynamicFontFromOSFont(
                    new[]
                    {
                        "Palatino Linotype", "Palatino", "Georgia", "Book Antiqua",
                        "Segoe UI", "Tahoma", "Verdana", "Helvetica", "DejaVu Sans", "Arial"
                    },
                    Mathf.Max(16, fontSize));
            }
            catch { /* ignore */ }

            if (_cachedFont == null)
            {
                try { _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { /* ignore */ }
            }
            if (_cachedFont == null)
            {
                try { _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { /* ignore */ }
            }
            if (_cachedFont == null)
                _cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 32);

            return _cachedFont;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = ResolveUiFont(fontSize);
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(3f, -3f);

            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return text;
        }

        public enum UiIcon
        {
            Coin,
            Gem,
            Relic,
            Magnet,
            Shield,
            Boost,
            SlowMo,
            Character,
            Biome,
            Gear,
            Score
        }

        static readonly System.Collections.Generic.Dictionary<UiIcon, Sprite> IconCache = new();

        public static Sprite IconSprite(UiIcon icon)
        {
            if (IconCache.TryGetValue(icon, out var cached) && cached != null) return cached;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float a = PaintIcon(icon, dx, dy);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            IconCache[icon] = sprite;
            return sprite;
        }

        static float PaintIcon(UiIcon icon, float dx, float dy)
        {
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            switch (icon)
            {
                case UiIcon.Coin:
                    return SoftRing(r, 0.85f, 0.55f) + SoftDisk(r, 0.35f) * 0.85f;
                case UiIcon.Gem:
                    return SoftDiamond(dx, dy, 0.75f);
                case UiIcon.Relic:
                    return SoftDisk(r, 0.7f) * 0.35f + SoftDiamond(dx, dy * 1.1f, 0.55f);
                case UiIcon.Magnet:
                {
                    float u = Mathf.Abs(dx);
                    float body = (u > 0.15f && u < 0.55f && dy > -0.55f && dy < 0.45f) ? 1f : 0f;
                    float arch = (dy > 0.2f && r < 0.75f && r > 0.35f) ? 1f : 0f;
                    return Mathf.Clamp01(body + arch);
                }
                case UiIcon.Shield:
                    return SoftDisk(new Vector2(dx, dy * 1.15f + 0.1f).magnitude, 0.7f)
                           * (dy < 0.55f ? 1f : 0f);
                case UiIcon.Boost:
                    return SoftTriangle(dx, dy, 0.75f);
                case UiIcon.SlowMo:
                    return SoftRing(r, 0.8f, 0.55f) + SoftDisk(r, 0.18f);
                case UiIcon.Character:
                    return SoftDisk(new Vector2(dx, dy - 0.28f).magnitude, 0.28f)
                           + SoftDisk(new Vector2(dx * 0.85f, dy + 0.25f).magnitude, 0.42f) * 0.9f;
                case UiIcon.Biome:
                    return SoftTriangle(dx, dy + 0.1f, 0.8f) * 0.85f
                           + SoftDisk(new Vector2(dx, dy + 0.45f).magnitude, 0.22f);
                case UiIcon.Gear:
                {
                    float teeth = Mathf.Abs(Mathf.Sin(Mathf.Atan2(dy, dx) * 4f)) > 0.55f && r < 0.85f && r > 0.45f ? 1f : 0f;
                    return SoftRing(r, 0.7f, 0.4f) + teeth + SoftDisk(r, 0.22f);
                }
                case UiIcon.Score:
                    return SoftDiamond(dx, dy, 0.65f) + SoftDisk(r, 0.2f);
                default:
                    return SoftDisk(r, 0.6f);
            }
        }

        static float SoftDisk(float r, float radius) =>
            Mathf.Clamp01((radius - r) / 0.08f);

        static float SoftRing(float r, float outer, float inner)
        {
            if (r > outer + 0.08f || r < inner - 0.08f) return 0f;
            float outerEdge = SoftDisk(r, outer);
            float hole = SoftDisk(r, inner);
            return Mathf.Clamp01(outerEdge * (1f - hole));
        }

        static float SoftDiamond(float dx, float dy, float size)
        {
            float d = (Mathf.Abs(dx) + Mathf.Abs(dy)) / size;
            return SoftDisk(d, 1f);
        }

        static float SoftTriangle(float dx, float dy, float size)
        {
            float px = dx / size;
            float py = dy / size;
            if (py < -0.7f || py > 0.75f) return 0f;
            float half = Mathf.Lerp(0.05f, 0.7f, (py + 0.7f) / 1.45f);
            return SoftDisk(Mathf.Abs(px) / Mathf.Max(0.05f, half), 1f) *
                   SoftDisk(Mathf.Abs(py), 0.85f);
        }

        public static Image CreateIcon(Transform parent, string name, UiIcon icon, Vector2 anchoredPos, Vector2 size, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = IconSprite(icon);
            img.color = tint;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return img;
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, UiIcon? icon = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            // Outer gold rim so the control is visible even if label font fails
            var rim = new GameObject("Rim");
            rim.transform.SetParent(go.transform, false);
            var rimImg = rim.AddComponent<Image>();
            rimImg.color = new Color(0.95f, 0.78f, 0.28f, 1f);
            var rimRt = rim.GetComponent<RectTransform>();
            rimRt.anchorMin = Vector2.zero;
            rimRt.anchorMax = Vector2.one;
            rimRt.offsetMin = new Vector2(-5f, -5f);
            rimRt.offsetMax = new Vector2(5f, 5f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.72f, 0.48f, 0.12f, 1f);
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.55f, 1f);
            colors.pressedColor = new Color(0.75f, 0.55f, 0.2f, 1f);
            btn.colors = colors;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            float labelLeft = 8f;
            if (icon.HasValue)
            {
                CreateIcon(go.transform, "Icon", icon.Value,
                    new Vector2(-size.x * 0.32f, 0f),
                    new Vector2(size.y * 0.48f, size.y * 0.48f),
                    new Color(1f, 0.92f, 0.55f, 1f));
                labelLeft = size.y * 0.42f;
            }

            var text = CreateText(go.transform, "Label", label, Mathf.Clamp(Mathf.RoundToInt(size.y * 0.42f), 22, 48),
                TextAnchor.MiddleCenter, new Color(1f, 0.97f, 0.88f, 1f));
            text.fontStyle = FontStyle.Bold;
            text.rectTransform.offsetMin = new Vector2(labelLeft, 0f);
            text.rectTransform.offsetMax = Vector2.zero;
            return btn;
        }

        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            // Let child buttons receive clicks; don't eat the whole screen for no reason
            img.raycastTarget = color.a > 0.85f;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return img;
        }

        /// <summary>Seasonal live-event banner for menu / HUD — accent-colored festival plaque.</summary>
        public static Text CreateEventBanner(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 size, string initial, Color accent, Color trim)
        {
            var frame = new GameObject(name + "Frame");
            frame.transform.SetParent(parent, false);
            var frameImg = frame.AddComponent<Image>();
            frameImg.color = new Color(accent.r * 0.25f, accent.g * 0.22f, accent.b * 0.18f, 0.92f);
            var frt = frame.GetComponent<RectTransform>();
            frt.anchorMin = anchorMin;
            frt.anchorMax = anchorMax;
            frt.pivot = pivot;
            frt.anchoredPosition = anchoredPos;
            frt.sizeDelta = size;

            var rim = new GameObject("EventRim");
            rim.transform.SetParent(frame.transform, false);
            var rimImg = rim.AddComponent<Image>();
            rimImg.color = accent;
            rimImg.raycastTarget = false;
            var rrt = rim.GetComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = new Vector2(-6f, -6f);
            rrt.offsetMax = new Vector2(6f, 6f);
            rim.transform.SetAsFirstSibling();

            var trimGo = new GameObject("EventTrim");
            trimGo.transform.SetParent(frame.transform, false);
            var trimImg = trimGo.AddComponent<Image>();
            trimImg.color = trim;
            trimImg.raycastTarget = false;
            var trt = trimGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(-3f, -3f);
            trt.offsetMax = new Vector2(3f, 3f);
            trimGo.transform.SetAsFirstSibling();
            rim.transform.SetAsFirstSibling();

            var inner = new GameObject("Inner");
            inner.transform.SetParent(frame.transform, false);
            var iImg = inner.AddComponent<Image>();
            iImg.color = new Color(0.06f, 0.07f, 0.08f, 0.94f);
            var irt = inner.GetComponent<RectTransform>();
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(6f, 6f);
            irt.offsetMax = new Vector2(-6f, -6f);

            var text = CreateText(inner.transform, "Value", initial, 26, TextAnchor.MiddleCenter,
                new Color(Mathf.Min(1f, accent.r + 0.15f), Mathf.Min(1f, accent.g + 0.1f), accent.b, 1f));
            text.fontStyle = FontStyle.Bold;
            return text;
        }

        /// <summary>Ornate carved stone HUD plaque (score / coins) — genre temple-tablet chrome.</summary>
        public static Text CreateHudPlaque(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size, string initial, UiIcon? icon = null)
        {
            var frame = new GameObject(name + "Frame");
            frame.transform.SetParent(parent, false);
            var frameImg = frame.AddComponent<Image>();
            frameImg.color = new Color(0.16f, 0.12f, 0.08f, 0.94f);
            var frt = frame.GetComponent<RectTransform>();
            frt.anchorMin = anchorMin;
            frt.anchorMax = anchorMax;
            frt.pivot = pivot;
            frt.anchoredPosition = anchoredPos;
            frt.sizeDelta = size;

            var outer = new GameObject("StoneRim");
            outer.transform.SetParent(frame.transform, false);
            var oImg = outer.AddComponent<Image>();
            oImg.color = new Color(0.38f, 0.3f, 0.18f, 0.98f);
            var ort = outer.GetComponent<RectTransform>();
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.offsetMin = new Vector2(-12f, -12f);
            ort.offsetMax = new Vector2(12f, 12f);
            outer.transform.SetAsFirstSibling();

            var border = new GameObject("GoldBorder");
            border.transform.SetParent(frame.transform, false);
            var bImg = border.AddComponent<Image>();
            bImg.color = new Color(0.92f, 0.74f, 0.28f, 1f);
            var brt = border.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(-7f, -7f);
            brt.offsetMax = new Vector2(7f, 7f);
            border.transform.SetAsFirstSibling();
            outer.transform.SetAsFirstSibling();

            var inner = new GameObject("Inner");
            inner.transform.SetParent(frame.transform, false);
            var iImg = inner.AddComponent<Image>();
            iImg.color = new Color(0.09f, 0.07f, 0.05f, 0.96f);
            var irt = inner.GetComponent<RectTransform>();
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(8f, 8f);
            irt.offsetMax = new Vector2(-8f, -8f);

            // Carved corner gems — stone tablet silhouette without floating sticker clutter.
            AddCornerGem(frame.transform, new Vector2(0f, 1f), new Vector2(6f, -6f));
            AddCornerGem(frame.transform, new Vector2(1f, 1f), new Vector2(-6f, -6f));
            AddCornerGem(frame.transform, new Vector2(0f, 0f), new Vector2(6f, 6f));
            AddCornerGem(frame.transform, new Vector2(1f, 0f), new Vector2(-6f, 6f));

            float textLeft = 0f;
            if (icon.HasValue)
            {
                var iconGo = new GameObject("PlaqueIcon");
                iconGo.transform.SetParent(inner.transform, false);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = IconSprite(icon.Value);
                iconImg.color = new Color(1f, 0.9f, 0.45f, 1f);
                iconImg.raycastTarget = false;
                var iconRt = iconImg.rectTransform;
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(10f, 0f);
                iconRt.sizeDelta = new Vector2(size.y * 0.42f, size.y * 0.42f);
                textLeft = size.y * 0.42f + 8f;
            }

            var text = CreateText(inner.transform, "Value", initial, 36, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.35f, 1f));
            text.fontStyle = FontStyle.Bold;
            text.rectTransform.offsetMin = new Vector2(textLeft, 0f);
            text.rectTransform.offsetMax = Vector2.zero;
            return text;
        }

        static void AddCornerGem(Transform parent, Vector2 anchor, Vector2 anchoredPos)
        {
            var gem = new GameObject("CornerGem");
            gem.transform.SetParent(parent, false);
            var img = gem.AddComponent<Image>();
            img.color = new Color(0.95f, 0.78f, 0.3f, 0.95f);
            img.raycastTarget = false;
            var rt = gem.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(14f, 14f);
        }

        /// <summary>Menu / post-run stone plaque panel with gold rim.</summary>
        public static Image CreateStonePanel(Transform parent, string name, Color fill)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = fill;
            img.raycastTarget = fill.a > 0.85f;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var rim = new GameObject("TabletRim");
            rim.transform.SetParent(go.transform, false);
            var rimImg = rim.AddComponent<Image>();
            rimImg.color = new Color(0.78f, 0.58f, 0.22f, 0.55f);
            rimImg.raycastTarget = false;
            var rrt = rim.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.04f, 0.06f);
            rrt.anchorMax = new Vector2(0.96f, 0.94f);
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;
            // Draw behind content
            rim.transform.SetAsFirstSibling();

            var inset = new GameObject("TabletInset");
            inset.transform.SetParent(go.transform, false);
            var insetImg = inset.AddComponent<Image>();
            insetImg.color = new Color(0.12f, 0.1f, 0.07f, Mathf.Clamp01(fill.a * 0.55f));
            insetImg.raycastTarget = false;
            var irt = inset.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.06f, 0.1f);
            irt.anchorMax = new Vector2(0.94f, 0.9f);
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = Vector2.zero;
            inset.transform.SetSiblingIndex(1);

            return img;
        }

        /// <summary>Temple Run-style power meter: stone disc with radial gold fill + center label.</summary>
        public static (Image fill, Text label) CreatePowerRing(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, float diameter)
        {
            var frame = new GameObject(name + "Ring");
            frame.transform.SetParent(parent, false);
            var frameImg = frame.AddComponent<Image>();
            frameImg.color = new Color(0.14f, 0.1f, 0.07f, 0.95f);
            var frt = frame.GetComponent<RectTransform>();
            frt.anchorMin = anchor;
            frt.anchorMax = anchor;
            frt.pivot = anchor;
            frt.anchoredPosition = anchoredPos;
            frt.sizeDelta = new Vector2(diameter, diameter);

            var rim = new GameObject("GoldRim");
            rim.transform.SetParent(frame.transform, false);
            var rimImg = rim.AddComponent<Image>();
            rimImg.color = new Color(0.9f, 0.72f, 0.28f, 1f);
            var rrt = rim.GetComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = new Vector2(-8f, -8f);
            rrt.offsetMax = new Vector2(8f, 8f);
            rim.transform.SetAsFirstSibling();

            var track = new GameObject("Track");
            track.transform.SetParent(frame.transform, false);
            var trackImg = track.AddComponent<Image>();
            trackImg.sprite = RadialSprite();
            trackImg.type = Image.Type.Simple;
            trackImg.color = new Color(0.22f, 0.18f, 0.12f, 0.95f);
            var trt = track.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10f, 10f);
            trt.offsetMax = new Vector2(-10f, -10f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(frame.transform, false);
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = RadialSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 0f;
            fill.color = new Color(1f, 0.82f, 0.28f, 1f);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(10f, 10f);
            fillRt.offsetMax = new Vector2(-10f, -10f);

            var core = new GameObject("Core");
            core.transform.SetParent(frame.transform, false);
            var coreImg = core.AddComponent<Image>();
            coreImg.color = new Color(0.08f, 0.06f, 0.04f, 0.98f);
            var crt = core.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(diameter * 0.48f, diameter * 0.48f);

            var label = CreateText(core.transform, "Label", "PWR", 26, TextAnchor.MiddleCenter,
                new Color(1f, 0.9f, 0.45f, 1f));
            label.fontStyle = FontStyle.Bold;
            return (fill, label);
        }

        static Sprite _radialSprite;

        static Sprite RadialSprite()
        {
            if (_radialSprite != null) return _radialSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float c = (size - 1) * 0.5f;
            float outer = c - 1f;
            float inner = outer * 0.55f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 0f;
                if (d <= outer && d >= inner) a = 1f;
                else if (d < inner && d > inner - 1.5f) a = Mathf.Clamp01(d - (inner - 1.5f));
                else if (d > outer && d < outer + 1.5f) a = Mathf.Clamp01((outer + 1.5f) - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, true);
            _radialSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _radialSprite;
        }

        /// <summary>Scrollable content area for locker / long plaque lists.</summary>
        public static (ScrollRect scroll, RectTransform content) CreateScrollArea(
            Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = anchorMin;
            rootRt.anchorMax = anchorMax;
            rootRt.offsetMin = offsetMin;
            rootRt.offsetMax = offsetMax;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(root.transform, false);
            var viewportRt = viewport.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var maskImg = viewport.AddComponent<Image>();
            maskImg.color = new Color(0.08f, 0.07f, 0.05f, 0.35f);

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0.5f, 1f);
            contentRt.anchorMax = new Vector2(0.5f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(900f, 400f);

            var scroll = root.AddComponent<ScrollRect>();
            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scroll.inertia = true;
            return (scroll, contentRt);
        }

        public static Button CreateTabButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, UiIcon? icon = null)
        {
            var btn = CreateButton(parent, name, label, anchoredPos, size, icon);
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = new Color(0.42f, 0.32f, 0.16f, 1f);
            return btn;
        }

        public static void SetTabSelected(Button tab, bool selected)
        {
            if (tab == null) return;
            var img = tab.GetComponent<Image>();
            if (img != null)
                img.color = selected
                    ? new Color(0.82f, 0.58f, 0.18f, 1f)
                    : new Color(0.42f, 0.32f, 0.16f, 1f);
        }

        public static Button CreateCircleButton(Transform parent, string name, string label, Vector2 anchor, Vector2 anchoredPos, float diameter, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.4f, 0.32f, 0.2f, 0.96f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.9f, 0.55f, 1f);
            btn.colors = colors;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(diameter, diameter);

            var rim = new GameObject("Rim");
            rim.transform.SetParent(go.transform, false);
            var rimImg = rim.AddComponent<Image>();
            rimImg.color = new Color(0.85f, 0.68f, 0.25f, 0.9f);
            var rrt = rim.GetComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = new Vector2(-5f, -5f);
            rrt.offsetMax = new Vector2(5f, 5f);
            rim.transform.SetAsFirstSibling();

            var text = CreateText(go.transform, "Label", label, 28, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.7f, 1f));
            text.fontStyle = FontStyle.Bold;
            return btn;
        }
    }
}
