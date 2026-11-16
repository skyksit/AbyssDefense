using System;
using System.Collections.Generic;

namespace AbyssDefense.Sim
{
    public sealed class Enemy
    {
        public EnemyDef def;
        public float x, y, vx, vy;
        public float health, maxHealth, armor;
        public float ox, oy;         // 칸 가운데에서 비켜 걷는 정도 (겹쳐 보이지 않게)
        public int cooldown;
        public int slowTicks;        // 남은 둔화 시간
        public float slowMul = 1;    // 둔화 중의 속도 배수
        public int burnTicks;        // 남은 불타는 시간
        public float burnDps;        // 불타는 동안 초당 받는 피해 (방어력 무시)
        public int acidTicks;        // 남은 산성 시간
        public float acidMul = 1;    // 산성에 젖은 동안의 방어력 배수
        public int wetTicks;         // 남은 젖음 시간 (전격에 1.5배, 불이 붙지 않는다)
        public float barrier, barrierMax;   // 보호막: 체력보다 먼저 닳는다
        public int barrierWait;      // 이 틱이 지나야 보호막이 다시 차오른다
        public bool elite;           // 정예 (크게 그린다, 전리품 3배)
        public int cargo;            // (수송체) 아직 내려놓지 않은 적의 수
        public Building goal;        // (표적 성향이 있는 비행체) 날아가 칠 건물
        public int goalTick;         // 그 건물을 다시 고를 틱
        public Unit preyUnit;        // 가는 길 둘레에서 찾아 먼저 치러 가는 아군 유닛
        public Building preyBuild;   // 가는 길 둘레에서 찾아 먼저 치러 가는 아군 건물
        public int preyTick;         // 먹잇감을 다시 고를 틱
        public float preyX, preyY;   // 유닛을 쫓기 시작한 자리
        public int noUnitUntil;      // 이 틱까지는 유닛을 쫓지 않는다 (너무 멀리 끌려갔다)
        public float Armor => acidTicks > 0 ? armor * acidMul : armor;
        public bool dead;
        public int hitFlash;
        public int serial;
    }

    public sealed class Bullet
    {
        public BulletDef def;
        public float x, y, vx, vy;
        public int life;
        public float damage;
        public bool dead;
        public Building build;       // 유닛이 적 건물을 겨눠 쏜 탄
        public bool fromUnit;
        public Enemy target;         // 유도탄이 쫓는 적
        public Unit owner;           // 쏜 유닛 (준 피해로 체력을 되찾는 유닛만)
        public List<Enemy> hit;      // 관통탄이 이미 맞힌 적
    }

    /// 기뢰 부설기가 깔아 둔 지뢰. 지상의 적이 밟으면 터진다
    public sealed class Mine
    {
        public BulletDef def;
        public float x, y, damage;
        public int arm;              // 이 틱이 지나야 터질 수 있다
        public Unit owner;
        public bool dead;
    }

    public enum FxKind { Hit, Explode, EnemyDeath, Spawn, BuildDestroyed, EnemyAttack, Shoot, Chain }

    public struct Fx
    {
        public FxKind kind;
        public float x, y, r;
        public float x2, y2;        // Chain: 번개가 닿는 곳. Shoot: 쏜 쪽으로 조금 더 나간 점 (방향만 쓴다)
        public uint color;
    }

    public enum SimState { Playing, Won, Lost }
    public enum PlaceResult { Ok, OutOfBounds, Blocked, NoResources, NearSpawn, Locked, NeedsWater, Hazard }

    /// 지역 한 판의 시뮬레이션. 화면 없이 Tick() 만으로 돈다.
    public sealed partial class Sim
    {
        public const int TicksPerSecond = 60;
        public const float RefundRatio = 0.75f;
        public const int BuildSlots = 2;             // 동시에 지을 수 있는 건물 수
        public const float MinArmorDamage = 0.2f;    // 방어력이 아무리 높아도 피해의 20% 는 들어간다
        const int SpawnSafeRadius = 3;
        public const int EnemyCoreSafeRadius = 9;    // 적 핵에서 이 칸 수 안에는 건물을 지을 수 없다

        public readonly RegionRules rules;
        public readonly AbyssDef abyss;
        public readonly World world;
        public readonly FlowField field;
        public readonly List<Enemy> enemies = new List<Enemy>();
        public readonly List<Bullet> bullets = new List<Bullet>();
        public readonly List<WaveGroup> groups;
        public readonly List<Fx> fx = new List<Fx>();   // 화면이 읽고 비운다

        public int tick, wave, waveCountdown, kills;
        public SimState state = SimState.Playing;

        public event Action<Building> BuildingAdded, BuildingRemoved;
        public readonly int[] looted = new int[Content.Items.Length];   // 이 판에서 전리품으로 얻은 자원

        struct Pending { public int tick, tile; public EnemyDef def; public WaveGroup group; }
        readonly List<Pending> pending = new List<Pending>();
        readonly Rng rng;
        bool fieldDirty = true;
        int serial;

        /// loadout = 기지에서 가져온 자원 [철광, 황석, 심연 결정]
        public Sim(int abyssId, int region, int[] loadout = null, SimMods mods = null, int difficulty = 1)
        {
            rules = RegionRules.For(abyssId, region, difficulty);
            abyss = Content.Abysses[abyssId];
            world = MapGen.Generate(rules, abyss);
            MakeLakes();
            MakeHazards();
            if (mods != null) world.mods = mods;
            world.core.health = world.core.maxHealth = Content.Core.health * world.mods.coreHealthMul;
            field = new FlowField(world);
            nav = new GroundNav(world);
            sea = new GroundNav(world, true);
            skim = new GroundNav(world, false, true);
            groups = WaveGen.Generate(rules);
            rng = new Rng(rules.seed * 977 + 13);
            waveCountdown = rules.firstWaveTicks;
            for (int i = 0; i < rules.startItems.Length; i++)
                world.core.items[i] = rules.startItems[i] + (loadout != null && i < loadout.Length ? loadout[i] : 0);
            field.Compute();
            fieldDirty = false;
            if (rules.assault) { BuildEnemyBase(); field.Compute(); }
            commandX = world.core.Cx; commandY = world.core.Cy + (world.Solid((int)world.core.Cx, world.core.y + 4) ? 0 : 3);
        }

        public CoreBuilding Core => world.core;
        /// 적의 공격력 배수 (깊을수록 세다)
        public float EnemyDamageMul => rules.enemyDamageMul;
        public bool AllWavesSent => !rules.assault && wave >= rules.winWave;
        public int PendingSpawns => pending.Count;

        // ───────── 건설 ─────────

        public bool IsUnlocked(BlockDef def) => def.tech < 0 || world.mods.tech[def.tech];

        /// (x, y) 에 def 를 놓으면 덮어 짓게 되는 건물. 같은 자리·같은 크기의 다 지어진 아군 건물이고 같은 계열(방벽·포탑·지원 건물)의 다른 건물일 때만이다
        public Building ReplaceTarget(BlockDef def, int x, int y)
        {
            var old = world.BuildAt(x, y);
            if (old == null || old.def.enemy || !old.Built || old.dead || old.def == def) return null;
            if (old.x != x || old.y != y || old.def.size != def.size || old.def.kind != def.kind) return null;
            if (def.kind != BlockKind.Wall && def.kind != BlockKind.Turret && def.kind != BlockKind.Support) return null;
            return old;
        }

        /// 덮어 지을 때 헐리는 건물에서 돌려받는 자원 (철거와 같다). 덮어 짓는 자리가 아니면 null
        public int[] ReplaceRefund(BlockDef def, int x, int y)
        {
            var old = ReplaceTarget(def, x, y);
            if (old == null) return null;
            var r = new int[Content.Items.Length];
            float ratio = RefundRatio * (old.health / old.maxHealth);
            foreach (var c in old.def.cost) r[c.item] += (int)(c.amount * ratio);
            return r;
        }

        /// allowReplace: 같은 계열의 건물 위에 놓아 덮어 짓는 것을 허락한다 (자동 재건과 재건 드론은 허락하지 않는다)
        public PlaceResult CanPlace(BlockDef def, int x, int y, bool checkCost = true, bool allowReplace = true)
        {
            if (def.enemy) return PlaceResult.Blocked;
            if (!IsUnlocked(def)) return PlaceResult.Locked;
            int safe = rules.assault ? EnemyCoreSafeRadius : SpawnSafeRadius;
            var old = allowReplace ? ReplaceTarget(def, x, y) : null;
            for (int dy = 0; dy < def.size; dy++)
                for (int dx = 0; dx < def.size; dx++)
                {
                    var t = world.At(x + dx, y + dy);
                    if (t == null) return PlaceResult.OutOfBounds;
                    if (t.wall || (t.build != null && t.build != old)) return PlaceResult.Blocked;
                    if (t.hazard) return PlaceResult.Hazard;
                    foreach (int s in world.spawns)
                    {
                        int sx = s % world.W, sy = s / world.W;
                        if (Math.Abs(sx - (x + dx)) <= safe && Math.Abs(sy - (y + dy)) <= safe) return PlaceResult.NearSpawn;
                    }
                }
            if (def.needsWater && !NearWater(x, y, def.size)) return PlaceResult.NeedsWater;
            if (checkCost && !CanAfford(def, old != null ? ReplaceRefund(def, x, y) : null)) return PlaceResult.NoResources;
            return PlaceResult.Ok;
        }

        /// 짓고 있는 건물들이 앞으로 가져갈 자원을 빼고도 이 건물 비용이 남는가
        public bool CanAfford(BlockDef def, int[] refund = null)
        {
            var need = Committed();
            foreach (var c in def.cost) need[c.item] += c.amount;
            for (int i = 0; i < need.Length; i++) if (Core.items[i] + (refund != null ? refund[i] : 0) < need[i]) return false;
            return true;
        }

        /// 건설 중인 건물들이 아직 내지 않은 비용의 합
        public int[] Committed()
        {
            var need = new int[Content.Items.Length];
            foreach (var b in world.buildings)
                if (!b.Built)
                    for (int k = 0; k < b.def.cost.Length; k++) need[b.def.cost[k].item] += b.def.cost[k].amount - b.paid[k];
            return need;
        }

        /// 건설 터를 놓는다. 비용은 건설이 진행되는 만큼 핵에서 조금씩 나가고, 다 지어져야 동작한다
        /// 같은 계열의 다 지어진 건물 위에 놓으면 덮어 짓는다: 헐리는 건물은 철거한 만큼 돌려받고, 새 건설 터는 헐린 건물의 남은 체력을 이어받는다
        public PlaceResult Place(BlockDef def, int x, int y, bool allowReplace = true)
        {
            if (state != SimState.Playing) return PlaceResult.Blocked;
            var res = CanPlace(def, x, y, true, allowReplace);
            if (res != PlaceResult.Ok) return res;
            float kept = 0;
            var old = allowReplace ? ReplaceTarget(def, x, y) : null;
            if (old != null)
            {
                kept = old.health;
                Core.Refund(old.def.cost, RefundRatio * (old.health / old.maxHealth));
                world.Remove(old);
                BuildingRemoved?.Invoke(old);
            }
            var b = Building.Create(def);
            b.x = x; b.y = y;
            b.maxHealth = def.health * world.mods.healthMul;
            b.health = Math.Min(b.maxHealth, Math.Max(b.maxHealth * Building.SiteHealth, kept));
            b.progress = 0;
            b.paid = new int[def.cost.Length];
            world.Add(b);
            if (b is FactoryBuilding f) DefaultRally(f);
            fieldDirty = true;
            BuildingAdded?.Invoke(b);
            return PlaceResult.Ok;
        }

        /// 건물을 철거하고 비용 일부를 돌려받는다. 짓던 건물을 취소하면 그때까지 낸 만큼 모두 돌려받는다
        public bool Remove(int x, int y)
        {
            var b = world.BuildAt(x, y);
            if (b == null && state == SimState.Playing) return RemoveRuin(x, y);
            if (b == null || b is CoreBuilding || b.def.enemy || state != SimState.Playing) return false;
            if (!b.Built) { for (int k = 0; k < b.paid.Length; k++) Core.Add(b.def.cost[k].item, b.paid[k]); }
            else Core.Refund(b.def.cost, RefundRatio * (b.health / b.maxHealth));
            world.Remove(b);
            fieldDirty = true;
            BuildingRemoved?.Invoke(b);
            return true;
        }

        /// 부서진 자리(다시 지으려고 적어 둔 곳)를 지운다
        public bool RemoveRuin(int x, int y)
        {
            for (int i = 0; i < ruins.Count; i++)
            {
                var r = ruins[i];
                if (x < r.x || y < r.y || x >= r.x + r.def.size || y >= r.y + r.def.size) continue;
                ruins.RemoveAt(i);
                return true;
            }
            return false;
        }

        // ───────── 한 틱 ─────────

        public void Tick()
        {
            if (state != SimState.Playing) return;
            tick++;
            if (fx.Count > 512) fx.Clear();

            // 웨이브 타이머
            if (!AllWavesSent && --waveCountdown <= 0) RunWave();
            for (int i = pending.Count - 1; i >= 0; i--)
                if (pending[i].tick <= tick) { Spawn(pending[i].def, pending[i].tile, pending[i].group); pending.RemoveAt(i); }

            if (fieldDirty) { field.Compute(); fieldDirty = false; }
            UpdateWeather();

            var bs = world.buildings;
            Construct();
            for (int i = 0; i < bs.Count; i++)
            {
                if (bs[i].hitFlash > 0) bs[i].hitFlash--;
                if (bs[i].Built) bs[i].Update(this);
            }
            for (int i = 0; i < units.Count; i++) UpdateUnit(units[i]);
            UpdateCommander();
            for (int i = 0; i < enemies.Count; i++) UpdateEnemy(enemies[i]);
            for (int i = 0; i < bullets.Count; i++) UpdateBullet(bullets[i]);
            UpdateMines();
            AutoRebuild();

            enemies.RemoveAll(e => e.dead);
            units.RemoveAll(u => u.dead);
            bullets.RemoveAll(b => b.dead);
            for (int i = bs.Count - 1; i >= 0; i--)
                if (bs[i].health <= 0) DestroyBuilding(bs[i]);

            CheckState();
        }

        /// 건설: 놓은 순서대로 한 번에 BuildSlots 곳까지만 짓는다. 진행한 만큼 핵에서 비용을 가져가고, 핵에 자원이 없으면 멈춘다
        void Construct()
        {
            var bs = world.buildings;
            int active = 0;
            for (int i = 0; i < bs.Count; i++) bs[i].constructing = false;
            for (int i = 0; i < bs.Count && active < BuildSlots; i++)
            {
                var b = bs[i];
                if (b.Built) continue;
                active++;
                float p = Math.Min(1f, b.progress + world.mods.buildSpeedMul * CommanderBoost(b) / b.def.buildTicks);
                // 이 진행도까지 내야 할 비용
                bool ok = true;
                for (int k = 0; k < b.def.cost.Length && ok; k++)
                {
                    var c = b.def.cost[k];
                    int due = (p >= 1f ? c.amount : (int)(c.amount * p)) - b.paid[k];
                    if (Core.items[c.item] < due) ok = false;
                }
                b.constructing = ok;
                if (!ok) continue;
                for (int k = 0; k < b.def.cost.Length; k++)
                {
                    var c = b.def.cost[k];
                    int due = (p >= 1f ? c.amount : (int)(c.amount * p)) - b.paid[k];
                    Core.items[c.item] -= due; b.paid[k] += due;
                }
                b.health = Math.Min(b.maxHealth, b.health + b.maxHealth * (1 - Building.SiteHealth) * (p - b.progress));
                b.progress = p;
                if (p >= 1f)
                {
                    b.constructing = false;
                    if (b.health > b.maxHealth - 0.5f) b.health = b.maxHealth;   // 더하다 생긴 소수 오차를 없앤다
                    fieldDirty = true;
                    fx.Add(new Fx { kind = FxKind.Spawn, x = b.Cx, y = b.Cy, r = b.def.size * 0.7f, color = b.def.color });
                }
            }
        }

        /// 남은 시간을 건너뛰고 다음 웨이브를 바로 부른다
        public void CallNextWave()
        {
            if (state == SimState.Playing && !AllWavesSent) waveCountdown = 1;
        }

        void RunWave()
        {
            wave++;
            waveCountdown = rules.waveTicks;
            if (world.spawns.Count == 0) return;
            int order = 0;
            foreach (var g in groups)
            {
                int n = g.GetSpawned(wave);
                for (int i = 0; i < n; i++)
                {
                    int gap = g.enemy.boss ? 120 : 22;
                    pending.Add(new Pending { tick = tick + 1 + order * gap, def = g.enemy, group = g, tile = world.spawns[rng.Range(0, world.spawns.Count - 1)] });
                    order++;
                }
                if (n > 0) order += 2;
            }
        }

        public const float EliteHealth = 2.5f, EliteSpeed = 1.15f, EliteBounty = 3f;

        /// 지금 회차의 세기로 적 하나를 만든다 (자리는 부르는 쪽이 정한다). group 이 있으면 그 무리의 보호막과 정예 여부를 따른다
        Enemy MakeEnemy(EnemyDef def, WaveGroup group = null)
        {
            var e = new Enemy { def = def, serial = ++serial };
            e.elite = group != null && group.elite;
            e.maxHealth = e.health = def.health * rules.enemyHealthMul * (1 + 0.12f * (Math.Max(1, wave) - 1)) * (e.elite ? EliteHealth : 1f);
            e.armor = def.armor * rules.enemyArmorMul;
            e.barrierMax = e.barrier = (def.barrier + (group != null ? group.ShieldAt(wave) : 0)) * rules.enemyHealthMul;
            e.cargo = def.carryCount;
            e.ox = rng.Range(-0.25f, 0.25f); e.oy = rng.Range(-0.25f, 0.25f);
            return e;
        }

        void Spawn(EnemyDef def, int s, WaveGroup group = null)
        {
            if (rules.assault && !(world.tiles[s].build is EnemyCoreBuilding)) return;   // 그 사이에 적 핵이 부서졌다
            float sx = s % world.W + 0.5f, sy = s / world.W + 0.5f;
            var e = MakeEnemy(def, group);
            if (def.flying) { e.x = sx + rng.Range(-4f, 4f); e.y = world.H - 0.5f; }
            else { e.x = sx + rng.Range(-1.2f, 1.2f); e.y = sy + rng.Range(-1.2f, 1.2f); }
            enemies.Add(e);
            fx.Add(new Fx { kind = FxKind.Spawn, x = e.x, y = e.y, r = def.radius * 2, color = def.color });
        }

        /// 싸움 도중에 생기는 적 (분열, 수송체가 내려놓는 적): (x, y) 둘레의 걸을 수 있는 칸에 놓는다
        void SpawnNear(EnemyDef def, float x, float y, int count)
        {
            int placed = 0;
            for (int ring = 0; ring <= 3 && placed < count; ring++)
                for (int dy = -ring; dy <= ring && placed < count; dy++)
                    for (int dx = -ring; dx <= ring && placed < count; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                        int tx = (int)Math.Floor(x) + dx, ty = (int)Math.Floor(y) + dy;
                        if (!world.In(tx, ty)) continue;
                        var t = world.tiles[world.Idx(tx, ty)];
                        if (!def.flying && (t.wall || t.build != null || field.cost[world.Idx(tx, ty)] == float.MaxValue)) continue;
                        // 한 칸에 여럿이 겹치지 않게 조금씩 비켜 놓는다
                        for (int k = 0; k < 2 && placed < count; k++)
                        {
                            var e = MakeEnemy(def);
                            e.x = tx + 0.5f + e.ox; e.y = ty + 0.5f + e.oy;
                            enemies.Add(e);
                            fx.Add(new Fx { kind = FxKind.Spawn, x = e.x, y = e.y, r = def.radius * 2, color = def.color });
                            placed++;
                        }
                    }
        }

        void CheckState()
        {
            if (Core == null || Core.health <= 0 || Core.dead) { state = SimState.Lost; return; }
            if (rules.assault) { if (EnemyCores == 0) state = SimState.Won; return; }
            if (AllWavesSent && enemies.Count == 0 && pending.Count == 0) state = SimState.Won;
        }

        // ───────── 적 ─────────

        void UpdateEnemy(Enemy e)
        {
            if (e.dead) return;
            if (e.burnTicks > 0)
            {
                // 불타는 적은 0.25초마다 피해를 입는다
                if (--e.burnTicks % 15 == 0)
                {
                    Damage(e, e.burnDps * 0.25f, true);
                    fx.Add(new Fx { kind = FxKind.Hit, x = e.x, y = e.y, r = e.def.radius * 0.8f, color = 0xFF922B });
                    if (e.dead) return;
                }
            }
            if (e.cooldown > 0) e.cooldown--;
            if (e.hitFlash > 0) e.hitFlash--;
            if (e.acidTicks > 0) e.acidTicks--;
            if (e.wetTicks > 0) e.wetTicks--;
            if (e.barrierWait > 0) e.barrierWait--;
            else if (e.def.barrierRegen > 0 && e.barrier < e.barrierMax)
                e.barrier = Math.Min(e.barrierMax, e.barrier + e.def.barrierRegen * rules.enemyHealthMul / TicksPerSecond);
            if (e.def.heal > 0 && (tick + e.serial) % 15 == 0) HealEnemies(e);
            if (rules.hazard != Hazard.None && !e.def.flying && (tick + e.serial) % 15 == 0) { HazardEnemy(e); if (e.dead) return; }
            e.vx = e.vy = 0;
            var core = Core;
            if (core == null) return;
            float speed = e.def.speed * abyss.enemySpeedMul * (e.elite ? EliteSpeed : 1f);
            if (e.def.flying && Blizzard) speed *= BlizzardFly;
            if (e.slowTicks > 0) { e.slowTicks--; speed *= e.slowMul; }

            // 손이 닿는 곳에 아군 유닛이 있으면 멈춰서 그 유닛부터 친다
            var near = NearestUnit(e.x, e.y, e.def.range + 0.3f, e.def.HitsAir);
            if (near != null) { AttackUnit(e, near); return; }

            // 가는 길 둘레의 유닛과 건물을 핵보다 먼저 친다
            if (tick >= e.preyTick) FindPrey(e);
            if (e.preyUnit != null && (e.preyUnit.dead || e.preyUnit.carrier != null)) e.preyUnit = null;
            if (e.preyBuild != null && (e.preyBuild.dead || e.preyBuild.health <= 0)) e.preyBuild = null;
            if (e.preyUnit != null)
            {
                // 유닛에게 끌려다니지 않는다: 쫓기 시작한 자리에서 ChaseLeash 넘게 벗어나면 그만두고 한동안 유닛을 쫓지 않는다
                float lx = e.x - e.preyX, ly = e.y - e.preyY, leash = e.def.aggro + ChaseLeash;
                if (lx * lx + ly * ly > leash * leash) { e.preyUnit = null; e.noUnitUntil = tick + ChaseRest; e.preyTick = tick; }
            }

            if (e.def.flying)
            {
                // 수송체: 아군 건물 곁에 닿으면 실은 적을 내려놓는다
                if (e.cargo > 0 && NearestBuilding(e.x, e.y, 2.5f) != null)
                {
                    SpawnNear(e.def.carries, e.x, e.y, e.cargo);
                    e.cargo = 0;
                }
                // 표적 성향: 그 종류의 가장 가까운 건물로 날아간다 (없으면 핵)
                Building dest = core;
                if (e.def.prefers != EnemyPrefer.Core)
                {
                    if (e.goal == null || e.goal.dead || tick >= e.goalTick) { e.goal = NearestOfKind(e.x, e.y, float.MaxValue, e.def.prefers); e.goalTick = tick + 60; }
                    if (e.goal != null) dest = e.goal;
                }
                if (e.preyUnit != null) { MoveToward(e, e.preyUnit.x, e.preyUnit.y, speed); return; }
                if (e.preyBuild != null && e.def.prefers == EnemyPrefer.Core) dest = e.preyBuild;
                if (dest.DistTo(e.x, e.y) <= e.def.range) { Attack(e, dest); return; }
                MoveToward(e, dest.Cx, dest.Cy, speed);
                return;
            }

            // 지상: 먹잇감이 손에 닿으면 치고, 아니면 곧게 걸어갈 수 있을 때만 다가간다 (벽이나 다른 건물에 막히면 가던 길을 간다)
            if (e.preyBuild != null && e.preyBuild.DistTo(e.x, e.y) <= e.def.range) { Attack(e, e.preyBuild); return; }
            if (e.preyUnit != null || e.preyBuild != null)
            {
                float px = e.preyUnit != null ? e.preyUnit.x : Math.Max(e.preyBuild.x, Math.Min(e.x, e.preyBuild.x + e.preyBuild.def.size));
                float py = e.preyUnit != null ? e.preyUnit.y : Math.Max(e.preyBuild.y, Math.Min(e.y, e.preyBuild.y + e.preyBuild.def.size));
                if (WalkStep(e, px, py, speed)) return;
                e.preyUnit = null; e.preyBuild = null;
            }

            int tx = (int)e.x, ty = (int)e.y;
            Building target = null;
            if (e.def.siege)
            {
                if (e.def.prefers != EnemyPrefer.Core) target = NearestOfKind(e.x, e.y, e.def.range, e.def.prefers);
                if (target == null) target = NearestBuilding(e.x, e.y, e.def.range);
            }
            int next = field.Next(tx, ty);
            if (target == null && next >= 0)
            {
                var nb = world.tiles[next].build;
                if (nb != null && !nb.def.enemy && nb.DistTo(e.x, e.y) <= e.def.range) target = nb;
            }
            if (target == null) target = world.BuildAt(tx, ty);   // 서 있는 자리에 건물이 지어졌다
            if (target != null && target.def.enemy) target = null;
            if (target != null) { Attack(e, target); return; }

            if (next < 0) { MoveToward(e, core.Cx, core.Cy, speed); return; }
            MoveToward(e, next % world.W + 0.5f + e.ox, next / world.W + 0.5f + e.oy, speed);
        }

        /// 적이 가는 길 둘레에서 먼저 칠 것을 고른다: aggro 안의 가장 가까운 아군 유닛(때릴 수 있는 것) 또는 건물. 방벽은 고르지 않는다 (길을 막을 때만 친다)
        public const float ChaseLeash = 1.5f, TurretLure = 0.4f;
        public const int ChaseRest = 3 * 60;

        void FindPrey(Enemy e)
        {
            e.preyTick = tick + 20;
            var was = e.preyUnit;
            e.preyUnit = null; e.preyBuild = null;
            float r = e.def.aggro;
            if (r <= 0) return;
            var u = tick >= e.noUnitUntil ? NearestUnit(e.x, e.y, r, e.def.HitsAir) : null;
            float ud = u != null ? (float)Math.Sqrt((u.x - e.x) * (u.x - e.x) + (u.y - e.y) * (u.y - e.y)) : float.MaxValue;
            // 건물은 가장 가까운 것부터, 포탑은 TurretLure 만큼 더 가까운 것으로 친다
            Building best = null; float bd = float.MaxValue;
            var bs = world.buildings;
            for (int i = 0; i < bs.Count; i++)
            {
                var b = bs[i];
                if (b.def.enemy || b.dead || b.def.kind == BlockKind.Wall) continue;
                float d = b.DistTo(e.x, e.y);
                if (d > r) continue;
                if (b.def.kind == BlockKind.Turret) d -= TurretLure;
                if (d < bd) { bd = d; best = b; }
            }
            if (u != null && ud <= bd)
            {
                e.preyUnit = u;
                if (was != u) { e.preyX = e.x; e.preyY = e.y; }   // 새로 쫓기 시작했다
            }
            else e.preyBuild = best;
        }

        /// 지상의 적이 (tx, ty) 쪽으로 곧게 한 걸음 간다. 디딜 칸이 벽이거나 건물이면 가지 않고 false
        bool WalkStep(Enemy e, float tx, float ty, float speed)
        {
            float dx = tx - e.x, dy = ty - e.y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d < 0.0001f) return true;
            float s = Math.Min(speed, d);
            float nx = e.x + dx / d * s, ny = e.y + dy / d * s;
            // 반 칸 앞까지 미리 본다 (벽 모서리에 비비지 않게)
            float lx = e.x + dx / d * Math.Min(d, 0.5f), ly = e.y + dy / d * Math.Min(d, 0.5f);
            int cx = (int)Math.Floor(lx), cy = (int)Math.Floor(ly);
            if (!world.In(cx, cy)) return false;
            var t = world.tiles[world.Idx(cx, cy)];
            if (t.wall || (t.build != null && t.build != e.preyBuild)) return false;
            if (t.build == e.preyBuild && t.build != null) return true;   // 닿았다: 다음 틱에 친다
            e.vx = nx - e.x; e.vy = ny - e.y;
            e.x = nx; e.y = ny;
            return true;
        }

        static void MoveToward(Enemy e, float tx, float ty, float speed)
        {
            float dx = tx - e.x, dy = ty - e.y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d < 0.0001f) return;
            float s = Math.Min(speed, d);
            e.vx = dx / d * s; e.vy = dy / d * s;
            e.x += e.vx; e.y += e.vy;
        }

        void Attack(Enemy e, Building b)
        {
            if (e.cooldown > 0) return;
            e.cooldown = e.def.attackTicks;
            float dmg = Shielded(b, Armored(e.def.damage * EnemyDamageMul, b.def.armor));
            b.health -= dmg;
            b.hitFlash = 6;
            fx.Add(new Fx { kind = FxKind.EnemyAttack, x = b.Cx, y = b.Cy, r = 0.4f, color = e.def.color });
        }

        /// 그 종류(공장·포탑)의 다 지어진 아군 건물 중 가장 가까운 것
        Building NearestOfKind(float x, float y, float range, EnemyPrefer kind)
        {
            Building best = null; float bd = range;
            var bs = world.buildings;
            for (int i = 0; i < bs.Count; i++)
            {
                var b = bs[i];
                if (b.def.enemy || !b.Built || b.dead) continue;
                if (kind == EnemyPrefer.Factory ? b.def.kind != BlockKind.Factory : b.def.kind != BlockKind.Turret) continue;
                float d = b.DistTo(x, y);
                if (d <= bd) { bd = d; best = b; }
            }
            return best;
        }

        /// 수리병: 둘레의 다른 적을 고친다 (0.25초마다)
        void HealEnemies(Enemy h)
        {
            float amount = h.def.heal * 0.25f * rules.enemyHealthMul, r2 = h.def.healRange * h.def.healRange;
            bool any = false;
            for (int i = 0; i < enemies.Count; i++)
            {
                var o = enemies[i];
                if (o == h || o.dead || o.def.boss || o.health >= o.maxHealth) continue;   // 보스는 고치지 못한다
                float dx = o.x - h.x, dy = o.y - h.y;
                if (dx * dx + dy * dy > r2) continue;
                o.health = Math.Min(o.maxHealth, o.health + amount);
                any = true;
            }
            if (any && (tick + h.serial) % 60 == 0) fx.Add(new Fx { kind = FxKind.Spawn, x = h.x, y = h.y, r = h.def.healRange, color = h.def.color });
        }

        /// 역장 발생기의 범위 안에 있는 건물은 역장이 피해를 먼저 받는다. 남은 피해를 돌려준다
        float Shielded(Building b, float dmg)
        {
            var bs = world.buildings;
            for (int i = 0; i < bs.Count && dmg > 0; i++)
            {
                if (!(bs[i] is SupportBuilding s) || s.def.shield <= 0 || !s.Built || s.shieldHp <= 0 || !s.Covers(b)) continue;
                dmg = s.Absorb(dmg);
                fx.Add(new Fx { kind = FxKind.Hit, x = b.Cx, y = b.Cy, r = b.def.size * 0.7f, color = s.def.color });
            }
            return dmg;
        }

        Building NearestBuilding(float x, float y, float range)
        {
            Building best = null; float bd = range;
            var bs = world.buildings;
            for (int i = 0; i < bs.Count; i++)
            {
                if (bs[i].def.enemy) continue;
                float d = bs[i].DistTo(x, y);
                if (d <= bd) { bd = d; best = bs[i]; }
            }
            return best;
        }

        void DestroyBuilding(Building b)
        {
            fx.Add(new Fx { kind = FxKind.BuildDestroyed, x = b.Cx, y = b.Cy, r = b.def.size * 0.8f, color = b.def.color });
            bool core = b is CoreBuilding;
            world.Remove(b);
            if (core) world.core.dead = true;
            else if (!b.def.enemy) AddRuin(b);
            if (b.def.enemy)
            {
                // 적 건물: 전리품이 들어오고, 적 핵이었다면 그 출현 지점이 사라진다
                float bm = abyss.bountyMul * world.mods.bountyMul * rules.bountyMul;
                foreach (var s in b.def.bounty)
                {
                    int n = Math.Max(1, (int)Math.Round(s.amount * bm));
                    Core.Add(s.item, n);
                    looted[s.item] += n;
                }
                if (b is EnemyCoreBuilding) world.spawns.Remove(world.Idx(b.x + 1, b.y + 1));
                nav.Clear(); skim.Clear();
                structuresDestroyed++;
            }
            fieldDirty = true;
            BuildingRemoved?.Invoke(b);
        }

        // ───────── 포탑·탄 ─────────

        public Enemy NearestEnemy(float x, float y, float range, bool air, bool ground)
        {
            Enemy best = null; float bd = float.MaxValue;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead || (e.def.flying ? !air : !ground)) continue;
                float dx = e.x - x, dy = e.y - y;
                float d = dx * dx + dy * dy;
                float lim = range + e.def.radius;
                if (d <= lim * lim && d < bd) { bd = d; best = e; }
            }
            return best;
        }

        /// 포탑의 표적 고르기. Nearest = 가장 가까운 적, Strongest = 남은 체력이 가장 많은 적,
        /// First = 핵에 가장 가까이 온 적, Air = 비행체부터(없으면 가장 가까운 적)
        public Enemy PickEnemy(float x, float y, float range, bool air, bool ground, TargetMode mode)
        {
            if (mode == TargetMode.Nearest) return NearestEnemy(x, y, range, air, ground);
            if (mode == TargetMode.Air)
            {
                var f = air ? NearestEnemy(x, y, range, true, false) : null;
                return f ?? NearestEnemy(x, y, range, air, ground);
            }
            Enemy best = null; float bk = float.MaxValue, bd = float.MaxValue;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead || (e.def.flying ? !air : !ground)) continue;
                float dx = e.x - x, dy = e.y - y;
                float d = dx * dx + dy * dy;
                float lim = range + e.def.radius;
                if (d > lim * lim) continue;
                float key = mode == TargetMode.Strongest ? -e.health : CoreDistance(e);
                if (key < bk || (key == bk && d < bd)) { bk = key; bd = d; best = e; }
            }
            return best;
        }

        /// 적이 핵까지 가야 할 길의 길이 (지상은 길 비용, 비행체는 곧은 거리)
        float CoreDistance(Enemy e)
        {
            var core = Core;
            if (core == null) return 0;
            if (!e.def.flying)
            {
                int tx = (int)e.x, ty = (int)e.y;
                if (world.In(tx, ty)) { float c = field.cost[world.Idx(tx, ty)]; if (c < float.MaxValue) return c; }
            }
            return core.DistTo(e.x, e.y);
        }

        public void Fire(TurretBuilding t, float tx, float ty)
        {
            var bl = t.Ammo.bullet;
            var b = FireFrom(t.Cx, t.Cy, tx, ty, bl, bl.damage * world.mods.damageMul, 0.5f);
            if (bl.homing > 0) b.target = t.target;
        }

        /// 포탑과 유닛이 함께 쓰는 발사
        Bullet FireFrom(float sx, float sy, float tx, float ty, BulletDef d, float damage, float muzzle)
        {
            float dx = tx - sx, dy = ty - sy;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) { dx = 1; dy = 0; len = 1; }
            var b = new Bullet { def = d, x = sx, y = sy, vx = dx / len * d.speed, vy = dy / len * d.speed, damage = damage };
            b.life = d.artillery ? Math.Max(1, (int)(len / d.speed)) : d.lifeTicks;
            if (d.pierce > 0) b.hit = new List<Enemy>();
            bullets.Add(b);
            fx.Add(new Fx { kind = FxKind.Shoot, x = sx + dx / len * muzzle, y = sy + dy / len * muzzle, x2 = sx + dx / len * (muzzle + 1), y2 = sy + dy / len * (muzzle + 1), r = 0.2f, color = d.color });
            return b;
        }

        /// 방어력: 피해에서 방어력만큼 빼되, 최소 20% 는 들어간다
        public static float Armored(float dmg, float armor) => Math.Max(dmg - armor, dmg * MinArmorDamage);

        void UpdateBullet(Bullet b)
        {
            if (b.target != null && !b.target.dead && b.def.homing > 0)
            {
                // 유도탄: 속력은 그대로 두고 겨눈 적 쪽으로 방향만 튼다
                float cur = (float)Math.Atan2(b.vy, b.vx), want = (float)Math.Atan2(b.target.y - b.y, b.target.x - b.x);
                float diff = want - cur;
                while (diff > Math.PI) diff -= 2 * (float)Math.PI;
                while (diff < -Math.PI) diff += 2 * (float)Math.PI;
                cur += Math.Max(-b.def.homing, Math.Min(b.def.homing, diff));
                b.vx = (float)Math.Cos(cur) * b.def.speed; b.vy = (float)Math.Sin(cur) * b.def.speed;
            }
            b.x += b.vx; b.y += b.vy;
            b.life--;
            var d = b.def;
            if (d.artillery)
            {
                if (b.life > 0) return;
                b.dead = true;
                Splash(b.x, b.y, d.splash, b.damage, false);
                StatusAround(b.x, b.y, d.splash, d);
                if (d.burn > 0) BurnAround(b.x, b.y, d.splash, d, false);
                if (d.heal > 0) HealBlast(b.x, b.y, d.splash, d.heal * b.damage / d.damage);
                if (b.fromUnit) SplashBuildings(b.x, b.y, d.splash, b.damage);
                fx.Add(new Fx { kind = FxKind.Explode, x = b.x, y = b.y, r = d.splash, color = d.color });
                if (d.frag != null)
                {
                    // 파편: 폭발 범위 가장자리에서 바깥으로 고르게 튄다. 범위 안의 적은 폭발만 맞는다 (피해는 원래 탄의 배수를 따른다)
                    float scale = b.damage / d.damage, a0 = (b.x + b.y) * 1.7f;
                    for (int k = 0; k < d.fragCount; k++)
                    {
                        float a = a0 + k * 6.2832f / d.fragCount;
                        float ex = b.x + (float)Math.Cos(a) * d.splash, ey = b.y + (float)Math.Sin(a) * d.splash;
                        var fb = FireFrom(ex, ey, ex + (float)Math.Cos(a), ey + (float)Math.Sin(a), d.frag, d.frag.damage * scale, 0.1f);
                        fb.fromUnit = b.fromUnit;
                    }
                }
                return;
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead) continue;
                float dx = e.x - b.x, dy = e.y - b.y, lim = e.def.radius + 0.12f;
                if (dx * dx + dy * dy > lim * lim) continue;
                if (b.hit != null) { if (b.hit.Contains(e)) continue; b.hit.Add(e); }
                Drain(b, Math.Min(e.health, Armored(b.damage, e.Armor)));
                Damage(e, b.damage * (d.chain > 0 ? Shock(e) : 1f), d.pierceArmor);
                if (d.burn > 0) Ignite(e, d);
                if (d.splash > 0) StatusAround(b.x, b.y, d.splash, d); else Status(e, d);
                fx.Add(new Fx { kind = FxKind.Hit, x = b.x, y = b.y, r = 0.25f, color = d.color });
                if (d.splash > 0) Splash(b.x, b.y, d.splash, b.damage * 0.5f, true);
                if (d.knockback > 0) Knock(b, e);
                if (d.chain > 0) Chain(b, e);
                if (b.hit == null || b.hit.Count > d.pierce) { b.dead = true; return; }
            }
            // 유닛이 적 건물을 겨눠 쏜 탄은 그 건물에 닿으면 맞는다
            if (b.build != null && !b.build.dead && b.build.DistTo(b.x, b.y) <= 0.05f)
            {
                Drain(b, Math.Min(b.build.health, Armored(b.damage, b.build.def.armor)));
                HurtBuilding(b.build, b.damage, d.pierceArmor);
                fx.Add(new Fx { kind = FxKind.Hit, x = b.x, y = b.y, r = 0.25f, color = d.color });
                if (d.splash > 0) SplashBuildings(b.x, b.y, d.splash, b.damage * 0.5f, b.build);
                b.dead = true;
                return;
            }
            if (b.life <= 0) b.dead = true;   // 탄은 벽 위를 넘어간다
        }

        /// 탄이 실제로 준 피해(방어력을 뺀 값, 남은 체력까지만)의 일부를 쏜 유닛이 되찾는다
        void Drain(Bullet b, float dealt)
        {
            var o = b.owner;
            if (o == null || o.dead || dealt <= 0 || o.health >= o.maxHealth) return;
            o.health = Math.Min(o.maxHealth, o.health + dealt * o.def.drain);
            fx.Add(new Fx { kind = FxKind.Spawn, x = o.x, y = o.y, r = o.def.radius * 1.6f, color = b.def.color });
        }

        public readonly List<Mine> mines = new List<Mine>();
        public const float MineTrigger = 0.55f;
        public const int MineArmTicks = 30;

        /// 지뢰: 지상의 적이 밟으면 터져 범위 안 지상의 적을 때린다 (둔화·화상이 있으면 함께 건다)
        void UpdateMines()
        {
            for (int k = 0; k < mines.Count; k++)
            {
                var m = mines[k];
                if (m.arm > 0) { m.arm--; continue; }
                bool hit = false;
                for (int i = 0; i < enemies.Count && !hit; i++)
                {
                    var e = enemies[i];
                    if (e.dead || e.def.flying) continue;
                    float dx = e.x - m.x, dy = e.y - m.y, lim = MineTrigger + e.def.radius;
                    hit = dx * dx + dy * dy <= lim * lim;
                }
                if (!hit) continue;
                m.dead = true;
                Splash(m.x, m.y, m.def.splash, m.damage, false);
                StatusAround(m.x, m.y, m.def.splash, m.def);
                if (m.def.burn > 0) BurnAround(m.x, m.y, m.def.splash, m.def, false);
                fx.Add(new Fx { kind = FxKind.Explode, x = m.x, y = m.y, r = m.def.splash, color = m.def.color });
            }
            mines.RemoveAll(m => m.dead);
        }

        readonly List<Enemy> chainHit = new List<Enemy>();

        /// 전격: 맞은 적에서 가장 가까운 다른 적으로 건너뛰며 줄어든 피해를 준다 (지상·공중 가리지 않는다)
        void Chain(Bullet b, Enemy first)
        {
            var d = b.def;
            chainHit.Clear(); chainHit.Add(first);
            var cur = first;
            float dmg = b.damage;
            for (int j = 0; j < d.chain; j++)
            {
                Enemy next = null; float bd = d.chainRange * d.chainRange;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e.dead || chainHit.Contains(e)) continue;
                    float dx = e.x - cur.x, dy = e.y - cur.y, dd = dx * dx + dy * dy;
                    if (dd <= bd) { bd = dd; next = e; }
                }
                if (next == null) break;
                dmg *= 0.7f;
                chainHit.Add(next);
                Damage(next, dmg * Shock(next));
                fx.Add(new Fx { kind = FxKind.Chain, x = cur.x, y = cur.y, x2 = next.x, y2 = next.y, r = 0.3f, color = d.color });
                fx.Add(new Fx { kind = FxKind.Hit, x = next.x, y = next.y, r = 0.3f, color = d.color });
                cur = next;
            }
            chainHit.Clear();
        }

        /// 수리 폭탄: 범위 안의 다 지어진 아군 건물을 고친다 (유닛은 고치지 않는다)
        void HealBlast(float x, float y, float rad, float amount)
        {
            var bs = world.buildings;
            for (int i = 0; i < bs.Count; i++)
            {
                var b = bs[i];
                if (b.def.enemy || !b.Built || b.health >= b.maxHealth || b.DistTo(x, y) > rad) continue;
                b.health = Math.Min(b.maxHealth, b.health + amount);
            }
            fx.Add(new Fx { kind = FxKind.Spawn, x = x, y = y, r = rad, color = 0x8CE99A });
        }

        /// 충격탄: 맞은 적과 범위 안의 적을 탄이 날아가던 쪽으로 밀어낸다
        void Knock(Bullet b, Enemy direct)
        {
            var d = b.def;
            float dirX = b.vx / d.speed, dirY = b.vy / d.speed;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead) continue;
                if (e != direct)
                {
                    float dx = e.x - b.x, dy = e.y - b.y, lim = d.splash + e.def.radius;
                    if (d.splash <= 0 || dx * dx + dy * dy > lim * lim) continue;
                }
                Push(e, dirX, dirY, d.knockback);
            }
        }

        /// 적을 밀어낸다. 보스는 절반만 밀리고, 지상의 적은 벽이나 건물 안으로는 밀리지 않는다
        void Push(Enemy e, float dirX, float dirY, float dist)
        {
            if (e.def.boss) dist *= 0.5f;
            float nx = e.x + dirX * dist, ny = e.y + dirY * dist;
            int tx = (int)Math.Floor(nx), ty = (int)Math.Floor(ny);
            if (!world.In(tx, ty)) return;
            if (!e.def.flying)
            {
                var t = world.tiles[world.Idx(tx, ty)];
                if (t.wall || t.build != null) return;
            }
            e.x = nx; e.y = ny;
        }

        /// 적을 느리게 한다. 더 센 둔화가 걸려 있으면 그것을 지키고, 보스는 절반만 느려진다
        void Slow(Enemy e, BulletDef d)
        {
            if (e.dead) return;
            float mul = e.def.boss ? 1f - (1f - d.slow) * 0.5f : d.slow;
            if (e.slowTicks > 0 && e.slowMul < mul) return;
            e.slowMul = mul; e.slowTicks = d.slowTicks;
        }

        public const float ShockWet = 1.5f, AcidBurn = 1.5f;

        /// 젖은 적은 전격에 1.5배를 받는다
        float Shock(Enemy e) => e.wetTicks > 0 ? ShockWet : 1f;

        /// 적을 불태운다. 더 센 불이 붙어 있으면 그것을 지킨다. 젖은 적은 불이 붙지 않고 젖음만 사라지며, 산성에 젖은 적은 1.5배 오래 탄다
        void Ignite(Enemy e, BulletDef d)
        {
            if (e.dead) return;
            if (e.wetTicks > 0) { e.wetTicks = 0; return; }
            if (e.burnTicks > 0 && e.burnDps > d.burn) return;
            e.burnDps = d.burn; e.burnTicks = e.acidTicks > 0 ? (int)(d.burnTicks * AcidBurn) : d.burnTicks;
        }

        /// 탄이 거는 상태 이상: 둔화, 산성(방어력 감소), 젖음. 보스는 둔화와 산성이 절반만 듣는다
        void Status(Enemy e, BulletDef d)
        {
            if (e.dead) return;
            if (d.slow > 0) Slow(e, d);
            if (d.acid > 0)
            {
                float mul = e.def.boss ? 1f - (1f - d.acid) * 0.5f : d.acid;
                if (!(e.acidTicks > 0 && e.acidMul < mul)) { e.acidMul = mul; e.acidTicks = d.acidTicks; }
            }
            if (d.wetTicks > 0)
            {
                e.wetTicks = Math.Max(e.wetTicks, d.wetTicks);
                e.burnTicks = 0;   // 젖으면 불이 꺼진다
            }
        }

        /// 범위 안의 적에게 상태 이상을 건다 (곡사탄·지뢰·자폭은 지상만)
        void StatusAround(float x, float y, float rad, BulletDef d)
        {
            if (d.slow <= 0 && d.acid <= 0 && d.wetTicks <= 0) return;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead || (e.def.flying && (d.artillery || d.speed <= 0))) continue;
                float dx = e.x - x, dy = e.y - y, lim = rad + e.def.radius;
                if (dx * dx + dy * dy <= lim * lim) Status(e, d);
            }
        }

        /// 범위 안의 적을 불태운다
        void BurnAround(float x, float y, float rad, BulletDef d, bool air)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead || (e.def.flying && !air)) continue;
                float dx = e.x - x, dy = e.y - y, lim = rad + e.def.radius;
                if (dx * dx + dy * dy <= lim * lim) Ignite(e, d);
            }
        }

        void Splash(float x, float y, float rad, float dmg, bool air)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead || (e.def.flying && !air)) continue;
                float dx = e.x - x, dy = e.y - y, lim = rad + e.def.radius;
                float d2 = dx * dx + dy * dy;
                if (d2 > lim * lim) continue;
                float falloff = 1f - 0.5f * Math.Min(1f, (float)Math.Sqrt(d2) / rad);
                Damage(e, dmg * falloff);
            }
        }

        void Damage(Enemy e, float dmg, bool ignoreArmor = false)
        {
            if (e.dead) return;
            float hit = ignoreArmor ? dmg : Armored(dmg, e.Armor);
            e.barrierWait = e.def.barrierDelay;
            if (e.barrier > 0)
            {
                // 보호막이 먼저 닳는다
                float take = Math.Min(e.barrier, hit);
                e.barrier -= take; hit -= take;
            }
            e.health -= hit;
            e.hitFlash = 5;
            if (e.health > 0) return;
            e.dead = true;
            kills++;
            if (e.def.splitInto != null) SpawnNear(e.def.splitInto, e.x, e.y, e.def.splitCount);
            // 전리품은 핵으로 바로 들어온다
            float bm = abyss.bountyMul * world.mods.bountyMul * rules.bountyMul * (e.elite ? EliteBounty : 1f);
            foreach (var s in e.def.bounty)
            {
                int n = Math.Max(1, (int)Math.Round(s.amount * bm));
                Core.Add(s.item, n);
                looted[s.item] += n;
            }
            fx.Add(new Fx { kind = FxKind.EnemyDeath, x = e.x, y = e.y, r = e.def.radius * 2.2f, color = e.def.color });
        }
    }
}
