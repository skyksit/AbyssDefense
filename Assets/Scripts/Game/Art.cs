using System;
using System.Collections.Generic;
using AbyssDefense.Sim;
using UnityEngine;

namespace AbyssDefense
{
    /// 임시 그림. 도형을 코드로 그려 스프라이트를 만든다.
    /// Resources/Art/{이름}.png 가 있으면 그 그림을 대신 쓴다 (이름은 아래 Get 에 넘기는 문자열).
    ///   block_{key}, block_{key}_top, icon_{key}, enemy_{key}, unit_{key}, item_{key}, fx_circle, fx_ring, white
    public static class Art
    {
        public const int Px = 48;   // 타일 한 칸의 픽셀 수

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Material mat;

        public static Color C(uint rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        /// 스프라이트·색 메시가 함께 쓰는 재질
        public static Material Mat => mat != null ? mat : mat = new Material(Shader.Find("Sprites/Default"));

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            s = Resources.Load<Sprite>("Art/" + name);
            if (s == null) s = Generate(name);
            cache[name] = s;
            return s;
        }

        public static Sprite Block(BlockDef d) => Get("block_" + d.key);
        public static Sprite BlockTop(BlockDef d) => HasTop(d) ? Get("block_" + d.key + "_top") : null;
        public static Sprite Icon(BlockDef d) => Get("icon_" + d.key);
        public static bool HasTop(BlockDef d) => d.kind == BlockKind.Turret || d.kind == BlockKind.EnemyTurret;

        // ───────── 그리기 도구 ─────────

        sealed class Canvas
        {
            public readonly int w, h;
            public readonly Color[] px;
            public Canvas(int w, int h) { this.w = w; this.h = h; px = new Color[w * h]; }

            /// sdf(x, y) 가 음수인 곳을 칠한다 (가장자리는 부드럽게)
            public void Fill(Func<float, float, float> sdf, Color c)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float a = Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f)) * c.a;
                        if (a <= 0) continue;
                        var o = px[y * w + x];
                        float oa = a + o.a * (1 - a);
                        px[y * w + x] = new Color(
                            (c.r * a + o.r * o.a * (1 - a)) / oa,
                            (c.g * a + o.g * o.a * (1 - a)) / oa,
                            (c.b * a + o.b * o.a * (1 - a)) / oa, oa);
                    }
            }

            public Sprite ToSprite()
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                t.SetPixels(px);
                t.Apply();
                return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Px);
            }
        }

        static Func<float, float, float> Box(float cx, float cy, float hw, float hh, float r) => (x, y) =>
        {
            float dx = Mathf.Abs(x - cx) - hw + r, dy = Mathf.Abs(y - cy) - hh + r;
            return Mathf.Min(Mathf.Max(dx, dy), 0) + new Vector2(Mathf.Max(dx, 0), Mathf.Max(dy, 0)).magnitude - r;
        };
        static Func<float, float, float> Circle(float cx, float cy, float r) => (x, y) =>
            Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
        static Func<float, float, float> Ring(float cx, float cy, float r, float th) => (x, y) =>
            Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r) - th;
        /// 볼록 다각형 (반시계 방향 꼭짓점)
        static Func<float, float, float> Poly(params float[] p) => (x, y) =>
        {
            float d = -1e9f;
            int n = p.Length / 2;
            for (int i = 0; i < n; i++)
            {
                float ax = p[i * 2], ay = p[i * 2 + 1], bx = p[(i + 1) % n * 2], by = p[(i + 1) % n * 2 + 1];
                float ex = bx - ax, ey = by - ay, len = Mathf.Sqrt(ex * ex + ey * ey);
                d = Mathf.Max(d, ((x - ax) * ey - (y - ay) * ex) / len);
            }
            return d;
        };

        static Color Shade(Color c, float m) => new Color(c.r * m, c.g * m, c.b * m, c.a);
        static Color Lite(Color c, float t) => Color.Lerp(c, Color.white, t);

        // ───────── 그림 ─────────

        static Sprite Generate(string name)
        {
            if (name == "white") { var c = new Canvas(4, 4); c.Fill((x, y) => -1, Color.white); return c.ToSprite(); }
            if (name == "fx_circle")
            {
                var c = new Canvas(64, 64);
                for (int i = 0; i < 8; i++) c.Fill(Circle(32, 32, 30 - i * 3.5f), new Color(1, 1, 1, 0.22f));
                return c.ToSprite();
            }
            if (name == "fx_ring") { var c = new Canvas(128, 128); c.Fill(Ring(64, 64, 60, 1.6f), Color.white); return c.ToSprite(); }
            if (name == "icon_lock")
            {
                var c = new Canvas(48, 48);
                c.Fill(Ring(24, 29, 9, 2.6f), Color.white);
                c.Fill(Box(24, 17, 14, 11, 3), Color.white);
                c.Fill(Circle(24, 18, 3), new Color(0, 0, 0, 0.6f));
                return c.ToSprite();
            }
            if (name == "fx_disc") { var c = new Canvas(64, 64); c.Fill(Circle(32, 32, 30), Color.white); return c.ToSprite(); }
            if (name.StartsWith("item_"))
            {
                foreach (var it in Content.Items)
                    if ("item_" + it.key == name)
                    {
                        var c = new Canvas(32, 32);
                        var col = C(it.color);
                        c.Fill(Poly(16, 2, 30, 16, 16, 30, 2, 16), Shade(col, 0.55f));
                        c.Fill(Poly(16, 6, 26, 16, 16, 26, 6, 16), col);
                        c.Fill(Poly(16, 9, 21, 16, 16, 18, 11, 16), Lite(col, 0.5f));
                        return c.ToSprite();
                    }
            }
            if (name.StartsWith("enemy_"))
                foreach (var e in Content.Enemies)
                    if ("enemy_" + e.key == name) return PaintEnemy(e);
            if (name.StartsWith("unit_"))
                foreach (var u in Content.Units)
                    if ("unit_" + u.key == name) return PaintUnit(u);
            foreach (var b in Content.Blocks)
            {
                if (name == "block_" + b.key) return PaintBlock(b, true, false);
                if (name == "block_" + b.key + "_top") return PaintBlock(b, false, true);
                if (name == "icon_" + b.key) return PaintBlock(b, true, true);
            }
            Debug.LogWarning("[Art] 모르는 그림: " + name);
            return Generate("white");
        }

        static Sprite PaintBlock(BlockDef d, bool baseLayer, bool topLayer)
        {
            int n = d.size * Px;
            var c = new Canvas(n, n);
            float m = n * 0.5f;
            var col = C(d.color);
            var dark = new Color(0.10f, 0.11f, 0.13f);

            if (baseLayer)
            {
                switch (d.kind)
                {
                    case BlockKind.Wall:
                        c.Fill(Box(m, m, m - 1, m - 1, 4), Shade(col, 0.45f));
                        c.Fill(Box(m, m, m - 4, m - 4, 3), col);
                        c.Fill(Box(m, m + 2, m - 9, m - 11, 2), Lite(col, 0.18f));
                        break;
                    case BlockKind.Core:
                    case BlockKind.EnemyCore:
                        c.Fill(Box(m, m, m - 2, m - 2, 14), dark);
                        c.Fill(Box(m, m, m - 7, m - 7, 11), Shade(col, 0.35f));
                        c.Fill(Ring(m, m, m * 0.62f, 5), Shade(col, 0.8f));
                        c.Fill(Circle(m, m, m * 0.42f), Shade(col, 0.55f));
                        c.Fill(Circle(m, m, m * 0.30f), col);
                        c.Fill(Circle(m, m, m * 0.16f), Lite(col, 0.7f));
                        for (int i = 0; i < 4; i++)
                        {
                            float bx = i % 2 == 0 ? 16 : n - 16, by = i < 2 ? 16 : n - 16;
                            c.Fill(Circle(bx, by, 5), Lite(col, 0.3f));
                        }
                        break;
                    case BlockKind.Factory:
                        c.Fill(Box(m, m, m - 2, m - 2, 8), dark);
                        c.Fill(Box(m, m, m - 6, m - 6, 6), Shade(col, 0.4f));
                        c.Fill(Box(m, m, m - 13, m - 13, 5), Shade(col, 0.22f));
                        if (d == Content.Shipyard)
                        {
                            // 물결과 배
                            c.Fill(Box(m, m - 20, m - 18, 4, 2), Shade(col, 0.9f));
                            c.Fill(Box(m, m - 30, m - 24, 3, 1), Shade(col, 0.6f));
                            c.Fill(Poly(m + 26, m, m + 8, m + 14, m - 24, m + 10, m - 24, m - 6, m + 8, m - 10), col);
                            c.Fill(Circle(m - 4, m + 2, 6), Lite(col, 0.6f));
                        }
                        else if (d == Content.SupportShop)
                        {
                            c.Fill(Circle(m, m, 24), Shade(col, 0.8f));
                            c.Fill(Box(m, m, 17, 6, 2), Color.white);
                            c.Fill(Box(m, m, 6, 17, 2), Color.white);
                        }
                        else if (d == Content.LegShop)
                        {
                            for (int i = 0; i < 6; i++)
                            {
                                float a = i * Mathf.PI / 3;
                                for (int k = 2; k <= 4; k++) c.Fill(Circle(m + Mathf.Cos(a) * 7 * k, m + Mathf.Sin(a) * 7 * k, 3), Shade(col, 0.85f));
                            }
                            c.Fill(Circle(m, m, 15), col);
                            c.Fill(Circle(m, m, 6), Lite(col, 0.6f));
                        }
                        else if (d == Content.Airfield)
                        {
                            // 활주로와 기체
                            c.Fill(Box(m, m, 5, m - 16, 2), Shade(col, 0.75f));
                            c.Fill(Poly(m, m + 22, m - 20, m - 12, m + 20, m - 12), col);
                            c.Fill(Poly(m, m + 10, m - 8, m - 8, m + 8, m - 8), Lite(col, 0.55f));
                        }
                        else
                        {
                            // 톱니와 로봇 머리
                            for (int i = 0; i < 8; i++)
                            {
                                float a = i * Mathf.PI / 4;
                                c.Fill(Circle(m + Mathf.Cos(a) * 24, m + Mathf.Sin(a) * 24, 6), Shade(col, 0.8f));
                            }
                            c.Fill(Circle(m, m, 22), Shade(col, 0.8f));
                            c.Fill(Circle(m, m, 15), col);
                            c.Fill(Box(m, m, 9, 4, 2), Lite(col, 0.65f));
                        }
                        for (int i = 0; i < 4; i++)
                        {
                            float bx = i % 2 == 0 ? 12 : n - 12, by = i < 2 ? 12 : n - 12;
                            c.Fill(Circle(bx, by, 3.5f), Lite(col, 0.4f));
                        }
                        break;
                    default:   // 포탑 받침
                        c.Fill(Box(m, m, m - 2, m - 2, 6), dark);
                        c.Fill(Box(m, m, m - 5, m - 5, 5), Shade(col, 0.42f));
                        c.Fill(Box(m, m, m - 9, m - 9, 4), Shade(col, 0.6f));
                        break;
                }
            }

            if (baseLayer && d.kind == BlockKind.EnemyCore)
            {
                // 적 핵: 가운데의 눈과 네 방향 가시
                c.Fill(Poly(m, m + 34, m - 12, m + 14, m + 12, m + 14), Lite(col, 0.25f));
                c.Fill(Poly(m, m - 34, m + 12, m - 14, m - 12, m - 14), Lite(col, 0.25f));
                c.Fill(Poly(m + 34, m, m + 14, m + 12, m + 14, m - 12), Lite(col, 0.25f));
                c.Fill(Poly(m - 34, m, m - 14, m - 12, m - 14, m + 12), Lite(col, 0.25f));
                c.Fill(Circle(m, m, 13), new Color(0.08f, 0.02f, 0.03f));
                c.Fill(Circle(m, m, 6), new Color(1f, 0.85f, 0.3f));
            }
            if (topLayer && d.kind == BlockKind.EnemyTurret)
            {
                c.Fill(Box(m + 11, m, 12, 3.5f, 1), Shade(col, 0.5f));
                c.Fill(Poly(m + 12, m, m - 6, m + 11, m - 6, m - 11), Lite(col, 0.1f));
                c.Fill(Circle(m - 1, m, 4), new Color(1f, 0.85f, 0.3f));
            }
            if (topLayer && d.kind == BlockKind.Turret)
            {
                var barrel = Lite(col, 0.15f);
                if (d == Content.RapidTurret)
                {
                    c.Fill(Box(m + 12, m - 4, 12, 2.5f, 1), Shade(col, 0.55f));
                    c.Fill(Box(m + 12, m + 4, 12, 2.5f, 1), Shade(col, 0.55f));
                    c.Fill(Circle(m, m, 11), barrel);
                    c.Fill(Circle(m, m, 5), Shade(col, 0.5f));
                }
                else if (d == Content.MortarTurret)
                {
                    c.Fill(Box(m + 14, m, 22, 11, 4), Shade(col, 0.55f));
                    c.Fill(Box(m + 30, m, 7, 13, 3), Shade(col, 0.4f));
                    c.Fill(Circle(m, m, 22), barrel);
                    c.Fill(Circle(m, m, 10), Shade(col, 0.5f));
                }
                else
                {
                    c.Fill(Box(m + 20, m, 26, 5, 2), Shade(col, 0.5f));
                    c.Fill(Poly(m + 34, m, m + 42, m - 7, m + 47, m, m + 42, m + 7), Lite(col, 0.6f));
                    c.Fill(Poly(m - 18, m, m, m - 18, m + 18, m, m, m + 18), barrel);
                    c.Fill(Circle(m, m, 7), Lite(col, 0.6f));
                }
            }
            return c.ToSprite();
        }

        /// 아군 유닛도 +x 쪽을 보고 있게 그린다. 단계가 오를수록 포신·날개가 늘어난다
        static Sprite PaintUnit(UnitDef u)
        {
            int n = Mathf.CeilToInt(u.radius * 2 * Px) + 12;
            var c = new Canvas(n, n);
            float m = n * 0.5f, r = u.radius * Px;
            var col = C(u.color);
            var dark = Shade(col, 0.35f);
            var lite = Lite(col, 0.55f);
            string shape = u.shape ?? (u.flying ? "air" : "mech");
            if (shape == "boat")
            {
                // 뾰족한 뱃머리의 선체와 포탑
                c.Fill(Poly(m + r + 3, m, m + r * 0.3f, m + r * 0.62f, m - r, m + r * 0.5f, m - r, m - r * 0.5f, m + r * 0.3f, m - r * 0.62f), dark);
                c.Fill(Poly(m + r - 2, m, m + r * 0.25f, m + r * 0.62f - 4, m - r + 3, m + r * 0.5f - 3, m - r + 3, m - r * 0.5f + 3, m + r * 0.25f, m - r * 0.62f + 4), col);
                c.Fill(Circle(m + r * 0.1f, m, r * 0.26f), lite);
                c.Fill(Box(m + r * 0.45f, m, r * 0.32f, 2.5f, 1), lite);
                if (u.tier >= 2) c.Fill(Circle(m - r * 0.5f, m, r * 0.2f), lite);
                if (u.fieldDamage > 0) { c.Fill(Ring(m + r * 0.1f, m, r * 0.42f, 2), Color.white); c.Fill(Circle(m + r * 0.1f, m, r * 0.16f), Color.white); }   // 방전함: 포 대신 방전 고리
                else if (u.tier >= 3) { c.Fill(Box(m - r * 0.15f, m + r * 0.3f, r * 0.2f, 2, 1), Color.white); c.Fill(Box(m - r * 0.15f, m - r * 0.3f, r * 0.2f, 2, 1), Color.white); }
            }
            else if (shape == "bug")
            {
                // 가시 돋친 둥근 몸
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3 + 0.5f;
                    c.Fill(Circle(m + Mathf.Cos(a) * r * 0.85f, m + Mathf.Sin(a) * r * 0.85f, r * 0.22f), dark);
                }
                c.Fill(Circle(m, m, r * 0.8f), dark);
                c.Fill(Circle(m, m, r * 0.62f), col);
                c.Fill(Circle(m + r * 0.2f, m, r * 0.25f), new Color(1f, 0.35f, 0.2f));
            }
            else if (shape == "legs")
            {
                // 여섯 개의 긴 다리
                for (int i = 0; i < 6; i++)
                {
                    float a = (i - 2.5f) * 0.6f + (i < 3 ? 1.6f : 2.1f);
                    for (int k = 1; k <= 4; k++) c.Fill(Circle(m + Mathf.Cos(a) * r * 0.28f * k, m + Mathf.Sin(a) * r * 0.28f * k, 2.2f), dark);
                }
                c.Fill(Circle(m, m, r * 0.58f), dark);
                c.Fill(Circle(m, m, r * 0.46f), col);
                c.Fill(Box(m + r * 0.55f, m, r * 0.4f, 2.5f, 1), lite);
                c.Fill(Circle(m, m, r * 0.2f), lite);
                if (u.bullet != null && u.bullet.slow > 0)
                {
                    // 산성을 던지는 유닛: 탄 색의 둥근 산성 주머니
                    c.Fill(Circle(m - r * 0.1f, m, r * 0.3f), C(u.bullet.color));
                    c.Fill(Circle(m + r * 0.75f, m, r * 0.14f), C(u.bullet.color));
                }
                if (u.drain > 0)
                {
                    // 체력을 빨아들이는 유닛: 탄 색의 심지와 앞으로 뻗은 두 송곳니
                    c.Fill(Circle(m, m, r * 0.2f), C(u.bullet.color));
                    c.Fill(Box(m + r * 0.5f, m + r * 0.22f, r * 0.3f, 2f, 1), lite);
                    c.Fill(Box(m + r * 0.5f, m - r * 0.22f, r * 0.3f, 2f, 1), lite);
                }
            }
            else if (shape == "drone" || shape == "transport")
            {
                // 네 귀퉁이의 회전 날개
                float k = shape == "transport" ? 0.75f : 0.7f;
                for (int i = 0; i < 4; i++)
                {
                    float bx = m + (i % 2 == 0 ? -1 : 1) * r * k, by = m + (i < 2 ? -1 : 1) * r * k;
                    c.Fill(Circle(bx, by, r * 0.3f), dark);
                    c.Fill(Circle(bx, by, r * 0.16f), lite);
                }
                if (shape == "transport")
                {
                    c.Fill(Box(m, m, r * 0.75f, r * 0.5f, 5), dark);
                    c.Fill(Box(m, m, r * 0.75f - 3, r * 0.5f - 3, 4), col);
                    c.Fill(Box(m, m, r * 0.4f, r * 0.26f, 2), Shade(col, 0.55f));
                }
                else
                {
                    c.Fill(Circle(m, m, r * 0.5f), dark);
                    c.Fill(Circle(m, m, r * 0.38f), col);
                    if (u.mine > 0) c.Fill(Poly(m + r * 0.95f, m, m + r * 0.3f, m + r * 0.24f, m + r * 0.3f, m - r * 0.24f), Color.white);   // 채굴 드론: 앞으로 뻗은 굴착 날
                    else { c.Fill(Box(m, m, r * 0.26f, 2, 1), Color.white); c.Fill(Box(m, m, 2, r * 0.26f, 1), Color.white); }
                }
            }
            else if (shape == "commander")
            {
                c.Fill(Ring(m, m, r * 0.9f, 2), lite);
                c.Fill(Poly(m + r, m, m, m + r * 0.7f, m - r * 0.7f, m, m, m - r * 0.7f), dark);
                c.Fill(Poly(m + r - 4, m, m, m + r * 0.7f - 4, m - r * 0.7f + 4, m, m, m - r * 0.7f + 4), col);
                c.Fill(Circle(m, m, r * 0.22f), Color.white);
            }
            else if (shape == "carrier")
            {
                // 넓은 날개와 갑판
                c.Fill(Poly(m + r, m, m + r * 0.2f, m + r, m - r, m + r * 0.7f, m - r, m - r * 0.7f, m + r * 0.2f, m - r), dark);
                c.Fill(Poly(m + r - 4, m, m + r * 0.2f, m + r - 5, m - r + 4, m + r * 0.7f - 4, m - r + 4, m - r * 0.7f + 4, m + r * 0.2f, m - r + 5), col);
                c.Fill(Box(m - r * 0.1f, m, r * 0.7f, r * 0.14f, 2), lite);
                c.Fill(Circle(m + r * 0.3f, m + r * 0.45f, r * 0.14f), lite); c.Fill(Circle(m + r * 0.3f, m - r * 0.45f, r * 0.14f), lite);
                c.Fill(Circle(m - r * 0.5f, m + r * 0.4f, r * 0.12f), Color.white); c.Fill(Circle(m - r * 0.5f, m - r * 0.4f, r * 0.12f), Color.white);
            }
            else if (shape == "air")
            {
                // 삼각 날개
                c.Fill(Poly(m + r + 2, m, m - r, m + r, m - r * 0.45f, m), dark);
                c.Fill(Poly(m + r + 2, m, m - r * 0.45f, m, m - r, m - r), dark);
                c.Fill(Poly(m + r - 3, m, m - r + 4, m + r - 5, m - r * 0.3f, m), col);
                c.Fill(Poly(m + r - 3, m, m - r * 0.3f, m, m - r + 4, m - r + 5), col);
                c.Fill(Box(m, m, r * 0.55f, r * 0.2f, 2), lite);
                if (u.tier >= 2) { c.Fill(Circle(m - r * 0.35f, m + r * 0.5f, r * 0.16f), lite); c.Fill(Circle(m - r * 0.35f, m - r * 0.5f, r * 0.16f), lite); }
                if (u.tier >= 3) c.Fill(Box(m + r * 0.55f, m, r * 0.3f, 2, 1), Color.white);
                if (u.tier >= 4) { c.Fill(Box(m, m + r * 0.42f, r * 0.3f, 2.5f, 1), Color.white); c.Fill(Box(m, m - r * 0.42f, r * 0.3f, 2.5f, 1), Color.white); }
            }
            else
            {
                // 두 다리와 몸통, 앞으로 뻗은 포신
                c.Fill(Box(m - r * 0.1f, m + r * 0.72f, r * 0.55f, r * 0.24f, 3), dark);
                c.Fill(Box(m - r * 0.1f, m - r * 0.72f, r * 0.55f, r * 0.24f, 3), dark);
                c.Fill(Box(m, m, r * 0.72f, r * 0.62f, 5), dark);
                c.Fill(Box(m, m, r * 0.72f - 3, r * 0.62f - 3, 4), col);
                if (u.tier >= 2) { c.Fill(Box(m + r * 0.7f, m + r * 0.3f, r * 0.45f, 2.5f, 1), lite); c.Fill(Box(m + r * 0.7f, m - r * 0.3f, r * 0.45f, 2.5f, 1), lite); }
                if (u.tier != 2) c.Fill(Box(m + r * 0.7f, m, r * 0.5f, u.tier >= 3 ? 4.5f : 2.5f, 1), lite);
                c.Fill(Circle(m - r * 0.1f, m, r * 0.26f), lite);
                if (u.tier >= 4) { c.Fill(Box(m - r * 0.45f, m + r * 0.42f, r * 0.2f, r * 0.12f, 2), lite); c.Fill(Box(m - r * 0.45f, m - r * 0.42f, r * 0.2f, r * 0.12f, 2), lite); }
                if (u.tier >= 5) c.Fill(Ring(m - r * 0.1f, m, r * 0.42f, 1.6f), Color.white);
                if (shape == "support") { c.Fill(Box(m - r * 0.1f, m, r * 0.34f, r * 0.1f, 1), Color.white); c.Fill(Box(m - r * 0.1f, m, r * 0.1f, r * 0.34f, 1), Color.white); }
            }
            return c.ToSprite();
        }

        /// 적은 +x 쪽을 보고 있게 그린다
        static Sprite PaintEnemy(EnemyDef e)
        {
            int n = Mathf.CeilToInt(e.radius * 2 * Px) + 8;
            var c = new Canvas(n, n);
            float m = n * 0.5f, r = e.radius * Px;
            var col = C(e.color);
            var dark = Shade(col, 0.4f);
            if (e == Content.Runner)
            {
                c.Fill(Poly(m + r, m, m - r, m + r * 0.8f, m - r, m - r * 0.8f), dark);
                c.Fill(Poly(m + r - 4, m, m - r + 2, m + r * 0.8f - 4, m - r + 2, m - r * 0.8f + 4), col);
            }
            else if (e == Content.Breaker)
            {
                c.Fill(Box(m, m, r, r, 6), dark);
                c.Fill(Box(m, m, r - 3, r - 3, 4), col);
                c.Fill(Box(m + r * 0.55f, m, r * 0.3f, r * 0.7f, 2), Lite(col, 0.35f));
            }
            else if (e == Content.Shell)
            {
                // 겹친 장갑판
                c.Fill(Poly(m + r, m, m + r * 0.5f, m + r * 0.87f, m - r * 0.5f, m + r * 0.87f, m - r, m, m - r * 0.5f, m - r * 0.87f, m + r * 0.5f, m - r * 0.87f), dark);
                c.Fill(Poly(m + r - 4, m, m + r * 0.5f - 2, m + r * 0.87f - 4, m - r * 0.5f + 2, m + r * 0.87f - 4, m - r + 4, m, m - r * 0.5f + 2, m - r * 0.87f + 4, m + r * 0.5f - 2, m - r * 0.87f + 4), col);
                c.Fill(Box(m, m, r * 0.5f, 2, 1), Lite(col, 0.5f));
                c.Fill(Circle(m + r * 0.5f, m, 2.5f), new Color(1f, 0.4f, 0.4f));
            }
            else if (e == Content.Flyer)
            {
                c.Fill(Poly(m + r, m, m - r, m + r, m - r, m - r), dark);
                c.Fill(Poly(m + r - 5, m, m - r + 2, m + r - 5, m - r + 2, m - r + 5), col);
                c.Fill(Box(m - r * 0.5f, m, r * 0.5f, 2, 1), dark);
                c.Fill(Circle(m + r * 0.2f, m, 3), Color.white);
            }
            else if (e.boss)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4;
                    c.Fill(Circle(m + Mathf.Cos(a) * r * 0.82f, m + Mathf.Sin(a) * r * 0.82f, r * 0.2f), dark);
                }
                c.Fill(Circle(m, m, r * 0.82f), dark);
                c.Fill(Circle(m, m, r * 0.7f), col);
                c.Fill(Circle(m + r * 0.3f, m + r * 0.28f, r * 0.14f), new Color(1, 0.9f, 0.3f));
                c.Fill(Circle(m + r * 0.3f, m - r * 0.28f, r * 0.14f), new Color(1, 0.9f, 0.3f));
            }
            else
            {
                c.Fill(Circle(m, m, r), dark);
                c.Fill(Circle(m, m, r - 3), col);
                c.Fill(Circle(m + r * 0.45f, m + r * 0.3f, 2.2f), Color.white);
                c.Fill(Circle(m + r * 0.45f, m - r * 0.3f, 2.2f), Color.white);
            }
            return c.ToSprite();
        }
    }
}
