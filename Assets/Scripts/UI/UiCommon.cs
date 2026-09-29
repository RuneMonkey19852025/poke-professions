using UnityEngine;
using UnityEngine.UI;

namespace PokeProfessions.UI
{
    /// <summary>Shared colors for the UI.</summary>
    public static class UiTheme
    {
        public static readonly Color Bg = Hex("#1A2028EE");
        public static readonly Color BgSoft = Hex("#232B36EE");
        public static readonly Color Panel = Hex("#2C3644F2");
        public static readonly Color Line = Hex("#46586ACC");
        public static readonly Color Text = Hex("#EAF0F6");
        public static readonly Color TextDim = Hex("#9FB0C0");
        public static readonly Color Accent = Hex("#6FCF97");
        public static readonly Color Accent2 = Hex("#F2994A");
        public static readonly Color Danger = Hex("#EB5757");
        public static readonly Color Hp = Hex("#6FCF97");
        public static readonly Color HpLow = Hex("#EB5757");
        public static readonly Color Xp = Hex("#56A8F5");

        public static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.white;
        }
    }

    /// <summary>Builds uGUI elements from code.</summary>
    public static class UIFactory
    {
        static Font font;
        public static Font DefaultFont
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Canvas BuildCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Root(Canvas canvas, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            return rt;
        }

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            return rt;
        }

        public static RectTransform Box(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            var rt = Panel(parent, name, color);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            return rt;
        }

        // ---- rounded 9-slice sprite (Pokémon-style UI) ----
        static Sprite _rounded;
        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int size = 64, r = 18;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Min(x, size - 1 - x);
                        float dy = Mathf.Min(y, size - 1 - y);
                        bool inside;
                        if (dx >= r || dy >= r) inside = true;
                        else
                        {
                            float cdx = r - dx, cdy = r - dy;
                            inside = cdx * cdx + cdy * cdy <= r * r;
                        }
                        px[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return _rounded;
            }
        }

        /// <summary>Rounded panel with optional border (fill drawn inside a border layer).</summary>
        public static RectTransform BoxRounded(Transform parent, string name, Color fill, Color border, float borderSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            var root = Panel(parent, name, new Color(0, 0, 0, 0));
            root.anchorMin = anchorMin; root.anchorMax = anchorMax;
            root.offsetMin = offMin; root.offsetMax = offMax;

            var outer = root.gameObject.GetComponent<Image>();
            outer.sprite = RoundedSprite;
            outer.type = Image.Type.Sliced;

            if (border.a > 0f)
            {
                outer.color = border;
                var innerGo = new GameObject(name + "_fill", typeof(RectTransform), typeof(Image));
                var inner = innerGo.GetComponent<RectTransform>();
                inner.SetParent(root, false);
                inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
                inner.offsetMin = new Vector2(borderSize, borderSize);
                inner.offsetMax = new Vector2(-borderSize, -borderSize);
                var iimg = innerGo.GetComponent<Image>();
                iimg.sprite = RoundedSprite;
                iimg.type = Image.Type.Sliced;
                iimg.color = fill;
            }
            else
            {
                outer.color = fill;
            }
            return root;
        }

        public static Text Text(Transform parent, string name, string content, int size, TextAnchor align, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = DefaultFont;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? UiTheme.Text;
            t.text = content;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Text TextAt(Transform parent, string name, string content, int size,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax,
            TextAnchor align, Color? color = null)
        {
            var t = Text(parent, name, content, size, align, color);
            var rt = t.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, Vector2 anchor, Vector2 size, System.Action onClick, int fontSize = 16, Color? bg = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = Vector2.one * 0.5f;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = bg ?? UiTheme.Panel;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
            colors.pressedColor = new Color(0f, 0f, 0f, 0.15f);
            btn.colors = colors;

            var txt = Text(rt, "label", label, fontSize, TextAnchor.MiddleCenter);
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = Vector2.zero;
            txt.rectTransform.offsetMax = Vector2.zero;

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>Creates a fill bar; returns background and fill rect transforms.</summary>
        public static void FillBar(Transform parent, string name, Color bgColor, Color fillColor,
            out RectTransform bg, out RectTransform fill)
        {
            var bgGo = new GameObject(name + "_bg", typeof(RectTransform), typeof(Image));
            bg = bgGo.GetComponent<RectTransform>();
            bg.SetParent(parent, false);
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.color = bgColor;
            bgImg.raycastTarget = false;

            var fillGo = new GameObject(name + "_fill", typeof(RectTransform), typeof(Image));
            fill = fillGo.GetComponent<RectTransform>();
            fill.SetParent(bg, false);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;

            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = new Vector2(0f, 0f);
        }

        /// <summary>Sets a fill bar (left-anchored) to 0..1 fraction.</summary>
        public static void SetFill(RectTransform bg, RectTransform fill, float frac)
        {
            if (bg == null || fill == null) return;
            frac = Mathf.Clamp01(frac);
            float w = bg.rect.width;
            fill.anchorMax = new Vector2(frac, 1f);
            fill.sizeDelta = new Vector2(0f, 0f);
            fill.offsetMin = new Vector2(0f, 0f);
            fill.offsetMax = new Vector2(0f, 0f);
            // width comes from anchor; force layout via sizeDelta trick:
            fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w * frac);
            fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bg.rect.height);
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
