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

    public enum FxKind { Hit, Explode, EnemyDeath, Spawn, BuildDestroyed, EnemyAttack, Shoot }

    public struct Fx
    {
        public FxKind kind;
        public float x, y, r;
        public uint color;
    }

    public enum SimState { Playing, Won, Lost }
    public enum PlaceResult { Ok, OutOfBounds, Blocked, NoResources, NearSpawn, Locked, NeedsWater }

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

        struct Pending { public int tick, tile; public EnemyDef def; }
        readonly List<Pending> pending = new List<Pending>();
        readonly Rng rng;
        bool fieldDirty = true;
        int serial;

        /// loadout = 기지에서 가져온 자원 [철광, 황석, 심연 결정]
        public Sim(int abyssId, int region, int[] loadout = null, SimMods mods = null)
        {
            rules = RegionRules.For(abyssId, region);
            abyss = Content.Abysses[abyssId];
            world = MapGen.Generate(rules, abyss);
            MakeLakes();
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
        public float EnemyDamageMul => 1.3f + 0.7f * rules.depth;
        public bool AllWavesSent => !rules.assault && wave >= rules.winWave;
        public int PendingSpawns => pending.Count;

        // ───────── 건설 ─────────

        public bool IsUnlocked(BlockDef def) => def.tech < 0 || world.mods.tech[def.tech];

        public PlaceResult CanPlace(BlockDef def, int x, int y, bool checkCost = true)
        {
            if (def.enemy) return PlaceResult.Blocked;
            if (!IsUnlocked(def)) return PlaceResult.Locked;
            int safe = rules.assault ? EnemyCoreSafeRadius : SpawnSafeRadius;
            for (int dy = 0; dy < def.size; dy++)
                for (int dx = 0; dx < def.size; dx++)
                {
                    var t = world.At(x + dx, y + dy);
                    if (t == null) return PlaceResult.OutOfBounds;
                    if (t.wall || t.build != null) return PlaceResult.Blocked;
                    foreach (int s in world.spawns)
                    {
                        int sx = s % world.W, sy = s / world.W;
                        if (Math.Abs(sx - (x + dx)) <= safe && Math.Abs(sy - (y + dy)) <= safe) return PlaceResult.NearSpawn;
                    }
                }
            if (def.needsWater && !NearWater(x, y, def.size)) return PlaceResult.NeedsWater;
            if (checkCost && !CanAfford(def)) return PlaceResult.NoResources;
            return PlaceResult.Ok;
        }

        /// 짓고 있는 건물들이 앞으로 가져갈 자원을 빼고도 이 건물 비용이 남는가
        public bool CanAfford(BlockDef def)
        {
            var need = Committed();
            foreach (var c in def.cost) need[c.item] += c.amount;
            for (int i = 0; i < need.Length; i++) if (Core.items[i] < need[i]) return false;
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
        public PlaceResult Place(BlockDef def, int x, int y)
        {
            if (state != SimState.Playing) return PlaceResult.Blocked;
            var res = CanPlace(def, x, y);
            if (res != PlaceResult.Ok) return res;
            var b = Building.Create(def);
            b.x = x; b.y = y;
            b.maxHealth = def.health * world.mods.healthMul;
            b.health = b.maxHealth * Building.SiteHealth;
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
            if (b == null || b is CoreBuilding || b.def.enemy || state != SimState.Playing) return false;
            if (!b.Built) { for (int k = 0; k < b.paid.Length; k++) Core.Add(b.def.cost[k].item, b.paid[k]); }
            else Core.Refund(b.def.cost, RefundRatio * (b.health / b.maxHealth));
            world.Remove(b);
            fieldDirty = true;
            BuildingRemoved?.Invoke(b);
            return true;
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
                if (pending[i].tick <= tick) { Spawn(pending[i].def, pending[i].tile); pending.RemoveAt(i); }

            if (fieldDirty) { field.Compute(); fieldDirty = false; }

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
                    pending.Add(new Pending { tick = tick + 1 + order * gap, def = g.enemy, tile = world.spawns[rng.Range(0, world.spawns.Count - 1)] });
                    order++;
                }
                if (n > 0) order += 2;
            }
        }

        void Spawn(EnemyDef def, int s)
        {
            if (rules.assault && !(world.tiles[s].build is EnemyCoreBuilding)) return;   // 그 사이에 적 핵이 부서졌다
            float sx = s % world.W + 0.5f, sy = s / world.W + 0.5f;
            var e = new Enemy { def = def, serial = ++serial };
            e.maxHealth = e.health = def.health * rules.enemyHealthMul * (1 + 0.12f * (wave - 1));
            e.armor = def.armor * (1 + 2f * rules.depth);
            e.ox = rng.Range(-0.25f, 0.25f); e.oy = rng.Range(-0.25f, 0.25f);
            if (def.flying) { e.x = sx + rng.Range(-4f, 4f); e.y = world.H - 0.5f; }
            else { e.x = sx + rng.Range(-1.2f, 1.2f); e.y = sy + rng.Range(-1.2f, 1.2f); }
            enemies.Add(e);
            fx.Add(new Fx { kind = FxKind.Spawn, x = e.x, y = e.y, r = def.radius * 2, color = def.color });
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
            e.vx = e.vy = 0;
            var core = Core;
            if (core == null) return;
            float speed = e.def.speed * abyss.enemySpeedMul;
            if (e.slowTicks > 0) { e.slowTicks--; speed *= e.slowMul; }

            // 손이 닿는 곳에 아군 유닛이 있으면 멈춰서 그 유닛부터 친다
            var near = NearestUnit(e.x, e.y, e.def.range + 0.3f, e.def.HitsAir);
            if (near != null) { AttackUnit(e, near); return; }

            if (e.def.flying)
            {
                if (core.DistTo(e.x, e.y) <= e.def.range) { Attack(e, core); return; }
                MoveToward(e, core.Cx, core.Cy, speed);
                return;
            }

            int tx = (int)e.x, ty = (int)e.y;
            Building target = null;
            if (e.def.siege) target = NearestBuilding(e.x, e.y, e.def.range);
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
            b.health -= Armored(e.def.damage * EnemyDamageMul, b.def.armor);
            b.hitFlash = 6;
            fx.Add(new Fx { kind = FxKind.EnemyAttack, x = b.Cx, y = b.Cy, r = 0.4f, color = e.def.color });
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
                float bm = abyss.bountyMul * world.mods.bountyMul;
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

        public void Fire(TurretBuilding t, float tx, float ty)
        {
            FireFrom(t.Cx, t.Cy, tx, ty, t.def.bullet, t.def.bullet.damage * world.mods.damageMul, 0.5f);
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
            fx.Add(new Fx { kind = FxKind.Shoot, x = sx + dx / len * muzzle, y = sy + dy / len * muzzle, r = 0.2f, color = d.color });
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
                if (d.slow > 0) SlowAround(b.x, b.y, d.splash, d);
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
                Drain(b, Math.Min(e.health, Armored(b.damage, e.armor)));
                Damage(e, b.damage, d.pierceArmor);
                if (d.burn > 0 && !e.dead && (e.burnTicks <= 0 || e.burnDps <= d.burn)) { e.burnDps = d.burn; e.burnTicks = d.burnTicks; }
                if (d.slow > 0) { if (d.splash > 0) SlowAround(b.x, b.y, d.splash, d); else Slow(e, d); }
                fx.Add(new Fx { kind = FxKind.Hit, x = b.x, y = b.y, r = 0.25f, color = d.color });
                if (d.splash > 0) Splash(b.x, b.y, d.splash, b.damage * 0.5f, true);
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

        /// 적을 느리게 한다. 더 센 둔화가 걸려 있으면 그것을 지키고, 보스는 절반만 느려진다
        void Slow(Enemy e, BulletDef d)
        {
            if (e.dead) return;
            float mul = e.def.boss ? 1f - (1f - d.slow) * 0.5f : d.slow;
            if (e.slowTicks > 0 && e.slowMul < mul) return;
            e.slowMul = mul; e.slowTicks = d.slowTicks;
        }

        /// 범위 안의 적을 느리게 한다 (곡사탄은 지상만)
        void SlowAround(float x, float y, float rad, BulletDef d)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead || (e.def.flying && d.artillery)) continue;
                float dx = e.x - x, dy = e.y - y, lim = rad + e.def.radius;
                if (dx * dx + dy * dy <= lim * lim) Slow(e, d);
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
            e.health -= ignoreArmor ? dmg : Armored(dmg, e.armor);
            e.hitFlash = 5;
            if (e.health > 0 || e.dead) return;
            e.dead = true;
            kills++;
            // 전리품은 핵으로 바로 들어온다
            float bm = abyss.bountyMul * world.mods.bountyMul;
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
