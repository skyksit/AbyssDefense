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

        public QuadBatch(Transform parent, string name, int order, Material material = null)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material != null ? material : Art.Mat;
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

        public void Tri(float x1, float y1, float x2, float y2, float x3, float y3, Color c1, Color c2, Color c3)
        {
            int n = verts.Count;
            verts.Add(new Vector3(x1, y1)); verts.Add(new Vector3(x2, y2)); verts.Add(new Vector3(x3, y3));
            cols.Add(c1); cols.Add(c2); cols.Add(c3);
            tris.Add(n); tris.Add(n + 1); tris.Add(n + 2);
        }

        public void Line(float x1, float y1, float x2, float y2, float w, Color col)
        {
            float dx = x2 - x1, dy = y2 - y1, len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.0001f) return;
            Quad((x1 + x2) * 0.5f, (y1 + y2) * 0.5f, len, w, col, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
        }

        /// 원판. 가운데와 가장자리의 색을 달리하면 부드럽게 번지는 빛이 된다
        public void Disc(float cx, float cy, float r, Color center, Color edge, int segs = 12)
        {
            int n = verts.Count;
            Color32 ce = edge;
            verts.Add(new Vector3(cx, cy)); cols.Add(center);
            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2 / segs;
                verts.Add(new Vector3(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r)); cols.Add(ce);
            }
            for (int i = 0; i < segs; i++) { tris.Add(n); tris.Add(n + 1 + i); tris.Add(n + 1 + (i + 1) % segs); }
        }

        public void Ring(float cx, float cy, float r, float th, Color col, int segs = 20)
        {
            int n = verts.Count;
            Color32 c32 = col;
            float r0 = Mathf.Max(0, r - th * 0.5f), r1 = r + th * 0.5f;
            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2 / segs, c = Mathf.Cos(a), sn = Mathf.Sin(a);
                verts.Add(new Vector3(cx + c * r0, cy + sn * r0)); verts.Add(new Vector3(cx + c * r1, cy + sn * r1));
                cols.Add(c32); cols.Add(c32);
            }
            for (int i = 0; i < segs; i++)
            {
                int a0 = n + i * 2, a1 = n + (i + 1) % segs * 2;
                tris.Add(a0); tris.Add(a0 + 1); tris.Add(a1 + 1); tris.Add(a0); tris.Add(a1 + 1); tris.Add(a1);
            }
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
        // 아래에서 위로: 바닥 → 액체 → 그을음 → 바닥 위의 것(건물 그림자·폐허·지뢰) → 건물 → 포신
        //   → 지상 그림자 → 지상의 적·유닛 → 비행 그림자 → 비행하는 적·유닛 → 탄 → 이펙트 → 막대
        const int OrderFloor = -20, OrderLiquid = -19, OrderScorch = -18, OrderGround = -15, OrderBlock = -10, OrderBlockTop = -8,
            OrderGroundShadow = 0, OrderEnemy = 2, OrderAirShadow = 8, OrderAir = 10, OrderBullet = 14, OrderFx = 16, OrderBars = 20;

        // 빛은 왼쪽 위에서 온다: 그림자는 오른쪽 아래로 떨어지고, 높이 뜬 것일수록 멀리 떨어진다
        static readonly Vector3 ShadowDir = new Vector3(0.7f, -0.7f);
        const float GroundShadowDist = 0.09f, AirShadowDist = 0.5f, BlockShadowDist = 0.13f;

        sealed class BView { public GameObject go; public SpriteRenderer sr, top, topShadow, aura; }
        // 이펙트는 그림 없이 도형(선·원판·고리)으로 매 프레임 그린다. 모양은 종류와 씨앗값에서 나오므로 상태는 흐른 시간뿐이다
        enum Vk : byte { Explode, Hit, Shoot, Death, Wreck, Slash, Spawn, Bolt, Smoke, Ember, Drip }
        struct VFx { public Vk kind; public float x, y, r, x2, y2, t, life; public Color col; public int seed; }
        const int MaxFx = 260;
        readonly List<VFx> vfx = new List<VFx>();
        int fxSeed;
        struct Scorch { public SpriteRenderer sr; public float t; }
        const float ScorchLife = 25f;
        const int MaxScorch = 50;
        readonly List<Scorch> scorches = new List<Scorch>();
        float shake;

        /// 이번 프레임에 카메라를 비켜 놓을 양 (PlayController 가 더했다가 다음 프레임에 뺀다)
        public Vector2 ShakeOffset() => shake > 0 ? Random.insideUnitCircle * shake : Vector2.zero;

        /// 큰 폭발·건물 파괴·보스의 죽음은 화면을 흔든다. 화면에서 멀수록 약하고, 여럿이 겹치면 가장 센 것만 남는다
        void AddShake(float amp, float x, float y)
        {
            var c = Camera.main;
            if (c == null) return;
            float d = Vector2.Distance(c.transform.position, new Vector2(x, y));
            float k = Mathf.Clamp01(1f - d / (c.orthographicSize * 2.4f));
            shake = Mathf.Max(shake, amp * k);
        }

        /// 폭발과 부서진 건물이 바닥에 남기는 그을음. 천천히 옅어지고, 너무 많으면 가장 오래된 것을 다시 쓴다
        void AddScorch(float x, float y, float r)
        {
            Scorch s;
            if (scorches.Count >= MaxScorch) { s = scorches[0]; scorches.RemoveAt(0); }
            else s = new Scorch { sr = NewSprite("Scorch", Art.Get("fx_circle"), OrderScorch) };
            s.t = 0;
            s.sr.enabled = true;
            s.sr.transform.position = new Vector3(x, y);
            s.sr.transform.localScale = Vector3.one * (r * 2.2f / s.sr.sprite.bounds.size.x);
            scorches.Add(s);
        }

        Sim.Sim sim;
        readonly Dictionary<Building, BView> views = new Dictionary<Building, BView>();
        readonly List<SpriteRenderer> enemyPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> unitPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> enemyShadows = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> unitShadows = new List<SpriteRenderer>();
        readonly List<Transform> spawnMarks = new List<Transform>();
        QuadBatch bars, ground, shots, fxSoft, fxAdd;

        public void Init(Sim.Sim s)
        {
            sim = s;
            BuildTerrain();
            ground = new QuadBatch(transform, "Ground", OrderGround);
            shots = new QuadBatch(transform, "Bullets", OrderBullet);
            fxSoft = new QuadBatch(transform, "FxSoft", OrderFx - 1);
            fxAdd = new QuadBatch(transform, "FxGlow", OrderFx, Art.AddMat);
            // 탄과 빛나는 이펙트는 빛 번짐에도 들어간다
            shots.go.layer = Glow.Layer;
            fxAdd.go.layer = Glow.Layer;
            Glow.Set(Camera.main, true);
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

        void OnDestroy() => Glow.Set(Camera.main, false);

        SpriteRenderer NewSprite(string name, Sprite sprite, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        /// 그림자: 본체와 같은 그림을 검게 칠해 비켜 놓는다. 비행체는 멀리 떨어뜨려 떠 있는 높이를 보인다
        void SyncShadow(SpriteRenderer sh, SpriteRenderer body, bool flying)
        {
            if (!sh.enabled) sh.enabled = true;
            sh.sprite = body.sprite;
            sh.sortingOrder = flying ? OrderAirShadow : OrderGroundShadow;
            sh.color = new Color(0, 0, 0, flying ? 0.24f : 0.32f);
            var tr = body.transform;
            sh.transform.SetPositionAndRotation(tr.position + ShadowDir * (flying ? AirShadowDist : GroundShadowDist), tr.rotation);
            sh.transform.localScale = tr.localScale * (flying ? 0.92f : 1f);
        }

        // ───────── 지형: 바닥·벽은 색 사각형 메시 하나, 물·용암·산성은 일렁이는 셰이더를 쓰는 메시 하나 ─────────

        static Color Mul(Color c, float m) => new Color(c.r * m, c.g * m, c.b * m, 1f);

        void BuildTerrain()
        {
            var w = sim.world;
            var floor = Art.C(sim.abyss.floorColor);
            var wall = Art.C(sim.abyss.wallColor);
            bool lava = sim.rules.hazard == Hazard.Lava;
            var verts = new List<Vector3>(); var cols = new List<Color32>(); var tris = new List<int>();
            var lverts = new List<Vector3>(); var lcols = new List<Color32>(); var luvs = new List<Vector2>(); var ltris = new List<int>();
            var glowTiles = new List<Vector2>();

            // 네 꼭짓점의 색이 다른 사각형 (c0 왼쪽 아래, c1 오른쪽 아래, c2 오른쪽 위, c3 왼쪽 위)
            void G(float x, float y, float sx, float sy, Color c0, Color c1, Color c2, Color c3)
            {
                int n = verts.Count;
                verts.Add(new Vector3(x, y)); verts.Add(new Vector3(x + sx, y));
                verts.Add(new Vector3(x + sx, y + sy)); verts.Add(new Vector3(x, y + sy));
                cols.Add(c0); cols.Add(c1); cols.Add(c2); cols.Add(c3);
                tris.Add(n); tris.Add(n + 1); tris.Add(n + 2); tris.Add(n); tris.Add(n + 2); tris.Add(n + 3);
            }
            void Q(float x, float y, float sx, float sy, Color c) => G(x, y, sx, sy, c, c, c, c);
            // 액체 메시의 사각형. kind: 0 물, 1 용암, 2 산성, 3 움직이지 않는 테두리
            void L(float x, float y, float sx, float sy, Color c, float kind)
            {
                int n = lverts.Count;
                lverts.Add(new Vector3(x, y)); lverts.Add(new Vector3(x + sx, y));
                lverts.Add(new Vector3(x + sx, y + sy)); lverts.Add(new Vector3(x, y + sy));
                Color32 c32 = c; var uv = new Vector2(kind, 0);
                for (int i = 0; i < 4; i++) { lcols.Add(c32); luvs.Add(uv); }
                ltris.Add(n); ltris.Add(n + 1); ltris.Add(n + 2); ltris.Add(n); ltris.Add(n + 2); ltris.Add(n + 3);
            }

            bool Rock(int x, int y) => !w.In(x, y) || (w.tiles[w.Idx(x, y)].wall && !w.tiles[w.Idx(x, y)].water);
            bool Wet(int x, int y) => w.In(x, y) && (w.tiles[w.Idx(x, y)].water || w.tiles[w.Idx(x, y)].hazard);

            // 벽의 깊이: 가장 가까운 바닥·호수에서 몇 칸 들어갔는가 (깊을수록 어둡다)
            var depth = new int[w.W * w.H];
            var queue = new Queue<int>();
            for (int i = 0; i < depth.Length; i++)
            {
                bool rock = w.tiles[i].wall && !w.tiles[i].water;
                depth[i] = rock ? -1 : 0;
                if (!rock) queue.Enqueue(i);
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), cx = i % w.W, cy = i / w.W;
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = cx + ox, ny = cy + oy;
                        if (!w.In(nx, ny) || depth[w.Idx(nx, ny)] >= 0) continue;
                        depth[w.Idx(nx, ny)] = depth[i] + 1;
                        queue.Enqueue(w.Idx(nx, ny));
                    }
            }

            var deepWall = Mul(wall, 0.7f);
            var shadow = new Color(0, 0, 0, 0.34f);
            var clear = new Color(0, 0, 0, 0);
            const float Sh = 0.38f;   // 벽이 바닥에 드리우는 그늘의 너비

            // 맵 바깥도 깊은 벽 색으로 넓게 깔아 가장자리가 비어 보이지 않게
            Q(-30, -30, w.W + 60, w.H + 60, deepWall);
            for (int y = 0; y < w.H; y++)
                for (int x = 0; x < w.W; x++)
                {
                    var t = w.tiles[w.Idx(x, y)];
                    float v = t.shade / 255f;
                    int h = unchecked(x * 73856093 ^ y * 19349663 ^ t.shade * 83492791) & 0x7fffffff;
                    if (t.water)
                    {
                        var deep = new Color(0.07f, 0.22f, 0.36f);
                        L(x, y, 1, 1, Color.Lerp(deep, Mul(deep, 1.35f), v), 0);
                        // 물가: 땅이나 벽과 맞닿은 변의 밝은 띠
                        var foam = new Color(0.62f, 0.82f, 0.92f, 0.55f);
                        if (!Wet(x - 1, y)) L(x, y, 0.07f, 1, foam, 3);
                        if (!Wet(x + 1, y)) L(x + 0.93f, y, 0.07f, 1, foam, 3);
                        if (!Wet(x, y - 1)) L(x, y, 1, 0.07f, foam, 3);
                        if (!Wet(x, y + 1)) L(x, y + 0.93f, 1, 0.07f, foam, 3);
                        continue;
                    }
                    if (t.wall)
                    {
                        // 바닥과 맞닿은 벽은 밝고(절벽 가장자리), 안쪽으로 들어갈수록 어두워진다
                        int d = depth[w.Idx(x, y)];
                        if (d > 3) continue;
                        var wc = d == 1 ? Color.Lerp(wall, floor, 0.45f + v * 0.1f) : d == 2 ? Color.Lerp(wall, floor, 0.16f + v * 0.06f) : Color.Lerp(deepWall, wall, 0.6f);
                        Q(x, y, 1, 1, wc);
                        if (d == 1)
                        {
                            // 빛을 받는 쪽(왼쪽·위)의 모서리는 밝게
                            var rim = Color.Lerp(wc, Color.white, 0.16f);
                            if (!Rock(x - 1, y)) Q(x, y, 0.08f, 1, rim);
                            if (!Rock(x, y + 1)) Q(x, y + 0.92f, 1, 0.08f, rim);
                            if ((h & 7) < 3) Q(x + 0.2f + 0.4f * v, y + 0.25f + ((h >> 4) & 7) * 0.05f, 0.22f, 0.1f, Mul(wc, 0.86f));
                        }
                        continue;
                    }
                    var fc = Color.Lerp(floor, Mul(floor, 1.25f), v);
                    Q(x, y, 1, 1, fc);
                    if (((x + y) & 1) == 0) Q(x + 0.04f, y + 0.04f, 0.92f, 0.92f, Mul(fc, 1.06f));
                    // 얼룩: 칸마다 자리와 크기가 다른 작은 조각
                    if ((h & 3) != 0)
                    {
                        float sx = 0.1f + ((h >> 2) & 7) * 0.09f, sy = 0.1f + ((h >> 5) & 7) * 0.09f, ss = 0.07f + ((h >> 8) & 3) * 0.03f;
                        Q(x + sx, y + sy, ss * (1 + ((h >> 10) & 1)), ss, Mul(fc, ((h >> 11) & 1) == 0 ? 0.86f : 1.13f));
                    }
                    if ((h & 15) == 5) Q(x + 0.55f, y + 0.6f, 0.26f, 0.05f, Mul(fc, 0.8f));
                    if (t.hazard)
                    {
                        // 위험 바닥: 용암 틈은 붉은 바탕, 산성 웅덩이는 탁한 연두 바탕. 흐르는 무늬는 셰이더가 얹는다
                        L(x, y, 1, 1, lava ? new Color(0.55f, 0.13f, 0.05f, 0.9f) : new Color(0.25f, 0.42f, 0.08f, 0.9f), lava ? 1 : 2);
                        glowTiles.Add(new Vector2(x, y));
                        var crust = lava ? new Color(0.16f, 0.05f, 0.03f, 0.75f) : new Color(0.1f, 0.18f, 0.04f, 0.7f);
                        if (!Wet(x - 1, y)) L(x, y, 0.09f, 1, crust, 3);
                        if (!Wet(x + 1, y)) L(x + 0.91f, y, 0.09f, 1, crust, 3);
                        if (!Wet(x, y - 1)) L(x, y, 1, 0.09f, crust, 3);
                        if (!Wet(x, y + 1)) L(x, y + 0.91f, 1, 0.09f, crust, 3);
                    }
                    // 벽의 그늘: 왼쪽·위에 벽이 있으면 그쪽 변에서 옅어지는 그늘
                    bool wl = Rock(x - 1, y), wu = Rock(x, y + 1);
                    if (wl) G(x, y, Sh, 1, shadow, clear, clear, shadow);
                    if (wu) G(x, y + 1 - Sh, 1, Sh, clear, clear, shadow, shadow);
                    if (!wl && !wu && Rock(x - 1, y + 1)) G(x, y + 1 - Sh, Sh, Sh, clear, clear, clear, shadow);
                }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            var go = new GameObject("Terrain");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Art.Mat;
            mr.sortingOrder = OrderFloor;

            if (lverts.Count == 0) return;
            var lmesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            lmesh.SetVertices(lverts); lmesh.SetColors(lcols); lmesh.SetUVs(0, luvs); lmesh.SetTriangles(ltris, 0);
            var lgo = new GameObject("Liquid");
            lgo.transform.SetParent(transform, false);
            lgo.AddComponent<MeshFilter>().sharedMesh = lmesh;
            var lmr = lgo.AddComponent<MeshRenderer>();
            lmr.sharedMaterial = Art.LiquidMat;
            lmr.sortingOrder = OrderLiquid;

            // 용암·산성의 은은한 빛: 화면에는 그리지 않고 빛 번짐에만 넣는다
            if (glowTiles.Count == 0) return;
            var gverts = new List<Vector3>(); var gcols = new List<Color32>(); var gtris = new List<int>();
            Color32 gc = lava ? new Color(1f, 0.42f, 0.1f, 0.5f) : new Color(0.55f, 0.95f, 0.25f, 0.28f);
            foreach (var p in glowTiles)
            {
                int n = gverts.Count;
                gverts.Add(new Vector3(p.x, p.y)); gverts.Add(new Vector3(p.x + 1, p.y));
                gverts.Add(new Vector3(p.x + 1, p.y + 1)); gverts.Add(new Vector3(p.x, p.y + 1));
                for (int i = 0; i < 4; i++) gcols.Add(gc);
                gtris.Add(n); gtris.Add(n + 1); gtris.Add(n + 2); gtris.Add(n); gtris.Add(n + 2); gtris.Add(n + 3);
            }
            var gmesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            gmesh.SetVertices(gverts); gmesh.SetColors(gcols); gmesh.SetTriangles(gtris, 0);
            var ggo = new GameObject("LiquidGlow") { layer = Glow.OnlyLayer };
            ggo.transform.SetParent(transform, false);
            ggo.AddComponent<MeshFilter>().sharedMesh = gmesh;
            var gmr = ggo.AddComponent<MeshRenderer>();
            gmr.sharedMaterial = Art.AddMat;
            gmr.sortingOrder = OrderLiquid;
        }

        // ───────── 건물 ─────────

        void Add(Building b)
        {
            var v = new BView();
            v.sr = NewSprite(b.def.key, Art.Block(b.def), OrderBlock);
            v.go = v.sr.gameObject;
            v.go.transform.position = new Vector3(b.Cx, b.Cy);
            var top = Art.BlockTop(b.def);
            if (top != null)
            {
                v.topShadow = NewSprite("topShadow", top, OrderBlockTop - 1, v.go.transform);
                v.topShadow.transform.localPosition = ShadowDir * GroundShadowDist;
                v.top = NewSprite("top", top, OrderBlockTop, v.go.transform);
            }
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
            float dt = Time.deltaTime;
            bars.Begin(); ground.Begin(); shots.Begin(); fxSoft.Begin(); fxAdd.Begin();

            foreach (var kv in views)
            {
                var b = kv.Key; var v = kv.Value;
                var tint = b.hitFlash > 0 ? new Color(1f, 0.55f, 0.55f) : Color.white;
                // 건물 그림자 (짓는 중에는 지어진 만큼만 짙다)
                float bs = b.def.size;
                ground.Quad(b.Cx + ShadowDir.x * BlockShadowDist, b.Cy + ShadowDir.y * BlockShadowDist, bs - 0.04f, bs - 0.04f,
                    new Color(0, 0, 0, b.Built ? 0.38f : 0.38f * (0.3f + 0.5f * b.progress)));
                if (!b.Built)
                {
                    // 건설 터: 진행될수록 또렷해지고, 순서를 기다리거나 자원이 없어 멈춘 곳은 막대가 회색
                    tint.a = 0.3f + 0.5f * b.progress;
                    float w = b.def.size * 0.8f, y0 = b.Cy - b.def.size * 0.5f + 0.12f;
                    bars.Quad(b.Cx, y0, w + 0.06f, 0.16f, new Color(0, 0, 0, 0.75f));
                    bars.Quad(b.Cx - w * 0.5f * (1 - b.progress), y0, w * b.progress, 0.1f, b.constructing ? new Color(1f, 0.82f, 0.3f) : new Color(0.6f, 0.6f, 0.65f));
                }
                v.sr.color = tint;
                if (v.top != null) { v.top.color = tint; v.topShadow.color = new Color(0, 0, 0, 0.32f * tint.a); }
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
                if (v.top != null) v.topShadow.transform.rotation = v.top.transform.rotation;
                // 광원: 핵은 천천히 맥동하는 빛, 역장 발생기는 남은 역장만큼의 빛
                if (b.Built && (b.def.kind == BlockKind.Core || b.def.kind == BlockKind.EnemyCore))
                    fxAdd.Disc(b.Cx, b.Cy, b.def.size * 0.5f, A(Art.C(b.def.color), 0.3f + 0.08f * Mathf.Sin(time * 2.2f)), A(Art.C(b.def.color), 0), 16);
                else if (b.Built && b is SupportBuilding gs && b.def.shield > 0 && gs.shieldHp > 0)
                    fxAdd.Disc(b.Cx, b.Cy, b.def.size * 0.7f, A(Art.C(b.def.color), 0.35f * Mathf.Clamp01(gs.shieldHp / gs.MaxShield)), A(Art.C(b.def.color), 0), 12);
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
                ground.Quad(rx, ry, rs - 0.12f, rs - 0.12f, new Color(rc.r, rc.g, rc.b, 0.12f));
                ground.Quad(rx, ry + rs * 0.5f - 0.08f, rs - 0.12f, 0.06f, rc); ground.Quad(rx, ry - rs * 0.5f + 0.08f, rs - 0.12f, 0.06f, rc);
                ground.Quad(rx + rs * 0.5f - 0.08f, ry, 0.06f, rs - 0.12f, rc); ground.Quad(rx - rs * 0.5f + 0.08f, ry, 0.06f, rs - 0.12f, rc);
            }

            // 적
            var es = sim.enemies;
            while (enemyPool.Count < es.Count) { enemyPool.Add(NewSprite("Enemy", null, OrderEnemy)); enemyShadows.Add(NewSprite("EnemyShadow", null, OrderGroundShadow)); }
            for (int i = 0; i < enemyPool.Count; i++)
            {
                var sr = enemyPool[i];
                if (i >= es.Count) { if (sr.enabled) { sr.enabled = false; enemyShadows[i].enabled = false; } continue; }
                var e = es[i];
                sr.enabled = true;
                sr.sprite = Art.Get("enemy_" + e.def.key);
                sr.sortingOrder = e.def.flying ? OrderAir : OrderEnemy;
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
                SyncShadow(enemyShadows[i], sr, e.def.flying);
                // 상태 이상: 불타면 불티가 오르고, 산성·젖음은 방울이 떨어진다
                if (e.burnTicks > 0) { if (Random.value < dt * 9) AddFx(Vk.Ember, e.x + Random.Range(-er, er), e.y + Random.Range(-er, er), 1, new Color(1f, 0.6f, 0.2f), 0.45f); }
                else if (e.acidTicks > 0) { if (Random.value < dt * 5) AddFx(Vk.Drip, e.x + Random.Range(-er, er), e.y + Random.Range(-er, er), 1, new Color(0.75f, 0.45f, 1f), 0.4f); }
                else if (e.wetTicks > 0) { if (Random.value < dt * 5) AddFx(Vk.Drip, e.x + Random.Range(-er, er), e.y + Random.Range(-er, er), 1, new Color(0.5f, 0.8f, 1f), 0.4f); }
                if (e.health < e.maxHealth) HealthBar(e.x, e.y + er + 0.14f, Mathf.Max(0.6f, e.def.radius * 2), e.health / e.maxHealth);
            }

            // 아군 유닛
            var us = sim.units;
            var cmd = sim.commander != null && !sim.commander.dead ? sim.commander : null;
            int unitCount = us.Count + (cmd != null ? 1 : 0);
            while (unitPool.Count < unitCount) { unitPool.Add(NewSprite("Unit", null, OrderEnemy + 1)); unitShadows.Add(NewSprite("UnitShadow", null, OrderGroundShadow)); }
            for (int i = 0; i < unitPool.Count; i++)
            {
                var sr = unitPool[i];
                if (i >= unitCount) { if (sr.enabled) { sr.enabled = false; unitShadows[i].enabled = false; } continue; }
                var u = i < us.Count ? us[i] : cmd;
                if (u.carrier != null) { if (sr.enabled) { sr.enabled = false; unitShadows[i].enabled = false; } continue; }   // 수송기에 실려 있다
                sr.enabled = true;
                sr.sprite = Art.Get("unit_" + u.def.key);
                sr.sortingOrder = u.def.flying ? OrderAir + 1 : OrderEnemy + 1;
                sr.color = u.hitFlash > 0 ? new Color(1f, 0.6f, 0.6f) : u.barrier > 0 ? new Color(0.75f, 0.93f, 1f) : Color.white;   // 보호막이 남아 있으면 하늘색
                sr.transform.position = new Vector3(u.x, u.y);
                sr.transform.rotation = Quaternion.Euler(0, 0, u.aim * Mathf.Rad2Deg);
                SyncShadow(unitShadows[i], sr, u.def.flying);
                if (u.health < u.maxHealth) HealthBar(u.x, u.y + u.def.radius + 0.14f, Mathf.Max(0.6f, u.def.radius * 2), u.health / u.maxHealth);
                if (u.cargo != null) for (int k = 0; k < u.cargo.Count; k++) bars.Quad(u.x - 0.3f + k * 0.2f, u.y - u.def.radius - 0.1f, 0.14f, 0.14f, Art.C(u.cargo[k].def.color), 45);
            }

            // 지뢰: 어두운 마름모 안에 지뢰 색의 점 (아직 터질 수 없는 동안은 흐리다)
            foreach (var mn in sim.mines)
            {
                var mc = Art.C(mn.def.color);
                if (mn.arm > 0) mc.a = 0.45f;
                ground.Quad(mn.x, mn.y, 0.34f, 0.34f, new Color(0.08f, 0.08f, 0.1f, mc.a), 45);
                ground.Quad(mn.x, mn.y, 0.16f, 0.16f, mc, 45);
            }

            // 탄
            int flick = (int)(time * 30);
            for (int i = 0; i < sim.bullets.Count; i++)
            {
                var bl = sim.bullets[i];
                var d = bl.def;
                var col = Art.C(d.color);
                float sp = Mathf.Sqrt(bl.vx * bl.vx + bl.vy * bl.vy);
                float ux = sp > 0 ? bl.vx / sp : 1, uy = sp > 0 ? bl.vy / sp : 0;
                if (d.burn > 0 && !d.artillery)
                {
                    // 화염: 일렁이는 불덩이
                    float fl = 0.8f + 0.45f * Rnd(i, flick);
                    fxAdd.Disc(bl.x, bl.y, d.size * 2.4f * fl, A(col, 0.7f), A(col, 0), 10);
                    fxAdd.Disc(bl.x, bl.y, d.size * 1.1f, new Color(1f, 0.95f, 0.7f, 0.8f), A(col, 0), 8);
                    continue;
                }
                if (d.artillery)
                {
                    // 곡사탄: 둥근 탄
                    shots.Disc(bl.x, bl.y, d.size * 0.62f, Color.Lerp(col, Color.white, 0.55f), col, 10);
                    fxAdd.Disc(bl.x, bl.y, d.size * 1.5f, A(col, 0.35f), A(col, 0), 10);
                    continue;
                }
                // 잔상: 날아온 쪽으로 가늘어지며 사라지는 꼬리
                float tail = Mathf.Min(1.5f, sp * (d.homing > 0 ? 7f : 4f)), hw = d.size * 0.3f;
                fxAdd.Tri(bl.x - uy * hw, bl.y + ux * hw, bl.x + uy * hw, bl.y - ux * hw, bl.x - ux * tail, bl.y - uy * tail, A(col, 0.6f), A(col, 0.6f), A(col, 0));
                float ang = Mathf.Atan2(bl.vy, bl.vx) * Mathf.Rad2Deg;
                if (d.wetTicks > 0 || d.slow > 0 || d.acid > 0)
                {
                    // 점액·산성: 둥근 방울
                    shots.Disc(bl.x, bl.y, d.size * 0.85f, col, Mul(col, 0.7f), 10);
                    shots.Disc(bl.x - d.size * 0.2f, bl.y + d.size * 0.25f, d.size * 0.28f, Color.Lerp(col, Color.white, 0.6f), col, 6);
                }
                else
                {
                    shots.Quad(bl.x, bl.y, d.size * 2.2f, d.size * 0.6f, col, ang);
                    shots.Quad(bl.x + ux * d.size * 0.3f, bl.y + uy * d.size * 0.3f, d.size * 1.2f, d.size * 0.28f, Color.Lerp(col, Color.white, d.chain > 0 ? 0.95f : 0.6f), ang);
                }
            }

            // 이펙트
            foreach (var f in sim.fx) SpawnFx(f);
            sim.fx.Clear();
            for (int i = vfx.Count - 1; i >= 0; i--)
            {
                var f = vfx[i];
                f.t += dt;
                if (f.t >= f.life) { vfx[i] = vfx[vfx.Count - 1]; vfx.RemoveAt(vfx.Count - 1); continue; }
                vfx[i] = f;
                DrawFx(in f, f.t / f.life);
            }

            // 눈보라: 바람에 비껴 흐르는 눈 줄기 (화면에 보이는 범위에만)
            if (sim.Blizzard)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    float hh = cam.orthographicSize + 1, hwd = hh * cam.aspect + 1;
                    Vector2 cp = cam.transform.position;
                    for (int i = 0; i < 90; i++)
                    {
                        float spd = 0.5f + Rnd(7, i);
                        float sx = cp.x - hwd + Mathf.Repeat(Rnd(11, i) + time * 0.4f * spd, 1f) * hwd * 2;
                        float sy = cp.y - hh + Mathf.Repeat(Rnd(13, i) - time * 0.14f * spd, 1f) * hh * 2;
                        fxAdd.Line(sx, sy, sx + 0.55f * spd, sy - 0.19f * spd, 0.035f, new Color(1, 1, 1, 0.16f + 0.2f * Rnd(17, i)));
                    }
                }
            }

            bars.End(); ground.End(); shots.End(); fxSoft.End(); fxAdd.End();

            shake *= Mathf.Exp(-Time.unscaledDeltaTime * 9f);
            if (shake < 0.004f) shake = 0;
            for (int i = scorches.Count - 1; i >= 0; i--)
            {
                var s = scorches[i];
                s.t += Time.deltaTime;
                if (s.t >= ScorchLife) { s.sr.enabled = false; Destroy(s.sr.gameObject); scorches.RemoveAt(i); continue; }
                s.sr.color = new Color(0, 0, 0, 0.5f * (1 - s.t / ScorchLife));
                scorches[i] = s;
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

        static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// 씨앗값과 번호에서 나오는 0~1 의 값. 같은 이펙트는 프레임이 바뀌어도 같은 모양을 유지한다
        static float Rnd(int seed, int i)
        {
            uint h = unchecked((uint)(seed * 374761393 + i * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }

        void AddFx(Vk kind, float x, float y, float r, Color col, float life, float x2 = 0, float y2 = 0)
        {
            if (vfx.Count >= MaxFx) return;
            vfx.Add(new VFx { kind = kind, x = x, y = y, r = r, x2 = x2, y2 = y2, col = col, life = life, seed = ++fxSeed });
        }

        void SpawnFx(Fx f)
        {
            var col = Art.C(f.color);
            var hot = Color.Lerp(col, Color.white, 0.5f);
            var smoke = new Color(0.16f, 0.16f, 0.18f);
            bool crowded = vfx.Count > MaxFx * 0.6f;   // 너무 많으면 자잘한 것부터 뺀다
            switch (f.kind)
            {
                case FxKind.Hit: if (!crowded) AddFx(Vk.Hit, f.x, f.y, f.r, hot, 0.16f); break;
                case FxKind.Shoot: if (!crowded) AddFx(Vk.Shoot, f.x, f.y, f.r, hot, 0.09f, f.x2, f.y2); break;
                case FxKind.Explode:
                    AddFx(Vk.Explode, f.x, f.y, f.r, hot, 0.42f);
                    if (!crowded) AddFx(Vk.Smoke, f.x, f.y, f.r, smoke, 0.9f);
                    break;
                case FxKind.EnemyDeath:
                    AddFx(Vk.Death, f.x, f.y, f.r, col, 0.4f);
                    if (f.r >= 1.2f) { AddFx(Vk.Explode, f.x, f.y, f.r * 1.3f, hot, 0.6f); AddFx(Vk.Smoke, f.x, f.y, f.r * 1.2f, smoke, 1.3f); }
                    break;
                case FxKind.BuildDestroyed:
                    AddFx(Vk.Wreck, f.x, f.y, f.r, col, 0.6f);
                    AddFx(Vk.Smoke, f.x, f.y, f.r * 1.2f, smoke, 1.1f);
                    break;
                case FxKind.EnemyAttack: AddFx(Vk.Slash, f.x, f.y, f.r, hot, 0.18f); break;
                case FxKind.Spawn: AddFx(Vk.Spawn, f.x, f.y, f.r, hot, 0.4f); break;
                case FxKind.Chain: AddFx(Vk.Bolt, f.x, f.y, f.r, hot, 0.14f, f.x2, f.y2); break;
            }
            if (f.kind == FxKind.Explode && f.r >= 0.9f) { AddShake(Mathf.Min(0.2f, 0.04f + f.r * 0.05f), f.x, f.y); AddScorch(f.x, f.y, f.r * 0.8f); }
            else if (f.kind == FxKind.BuildDestroyed) { AddShake(0.12f + f.r * 0.06f, f.x, f.y); AddScorch(f.x, f.y, f.r); }
            else if (f.kind == FxKind.EnemyDeath && f.r >= 1.2f) { AddShake(0.3f, f.x, f.y); AddScorch(f.x, f.y, f.r * 0.7f); }   // 보스
        }

        /// 가운데에서 ang 쪽으로 d 만큼 나간 곳에 머리를 둔, 길이 len 의 불꽃 선
        void Spark(float x, float y, float ang, float d, float len, float w, Color col)
        {
            float c = Mathf.Cos(ang), sn = Mathf.Sin(ang), d0 = Mathf.Max(0, d - len);
            fxAdd.Line(x + c * d0, y + sn * d0, x + c * d, y + sn * d, w, col);
        }

        /// 파편: 사방으로 튀며 도는 작은 조각
        void Chunks(in VFx f, int n, float reach, float size, float e, float fade, Color a, Color b)
        {
            for (int i = 0; i < n; i++)
            {
                float ang = Rnd(f.seed, i) * Mathf.PI * 2, d = reach * (0.25f + 0.9f * Rnd(f.seed, i + 40)) * e;
                float sz = size * (0.6f + 0.8f * Rnd(f.seed, i + 80)) * (0.4f + 0.6f * fade);
                fxSoft.Quad(f.x + Mathf.Cos(ang) * d, f.y + Mathf.Sin(ang) * d, sz, sz, A((i & 1) == 0 ? a : b, Mathf.Min(1, fade * 1.6f)), Rnd(f.seed, i + 120) * 360 + f.t * 420);
            }
        }

        void DrawFx(in VFx f, float k)
        {
            float e = 1 - (1 - k) * (1 - k), fade = 1 - k, r = f.r;
            var col = f.col;
            switch (f.kind)
            {
                case Vk.Explode:
                {
                    fxAdd.Disc(f.x, f.y, r * (0.35f + 0.75f * e), A(Color.Lerp(col, Color.white, 0.5f), 0.85f * fade * fade), A(col, 0), 14);
                    fxAdd.Ring(f.x, f.y, r * (0.25f + 0.85f * e), (0.03f + 0.1f * fade) * Mathf.Min(1.5f, r + 0.3f), A(col, fade), 22);
                    int n = Mathf.Clamp((int)(5 + r * 6), 6, 16);
                    for (int i = 0; i < n; i++)
                        Spark(f.x, f.y, Rnd(f.seed, i) * Mathf.PI * 2, r * (0.35f + 0.95f * Rnd(f.seed, i + 40)) * e, r * 0.4f * fade + 0.05f, 0.05f,
                            A(Color.Lerp(col, Color.white, Rnd(f.seed, i + 80)), fade));
                    break;
                }
                case Vk.Hit:
                {
                    fxAdd.Disc(f.x, f.y, r * (0.5f + 0.6f * e), A(Color.white, 0.7f * fade), A(col, 0), 8);
                    for (int i = 0; i < 4; i++)
                        Spark(f.x, f.y, Rnd(f.seed, i) * Mathf.PI * 2, r * (0.5f + 1.6f * Rnd(f.seed, i + 40)) * e, r * 0.7f * fade + 0.03f, 0.04f, A(col, fade));
                    break;
                }
                case Vk.Shoot:
                {
                    fxAdd.Disc(f.x, f.y, r * (0.8f + 0.5f * k), A(Color.white, 0.8f * fade), A(col, 0), 8);
                    float dx = f.x2 - f.x, dy = f.y2 - f.y, len = Mathf.Sqrt(dx * dx + dy * dy);
                    if (len > 0.001f)
                    {
                        // 쏜 쪽으로 뻗는 불꽃
                        dx /= len; dy /= len;
                        float w = r * 0.55f, reach = r * (2.2f + 1.5f * k);
                        fxAdd.Tri(f.x - dy * w, f.y + dx * w, f.x + dy * w, f.y - dx * w, f.x + dx * reach, f.y + dy * reach, A(col, fade), A(col, fade), A(col, 0));
                    }
                    break;
                }
                case Vk.Death:
                {
                    fxAdd.Disc(f.x, f.y, r * (0.4f + 0.5f * e), A(Color.Lerp(col, Color.white, 0.3f), 0.6f * fade), A(col, 0), 10);
                    Chunks(in f, Mathf.Clamp((int)(4 + r * 5), 5, 12), r, 0.13f * Mathf.Max(0.7f, r), e, fade, col, Mul(col, 0.5f));
                    break;
                }
                case Vk.Wreck:
                {
                    fxAdd.Disc(f.x, f.y, r * (0.5f + 0.9f * e), A(Color.white, 0.7f * fade * fade), A(col, 0), 14);
                    fxAdd.Ring(f.x, f.y, r * (0.4f + 1.1f * e), 0.14f * fade, A(Color.Lerp(col, Color.white, 0.5f), fade), 24);
                    Chunks(in f, 12, r * 1.5f, 0.2f, e, fade, col, new Color(0.2f, 0.21f, 0.24f));
                    for (int i = 0; i < 8; i++)
                        Spark(f.x, f.y, Rnd(f.seed, i + 200) * Mathf.PI * 2, r * (0.6f + 1.2f * Rnd(f.seed, i + 240)) * e, 0.35f * fade + 0.05f, 0.05f, A(new Color(1f, 0.8f, 0.45f), fade));
                    break;
                }
                case Vk.Slash:
                {
                    // 적의 근접 공격: 엇갈린 두 줄의 베기
                    float a0 = Rnd(f.seed, 0) * 180, len = r * (2.4f + 2.4f * e);
                    fxAdd.Quad(f.x, f.y, len, 0.02f + 0.07f * fade, A(col, fade), a0);
                    fxAdd.Quad(f.x, f.y, len * 0.8f, 0.02f + 0.05f * fade, A(Color.white, fade), a0 + 65);
                    break;
                }
                case Vk.Spawn:
                    fxAdd.Ring(f.x, f.y, r * Mathf.Lerp(1.5f, 0.3f, e), 0.05f, A(col, 0.8f * fade), r > 3 ? 44 : 22);
                    break;
                case Vk.Bolt:
                {
                    // 번개: 프레임마다 다시 꺾이는 선
                    float dx = f.x2 - f.x, dy = f.y2 - f.y, dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < 0.01f) break;
                    float px = -dy / dist, py = dx / dist;
                    int n = Mathf.Clamp((int)(dist / 0.3f) + 2, 3, 14), frame = (int)(f.t * 45);
                    float lx = f.x, ly = f.y;
                    for (int i = 1; i <= n; i++)
                    {
                        float tt = i / (float)n, off = i == n ? 0 : (Rnd(f.seed + frame * 31, i) - 0.5f) * 0.4f;
                        float cx = f.x + dx * tt + px * off, cy = f.y + dy * tt + py * off;
                        fxAdd.Line(lx, ly, cx, cy, 0.16f, A(col, 0.35f * fade));
                        fxAdd.Line(lx, ly, cx, cy, 0.05f, A(Color.white, fade));
                        lx = cx; ly = cy;
                    }
                    fxAdd.Disc(f.x2, f.y2, 0.28f, A(Color.white, 0.6f * fade), A(col, 0), 8);
                    break;
                }
                case Vk.Smoke:
                {
                    int n = Mathf.Clamp((int)(3 + r * 3), 3, 7);
                    float a = 0.36f * fade * Mathf.Min(1, k / 0.12f);
                    for (int i = 0; i < n; i++)
                    {
                        float ang = Rnd(f.seed, i) * Mathf.PI * 2, d = r * 0.6f * Rnd(f.seed, i + 40) * (0.4f + 0.6f * e);
                        float rad = r * (0.18f + 0.2f * Rnd(f.seed, i + 80)) * (0.6f + 0.8f * k);
                        fxSoft.Disc(f.x + Mathf.Cos(ang) * d, f.y + Mathf.Sin(ang) * d + 0.3f * k, rad, A(col, a), A(col, a * 0.15f), 10);
                    }
                    break;
                }
                case Vk.Ember:
                    fxAdd.Disc(f.x, f.y + 0.5f * k, 0.03f + 0.07f * fade, A(Color.Lerp(col, Color.white, 0.4f), fade), A(col, 0), 6);
                    break;
                case Vk.Drip:
                    fxSoft.Disc(f.x, f.y - 0.3f * k * k, 0.05f * (0.5f + 0.5f * fade), A(col, 0.9f * fade), A(col, 0.5f * fade), 6);
                    break;
            }
        }
    }
}
