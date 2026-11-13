using System;
using UnityEngine;
using UnityEngine.UI;

namespace AbyssDefense
{
    /// uGUI 를 코드로 조립하는 도우미. 기준 화면은 가로 1080 (세로는 기기 비율대로).
    public static class Ui
    {
        public static readonly Color Bg = new Color(0.06f, 0.07f, 0.09f);
        public static readonly Color PanelCol = new Color(0.11f, 0.12f, 0.16f, 0.96f);
        public static readonly Color Card = new Color(0.17f, 0.19f, 0.24f);
        public static readonly Color TextCol = new Color(0.93f, 0.93f, 0.95f);
        public static readonly Color Dim = new Color(0.62f, 0.65f, 0.72f);
        public static readonly Color Gold = new Color(0.96f, 0.76f, 0.35f);
        public static readonly Color Good = new Color(0.40f, 0.85f, 0.50f);
        public static readonly Color Bad = new Color(0.95f, 0.35f, 0.35f);

        static Font font, bold;
        static Sprite round;

        public static Font Font => font != null ? font : font = Load("NanumGothic-Regular");
        public static Font Bold => bold != null ? bold : bold = Load("NanumGothic-Bold");
        static Font Load(string n) => Resources.Load<Font>("Fonts/" + n) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// 모서리가 둥근 9분할 판
        public static Sprite Round
        {
            get
            {
                if (round != null) return round;
                const int n = 48; const float r = 14;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = Mathf.Abs(x + 0.5f - n / 2f) - n / 2f + r, dy = Mathf.Abs(y + 0.5f - n / 2f) - n / 2f + r;
                        float d = Mathf.Min(Mathf.Max(dx, dy), 0) + new Vector2(Mathf.Max(dx, 0), Mathf.Max(dy, 0)).magnitude - r;
                        px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
                    }
                t.SetPixels(px); t.Apply();
                return round = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
            }
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// 부모를 꽉 채운다 (여백: 왼쪽, 아래, 오른쪽, 위)
        public static RectTransform Fill(this RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// 부모의 한 점(anchor)에 고정 크기로 놓는다
        public static RectTransform At(this RectTransform rt, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// 위쪽에 가로로 꽉 차는 띠 (top = 위에서 내려온 거리)
        public static RectTransform TopBar(this RectTransform rt, float top, float h, float l = 0, float r = 0)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(l, -top - h); rt.offsetMax = new Vector2(-r, -top);
            return rt;
        }

        /// 아래쪽에 가로로 꽉 차는 띠
        public static RectTransform BottomBar(this RectTransform rt, float bottom, float h, float l = 0, float r = 0)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(l, bottom); rt.offsetMax = new Vector2(-r, bottom + h);
            return rt;
        }

        public static Image Panel(Transform parent, Color c, bool rounded = false, string name = "Panel")
        {
            var img = Node(name, parent).gameObject.AddComponent<Image>();
            img.color = c;
            if (rounded) { img.sprite = Round; img.type = Image.Type.Sliced; }
            return img;
        }

        public static Image Icon(Transform parent, Sprite s, Color? c = null)
        {
            var img = Node("Icon", parent).gameObject.AddComponent<Image>();
            img.sprite = s; img.color = c ?? Color.white; img.raycastTarget = false; img.preserveAspect = true;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color? c = null, TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false)
        {
            var t = Node("Text", parent).gameObject.AddComponent<Text>();
            t.font = bold ? Bold : Font;
            t.text = text; t.fontSize = size; t.color = c ?? TextCol; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.rectTransform.Fill();
            return t;
        }

        public static Button Btn(Transform parent, string text, int size, Color bg, Action onClick, Color? textCol = null)
        {
            var img = Panel(parent, bg, true, "Button");
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.highlightedColor = Color.white; cb.pressedColor = new Color(0.72f, 0.72f, 0.72f); cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            if (!string.IsNullOrEmpty(text)) Label(img.transform, text, size, textCol ?? TextCol, TextAnchor.MiddleCenter, true);
            return b;
        }

        public static RectTransform Rt(this Component c) => (RectTransform)c.transform;
    }
}
