using System;
using System.Collections.Generic;

namespace AbyssDefense.Sim
{
    /// 시드가 같으면 어디서나 같은 수열을 내는 난수
    public sealed class Rng
    {
        ulong s;
        public Rng(long seed) { s = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x1234567UL; }
        public uint NextU()
        {
            s += 0x9E3779B97F4A7C15UL;
            ulong z = s;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return (uint)((z ^ (z >> 31)) >> 16);
        }
        public float Float() => (NextU() & 0xFFFFFF) / (float)0x1000000;
        public float Range(float a, float b) => a + (b - a) * Float();
        public int Range(int a, int b) => b <= a ? a : a + (int)(NextU() % (uint)(b - a + 1));   // 양끝 포함
        public bool Chance(float p) => Float() < p;
    }

    /// 격자 값 노이즈 (0~1)
    public static class Noise
    {
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        public static float Value(float x, float y, int seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed);
            float c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        public static float Fractal(float x, float y, int seed)
        {
            return Value(x, y, seed) * 0.6f + Value(x * 2.1f, y * 2.1f, seed + 17) * 0.3f + Value(x * 4.3f, y * 4.3f, seed + 31) * 0.1f;
        }
    }

    /// 지역 맵 생성: 노이즈 벽 → 다듬기 → 핵 방·출현 방 → 구불구불한 통로 → 닿지 않는 곳 메우기
    public static class MapGen
    {
        public static World Generate(RegionRules r, AbyssDef abyss)
        {
            int W = r.mapW, H = r.mapH;
            var w = new World(W, H);
            var rng = new Rng(r.seed);
            int ns = (int)(r.seed % 100000);

            // 1) 벽
            var wall = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool edge = x == 0 || y == 0 || x == W - 1 || y == H - 1;
                    wall[y * W + x] = edge || Noise.Fractal(x / 5.5f, y / 5.5f, ns) > 0.56f;
                }

            // 2) 셀룰러 다듬기
            for (int pass = 0; pass < 3; pass++)
            {
                var next = new bool[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        if (x == 0 || y == 0 || x == W - 1 || y == H - 1) { next[y * W + x] = true; continue; }
                        int n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                if ((dx != 0 || dy != 0) && wall[(y + dy) * W + x + dx]) n++;
                        next[y * W + x] = n > 4 || (n == 4 && wall[y * W + x]);
                    }
                wall = next;
            }

            // 3) 코어 방(아래)과 출현 방(위)
            int coreX = W / 2, coreY = 6;
            Carve(wall, W, H, coreX, coreY, 5.2f);
            var spawnPts = new List<int[]>();
            for (int i = 0; i < r.spawnCount; i++)
            {
                float t = r.spawnCount == 1 ? 0.5f : (i + 0.5f) / r.spawnCount;
                int sx = Clamp((int)(W * t) + rng.Range(-2, 2), 4, W - 5);
                if (r.spawnCount == 1) sx = Clamp(W / 2 + rng.Range(-W / 4, W / 4), 4, W - 5);
                int sy = H - 5;
                Carve(wall, W, H, sx, sy, 3.2f);
                spawnPts.Add(new[] { sx, sy });
            }

            // 4) 출현 지점 → 중간 지점 둘 → 코어 로 이어지는 통로 (연결 보장)
            foreach (var sp in spawnPts)
            {
                float px = sp[0], py = sp[1];
                float[] wx = { rng.Range(4f, W - 5f), rng.Range(4f, W - 5f), coreX };
                float[] wy = { H * 0.66f + rng.Range(-2f, 2f), H * 0.36f + rng.Range(-2f, 2f), coreY + 4 };
                float brush = rng.Range(1.5f, 2.3f);
                for (int k = 0; k < 3; k++)
                {
                    float len = (float)Math.Sqrt((wx[k] - px) * (wx[k] - px) + (wy[k] - py) * (wy[k] - py));
                    int steps = Math.Max(2, (int)(len * 2));
                    for (int s = 0; s <= steps; s++)
                    {
                        float t = s / (float)steps;
                        float wob = (Noise.Value(s * 0.15f, k * 7.3f, ns + 99) - 0.5f) * 3f * (float)Math.Sin(t * Math.PI);
                        float cx = px + (wx[k] - px) * t + wob;
                        float cy = py + (wy[k] - py) * t;
                        Carve(wall, W, H, cx, cy, brush);
                    }
                    px = wx[k]; py = wy[k];
                }
            }

            for (int i = 0; i < wall.Length; i++)
            {
                w.tiles[i].wall = wall[i];
                w.tiles[i].shade = (byte)(rng.NextU() & 0xFF);
            }

            // 5) 핵에서 닿지 않는 빈 칸은 벽으로 메운다
            foreach (var sp in spawnPts) w.spawns.Add(w.Idx(sp[0], sp[1]));
            FillUnreachable(w, coreX, coreY);

            // 6) 핵
            var core = (CoreBuilding)Building.Create(Content.Core);
            core.x = coreX - 1; core.y = coreY - 1;
            w.Add(core);
            return w;
        }

        static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;

        static void Carve(bool[] wall, int W, int H, float cx, float cy, float rad)
        {
            int r = (int)Math.Ceiling(rad);
            for (int y = (int)cy - r; y <= (int)cy + r; y++)
                for (int x = (int)cx - r; x <= (int)cx + r; x++)
                {
                    if (x < 1 || y < 1 || x >= W - 1 || y >= H - 1) continue;
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= rad * rad) wall[y * W + x] = false;
                }
        }

        static void FillUnreachable(World w, int sx, int sy)
        {
            var seen = new bool[w.W * w.H];
            var q = new Queue<int>();
            q.Enqueue(w.Idx(sx, sy)); seen[w.Idx(sx, sy)] = true;
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                int x = i % w.W, y = i / w.W;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + World.DX[d], ny = y + World.DY[d];
                    if (!w.In(nx, ny)) continue;
                    int ni = w.Idx(nx, ny);
                    if (seen[ni] || w.tiles[ni].wall) continue;
                    seen[ni] = true; q.Enqueue(ni);
                }
            }
            for (int i = 0; i < seen.Length; i++)
                if (!seen[i] && !w.tiles[i].wall) w.tiles[i].wall = true;
        }
    }
}
