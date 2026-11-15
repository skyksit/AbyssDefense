using System.Collections.Generic;
using AbyssDefense.Sim;
using UnityEngine;

namespace AbyssDefense
{
    /// 매 프레임 다시 채우는 색 사각형 묶음 (탄, 체력 막대, 설치 미리보기)
    public sealed class QuadBatch
    {
        readonly Mesh mesh = new Mesh();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color32> cols = new List<Color32>();
        readonly List<int> tris = new List<int>();
        public readonly GameObject go;

        public QuadBatch(Transform parent, string name, int order)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Art.Mat;
            mr.sortingOrder = order;
            mesh.MarkDynamic();
        }

        public void Begin() { verts.Clear(); cols.Clear(); tris.Clear(); }

        public void Quad(float cx, float cy, float w, float h, Color col, float rotDeg = 0)
        {
            int n = verts.Count;
            float hw = w * 0.5f, hh = h * 0.5f;
            if (rotDeg == 0)
            {
                verts.Add(new Vector3(cx - hw, cy - hh)); verts.Add(new Vector3(cx + hw, cy - hh));
                verts.Add(new Vector3(cx + hw, cy + hh)); verts.Add(new Vector3(cx - hw, cy + hh));
            }
            else
            {
                float r = rotDeg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
                verts.Add(new Vector3(cx + (-hw * c + hh * s), cy + (-hw * s - hh * c)));
                verts.Add(new Vector3(cx + (hw * c + hh * s), cy + (hw * s - hh * c)));
                verts.Add(new Vector3(cx + (hw * c - hh * s), cy + (hw * s + hh * c)));
                verts.Add(new Vector3(cx + (-hw * c - hh * s), cy + (-hw * s + hh * c)));
            }
            Color32 c32 = col;
            cols.Add(c32); cols.Add(c32); cols.Add(c32); cols.Add(c32);
            tris.Add(n); tris.Add(n + 1); tris.Add(n + 2); tris.Add(n); tris.Add(n + 2); tris.Add(n + 3);
        }

        public void End()
        {
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1000, 1000, 10));
        }
    }

    /// 지역 한 판의 그림. Sim 을 읽기만 한다.
    public sealed class GameView : MonoBehaviour
    {
        const int OrderFloor = -10, OrderBlock = 0, OrderBlockTop = 2, OrderItems = 4, OrderEnemy = 10, OrderBullet = 14, OrderFx = 16, OrderBars = 20;

        sealed class BView { public GameObject go; public SpriteRenderer sr, top, aura; }
        struct FxItem { public SpriteRenderer sr; public float t, life, r0, r1; public Color col; }

        Sim.Sim sim;
        readonly Dictionary<Building, BView> views = new Dictionary<Building, BView>();
        readonly List<SpriteRenderer> enemyPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> unitPool = new List<SpriteRenderer>();
        readonly List<FxItem> fxLive = new List<FxItem>();
        readonly Stack<SpriteRenderer> fxFree = new Stack<SpriteRenderer>();
        readonly List<Transform> spawnMarks = new List<Transform>();
        QuadBatch bars;

        public void Init(Sim.Sim s)
        {
            sim = s;
            BuildTerrain();
            bars = new QuadBatch(transform, "Bars", OrderBars);
            foreach (var b in sim.world.buildings) Add(b);
            sim.BuildingAdded += Add;
            sim.BuildingRemoved += Remove;

            foreach (int sp in sim.world.spawns)
            {
                var sr = NewSprite("Spawn", Art.Get("fx_ring"), OrderBlock - 1);
                sr.color = new Color(1f, 0.25f, 0.3f, 0.8f);
                sr.transform.position = new Vector3(sp % sim.world.W + 0.5f, sp / sim.world.W + 0.5f);
                spawnMarks.Add(sr.transform);
            }
        }

        SpriteRenderer NewSprite(string name, Sprite sprite, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        // ───────── 지형: 바닥·벽을 색 사각형 메시 하나로 ─────────

        void BuildTerrain()
        {
            var w = sim.world;
            var floor = Art.C(sim.abyss.floorColor);
            var wall = Art.C(sim.abyss.wallColor);
            var verts = new List<Vector3>(); var cols = new List<Color32>(); var tris = new List<int>();

            void Q(float x, float y, float sx, float sy, Color c)
            {
                int n = verts.Count;
                verts.Add(new Vector3(x, y)); verts.Add(new Vector3(x + sx, y));
                verts.Add(new Vector3(x + sx, y + sy)); verts.Add(new Vector3(x, y + sy));
                Color32 c32 = c;
                cols.Add(c32); cols.Add(c32); cols.Add(c32); cols.Add(c32);
                tris.Add(n); tris.Add(n + 1); tris.Add(n + 2); tris.Add(n); tris.Add(n + 2); tris.Add(n + 3);
            }

            // 맵 바깥도 벽 색으로 넓게 깔아 가장자리가 비어 보이지 않게
            Q(-30, -30, w.W + 60, w.H + 60, wall);
            for (int y = 0; y < w.H; y++)
                for (int x = 0; x < w.W; x++)
                {
                    var t = w.tiles[w.Idx(x, y)];
                    float v = t.shade / 255f;
                    if (t.water)
                    {
                        // 호수: 물결 무늬
                        var deep = new Color(0.07f, 0.22f, 0.36f);
                        Q(x, y, 1, 1, Color.Lerp(deep, deep * 1.35f, v));
                        if (((x + y) & 1) == 0) Q(x + 0.15f, y + 0.42f, 0.7f, 0.08f, new Color(0.35f, 0.62f, 0.8f, 0.55f));
                        continue;
                    }
                    if (t.wall)
                    {
                        // 바닥과 맞닿은 벽은 조금 밝게 (절벽 가장자리)
                        bool edge = !w.Solid(x + 1, y) || !w.Solid(x - 1, y) || !w.Solid(x, y + 1) || !w.Solid(x, y - 1);
                        if (edge) Q(x, y, 1, 1, Color.Lerp(wall, floor, 0.45f + v * 0.1f));
                        continue;
                    }
                    Q(x, y, 1, 1, Color.Lerp(floor, floor * 1.25f, v));
                    if (((x + y) & 1) == 0) Q(x + 0.04f, y + 0.04f, 0.92f, 0.92f, Color.Lerp(floor, floor * 1.25f, v) * 1.06f);
                    if (t.hazard)
                    {
                        // 위험 바닥: 용암 틈은 붉은 바탕에 밝은 금, 산성 웅덩이는 탁한 연두 바탕에 밝은 방울
                        bool lava = sim.rules.hazard == Hazard.Lava;
                        Q(x, y, 1, 1, lava ? new Color(0.55f, 0.13f, 0.05f, 0.85f) : new Color(0.25f, 0.42f, 0.08f, 0.85f));
                        if (lava) Q(x + 0.12f + 0.3f * v, y + 0.2f, 0.5f, 0.13f, new Color(1f, 0.62f, 0.18f, 0.9f));
                        else Q(x + 0.2f + 0.4f * v, y + 0.25f + 0.3f * (1 - v), 0.2f, 0.2f, new Color(0.75f, 0.95f, 0.35f, 0.9f));
                    }
                }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            var go = new GameObject("Terrain");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Art.Mat;
            mr.sortingOrder = OrderFloor;
        }

        // ───────── 건물 ─────────

        void Add(Building b)
        {
            var v = new BView();
            v.sr = NewSprite(b.def.key, Art.Block(b.def), OrderBlock);
            v.go = v.sr.gameObject;
            v.go.transform.position = new Vector3(b.Cx, b.Cy);
            var top = Art.BlockTop(b.def);
            if (top != null) v.top = NewSprite("top", top, OrderBlockTop, v.go.transform);
            if (b.def.kind == BlockKind.Support)
            {
                // 지원 건물의 범위 고리
                v.aura = NewSprite("aura", Art.Get("fx_ring"), OrderBlock - 1, v.go.transform);
                v.aura.transform.localScale = Vector3.one * (b.def.supportRange * 2 / v.aura.sprite.bounds.size.x);
            }
            views[b] = v;
        }

        void Remove(Building b)
        {
            if (!views.TryGetValue(b, out var v)) return;
            Destroy(v.go);
            views.Remove(b);
        }

        // ───────── 매 프레임 ─────────

        void LateUpdate()
        {
            if (sim == null) return;
            float time = Time.unscaledTime;
            bars.Begin();

            foreach (var kv in views)
            {
                var b = kv.Key; var v = kv.Value;
                var tint = b.hitFlash > 0 ? new Color(1f, 0.55f, 0.55f) : Color.white;
                if (!b.Built)
                {
                    // 건설 터: 진행될수록 또렷해지고, 순서를 기다리거나 자원이 없어 멈춘 곳은 막대가 회색
                    tint.a = 0.3f + 0.5f * b.progress;
                    float w = b.def.size * 0.8f, y0 = b.Cy - b.def.size * 0.5f + 0.12f;
                    bars.Quad(b.Cx, y0, w + 0.06f, 0.16f, new Color(0, 0, 0, 0.75f));
                    bars.Quad(b.Cx - w * 0.5f * (1 - b.progress), y0, w * b.progress, 0.1f, b.constructing ? new Color(1f, 0.82f, 0.3f) : new Color(0.6f, 0.6f, 0.65f));
                }
                v.sr.color = tint;
                if (v.top != null) v.top.color = tint;
                if (b is TurretBuilding t && b.Built)
                {
                    v.top.transform.rotation = Quaternion.Euler(0, 0, t.aim * Mathf.Rad2Deg);
                    // 핵에 탄약 자원이 없다: 붉게 깜빡이는 점
                    float half = t.def.size * 0.5f;
                    if (t.Starved && Mathf.Repeat(time, 0.8f) < 0.5f) bars.Quad(t.Cx + half - 0.2f, t.Cy + half - 0.2f, 0.26f, 0.26f, new Color(1f, 0.2f, 0.2f), 45);
                }
                if (v.aura != null)
                {
                    // 범위 고리: 다 지어져야 보이고, 역장 발생기는 남은 역장만큼 또렷하다 (다 닳으면 붉게 깜빡인다)
                    var ac = Art.C(b.def.color, 0.22f);
                    if (b is SupportBuilding sb && b.def.shield > 0)
                    {
                        float k = Mathf.Clamp01(sb.shieldHp / sb.MaxShield);
                        ac = k > 0 ? Art.C(b.def.color, 0.15f + 0.55f * k) : new Color(1f, 0.3f, 0.3f, Mathf.Repeat(time, 0.8f) < 0.4f ? 0.35f : 0.1f);
                    }
                    v.aura.enabled = b.Built;
                    v.aura.color = ac;
                }
                if (b is EnemyTurretBuilding et) v.top.transform.rotation = Quaternion.Euler(0, 0, et.aim * Mathf.Rad2Deg);
                if (b is FactoryBuilding fb && b.Built && fb.making != null)
                {
                    // 유닛 생산 진행 막대
                    float w = b.def.size * 0.8f, y0 = b.Cy - b.def.size * 0.5f + 0.12f;
                    bars.Quad(b.Cx, y0, w + 0.06f, 0.16f, new Color(0, 0, 0, 0.75f));
                    bars.Quad(b.Cx - w * 0.5f * (1 - fb.work), y0, w * fb.work, 0.1f, Art.C(fb.making.color));
                }
                if (b.Built && b.health < b.maxHealth) HealthBar(b.Cx, b.Cy + b.def.size * 0.5f - 0.1f, b.def.size * 0.8f, b.health / b.maxHealth);
            }

            // 부서진 자리: 다시 지어질 건물의 흐린 윤곽
            foreach (var r in sim.ruins)
            {
                float rs = r.def.size, rx = r.x + rs * 0.5f, ry = r.y + rs * 0.5f;
                var rc = Art.C(r.def.color, 0.28f);
                bars.Quad(rx, ry, rs - 0.12f, rs - 0.12f, new Color(rc.r, rc.g, rc.b, 0.12f));
                bars.Quad(rx, ry + rs * 0.5f - 0.08f, rs - 0.12f, 0.06f, rc); bars.Quad(rx, ry - rs * 0.5f + 0.08f, rs - 0.12f, 0.06f, rc);
                bars.Quad(rx + rs * 0.5f - 0.08f, ry, 0.06f, rs - 0.12f, rc); bars.Quad(rx - rs * 0.5f + 0.08f, ry, 0.06f, rs - 0.12f, rc);
            }

            // 적
            var es = sim.enemies;
            while (enemyPool.Count < es.Count) enemyPool.Add(NewSprite("Enemy", null, OrderEnemy));
            for (int i = 0; i < enemyPool.Count; i++)
            {
                var sr = enemyPool[i];
                if (i >= es.Count) { if (sr.enabled) sr.enabled = false; continue; }
                var e = es[i];
                sr.enabled = true;
                sr.sprite = Art.Get("enemy_" + e.def.key);
                sr.sortingOrder = e.def.flying ? OrderEnemy + 2 : OrderEnemy;
                // 불타면 주황, 산성이면 보라, 젖으면 파랑, 둔화 중이면 연두
                sr.color = e.hitFlash > 0 ? new Color(1f, 1f, 1f, 0.6f) : e.burnTicks > 0 ? new Color(1f, 0.62f, 0.3f) : e.acidTicks > 0 ? new Color(0.85f, 0.6f, 1f)
                    : e.wetTicks > 0 ? new Color(0.55f, 0.8f, 1f) : e.slowTicks > 0 ? new Color(0.72f, 1f, 0.55f) : Color.white;
                sr.transform.position = new Vector3(e.x, e.y);
                sr.transform.localScale = Vector3.one * (e.elite ? 1.35f : 1f);
                float er = e.def.radius * (e.elite ? 1.35f : 1f);
                if (e.elite)
                {
                    // 정예: 머리 위의 금색 마름모
                    bars.Quad(e.x, e.y + er + 0.3f, 0.2f, 0.2f, new Color(1f, 0.82f, 0.3f), 45);
                }
                if (e.barrier > 0 && e.barrierMax > 0)
                {
                    // 보호막: 체력 막대 위의 하늘색 막대
                    float bw = Mathf.Max(0.6f, e.def.radius * 2), bf = Mathf.Clamp01(e.barrier / e.barrierMax);
                    bars.Quad(e.x, e.y + er + 0.26f, bw + 0.06f, 0.1f, new Color(0, 0, 0, 0.75f));
                    bars.Quad(e.x - bw * 0.5f * (1 - bf), e.y + er + 0.26f, bw * bf, 0.06f, new Color(0.45f, 0.85f, 1f));
                }
                if (e.cargo > 0) for (int k = 0; k < e.cargo; k++) bars.Quad(e.x - 0.3f + k * 0.2f, e.y - er - 0.1f, 0.14f, 0.14f, Art.C(e.def.carries.color), 45);
                if (e.vx != 0 || e.vy != 0)
                    sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(e.vy, e.vx) * Mathf.Rad2Deg);
                if (e.health < e.maxHealth) HealthBar(e.x, e.y + er + 0.14f, Mathf.Max(0.6f, e.def.radius * 2), e.health / e.maxHealth);
            }

            // 아군 유닛
            var us = sim.units;
            var cmd = sim.commander != null && !sim.commander.dead ? sim.commander : null;
            int unitCount = us.Count + (cmd != null ? 1 : 0);
            while (unitPool.Count < unitCount) unitPool.Add(NewSprite("Unit", null, OrderEnemy + 1));
            for (int i = 0; i < unitPool.Count; i++)
            {
                var sr = unitPool[i];
                if (i >= unitCount) { if (sr.enabled) sr.enabled = false; continue; }
                var u = i < us.Count ? us[i] : cmd;
                if (u.carrier != null) { if (sr.enabled) sr.enabled = false; continue; }   // 수송기에 실려 있다
                sr.enabled = true;
                sr.sprite = Art.Get("unit_" + u.def.key);
                sr.sortingOrder = u.def.flying ? OrderEnemy + 3 : OrderEnemy + 1;
                sr.color = u.hitFlash > 0 ? new Color(1f, 0.6f, 0.6f) : u.barrier > 0 ? new Color(0.75f, 0.93f, 1f) : Color.white;   // 보호막이 남아 있으면 하늘색
                sr.transform.position = new Vector3(u.x, u.y);
                sr.transform.rotation = Quaternion.Euler(0, 0, u.aim * Mathf.Rad2Deg);
                if (u.health < u.maxHealth) HealthBar(u.x, u.y + u.def.radius + 0.14f, Mathf.Max(0.6f, u.def.radius * 2), u.health / u.maxHealth);
                if (u.cargo != null) for (int k = 0; k < u.cargo.Count; k++) bars.Quad(u.x - 0.3f + k * 0.2f, u.y - u.def.radius - 0.1f, 0.14f, 0.14f, Art.C(u.cargo[k].def.color), 45);
            }

            // 지뢰: 어두운 마름모 안에 지뢰 색의 점 (아직 터질 수 없는 동안은 흐리다)
            foreach (var mn in sim.mines)
            {
                var mc = Art.C(mn.def.color);
                if (mn.arm > 0) mc.a = 0.45f;
                bars.Quad(mn.x, mn.y, 0.34f, 0.34f, new Color(0.08f, 0.08f, 0.1f, mc.a), 45);
                bars.Quad(mn.x, mn.y, 0.16f, 0.16f, mc, 45);
            }

            // 탄
            foreach (var bl in sim.bullets)
            {
                var col = Art.C(bl.def.color);
                float ang = Mathf.Atan2(bl.vy, bl.vx) * Mathf.Rad2Deg;
                if (bl.def.artillery) bars.Quad(bl.x, bl.y, bl.def.size, bl.def.size, col, 45);
                else bars.Quad(bl.x, bl.y, bl.def.size * 2.2f, bl.def.size * 0.6f, col, ang);
            }

            bars.End();

            // 이펙트
            foreach (var f in sim.fx) SpawnFx(f);
            sim.fx.Clear();
            for (int i = fxLive.Count - 1; i >= 0; i--)
            {
                var f = fxLive[i];
                f.t += Time.deltaTime;
                float k = f.t / f.life;
                if (k >= 1) { f.sr.enabled = false; fxFree.Push(f.sr); fxLive.RemoveAt(i); continue; }
                float r = Mathf.Lerp(f.r0, f.r1, 1 - (1 - k) * (1 - k));
                f.sr.transform.localScale = Vector3.one * (r * 2 / f.sr.sprite.bounds.size.x);
                f.sr.color = new Color(f.col.r, f.col.g, f.col.b, f.col.a * (1 - k));
                fxLive[i] = f;
            }

            float pulse = 1.1f + 0.25f * Mathf.Sin(time * 3);
            foreach (var m in spawnMarks) m.localScale = Vector3.one * pulse;
        }

        void HealthBar(float x, float y, float w, float f)
        {
            f = Mathf.Clamp01(f);
            bars.Quad(x, y, w + 0.06f, 0.14f, new Color(0, 0, 0, 0.75f));
            bars.Quad(x - w * 0.5f * (1 - f), y, w * f, 0.08f, Color.Lerp(new Color(0.95f, 0.25f, 0.25f), new Color(0.35f, 0.9f, 0.45f), f));
        }

        void SpawnFx(Fx f)
        {
            if (fxLive.Count > 200) return;
            bool ring = f.kind == FxKind.Explode || f.kind == FxKind.Spawn || f.kind == FxKind.BuildDestroyed;
            var sr = fxFree.Count > 0 ? fxFree.Pop() : NewSprite("Fx", null, OrderFx);
            sr.enabled = true;
            sr.sprite = Art.Get(ring ? "fx_ring" : "fx_circle");
            sr.transform.position = new Vector3(f.x, f.y);
            var col = Color.Lerp(Art.C(f.color), Color.white, f.kind == FxKind.EnemyDeath ? 0.1f : 0.5f);
            var it = new FxItem { sr = sr, col = col, life = 0.25f, r0 = f.r * 0.4f, r1 = f.r };
            switch (f.kind)
            {
                case FxKind.Explode: it.life = 0.4f; it.r0 = f.r * 0.2f; break;
                case FxKind.EnemyDeath: it.life = 0.35f; break;
                case FxKind.BuildDestroyed: it.life = 0.5f; it.r1 = f.r * 1.5f; break;
                case FxKind.Shoot: it.life = 0.08f; break;
                case FxKind.Hit: it.life = 0.12f; break;
                case FxKind.Spawn: it.life = 0.4f; it.r0 = f.r * 1.5f; it.r1 = f.r * 0.3f; break;
            }
            fxLive.Add(it);
            if (f.kind == FxKind.Explode && fxLive.Count < 200)
            {
                var sr2 = fxFree.Count > 0 ? fxFree.Pop() : NewSprite("Fx", null, OrderFx);
                sr2.enabled = true; sr2.sprite = Art.Get("fx_circle");
                sr2.transform.position = sr.transform.position;
                fxLive.Add(new FxItem { sr = sr2, col = col, life = 0.3f, r0 = f.r * 0.3f, r1 = f.r * 0.9f });
            }
        }
    }
}
