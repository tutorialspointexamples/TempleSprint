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

            // Unity 6 often has no LegacyRuntime / Arial builtins — OS fonts first on Windows
            try
            {
                _cachedFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Segoe UI", "Arial", "Tahoma", "Verdana", "Helvetica", "DejaVu Sans" },
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

        public static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
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

            var text = CreateText(go.transform, "Label", label, Mathf.Clamp(Mathf.RoundToInt(size.y * 0.42f), 22, 48),
                TextAnchor.MiddleCenter, new Color(1f, 0.97f, 0.88f, 1f));
            text.fontStyle = FontStyle.Bold;
            text.rectTransform.offsetMin = Vector2.zero;
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

        /// <summary>Temple Run–style ornate HUD plaque (score / coins).</summary>
        public static Text CreateHudPlaque(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size, string initial)
        {
            var frame = new GameObject(name + "Frame");
            frame.transform.SetParent(parent, false);
            var frameImg = frame.AddComponent<Image>();
            frameImg.color = new Color(0.14f, 0.1f, 0.07f, 0.92f);
            var frt = frame.GetComponent<RectTransform>();
            frt.anchorMin = anchorMin;
            frt.anchorMax = anchorMax;
            frt.pivot = pivot;
            frt.anchoredPosition = anchoredPos;
            frt.sizeDelta = size;

            var border = new GameObject("GoldBorder");
            border.transform.SetParent(frame.transform, false);
            var bImg = border.AddComponent<Image>();
            bImg.color = new Color(0.9f, 0.72f, 0.28f, 1f);
            var brt = border.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(-6f, -6f);
            brt.offsetMax = new Vector2(6f, 6f);
            border.transform.SetAsFirstSibling();

            var outer = new GameObject("StoneRim");
            outer.transform.SetParent(frame.transform, false);
            var oImg = outer.AddComponent<Image>();
            oImg.color = new Color(0.35f, 0.28f, 0.18f, 0.95f);
            var ort = outer.GetComponent<RectTransform>();
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.offsetMin = new Vector2(-10f, -10f);
            ort.offsetMax = new Vector2(10f, 10f);
            outer.transform.SetAsFirstSibling();

            var inner = new GameObject("Inner");
            inner.transform.SetParent(frame.transform, false);
            var iImg = inner.AddComponent<Image>();
            iImg.color = new Color(0.1f, 0.08f, 0.06f, 0.95f);
            var irt = inner.GetComponent<RectTransform>();
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(8f, 8f);
            irt.offsetMax = new Vector2(-8f, -8f);

            var text = CreateText(inner.transform, "Value", initial, 36, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.35f, 1f));
            text.fontStyle = FontStyle.Bold;
            return text;
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
