using System;
using System.Collections.Generic;

namespace AbyssDefense.Sim
{
    /// 점검용 자동 플레이. 적이 핵으로 오는 길가에 포탑을 세우고, 오는 적에 맞춰 대공포·박격포·전격탑과 지원 건물을 섞고, 자원이 넉넉하면 탄종을 바꾼다. 사람처럼 잘 두지는 못한다.
    public sealed class AutoBot
    {
        readonly Sim sim;
        readonly World w;
        List<int> path;      // 출현 지점들에서 핵까지의 길 (핵에 가까운 칸부터)
        int nextThink, turrets, sent;

        public AutoBot(Sim sim)
        {
            this.sim = sim; w = sim.world;
            sim.autoRebuild = false;   // 봇은 포탑을 스스로 다시 짓는다. 자동 재건까지 켜 두면 부서지는 자리에 철광을 계속 쏟아 탄약이 마른다
        }

        public void Step()
        {
            if (sim.tick < nextThink || sim.state != SimState.Playing) return;
            nextThink = sim.tick + 30;
            if (path == null) BuildPath();
            var items = sim.Core.items;

            // 적 거점 지역: 유닛이 몇 대 모이면 적 핵을 치러 보낸다
            // (한도가 찰 때까지 모았다가 한꺼번에 보내고, 거의 다 잃으면 불러들여 다시 모은다)
            bool raiding = false;
            if (sim.rules.assault)
            {
                bool making = false;
                foreach (var b in w.buildings) if (b is FactoryBuilding fb && fb.making != null) making = true;
                if (sim.stance == UnitStance.Defend && sim.SupplyUsed >= sim.UnitCap && !making) { sim.stance = UnitStance.Attack; sent = sim.units.Count; }
                else if (sim.stance == UnitStance.Attack && sim.units.Count * 3 <= sent) sim.stance = UnitStance.Defend;   // 셋 중 둘을 잃으면 불러들여 다시 모은다
                raiding = sim.stance == UnitStance.Attack;   // 치러 나간 동안은 새로 만들지 않는다 (하나씩 뒤따라가 죽지 않게)
            }

            // 포탑의 탄: 강철이 넉넉하면 속사포에 철갑탄(방어력 무시, 철광도 아낀다), 화약이 넉넉하면 대공포에 화약 대공탄
            int flak = 0, tesla = 0, mortar = 0, mend = 0, amp = 0, shield = 0;
            foreach (var b in w.buildings)
            {
                if (b.def == Content.FlakTurret) flak++;
                else if (b.def == Content.TeslaTurret) tesla++;
                else if (b.def == Content.MortarTurret) mortar++;
                else if (b.def == Content.MendTower) mend++;
                else if (b.def == Content.Amplifier) amp++;
                else if (b.def == Content.ShieldGen) shield++;
                if (!(b is TurretBuilding t) || !b.Built) continue;
                // 회로가 넉넉하면 과부하를 켠다
                if (!t.overcharge && items[Content.Circuit] >= 400) t.overcharge = true;
                else if (t.overcharge && items[Content.Circuit] < 150) t.overcharge = false;
                if (b.def == Content.RapidTurret)
                {
                    if (t.ammoIdx == 0 && items[Content.Steel] >= 300) t.SetAmmo(1);
                    else if (t.ammoIdx == 1 && items[Content.Steel] < 40) t.SetAmmo(0);
                }
                else if (b.def == Content.FlakTurret)
                {
                    if (t.ammoIdx == 0 && items[Content.Powder] >= 200) t.SetAmmo(1);
                    else if (t.ammoIdx == 1 && items[Content.Powder] < 20) t.SetAmmo(0);
                }
            }
            // 이 지역에 어떤 적이 오는가
            bool flyers = false, siege = false;
            foreach (var g in sim.groups) { if (g.enemy.flying) flyers = true; if (g.enemy == Content.Bombard) siege = true; }

            // 공장은 지금 낼 수 있는 가장 높은 단계의 유닛을 만든다
            int robot = 0, air = 0;
            foreach (var b in w.buildings)
            {
                if (!(b is FactoryBuilding f)) continue;
                if (f.def == Content.RobotFactory) robot++; else air++;
                f.product = 0;
                int room = sim.UnitCap - sim.SupplyUsed;
                for (int k = f.def.units.Length - 1; k > 0; k--)
                    if (sim.IsUnlocked(f.def.units[k]) && sim.Core.Has(f.def.units[k].cost) && (!sim.rules.assault || f.def.units[k].supply <= room)) { f.product = k; break; }
                if (raiding) { f.product = -1; continue; }
                bool usesIron = false;
                foreach (var c in f.def.units[f.product].cost) if (c.item == Content.Iron) usesIron = true;
                if (usesIron && items[Content.Iron] < 80 + turrets * 6) f.product = -1;   // 탄약으로 쓸 철광까지 유닛에 쓰지 않는다
            }

            // 탄약으로 쓸 자원은 남겨 둔다
            int reserveIron = 25 + turrets * 6;
            BlockDef def = null;
            if (robot == 0 && turrets >= 4 && sim.IsUnlocked(Content.RobotFactory) && sim.CanAfford(Content.RobotFactory) && items[Content.Iron] >= 120 + reserveIron) def = Content.RobotFactory;
            else if (air == 0 && turrets >= 6 && sim.IsUnlocked(Content.Airfield) && sim.CanAfford(Content.Airfield) && items[Content.Iron] >= 100 + reserveIron) def = Content.Airfield;
            // 지원 건물: 포탑이 모이면 수리탑, 증폭탑, 역장 발생기를 하나씩 곁들인다 (포탑 줄 곁에 놓인다)
            else if (sim.IsUnlocked(Content.MendTower) && turrets >= 6 && mend < 1 + turrets / 14 && sim.CanAfford(Content.MendTower) && items[Content.Iron] >= 40 + reserveIron) def = Content.MendTower;
            else if (sim.IsUnlocked(Content.Amplifier) && turrets >= 8 && amp < 1 + turrets / 16 && sim.CanAfford(Content.Amplifier) && items[Content.Iron] >= 60 + reserveIron) def = Content.Amplifier;
            else if (sim.IsUnlocked(Content.ShieldGen) && turrets >= 10 && shield < 1 && sim.CanAfford(Content.ShieldGen) && items[Content.Iron] >= 100 + reserveIron) def = Content.ShieldGen;
            // 비행체가 오는 지역에는 대공포를 섞는다
            else if (flyers && sim.IsUnlocked(Content.FlakTurret) && turrets >= 3 && flak < 1 + turrets / 8 && sim.CanAfford(Content.FlakTurret) && items[Content.Iron] >= 50 + reserveIron) def = Content.FlakTurret;
            else if (sim.IsUnlocked(Content.CrystalTurret) && sim.Core.Has(Content.CrystalTurret.cost) && items[Content.Crystal] >= 60 && items[Content.Iron] >= 80 + reserveIron) def = Content.CrystalTurret;
            else if (sim.IsUnlocked(Content.TeslaTurret) && turrets >= 5 && tesla < turrets / 6 && sim.CanAfford(Content.TeslaTurret) && items[Content.Circuit] >= 120 && items[Content.Iron] >= 90 + reserveIron) def = Content.TeslaTurret;
            // 포격병이 오는 지역에는 사거리가 긴 박격포를 더 자주 세운다
            else if (sim.IsUnlocked(Content.MortarTurret) && turrets >= 3 && (turrets % 3 == 0 || (siege && mortar < turrets / 2)) && sim.Core.Has(Content.MortarTurret.cost) && items[Content.Sulfur] >= 70 && items[Content.Iron] >= 60 + reserveIron) def = Content.MortarTurret;
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
