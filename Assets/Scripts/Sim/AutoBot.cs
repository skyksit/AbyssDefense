using System;
using System.Collections.Generic;

namespace AbyssDefense.Sim
{
    /// 점검용 자동 플레이. 적이 핵으로 오는 길가에 포탑을 세운다. 사람처럼 잘 두지는 못한다.
    public sealed class AutoBot
    {
        readonly Sim sim;
        readonly World w;
        List<int> path;      // 출현 지점들에서 핵까지의 길 (핵에 가까운 칸부터)
        int nextThink, turrets;

        public AutoBot(Sim sim) { this.sim = sim; w = sim.world; }

        public void Step()
        {
            if (sim.tick < nextThink || sim.state != SimState.Playing) return;
            nextThink = sim.tick + 30;
            if (path == null) BuildPath();
            var items = sim.Core.items;

            // 적 거점 지역: 유닛이 몇 대 모이면 적 핵을 치러 보낸다
            // (한도가 찰 때까지 모았다가 한꺼번에 보내고, 거의 다 잃으면 불러들여 다시 모은다)
            if (sim.rules.assault)
            {
                if (sim.SupplyUsed >= sim.UnitCap) sim.stance = UnitStance.Attack;
                else if (sim.units.Count <= 1) sim.stance = UnitStance.Defend;
            }

            // 공장은 지금 낼 수 있는 가장 높은 단계의 유닛을 만든다
            int robot = 0, air = 0;
            foreach (var b in w.buildings)
            {
                if (!(b is FactoryBuilding f)) continue;
                if (f.def == Content.RobotFactory) robot++; else air++;
                f.product = 0;
                for (int k = f.def.units.Length - 1; k > 0; k--)
                    if (sim.IsUnlocked(f.def.units[k]) && sim.Core.Has(f.def.units[k].cost)) { f.product = k; break; }
                bool usesIron = false;
                foreach (var c in f.def.units[f.product].cost) if (c.item == Content.Iron) usesIron = true;
                if (usesIron && items[Content.Iron] < 80 + turrets * 6) f.product = -1;   // 탄약으로 쓸 철광까지 유닛에 쓰지 않는다
            }

            // 탄약으로 쓸 자원은 남겨 둔다
            int reserveIron = 25 + turrets * 6;
            BlockDef def = null;
            if (robot == 0 && turrets >= 4 && sim.IsUnlocked(Content.RobotFactory) && sim.CanAfford(Content.RobotFactory) && items[Content.Iron] >= 120 + reserveIron) def = Content.RobotFactory;
            else if (air == 0 && turrets >= 6 && sim.IsUnlocked(Content.Airfield) && sim.CanAfford(Content.Airfield) && items[Content.Iron] >= 100 + reserveIron) def = Content.Airfield;
            else if (sim.IsUnlocked(Content.CrystalTurret) && sim.Core.Has(Content.CrystalTurret.cost) && items[Content.Crystal] >= 60 && items[Content.Iron] >= 80 + reserveIron) def = Content.CrystalTurret;
            else if (sim.IsUnlocked(Content.MortarTurret) && turrets >= 3 && turrets % 3 == 0 && sim.Core.Has(Content.MortarTurret.cost) && items[Content.Sulfur] >= 70 && items[Content.Iron] >= 60 + reserveIron) def = Content.MortarTurret;
            else if (items[Content.Iron] >= 35 + reserveIron) def = Content.RapidTurret;
            if (def != null && def.kind == BlockKind.Turret && sim.rules.assault && turrets >= 10 && items[Content.Iron] < 500) def = null;   // 거점 지역에서는 유닛 몫을 남긴다
            if (def == null) return;

            // 길에서 한두 칸 떨어진 빈 자리 중 핵에 가까운 곳부터
            foreach (int p in path)
            {
                int px = p % w.W, py = p / w.W;
                for (int ring = 1; ring <= 2; ring++)
                    for (int dy = -ring - def.size + 1; dy <= ring; dy++)
                        for (int dx = -ring - def.size + 1; dx <= ring; dx++)
                        {
                            int x = px + dx, y = py + dy;
                            if (OnPath(x, y, def.size)) continue;
                            if (sim.Place(def, x, y) == PlaceResult.Ok) { if (def.kind == BlockKind.Turret) turrets++; return; }
                        }
            }
        }

        void BuildPath()
        {
            path = new List<int>();
            var seen = new HashSet<int>();
            var all = new List<int>();
            foreach (int s in w.spawns)
            {
                int cur = s;
                for (int guard = 0; guard < 4000 && cur >= 0; guard++)
                {
                    if (seen.Add(cur)) all.Add(cur);
                    cur = sim.field.Next(cur % w.W, cur / w.W);
                }
            }
            // 핵에서 4~14칸 떨어진 길을 가까운 순으로
            all.Sort((a, b) => sim.field.cost[a].CompareTo(sim.field.cost[b]));
            foreach (int p in all)
                if (sim.field.cost[p] >= 4 && sim.field.cost[p] <= 14) path.Add(p);
            pathSet = seen;
        }

        HashSet<int> pathSet;

        bool OnPath(int x, int y, int size)
        {
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                    if (!w.In(x + dx, y + dy) || pathSet.Contains(w.Idx(x + dx, y + dy))) return true;
            return false;
        }
    }
}
