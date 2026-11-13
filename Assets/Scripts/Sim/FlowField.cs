using System;

namespace AbyssDefense.Sim
{
    /// 코어에서 바깥으로 퍼뜨린 비용 지도. 지상 적은 비용이 낮아지는 쪽으로 걷는다.
    /// 벽은 지날 수 없고, 플레이어 건물은 '비싼 통과 칸' 이라 길이 다 막히면 약한 곳을 부수고 온다.
    public sealed class FlowField
    {
        readonly World w;
        public readonly float[] cost;
        readonly int[] next;
        readonly int[] heap;
        readonly float[] heapKey;
        int heapN;

        public FlowField(World world)
        {
            w = world;
            cost = new float[w.W * w.H];
            next = new int[w.W * w.H];
            heap = new int[w.W * w.H * 8 + 16];
            heapKey = new float[heap.Length];
        }

        /// 이 칸에서 다음으로 갈 칸 (없으면 -1)
        public int Next(int x, int y) => w.In(x, y) ? next[y * w.W + x] : -1;

        float EnterCost(int i)
        {
            var b = w.tiles[i].build;
            if (b == null || b is CoreBuilding || b.def.enemy) return 0;
            return 6 + Math.Min(b.health / 8f, 70f);
        }

        public void Compute()
        {
            int W = w.W, H = w.H;
            for (int i = 0; i < cost.Length; i++) { cost[i] = float.MaxValue; next[i] = -1; }
            heapN = 0;
            var core = w.core;
            if (core == null) return;
            for (int dy = 0; dy < core.def.size; dy++)
                for (int dx = 0; dx < core.def.size; dx++)
                {
                    int i = w.Idx(core.x + dx, core.y + dy);
                    cost[i] = 0; Push(i, 0);
                }

            while (heapN > 0)
            {
                float c; int cur = Pop(out c);
                if (c > cost[cur]) continue;
                int cx = cur % W, cy = cur / W;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        int ni = ny * W + nx;
                        if (w.tiles[ni].wall) continue;
                        float step = 1f;
                        if (dx != 0 && dy != 0)
                        {
                            // 대각선은 양옆 칸이 모두 비어 있을 때만 (모서리를 뚫고 가지 않게)
                            var s1 = w.tiles[cy * W + nx]; var s2 = w.tiles[ny * W + cx];
                            if (s1.wall || s2.wall || s1.build != null || s2.build != null) continue;
                            if (w.tiles[ni].build != null || w.tiles[cur].build != null) continue;
                            step = 1.414f;
                        }
                        // cur 는 코어에 더 가까운 칸. ni 에서 cur 로 들어가는 비용은 cur 의 건물 비용
                        float nc = c + step + EnterCost(cur);
                        if (nc < cost[ni]) { cost[ni] = nc; next[ni] = cur; Push(ni, nc); }
                    }
            }
        }

        void Push(int v, float k)
        {
            int i = heapN++;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (heapKey[p] <= k) break;
                heap[i] = heap[p]; heapKey[i] = heapKey[p]; i = p;
            }
            heap[i] = v; heapKey[i] = k;
        }

        int Pop(out float key)
        {
            int top = heap[0]; key = heapKey[0];
            heapN--;
            int v = heap[heapN]; float k = heapKey[heapN];
            int i = 0;
            while (true)
            {
                int c = i * 2 + 1;
                if (c >= heapN) break;
                if (c + 1 < heapN && heapKey[c + 1] < heapKey[c]) c++;
                if (heapKey[c] >= k) break;
                heap[i] = heap[c]; heapKey[i] = heapKey[c]; i = c;
            }
            heap[i] = v; heapKey[i] = k;
            return top;
        }
    }
}
