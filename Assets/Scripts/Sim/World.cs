using System;
using System.Collections.Generic;

namespace AbyssDefense.Sim
{
    /// 타일 한 칸: 바닥/벽과 그 위의 건물.
    public sealed class Tile
    {
        public bool wall;
        public bool water;          // 호수: 지상에서는 벽처럼 막혀 있고(wall 도 true) 해상 유닛만 다닌다
        public bool hazard;         // 위험 바닥 (용암 틈·산성 웅덩이): 걸을 수는 있지만 지을 수 없다
        public Building build;      // 여러 칸짜리 건물은 덮은 칸 모두가 같은 건물을 가리킨다
        public byte shade;          // 그림용 무작위 값
    }

    public sealed class World
    {
        public readonly int W, H;
        public readonly Tile[] tiles;
        public readonly List<Building> buildings = new List<Building>();
        public readonly List<int> spawns = new List<int>();   // 적 출현 칸 (인덱스)
        public CoreBuilding core;
        public SimMods mods = new SimMods();

        public static readonly int[] DX = { 1, 0, -1, 0 };
        public static readonly int[] DY = { 0, 1, 0, -1 };

        public World(int w, int h)
        {
            W = w; H = h;
            tiles = new Tile[w * h];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = new Tile();
        }

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public int Idx(int x, int y) => y * W + x;
        public Tile At(int x, int y) => In(x, y) ? tiles[y * W + x] : null;
        public Building BuildAt(int x, int y) => In(x, y) ? tiles[y * W + x].build : null;
        public bool Solid(int x, int y) => !In(x, y) || tiles[y * W + x].wall;

        /// 건물을 타일에 올린다 (비용·유효성 검사는 Sim 이 한다)
        public void Add(Building b)
        {
            b.world = this;
            for (int dy = 0; dy < b.def.size; dy++)
                for (int dx = 0; dx < b.def.size; dx++)
                    tiles[Idx(b.x + dx, b.y + dy)].build = b;
            buildings.Add(b);
            if (b is CoreBuilding c) core = c;
        }

        public void Remove(Building b)
        {
            for (int dy = 0; dy < b.def.size; dy++)
                for (int dx = 0; dx < b.def.size; dx++)
                    tiles[Idx(b.x + dx, b.y + dy)].build = null;
            buildings.Remove(b);
            b.dead = true;
        }
    }

    /// 설치된 건물 하나. 종류별 동작은 하위 클래스가 맡는다.
    public abstract class Building
    {
        public BlockDef def;
        public World world;
        public int x, y;            // 왼쪽 아래 칸
        public float health, maxHealth;
        public const float SiteHealth = 0.15f;   // 막 놓은 건설 터의 체력 비율
        public float progress = 1f;              // 건설 진행도 (1 = 완성)
        public int[] paid;                       // 건설하며 지금까지 낸 비용 (def.cost 순서)
        public bool constructing;                // 이번 틱에 실제로 지어지고 있다 (순서를 기다리거나 자원이 없으면 false)
        public bool Built => progress >= 1f;
        public bool dead;
        public int hitFlash;        // 그림용: 맞은 뒤 남은 틱
        public int boostTick = -10; // 증폭탑의 범위 안에 있었던 마지막 틱

        public float Cx => x + def.size * 0.5f;
        public float Cy => y + def.size * 0.5f;

        public virtual void Update(Sim sim) { }

        /// 점에서 건물 사각형까지의 거리
        public float DistTo(float px, float py)
        {
            float dx = Math.Max(Math.Max(x - px, 0), px - (x + def.size));
            float dy = Math.Max(Math.Max(y - py, 0), py - (y + def.size));
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static Building Create(BlockDef def)
        {
            Building b;
            switch (def.kind)
            {
                case BlockKind.Core: b = new CoreBuilding(); break;
                case BlockKind.Turret: b = new TurretBuilding(); break;
                case BlockKind.Factory: b = new FactoryBuilding(); break;
                case BlockKind.EnemyCore: b = new EnemyCoreBuilding(); break;
                case BlockKind.EnemyTurret: b = new EnemyTurretBuilding(); break;
                case BlockKind.Support: b = new SupportBuilding(); break;
                default: b = new WallBuilding(); break;
            }
            b.def = def;
            b.health = b.maxHealth = def.health;
            return b;
        }
    }

    public sealed class WallBuilding : Building { }

    /// 지원 건물: 수리탑·증폭탑·역장 발생기. 범위 안의 다 지어진 아군 건물을 돕는다
    public sealed class SupportBuilding : Building
    {
        public float shieldHp;      // 남은 역장
        public int shieldWait;      // 이 틱이 지나야 역장이 다시 차오른다
        int timer;

        public float MaxShield => def.shield * world.mods.healthMul;
        public bool Covers(Building b) => b.DistTo(Cx, Cy) <= def.supportRange;

        public override void Update(Sim sim)
        {
            var bs = world.buildings;
            if (def.mendPct > 0 && ++timer >= def.mendTicks)
            {
                timer = 0;
                bool any = false;
                for (int i = 0; i < bs.Count; i++)
                {
                    var b = bs[i];
                    if (b == this || b.def.enemy || !b.Built || b.health >= b.maxHealth || !Covers(b)) continue;
                    b.health = Math.Min(b.maxHealth, b.health + b.maxHealth * def.mendPct);
                    any = true;
                }
                if (any) sim.fx.Add(new Fx { kind = FxKind.Spawn, x = Cx, y = Cy, r = def.supportRange, color = def.color });
            }
            if (def.boost)
                for (int i = 0; i < bs.Count; i++)
                    if (bs[i] is TurretBuilding && Covers(bs[i])) bs[i].boostTick = sim.tick;
            if (def.shield > 0)
            {
                if (shieldWait > 0) shieldWait--;
                else if (shieldHp < MaxShield) shieldHp = Math.Min(MaxShield, shieldHp + def.shieldRegen / Sim.TicksPerSecond);
            }
        }

        /// 역장이 피해를 대신 받는다. 받고 남은 피해를 돌려준다
        public float Absorb(float dmg)
        {
            if (shieldHp <= 0) return dmg;
            float take = Math.Min(shieldHp, dmg);
            shieldHp -= take;
            shieldWait = def.shieldDelay;
            return dmg - take;
        }
    }

    /// 적 거점의 핵. 적이 여기서 나오고, 모두 부서지면 그 지역을 점령한다
    public sealed class EnemyCoreBuilding : Building { }

    /// 적 거점의 포탑. 사거리 안의 아군 유닛을 쏜다 (탄은 바로 맞는다)
    public sealed class EnemyTurretBuilding : Building
    {
        public float aim = -1.5708f, damage;
        public int reload;

        public override void Update(Sim sim)
        {
            if (reload > 0) { reload--; return; }
            var u = sim.NearestUnit(Cx, Cy, def.range, true);
            if (u == null) return;
            reload = def.reloadTicks;
            aim = (float)Math.Atan2(u.y - Cy, u.x - Cx);
            sim.fx.Add(new Fx { kind = FxKind.Shoot, x = Cx + (float)Math.Cos(aim) * 0.5f, y = Cy + (float)Math.Sin(aim) * 0.5f, x2 = Cx + (float)Math.Cos(aim) * 1.5f, y2 = Cy + (float)Math.Sin(aim) * 1.5f, r = 0.25f, color = def.bullet.color });
            sim.HurtUnit(u, damage, def.bullet.color);
        }
    }

    /// 가져온 자원과 전리품이 쌓이는 곳. 건설 비용과 포탑 탄약이 모두 여기서 나간다.
    public sealed class CoreBuilding : Building
    {
        public const int Capacity = 99999;
        public readonly int[] items = new int[Content.Items.Length];

        public void Add(int item, int amount) { items[item] = Math.Min(Capacity, items[item] + amount); }

        public bool Has(ItemStack[] cost)
        {
            foreach (var c in cost) if (items[c.item] < c.amount) return false;
            return true;
        }
        public void Pay(ItemStack[] cost) { foreach (var c in cost) items[c.item] -= c.amount; }
        public void Refund(ItemStack[] cost, float ratio)
        {
            foreach (var c in cost) Add(c.item, (int)(c.amount * ratio));
        }
    }

    public enum FactoryStatus { Stopped, Producing, NoResources, Cap, Locked }
    /// 공장에서 나온 유닛의 태세: 위의 전역 태세를 따르거나, 이 공장의 유닛만 늘 지키거나 늘 공격한다
    public enum FactoryStance { Follow, Defend, Attack }

    /// 유닛 공장. 고른 유닛을 핵의 자원으로 계속 만든다. 비용은 생산을 시작할 때 낸다
    public sealed class FactoryBuilding : Building
    {
        public int product;              // def.units 의 번호 (-1 = 생산 정지)
        public UnitDef making;           // 지금 만들고 있는 유닛
        public float work;               // 생산 진행도 0~1
        public float rallyX, rallyY;     // 만든 유닛이 모이는 곳
        public FactoryStance stance;     // 이 공장에서 나온 유닛의 태세 (기본: 전역 태세를 따른다)
        public FactoryStatus status;

        public override void Update(Sim sim)
        {
            if (making != null)
            {
                status = FactoryStatus.Producing;
                work += 1f / making.buildTicks;
                if (work < 1f) return;
                sim.SpawnUnit(making, this);
                making = null; work = 0;
            }
            if (product < 0 || product >= def.units.Length) { status = FactoryStatus.Stopped; return; }
            var u = def.units[product];
            if (!sim.IsUnlocked(u)) status = FactoryStatus.Locked;
            else if (sim.SupplyUsed + u.supply > sim.UnitCap) status = FactoryStatus.Cap;
            else if (!world.core.Has(u.cost)) status = FactoryStatus.NoResources;
            else
            {
                world.core.Pay(u.cost);
                making = u; work = 0;
                status = FactoryStatus.Producing;
            }
        }
    }

    public sealed class TurretBuilding : Building
    {
        public int ammo, reload;
        public int ammoIdx;              // def.ammos 의 번호: 지금 넣는 탄
        public TargetMode mode;          // 누구를 겨누는가
        public bool overcharge;          // 과부하: 겨눌 적이 있는 동안 핵의 회로를 태워 1.5배 빨리 쏜다
        public int overchargeLeft;       // 회로 하나로 남은 과부하 틱
        public float aim = 1.5708f;      // 그림용 포신 방향 (라디안)
        public Enemy target;
        int retarget;

        public AmmoDef Ammo => def.ammos[ammoIdx];
        public float Range => def.range * world.mods.rangeMul;
        public int AmmoPerItem => (int)Math.Round(Ammo.perItem * world.mods.ammoMul);
        /// 탄도 없고 핵에 탄약 자원도 없다
        public bool Starved => ammo <= 0 && world.core.items[Ammo.item] <= 0;

        /// 넣는 탄을 바꾼다. 장전해 둔 탄은 버린다
        public bool SetAmmo(int k)
        {
            if (k < 0 || k >= def.ammos.Length || k == ammoIdx) return false;
            ammoIdx = k; ammo = 0;
            return true;
        }

        public override void Update(Sim sim)
        {
            if (reload > 0 && !sim.BlizzardStall) reload--;
            if (reload > 0 && sim.tick - boostTick <= 1 && sim.tick % 3 == 0) reload--;   // 증폭탑 곁: 3틱에 한 번 한 틱 더

            if (--retarget <= 0 || target == null || target.dead || !InRange(target))
            {
                retarget = 12;
                target = sim.PickEnemy(Cx, Cy, Range, def.targetAir, def.targetGround, mode);
            }
            if (target == null) return;

            // 과부하: 쏠 적이 있을 때만 회로 하나를 태워 OverchargeTicks 동안 두 틱에 한 번 재장전을 한 틱 더 줄인다
            if (overcharge && overchargeLeft <= 0 && world.core.items[Sim.OverchargeItem] > 0)
            {
                world.core.items[Sim.OverchargeItem]--;
                overchargeLeft = Sim.OverchargeTicks;
            }
            if (overchargeLeft > 0)
            {
                overchargeLeft--;
                if (reload > 0 && sim.tick % 2 == 0) reload--;
            }

            // 탄이 닿을 때의 적 위치를 겨눈다
            var bl = Ammo.bullet;
            float tx = target.x, ty = target.y;
            for (int k = 0; k < 2; k++)
            {
                float d = Dist(tx, ty);
                float t = d / bl.speed;
                tx = target.x + target.vx * t;
                ty = target.y + target.vy * t;
            }
            aim = (float)Math.Atan2(ty - Cy, tx - Cx);
            if (reload > 0) return;

            // 탄이 떨어지면 쏠 일이 있을 때 핵 재고에서 자원 하나를 가져와 탄으로 만든다
            if (ammo <= 0)
            {
                var core = world.core;
                if (core.items[Ammo.item] <= 0) return;
                core.items[Ammo.item]--;
                ammo += AmmoPerItem;
            }
            reload = def.reloadTicks;
            ammo--;
            sim.Fire(this, tx, ty);
        }

        bool InRange(Enemy e) => Dist(e.x, e.y) <= Range + e.def.radius;
        float Dist(float px, float py) => (float)Math.Sqrt((px - Cx) * (px - Cx) + (py - Cy) * (py - Cy));
    }
}
