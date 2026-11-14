using System;
using System.Collections.Generic;

namespace AbyssDefense.Sim
{
    /// 공장에서 나온 아군 유닛 하나
    public sealed class Unit
    {
        public UnitDef def;
        public float x, y, vx, vy;
        public float health, maxHealth;
        public float aim;                // 그림용: 보고 있는 방향 (라디안)
        public int reload, retarget, hitFlash, serial;
        public Enemy target;
        public Building tbuild;          // 겨누고 있는 적 건물 (적 거점 지역)
        public FactoryBuilding home;     // 태어난 공장. 그 공장의 집결 지점을 따른다
        public float rallyX, rallyY;
        public float ox, oy;             // 집결 지점에서 흩어져 서는 정도
        public bool dead;
        public Unit carrier;             // 나를 싣고 가는 수송기
        public List<Unit> cargo;         // (수송기) 싣고 있는 유닛
        public Unit parent;              // (함재기) 나를 띄운 기함
        public int spawnTimer;           // (기함) 다음 함재기를 띄우기까지 쌓인 틱
        public int navalGoal = -1, navalTick;   // (해상) 지금 가고 있는 물 칸
        public int mineTile = -1, mineWork, mineLoad, mineItem, mineTrips;   // (채굴 드론) 캐는 벽 칸, 캔 틱, 싣고 있는 양과 자원, 나른 횟수
    }

    /// 적에게 부서진 아군 건물의 자리 (재건 드론이 다시 짓는다)
    public struct Ruin
    {
        public BlockDef def;
        public int x, y;
    }

    /// 방어: 핵 옆의 집결 지점을 지키며 그 둘레로 온 적만 쫓는다. 공격: 적 출현 지점 앞으로 나가 그 둘레의 적과 싸운다
    public enum UnitStance { Defend, Attack }

    /// 유닛의 길찾기. 지상: 벽과 적 건물이 막힌 것으로 본다 (아군 건물은 넘어 다닌다). 해상: 물 칸만 다닌다.
    /// 부양: 지상과 같되 호수도 지난다.
    /// 목표 칸마다 거리장을 만들어 두고 다시 쓴다
    public sealed class GroundNav
    {
        public const int Unreachable = int.MaxValue;
        readonly World w;
        readonly bool water, hover;
        readonly Dictionary<int, int[]> cache = new Dictionary<int, int[]>();
        readonly int[] queue;

        public GroundNav(World w, bool water = false, bool hover = false) { this.w = w; this.water = water; this.hover = hover; queue = new int[w.W * w.H]; }

        /// 적 건물이 부서지면 길이 달라진다
        public void Clear() { cache.Clear(); }

        /// goal 칸까지의 걸음 수 (닿지 않는 칸은 Unreachable)
        public int[] Field(int goal)
        {
            if (cache.TryGetValue(goal, out var f)) return f;
            if (cache.Count >= 200) cache.Clear();
            f = new int[w.W * w.H];
            for (int i = 0; i < f.Length; i++) f[i] = Unreachable;
            int head = 0, tail = 0;
            f[goal] = 0; queue[tail++] = goal;
            var goalBuild = w.tiles[goal].build;   // 목표가 적 건물이면 그 건물의 칸에서는 뻗어 나갈 수 있다
            while (head < tail)
            {
                int cur = queue[head++];
                int cx = cur % w.W, cy = cur / w.W;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + World.DX[d], ny = cy + World.DY[d];
                    if (!w.In(nx, ny)) continue;
                    int n = w.Idx(nx, ny);
                    if (f[n] != Unreachable) continue;
                    if (water) { if (!w.tiles[n].water) continue; }
                    else
                    {
                        if (w.tiles[n].wall && !(hover && w.tiles[n].water)) continue;
                        var nb = w.tiles[n].build;
                        if (nb != null && nb.def.enemy && nb != goalBuild) continue;
                    }
                    f[n] = f[cur] + 1;
                    queue[tail++] = n;
                }
            }
            cache[goal] = f;
            return f;
        }

        /// (x, y) 에서 가장 가까운 벽 아닌 칸 (해상이면 물 칸). 없으면 -1
        public int Open(int x, int y, int maxR = 8)
        {
            for (int r = 0; r <= maxR; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r || !w.In(x + dx, y + dy)) continue;
                        var t = w.tiles[w.Idx(x + dx, y + dy)];
                        if (water ? t.water : !t.wall || (hover && t.water)) return w.Idx(x + dx, y + dy);
                    }
            return -1;
        }
    }

    public sealed partial class Sim
    {
        public const float UnitLeash = 7f;           // 지키는 곳(방어 = 집결 지점, 공격 = 출현 지점)에서 이 거리 안의 적만 쫓는다
        public const float CommanderLeash = 4f, CommanderBuildRange = 4f, CommanderBuildBoost = 1.5f;
        public const int CommanderRespawnTicks = 20 * 60;

        public readonly List<Unit> units = new List<Unit>();
        public UnitStance stance = UnitStance.Defend;
        public int unitsBuilt, unitsLost, structuresDestroyed;
        public readonly int[] mined = new int[Content.Items.Length];   // 채굴 드론이 핵으로 나른 자원
        public const int MaxRuins = 40;
        /// 재건 드론이 다시 지을 자리. 부서진 순서대로 짓는다
        public readonly List<Ruin> ruins = new List<Ruin>();
        GroundNav nav, sea, skim;

        /// 걸어 다니는(또는 떠서 다니는) 유닛에게 막힌 칸인가. 부양 유닛은 호수를 지난다
        bool Blocks(UnitDef d, int x, int y) => world.Solid(x, y) && !(d.hover && world.tiles[world.Idx(x, y)].water);

        // 지휘기: 핵에서 나오는 코어 유닛. 한도를 차지하지 않고, 잃어도 잠시 뒤 다시 나온다. 플레이어가 갈 곳을 찍어 준다
        public Unit commander;
        public float commandX, commandY;
        public int commanderRespawn;
        public bool commanderEnabled = true;

        /// 남은 적 핵의 수 (적 거점 지역)
        public int EnemyCores
        {
            get
            {
                int n = 0;
                var bs = world.buildings;
                for (int i = 0; i < bs.Count; i++) if (bs[i] is EnemyCoreBuilding && !bs[i].dead && bs[i].health > 0) n++;
                return n;
            }
        }

        public int UnitCap => world.mods.unitCap;
        public bool IsUnlocked(UnitDef u) => u.tech < 0 || world.mods.tech[u.tech];

        /// 살아 있는 유닛과 만들고 있는 유닛이 차지한 한도
        public int SupplyUsed
        {
            get
            {
                int s = 0;
                for (int i = 0; i < units.Count; i++) if (!units[i].dead) s += units[i].def.supply;
                var bs = world.buildings;
                for (int i = 0; i < bs.Count; i++) if (bs[i] is FactoryBuilding f && f.making != null) s += f.making.supply;
                return s;
            }
        }

        // ───────── 맵: 호수 ─────────

        /// 벽 덩어리 일부를 호수로 바꾼다. 지상에서는 여전히 벽처럼 막혀 있고, 해상 유닛만 그 위를 다닌다
        void MakeLakes()
        {
            var r = new Rng(rules.seed * 131 + 5);
            var shore = new List<int>();
            for (int y = 2; y < world.H - 2; y++)
                for (int x = 2; x < world.W - 2; x++)
                {
                    if (!world.tiles[world.Idx(x, y)].wall) continue;
                    if (world.Solid(x + 1, y) && world.Solid(x - 1, y) && world.Solid(x, y + 1) && world.Solid(x, y - 1)) continue;
                    shore.Add(world.Idx(x, y));
                }
            if (shore.Count == 0) return;
            int lakes = 2 + world.W * world.H / 450;
            var q = new List<int>();
            for (int k = 0; k < lakes; k++)
            {
                int seed = shore[r.Range(0, shore.Count - 1)];
                int size = r.Range(12, 34);
                q.Clear(); q.Add(seed);
                world.tiles[seed].water = true;
                for (int head = 0; head < q.Count && q.Count < size; head++)
                {
                    int cx = q[head] % world.W, cy = q[head] / world.W;
                    for (int d = 0; d < 4 && q.Count < size; d++)
                    {
                        int nx = cx + World.DX[d], ny = cy + World.DY[d];
                        if (nx < 2 || ny < 2 || nx >= world.W - 2 || ny >= world.H - 2) continue;
                        var t = world.tiles[world.Idx(nx, ny)];
                        if (!t.wall || t.water) continue;
                        t.water = true;
                        q.Add(world.Idx(nx, ny));
                    }
                }
            }
        }

        // ───────── 생산·집결 ─────────

        public void SpawnUnit(UnitDef def, FactoryBuilding home)
        {
            var u = new Unit { def = def, home = home, serial = ++serial, x = home.Cx, y = home.Cy, aim = 1.5708f };
            if (def.naval)
            {
                int wt = sea.Open((int)home.Cx, (int)home.Cy, 4);
                if (wt >= 0) { u.x = wt % world.W + 0.5f; u.y = wt / world.W + 0.5f; }
            }
            u.maxHealth = u.health = def.health * world.mods.unitMul;
            u.rallyX = home.rallyX; u.rallyY = home.rallyY;
            u.ox = rng.Range(-0.9f, 0.9f); u.oy = rng.Range(-0.9f, 0.9f);
            if (def.capacity > 0) u.cargo = new List<Unit>();
            units.Add(u);
            unitsBuilt++;
            fx.Add(new Fx { kind = FxKind.Spawn, x = u.x, y = u.y, r = def.radius * 2.5f, color = def.color });
        }

        /// 건물 둘레 한 칸 안에 물이 있는가 (조선소를 지을 수 있는 자리)
        public bool NearWater(int x, int y, int size)
        {
            for (int dy = -1; dy <= size; dy++)
                for (int dx = -1; dx <= size; dx++)
                {
                    var t = world.At(x + dx, y + dy);
                    if (t != null && t.water) return true;
                }
            return false;
        }

        /// 공장을 놓을 때의 집결 지점: 적이 핵으로 오는 길에서 핵 바로 앞 칸 (방어 태세의 유닛은 핵을 지킨다). 조선소는 바로 옆 물
        void DefaultRally(FactoryBuilding f)
        {
            f.rallyX = f.Cx; f.rallyY = f.Cy;
            if (f.def.units != null && f.def.units.Length > 0 && f.def.units[0].naval)
            {
                int wt = sea.Open((int)f.Cx, (int)f.Cy, 4);
                if (wt >= 0) { f.rallyX = wt % world.W + 0.5f; f.rallyY = wt / world.W + 0.5f; }
                return;
            }
            if (world.spawns.Count == 0) return;
            int cur = world.spawns[0];
            for (int guard = 0; guard < world.tiles.Length && cur >= 0; guard++)
            {
                int x = cur % world.W, y = cur / world.W;
                var t = world.tiles[cur];
                if (world.core.DistTo(x + 0.5f, y + 0.5f) <= 2f && !t.wall && t.build == null)
                {
                    f.rallyX = x + 0.5f; f.rallyY = y + 0.5f;
                    return;
                }
                cur = field.Next(x, y);
            }
        }

        /// 공장의 집결 지점을 옮긴다. 지상 공장은 벽 위에, 조선소는 물 밖에 둘 수 없다
        public bool SetRally(FactoryBuilding f, float x, float y)
        {
            int tx = (int)Math.Floor(x), ty = (int)Math.Floor(y);
            if (f == null || f.dead || !world.In(tx, ty)) return false;
            var first = f.def.units != null && f.def.units.Length > 0 ? f.def.units[0] : null;
            if (first != null && first.naval) { if (!world.tiles[world.Idx(tx, ty)].water) return false; }
            else
            {
                bool free = false;   // 벽을 넘나드는 유닛을 하나라도 만드는 공장은 어디든 둘 수 있다
                if (f.def.units != null) foreach (var u in f.def.units) if (u.flying || u.legs) free = true;
                if (!free && world.Solid(tx, ty)) return false;
            }
            f.rallyX = x; f.rallyY = y;
            return true;
        }

        // ───────── 지휘기 ─────────

        void SpawnCommander()
        {
            var c = world.core;
            commander = new Unit { def = Content.Commander, serial = ++serial, x = c.Cx, y = c.Cy, aim = 1.5708f };
            commander.maxHealth = commander.health = Content.Commander.health * world.mods.unitMul;
            fx.Add(new Fx { kind = FxKind.Spawn, x = c.Cx, y = c.Cy, r = 1f, color = Content.Commander.color });
        }

        /// 점검용: 지휘기 없이 돌린다
        public void DisableCommander() { commanderEnabled = false; commander = null; }

        /// 지휘기가 가서 지킬 곳을 정한다
        public bool SetCommand(float x, float y)
        {
            if (!world.In((int)Math.Floor(x), (int)Math.Floor(y))) return false;
            commandX = x; commandY = y;
            return true;
        }

        void UpdateCommander()
        {
            if (!commanderEnabled) return;
            if (commander == null || commander.dead)
            {
                if (--commanderRespawn <= 0) SpawnCommander();
                return;
            }
            UpdateUnit(commander);
        }

        /// 지휘기가 곁에 있는 건설 터는 더 빨리 지어진다
        float CommanderBoost(Building b)
        {
            var c = commander;
            return c != null && !c.dead && b.DistTo(c.x, c.y) <= CommanderBuildRange ? CommanderBuildBoost : 1f;
        }

        // ───────── 유닛 한 틱 ─────────

        void UpdateUnit(Unit u)
        {
            if (u.dead) return;
            if (u.reload > 0) u.reload--;
            if (u.hitFlash > 0) u.hitFlash--;
            u.vx = u.vy = 0;
            if (u.carrier != null)
            {
                // 수송기에 실려 있다
                if (u.carrier.dead) u.carrier = null;
                else { u.x = u.carrier.x; u.y = u.carrier.y; return; }
            }
            var d = u.def;
            if (d.commander) { u.rallyX = commandX; u.rallyY = commandY; }
            else if (u.home != null && !u.home.dead) { u.rallyX = u.home.rallyX; u.rallyY = u.home.rallyY; }

            if (u.parent != null)
            {
                // 함재기: 기함 곁을 지키고, 기함을 잃으면 함께 떨어진다
                if (u.parent.dead)
                {
                    u.dead = true;
                    fx.Add(new Fx { kind = FxKind.EnemyDeath, x = u.x, y = u.y, r = d.radius * 2.2f, color = d.color });
                    return;
                }
                u.rallyX = u.parent.x; u.rallyY = u.parent.y;
            }
            if (d.spawns != null) UpdateHangar(u);

            if (d.heal > 0 && (tick + u.serial) % 30 == 0) HealAround(u);
            if (d.crush > 0 && (tick + u.serial) % 15 == 0) Crush(u);

            // 지키는 곳: 방어면 집결 지점, 공격이면 가장 가까운 출현 지점
            float ax = u.rallyX, ay = u.rallyY;
            bool attack = stance == UnitStance.Attack && world.spawns.Count > 0 && !d.commander && u.parent == null;
            if (attack)
            {
                float bd = float.MaxValue;
                foreach (int s in world.spawns)
                {
                    float sx = s % world.W + 0.5f, sy = s / world.W + 0.5f;
                    float dd = (sx - u.x) * (sx - u.x) + (sy - u.y) * (sy - u.y);
                    if (dd < bd) { bd = dd; ax = sx; ay = sy; }
                }
            }

            if (d.capacity > 0) { UpdateTransport(u, attack, ax, ay); return; }
            if (d.rebuild) { UpdateRebuilder(u); return; }
            if (d.mine > 0) { UpdateMiner(u); return; }
            if (d.bullet == null && d.fieldDamage <= 0) { UpdateRepairer(u); return; }

            if (--u.retarget <= 0 || u.target == null || u.target.dead)
            {
                u.retarget = 10;
                u.target = UnitTarget(u, ax, ay);
                u.tbuild = u.target == null && rules.assault ? BuildTarget(u, ax, ay, attack) : null;
            }

            if (u.target != null)
            {
                var t = u.target;
                float dx = t.x - u.x, dy = t.y - u.y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d.suicide)
                {
                    // 자폭: 붙어서 터진다
                    if (dist <= d.radius + t.def.radius + 0.2f) Explode(u);
                    else MoveUnit(u, t.x, t.y);
                    return;
                }
                float reach = d.range + t.def.radius;
                if (dist > reach * 0.85f) MoveUnit(u, t.x, t.y);
                if (dist <= reach)
                {
                    u.aim = (float)Math.Atan2(dy, dx);
                    if (u.reload <= 0)
                    {
                        u.reload = d.reloadTicks;
                        if (d.fieldDamage > 0) { Pulse(u); return; }
                        float lead = dist / d.bullet.speed;
                        var bl = FireFrom(u.x, u.y, t.x + t.vx * lead, t.y + t.vy * lead, d.bullet, d.bullet.damage * world.mods.unitMul, d.radius);
                        bl.fromUnit = true;
                        if (d.drain > 0) bl.owner = u;
                        if (d.bullet.homing > 0) bl.target = t;
                    }
                }
                return;
            }

            // 적 유닛이 없으면 적 건물을 친다
            if (u.tbuild != null && !u.tbuild.dead)
            {
                var tb = u.tbuild;
                float dist = tb.DistTo(u.x, u.y);
                if (d.suicide)
                {
                    if (dist <= 0.35f) Explode(u);
                    else MoveUnit(u, tb.Cx, tb.Cy);
                    return;
                }
                if (dist > d.range * 0.85f) MoveUnit(u, tb.Cx, tb.Cy);
                if (dist <= d.range)
                {
                    u.aim = (float)Math.Atan2(tb.Cy - u.y, tb.Cx - u.x);
                    if (u.reload <= 0)
                    {
                        u.reload = d.reloadTicks;
                        if (d.fieldDamage > 0) { Pulse(u); return; }
                        var bl = FireFrom(u.x, u.y, tb.Cx, tb.Cy, d.bullet, d.bullet.damage * world.mods.unitMul * d.buildingMul, d.radius);
                        bl.build = tb; bl.fromUnit = true;
                        if (d.drain > 0) bl.owner = u;
                        if (!d.bullet.artillery) bl.life = Math.Max(bl.life, (int)((dist + tb.def.size) / d.bullet.speed) + 2);
                    }
                }
                return;
            }

            // 표적이 없다: 방어면 집결 지점으로, 공격이면 출현 지점 앞으로
            float gx, gy, stop = 0.15f;
            if (attack) { gx = ax; gy = ay; stop = 3f; }
            else
            {
                gx = u.rallyX + u.ox; gy = u.rallyY + u.oy;
                if (d.naval || (d.GroundBound && Blocks(d, (int)gx, (int)gy))) { gx = u.rallyX; gy = u.rallyY; }
            }
            float ddx = gx - u.x, ddy = gy - u.y;
            if (ddx * ddx + ddy * ddy > stop * stop) MoveUnit(u, gx, gy);
        }

        /// 지키는 곳 (ax, ay) 둘레의 적이나 바로 곁의 적 중 가장 가까운 적
        Enemy UnitTarget(Unit u, float ax, float ay)
        {
            var d = u.def;
            float leash = d.commander ? CommanderLeash : UnitLeash;
            Enemy best = null; float bd = float.MaxValue;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead || (e.def.flying ? !d.targetAir : !d.targetGround)) continue;
                float dx = e.x - u.x, dy = e.y - u.y;
                float du = dx * dx + dy * dy;
                if (du >= bd) continue;
                float near = d.range + e.def.radius + 1.5f;
                float rx = e.x - ax, ry = e.y - ay;
                if (du > near * near && rx * rx + ry * ry > leash * leash) continue;
                bd = du; best = e;
            }
            return best;
        }

        /// 바로 곁의 적 건물, 또는 공격 태세일 때 노리는 적 핵 둘레의 적 건물 중 가장 가까운 것
        Building BuildTarget(Unit u, float ax, float ay, bool attack)
        {
            if (!u.def.targetGround) return null;
            Building best = null; float bd = float.MaxValue;
            var bs = world.buildings;
            for (int i = 0; i < bs.Count; i++)
            {
                var b = bs[i];
                if (!b.def.enemy || b.dead) continue;
                float du = b.DistTo(u.x, u.y);
                if (du >= bd) continue;
                if (du > u.def.range + 1.5f && !(attack && b.DistTo(ax, ay) <= UnitLeash + 2)) continue;
                bd = du; best = b;
            }
            return best;
        }

        // ───────── 특수 유닛 ─────────

        /// 전차: 몸에 닿은(반 칸 안으로 붙은) 지상의 적을 짓밟는다 (0.25초마다, 방어력 무시)
        void Crush(Unit u)
        {
            float dmg = u.def.crush * 0.25f * world.mods.unitMul;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead || e.def.flying) continue;
                float dx = e.x - u.x, dy = e.y - u.y, lim = u.def.radius + e.def.radius + 0.5f;
                if (dx * dx + dy * dy > lim * lim) continue;
                Damage(e, dmg, true);
                fx.Add(new Fx { kind = FxKind.Hit, x = e.x, y = e.y, r = 0.3f, color = u.def.color });
            }
        }

        /// 기함: 거느린 함재기가 모자라면 시간이 찰 때마다 하나씩 띄운다
        void UpdateHangar(Unit u)
        {
            var d = u.def;
            int have = 0;
            for (int i = 0; i < units.Count; i++) if (units[i].parent == u && !units[i].dead) have++;
            if (have >= d.spawnMax) { u.spawnTimer = 0; return; }
            if (++u.spawnTimer < d.spawnTicks) return;
            u.spawnTimer = 0;
            var c = new Unit { def = d.spawns, home = u.home, parent = u, serial = ++serial, x = u.x, y = u.y, aim = u.aim };
            c.maxHealth = c.health = d.spawns.health * world.mods.unitMul;
            c.rallyX = u.x; c.rallyY = u.y;
            c.ox = rng.Range(-1.2f, 1.2f); c.oy = rng.Range(-1.2f, 1.2f);
            units.Add(c);
            fx.Add(new Fx { kind = FxKind.Spawn, x = u.x, y = u.y, r = d.spawns.radius * 2.5f, color = d.spawns.color });
        }

        readonly List<Enemy> pulseHit = new List<Enemy>();

        /// 방전: 사거리 안의 적을 가까운 순으로 fieldTargets 기까지 한꺼번에 친다. 남은 몫은 사거리 안의 적 건물을 친다
        void Pulse(Unit u)
        {
            var d = u.def;
            float dmg = d.fieldDamage * world.mods.unitMul;
            uint col = 0x99E9F2;
            pulseHit.Clear();
            while (pulseHit.Count < d.fieldTargets)
            {
                Enemy best = null; float bd = float.MaxValue;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e.dead || (e.def.flying ? !d.targetAir : !d.targetGround) || pulseHit.Contains(e)) continue;
                    float dx = e.x - u.x, dy = e.y - u.y, lim = d.range + e.def.radius;
                    float dd = dx * dx + dy * dy;
                    if (dd <= lim * lim && dd < bd) { bd = dd; best = e; }
                }
                if (best == null) break;
                pulseHit.Add(best);
            }
            int left = d.fieldTargets - pulseHit.Count;
            foreach (var e in pulseHit)
            {
                fx.Add(new Fx { kind = FxKind.Hit, x = e.x, y = e.y, r = 0.3f, color = col });
                Damage(e, dmg);
            }
            if (rules.assault && d.targetGround)
            {
                var bs = world.buildings;
                for (int i = 0; i < bs.Count && left > 0; i++)
                {
                    var b = bs[i];
                    if (!b.def.enemy || b.dead || b.DistTo(u.x, u.y) > d.range) continue;
                    HurtBuilding(b, dmg * d.buildingMul);
                    fx.Add(new Fx { kind = FxKind.Hit, x = b.Cx, y = b.Cy, r = 0.3f, color = col });
                    left--;
                }
            }
            fx.Add(new Fx { kind = FxKind.Explode, x = u.x, y = u.y, r = d.radius * 2.2f, color = col });
            pulseHit.Clear();
        }

        /// 자폭 유닛이 터진다: 둘레의 적과 적 건물에 범위 피해
        void Explode(Unit u)
        {
            var bl = u.def.bullet;
            float dmg = bl.damage * world.mods.unitMul;
            Splash(u.x, u.y, bl.splash, dmg, true);
            SplashBuildings(u.x, u.y, bl.splash, dmg * u.def.buildingMul);
            fx.Add(new Fx { kind = FxKind.Explode, x = u.x, y = u.y, r = bl.splash, color = bl.color });
            u.dead = true;
        }

        /// 둘레의 아군 유닛과 아군 건물을 고친다 (0.5초마다)
        void HealAround(Unit u)
        {
            float amount = u.def.heal * 0.5f, r = u.def.healRange;
            bool any = false;
            for (int i = 0; i < units.Count; i++)
            {
                var o = units[i];
                if (o == u || o.dead || o.carrier != null || o.health >= o.maxHealth) continue;
                float dx = o.x - u.x, dy = o.y - u.y;
                if (dx * dx + dy * dy > r * r) continue;
                o.health = Math.Min(o.maxHealth, o.health + amount);
                any = true;
            }
            var bs = world.buildings;
            for (int i = 0; i < bs.Count; i++)
            {
                var b = bs[i];
                if (b.def.enemy || !b.Built || b.health >= b.maxHealth || b.DistTo(u.x, u.y) > r) continue;
                b.health = Math.Min(b.maxHealth, b.health + amount);
                any = true;
            }
            if (any) fx.Add(new Fx { kind = FxKind.Spawn, x = u.x, y = u.y, r = r * 0.6f, color = 0x8CE99A });
        }

        /// 무기 없는 수리 유닛: 가장 많이 상한 아군 건물로 가서 곁을 지킨다. 없으면 집결 지점
        void UpdateRepairer(Unit u)
        {
            float gx = u.rallyX + u.ox, gy = u.rallyY + u.oy, worst = 0.999f;
            if (--u.retarget <= 0) { u.retarget = 20; u.tbuild = null; }
            if (u.tbuild == null || u.tbuild.dead || u.tbuild.health >= u.tbuild.maxHealth)
            {
                u.tbuild = null;
                var bs = world.buildings;
                for (int i = 0; i < bs.Count; i++)
                {
                    var b = bs[i];
                    if (b.def.enemy || !b.Built) continue;
                    float f = b.health / b.maxHealth;
                    if (f < worst) { worst = f; u.tbuild = b; }
                }
            }
            float stop = 0.2f;
            if (u.tbuild != null) { gx = u.tbuild.Cx; gy = u.tbuild.Cy; stop = u.def.healRange * 0.5f; }
            float dx = gx - u.x, dy = gy - u.y;
            if (dx * dx + dy * dy > stop * stop) MoveUnit(u, gx, gy);
        }

        /// 아군 건물이 적에게 부서졌다: 살아 있는 재건 드론이 있으면 그 자리를 적어 둔다 (철거한 건물은 적지 않는다)
        void AddRuin(Building b)
        {
            if (ruins.Count >= MaxRuins) return;
            bool any = false;
            for (int i = 0; i < units.Count && !any; i++) any = units[i].def.rebuild && !units[i].dead;
            if (any) ruins.Add(new Ruin { def = b.def, x = b.x, y = b.y });
        }

        /// 재건 드론: 가장 먼저 부서진 자리로 가서 같은 건물의 건설 터를 놓는다. 자원이 모자라면 그 자리에서 기다리고,
        /// 그 자리에 다시는 지을 수 없으면(다른 건물이 들어섰다) 그 자리를 버린다. 할 일이 없으면 집결 지점에 머문다
        void UpdateRebuilder(Unit u)
        {
            if (ruins.Count == 0)
            {
                if (!Near(u, u.rallyX + u.ox, u.rallyY + u.oy, 0.3f)) MoveUnit(u, u.rallyX + u.ox, u.rallyY + u.oy);
                return;
            }
            // 드론마다 다른 자리를 맡는다 (자리가 드론보다 적으면 같은 자리로 모인다)
            int order = 0;
            for (int i = 0; i < units.Count; i++) if (units[i].def.rebuild && !units[i].dead && units[i].serial < u.serial) order++;
            var r = ruins[order % ruins.Count];
            float tx = r.x + r.def.size * 0.5f, ty = r.y + r.def.size * 0.5f;
            if (!Near(u, tx, ty, 0.8f)) { MoveUnit(u, tx, ty); return; }
            u.aim = (float)Math.Atan2(ty - u.y, tx - u.x);
            var res = CanPlace(r.def, r.x, r.y);
            if (res == PlaceResult.NoResources) return;
            ruins.Remove(r);
            if (res == PlaceResult.Ok) Place(r.def, r.x, r.y);
        }

        /// (x, y) 에서 가장 가까운 지형 벽 칸 (호수 제외). 없으면 -1
        int NearestRock(float x, float y)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < world.tiles.Length; i++)
            {
                var t = world.tiles[i];
                if (!t.wall || t.water) continue;
                float dx = i % world.W + 0.5f - x, dy = i / world.W + 0.5f - y;
                float dd = dx * dx + dy * dy;
                if (dd < bd) { bd = dd; best = i; }
            }
            return best;
        }

        /// 채굴 드론: 집결 지점에서 가장 가까운 벽으로 가서 캐고, 다 캐면 핵으로 날라 내려놓는다
        void UpdateMiner(Unit u)
        {
            var d = u.def;
            var core = Core;
            if (core == null) return;
            if (u.mineLoad > 0)
            {
                if (core.DistTo(u.x, u.y) > 0.3f) { MoveUnit(u, core.Cx, core.Cy); return; }
                core.Add(u.mineItem, u.mineLoad);
                mined[u.mineItem] += u.mineLoad;
                fx.Add(new Fx { kind = FxKind.Spawn, x = u.x, y = u.y, r = 0.6f, color = Content.Items[u.mineItem].color });
                u.mineLoad = 0; u.mineTrips++; u.mineTile = -1;
                return;
            }
            if (u.mineTile < 0 || (tick + u.serial) % 60 == 0)
            {
                int rock = NearestRock(u.rallyX, u.rallyY);
                if (rock != u.mineTile) { u.mineTile = rock; u.mineWork = 0; }
            }
            if (u.mineTile < 0) { if (!Near(u, u.rallyX, u.rallyY, 0.3f)) MoveUnit(u, u.rallyX, u.rallyY); return; }
            float tx = u.mineTile % world.W + 0.5f, ty = u.mineTile / world.W + 0.5f;
            if (!Near(u, tx, ty, 0.5f)) { MoveUnit(u, tx, ty); return; }
            u.aim = (float)Math.Atan2(ty - u.y, tx - u.x);
            u.mineItem = u.mineTrips % 3 == 2 ? Content.Sulfur : Content.Iron;
            if (u.mineWork % 20 == 0) fx.Add(new Fx { kind = FxKind.Hit, x = tx, y = ty, r = 0.22f, color = Content.Items[u.mineItem].color });
            if (++u.mineWork < d.mineTicks) return;
            u.mineWork = 0;
            u.mineLoad = u.mineItem == Content.Sulfur ? Math.Max(1, d.mine / 2) : d.mine;
        }

        /// 출현 지점에서 내 핵 쪽으로 몇 걸음 나온 빈 칸 (수송기가 유닛을 내려놓는 곳)
        int DropTile(float ax, float ay)
        {
            int cur = world.Idx((int)ax, (int)ay), ok = -1;
            for (int i = 0; i < 12 && cur >= 0; i++)
            {
                var t = world.tiles[cur];
                if (!t.wall && t.build == null) { ok = cur; if (i >= 7) break; }
                cur = field.Next(cur % world.W, cur / world.W);
            }
            return ok;
        }

        /// 수송기: 공격 태세면 뒤처진 지상 유닛을 실어 적 출현 지점 앞에 내려놓고, 방어 태세면 집결 지점에 내려놓는다
        void UpdateTransport(Unit u, bool attack, float ax, float ay)
        {
            var cargo = u.cargo;
            cargo.RemoveAll(c => c.dead);
            int load = 0;
            foreach (var c in cargo) load += c.def.supply;

            if (!attack)
            {
                if (cargo.Count > 0)
                {
                    if (Near(u, u.rallyX, u.rallyY, 0.4f)) Unload(u); else MoveUnit(u, u.rallyX, u.rallyY);
                }
                else if (!Near(u, u.rallyX + u.ox, u.rallyY + u.oy, 0.3f)) MoveUnit(u, u.rallyX + u.ox, u.rallyY + u.oy);
                return;
            }

            // 실을 유닛: 걸어가는 유닛 중 아직 출현 지점에서 먼 것
            Unit pick = null; float bd = float.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                var o = units[i];
                if (o.dead || o.carrier != null || !o.def.GroundBound || o.def.supply + load > u.def.capacity) continue;
                float ex = o.x - ax, ey = o.y - ay;
                if (ex * ex + ey * ey < 11 * 11) continue;
                float dx = o.x - u.x, dy = o.y - u.y;
                float dd = dx * dx + dy * dy;
                if (dd < bd) { bd = dd; pick = o; }
            }
            if (pick != null)
            {
                if (bd <= 0.6f * 0.6f) { pick.carrier = u; cargo.Add(pick); }
                else MoveUnit(u, pick.x, pick.y);
                return;
            }
            int drop = DropTile(ax, ay);
            if (drop < 0) return;
            float gx = drop % world.W + 0.5f, gy = drop / world.W + 0.5f;
            if (cargo.Count > 0)
            {
                if (Near(u, gx, gy, 0.4f)) Unload(u); else MoveUnit(u, gx, gy);
            }
            else
            {
                // 빈 수송기는 내려놓는 곳 뒤쪽에서 기다린다
                int back = DropTile(gx, gy);
                if (back >= 0 && !Near(u, back % world.W + 0.5f, back / world.W + 0.5f, 0.5f)) MoveUnit(u, back % world.W + 0.5f, back / world.W + 0.5f);
            }
        }

        static bool Near(Unit u, float x, float y, float r) => (u.x - x) * (u.x - x) + (u.y - y) * (u.y - y) <= r * r;

        /// 실은 유닛을 지금 자리(벽 위라면 가장 가까운 바닥)에 내려놓는다
        void Unload(Unit u)
        {
            int t = nav.Open((int)u.x, (int)u.y);
            float px = t >= 0 ? t % world.W + 0.5f : u.x, py = t >= 0 ? t / world.W + 0.5f : u.y;
            foreach (var c in u.cargo) { c.carrier = null; c.x = px; c.y = py; }
            u.cargo.Clear();
        }

        /// 방패 유닛의 보호막 안에 있으면 받는 피해가 줄어든다 (겹쳐도 한 번만)
        float ShieldMul(Unit u)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var s = units[i];
                if (s.dead || s.carrier != null || s.def.shieldRange <= 0) continue;
                float dx = s.x - u.x, dy = s.y - u.y;
                if (dx * dx + dy * dy <= s.def.shieldRange * s.def.shieldRange) return s.def.shieldMul;
            }
            return 1f;
        }

        // ───────── 적 건물 ─────────

        /// 적 건물에 피해를 준다 (부서지는 처리는 틱 끝에서)
        void HurtBuilding(Building b, float dmg, bool ignoreArmor = false)
        {
            b.health -= ignoreArmor ? dmg : Armored(dmg, b.def.armor);
            b.hitFlash = 6;
        }

        void SplashBuildings(float x, float y, float rad, float dmg, Building except = null)
        {
            var bs = world.buildings;
            for (int i = 0; i < bs.Count; i++)
            {
                var b = bs[i];
                if (!b.def.enemy || b.dead || b == except) continue;
                float dist = b.DistTo(x, y);
                if (dist > rad) continue;
                HurtBuilding(b, dmg * (1f - 0.5f * dist / rad));
            }
        }

        /// 적 거점: 출현 지점마다 적 핵을 세우고, 그 둘레에 적 포탑과 (아군 쪽을 보는) 적 방벽을 두른다
        void BuildEnemyBase()
        {
            float d = rules.depth;
            float hp = 1 + 3f * d;
            for (int k = 0; k < world.spawns.Count; k++)
            {
                int s = world.spawns[k];
                int cx = Math.Max(3, Math.Min(world.W - 4, s % world.W)), cy = Math.Max(3, Math.Min(world.H - 4, s / world.W));
                world.spawns[k] = world.Idx(cx, cy);
                // 핵 둘레를 터서 자리를 만든다
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        var ct = world.tiles[world.Idx(cx + dx, cy + dy)];
                        ct.wall = false; ct.water = false;
                    }
                AddEnemy(Content.EnemyCore, cx - 1, cy - 1, hp);

                int turrets = 3 + (int)(d * 7), placed = 0;
                for (int tries = 0; tries < 300 && placed < turrets; tries++)
                {
                    int x = cx + rng.Range(-6, 6), y = cy + rng.Range(-6, 6);
                    int far = Math.Max(Math.Abs(x - cx), Math.Abs(y - cy));
                    var t = world.At(x, y);
                    if (far < 3 || t == null || t.wall || t.build != null || field.cost[world.Idx(x, y)] == float.MaxValue) continue;
                    var tb = (EnemyTurretBuilding)AddEnemy(Content.EnemyTurret, x, y, hp);
                    tb.damage = Content.EnemyTurret.bullet.damage * (1 + 1.5f * d);
                    placed++;
                    // 포탑의 아군 쪽 면에 방벽
                    float here = field.cost[world.Idx(x, y)];
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            var o = world.At(x + dx, y + dy);
                            if (o == null || o.wall || o.build != null) continue;
                            if (field.cost[world.Idx(x + dx, y + dy)] >= here || rng.Range(0f, 1f) > 0.7f) continue;
                            AddEnemy(Content.EnemyWall, x + dx, y + dy, hp);
                        }
                }
            }
            nav.Clear(); sea.Clear(); skim.Clear();
        }

        Building AddEnemy(BlockDef def, int x, int y, float healthMul)
        {
            var b = Building.Create(def);
            b.x = x; b.y = y;
            b.health = b.maxHealth = def.health * healthMul;
            world.Add(b);
            return b;
        }

        // ───────── 이동 ─────────

        void MoveUnit(Unit u, float tx, float ty)
        {
            float speed = u.def.speed;
            if (u.def.flying || u.def.legs) { StepUnit(u, tx, ty, speed); return; }   // 날거나 벽을 타 넘는다
            if (u.def.naval) { MoveNaval(u, tx, ty, speed); return; }

            int cx = (int)u.x, cy = (int)u.y;
            int gx = (int)tx, gy = (int)ty;
            var nv = u.def.hover ? skim : nav;
            int goal = Blocks(u.def, gx, gy) ? nv.Open(gx, gy) : world.Idx(gx, gy);   // 벽 위를 나는 적은 가장 가까운 바닥에서 쏜다
            int cur = world.Idx(cx, cy);
            if (goal < 0) return;
            if (goal == cur)
            {
                if (!Blocks(u.def, gx, gy)) StepUnit(u, tx, ty, speed);
                return;
            }
            var f = nv.Field(goal);
            int best = -1, bv = f[cur];
            for (int k = 0; k < 4; k++)
            {
                int nx = cx + World.DX[k], ny = cy + World.DY[k];
                if (Blocks(u.def, nx, ny)) continue;
                int n = world.Idx(nx, ny);
                if (f[n] < bv) { bv = f[n]; best = n; }
            }
            if (best < 0) return;
            StepUnit(u, best % world.W + 0.5f, best / world.W + 0.5f, speed);
        }

        /// 해상 유닛: 지금 있는 호수 안에서 목표에 가장 가까운 물 칸으로 간다
        void MoveNaval(Unit u, float tx, float ty, float speed)
        {
            int cur = world.Idx((int)u.x, (int)u.y);
            if (!world.tiles[cur].water) return;
            if (tick >= u.navalTick || u.navalGoal < 0)
            {
                u.navalTick = tick + 20;
                var reach = sea.Field(cur);
                float bd = float.MaxValue;
                u.navalGoal = cur;
                for (int i = 0; i < reach.Length; i++)
                {
                    if (reach[i] == GroundNav.Unreachable) continue;
                    float dx = i % world.W + 0.5f - tx, dy = i / world.W + 0.5f - ty;
                    float dd = dx * dx + dy * dy;
                    if (dd < bd) { bd = dd; u.navalGoal = i; }
                }
            }
            if (u.navalGoal == cur) { StepUnit(u, cur % world.W + 0.5f, cur / world.W + 0.5f, speed); return; }
            var f = sea.Field(u.navalGoal);
            int best = -1, bv = f[cur];
            int cx = cur % world.W, cy = cur / world.W;
            for (int k = 0; k < 4; k++)
            {
                int nx = cx + World.DX[k], ny = cy + World.DY[k];
                if (!world.In(nx, ny)) continue;
                int n = world.Idx(nx, ny);
                if (f[n] < bv) { bv = f[n]; best = n; }
            }
            if (best >= 0) StepUnit(u, best % world.W + 0.5f, best / world.W + 0.5f, speed);
        }

        static void StepUnit(Unit u, float tx, float ty, float speed)
        {
            float dx = tx - u.x, dy = ty - u.y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d < 0.0001f) return;
            float s = Math.Min(speed, d);
            u.vx = dx / d * s; u.vy = dy / d * s;
            u.x += u.vx; u.y += u.vy;
            u.aim = (float)Math.Atan2(dy, dx);
        }

        // ───────── 적이 유닛을 친다 ─────────

        public Unit NearestUnit(float x, float y, float range, bool air)
        {
            Unit best = null; float bd = float.MaxValue;
            for (int i = -1; i < units.Count; i++)
            {
                var u = i < 0 ? commander : units[i];
                if (u == null || u.dead || u.carrier != null || (u.def.flying && !air)) continue;
                float dx = u.x - x, dy = u.y - y;
                float d = dx * dx + dy * dy;
                float lim = range + u.def.radius;
                if (d <= lim * lim && d < bd) { bd = d; best = u; }
            }
            return best;
        }

        void AttackUnit(Enemy e, Unit u)
        {
            if (e.cooldown > 0) return;
            e.cooldown = e.def.attackTicks;
            HurtUnit(u, e.def.damage * EnemyDamageMul, e.def.color);
        }

        /// 유닛이 피해를 입는다 (적의 공격, 적 포탑)
        public void HurtUnit(Unit u, float dmg, uint color)
        {
            if (u.dead) return;
            u.health -= Armored(dmg * ShieldMul(u), u.def.armor);
            u.hitFlash = 6;
            fx.Add(new Fx { kind = FxKind.EnemyAttack, x = u.x, y = u.y, r = 0.35f, color = color });
            if (u.health > 0) return;
            u.dead = true;
            fx.Add(new Fx { kind = FxKind.EnemyDeath, x = u.x, y = u.y, r = u.def.radius * 2.2f, color = u.def.color });
            if (u.def.commander) { commanderRespawn = CommanderRespawnTicks; return; }
            if (u.parent == null) unitsLost++;   // 함재기는 잃은 유닛으로 세지 않는다
            if (u.cargo != null && u.cargo.Count > 0) Unload(u);   // 수송기가 떨어지면 실린 유닛은 그 자리에 내린다
        }
    }
}
