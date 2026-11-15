using System;
using System.Collections.Generic;

namespace AbyssDefense.Sim
{
    public enum CmdKind { Place, Remove, Product, Rally, Stance, CallWave, Command, Ammo, Mode, AutoRebuild }

    /// 플레이어가 내린 명령 하나. 같은 지역에 같은 명령을 같은 틱에 다시 넣으면 같은 판이 된다 (진행 중 저장이 이것만 적어 둔다)
    [Serializable]
    public struct SimCommand
    {
        public int tick;        // 이 틱의 시뮬레이션이 돌기 전에 적용한다
        public CmdKind kind;
        public int a, b, c;     // Place: 블록 id, x, y / Remove: x, y / 건물을 가리키는 명령: 건물 칸 x, y 와 값
        public float x, y;      // Rally, Command: 가리킨 곳
    }

    /// 다음 회차에 나올 적 한 종류
    public struct WavePreview
    {
        public EnemyDef def;
        public int count;
        public bool elite, shield;
    }

    public sealed partial class Sim
    {
        /// 이 판에서 받아들인 명령 (적용한 순서대로)
        public readonly List<SimCommand> log = new List<SimCommand>();

        // ───────── 화면이 부르는 명령: 실행하고, 받아들였으면 적어 둔다 ─────────

        public PlaceResult CmdPlace(BlockDef def, int x, int y)
        {
            var c = new SimCommand { kind = CmdKind.Place, a = def.id, b = x, c = y };
            var res = Place(def, x, y);
            if (res == PlaceResult.Ok) Record(c);
            return res;
        }
        public bool CmdRemove(int x, int y) => Do(new SimCommand { kind = CmdKind.Remove, a = x, b = y });
        public bool CmdProduct(FactoryBuilding f, int k) => f != null && Do(new SimCommand { kind = CmdKind.Product, a = f.x, b = f.y, c = k });
        public bool CmdRally(FactoryBuilding f, float x, float y) => f != null && Do(new SimCommand { kind = CmdKind.Rally, a = f.x, b = f.y, x = Snap(x), y = Snap(y) });
        public bool CmdStance(UnitStance s) => Do(new SimCommand { kind = CmdKind.Stance, a = (int)s });
        public bool CmdCallWave() => Do(new SimCommand { kind = CmdKind.CallWave });
        public bool CmdCommand(float x, float y) => Do(new SimCommand { kind = CmdKind.Command, x = Snap(x), y = Snap(y) });
        public bool CmdAmmo(TurretBuilding t, int k) => t != null && Do(new SimCommand { kind = CmdKind.Ammo, a = t.x, b = t.y, c = k });
        public bool CmdMode(TurretBuilding t, TargetMode m) => t != null && Do(new SimCommand { kind = CmdKind.Mode, a = t.x, b = t.y, c = (int)m });
        public bool CmdAutoRebuild(bool on) => Do(new SimCommand { kind = CmdKind.AutoRebuild, a = on ? 1 : 0 });

        /// 가리킨 곳은 1/16 칸으로 맞춘다 (저장했다 읽어도 값이 그대로이게)
        static float Snap(float v) => (float)Math.Round(v * 16) / 16f;

        bool Do(SimCommand c)
        {
            if (!Apply(c)) return false;
            Record(c);
            return true;
        }

        void Record(SimCommand c) { c.tick = tick; log.Add(c); }

        /// 명령 하나를 실행한다. 받아들이지 않았으면 false
        public bool Apply(SimCommand c)
        {
            if (state != SimState.Playing) return false;
            switch (c.kind)
            {
                case CmdKind.Place:
                    return c.a >= 0 && c.a < Content.Blocks.Length && Place(Content.Blocks[c.a], c.b, c.c) == PlaceResult.Ok;
                case CmdKind.Remove:
                    return Remove(c.a, c.b);
                case CmdKind.Product:
                {
                    if (!(world.BuildAt(c.a, c.b) is FactoryBuilding f) || c.c >= f.def.units.Length) return false;
                    if (c.c >= 0 && !IsUnlocked(f.def.units[c.c])) return false;
                    f.product = c.c < 0 ? -1 : c.c;
                    return true;
                }
                case CmdKind.Rally:
                    return SetRally(world.BuildAt(c.a, c.b) as FactoryBuilding, c.x, c.y);
                case CmdKind.Stance:
                    stance = c.a == (int)UnitStance.Attack ? UnitStance.Attack : UnitStance.Defend;
                    return true;
                case CmdKind.CallWave:
                    if (AllWavesSent) return false;
                    CallNextWave();
                    return true;
                case CmdKind.Command:
                    return SetCommand(c.x, c.y);
                case CmdKind.Ammo:
                    return world.BuildAt(c.a, c.b) is TurretBuilding t && t.SetAmmo(c.c);
                case CmdKind.Mode:
                {
                    if (!(world.BuildAt(c.a, c.b) is TurretBuilding t2) || c.c < 0 || c.c > (int)TargetMode.Air) return false;
                    t2.mode = (TargetMode)c.c;
                    return true;
                }
                case CmdKind.AutoRebuild:
                    autoRebuild = c.a != 0;
                    if (!autoRebuild) ruins.Clear();
                    return true;
            }
            return false;
        }

        /// 적어 둔 명령을 틱에 맞춰 넣으며 untilTick 까지 돌린다 (이어하기). from 부터 이어서 넣고, 다음에 넣을 명령의 번호를 돌려준다.
        /// maxTicks 만큼만 돌고 돌아오므로 여러 프레임에 나눠 부를 수 있다
        public int Replay(IList<SimCommand> cmds, int from, int untilTick, int maxTicks = int.MaxValue)
        {
            int i = from;
            for (int n = 0; n < maxTicks && tick < untilTick && state == SimState.Playing; n++)
            {
                while (i < cmds.Count && cmds[i].tick <= tick)
                {
                    if (Apply(cmds[i])) log.Add(cmds[i]);
                    i++;
                }
                Tick();
            }
            if (tick >= untilTick)
                while (i < cmds.Count && cmds[i].tick <= tick)
                {
                    if (Apply(cmds[i])) log.Add(cmds[i]);
                    i++;
                }
            return i;
        }

        /// 판의 상태를 한 숫자로 줄인 값 (같은 판인지 견줄 때 쓴다)
        public long StateHash()
        {
            unchecked
            {
                long h = 1469598103934665603L;
                void Mix(float v) { h = (h ^ BitConverter.SingleToInt32Bits(v)) * 1099511628211L; }
                Mix(tick); Mix(wave); Mix(kills); Mix(waveCountdown); Mix((int)state); Mix((int)stance); Mix(autoRebuild ? 1 : 0);
                foreach (int it in Core.items) Mix(it);
                foreach (var b in world.buildings) { Mix(b.def.id); Mix(b.x); Mix(b.y); Mix(b.health); Mix(b.progress); }
                foreach (var e in enemies) { Mix(e.def.id); Mix(e.x); Mix(e.y); Mix(e.health); Mix(e.barrier); }
                foreach (var u in units) { Mix(u.def.id); Mix(u.x); Mix(u.y); Mix(u.health); }
                foreach (var bl in bullets) { Mix(bl.x); Mix(bl.y); }
                Mix(mines.Count); Mix(ruins.Count); Mix(commandX); Mix(commandY); Mix(blizzardLeft); Mix(blizzardIn);
                return h;
            }
        }

        /// 규칙이 바뀌면(코드의 동작이 달라지면) 올린다. 진행 중 저장은 이 값과 수치 지문이 같을 때만 이어진다
        public const int RulesVersion = 1;

        /// 수치 지문: 유닛·적·건물·지역 규칙·웨이브의 숫자를 한 값으로 줄인다. 밸런스를 고치면 값이 달라져, 예전 판의 명령 기록을 잘못 되돌리지 않는다
        public static int RulesFingerprint()
        {
            unchecked
            {
                int h = RulesVersion * 16777619;
                void Mix(float v) { h = (h ^ BitConverter.SingleToInt32Bits(v)) * 16777619; }
                void Bullet(BulletDef b)
                {
                    if (b == null) { Mix(-1); return; }
                    Mix(b.speed); Mix(b.damage); Mix(b.splash); Mix(b.lifeTicks); Mix(b.pierce); Mix(b.chain); Mix(b.burn); Mix(b.slow); Mix(b.acid); Mix(b.wetTicks); Mix(b.homing); Mix(b.knockback);
                }
                foreach (var u in Content.Units)
                {
                    Mix(u.health); Mix(u.armor); Mix(u.speed); Mix(u.range); Mix(u.reloadTicks); Mix(u.buildTicks); Mix(u.supply); Mix(u.heal); Mix(u.barrier); Mix(u.fieldDamage); Mix(u.crush);
                    foreach (var c in u.cost) { Mix(c.item); Mix(c.amount); }
                    Bullet(u.bullet); Bullet(u.minelay);
                }
                foreach (var e in Content.Enemies)
                {
                    Mix(e.health); Mix(e.speed); Mix(e.armor); Mix(e.damage); Mix(e.attackTicks); Mix(e.range); Mix(e.heal); Mix(e.barrier); Mix(e.barrierRegen); Mix(e.splitCount); Mix(e.carryCount);
                }
                foreach (var b in Content.Blocks)
                {
                    Mix(b.health); Mix(b.armor); Mix(b.range); Mix(b.reloadTicks); Mix(b.buildTicks); Mix(b.supportRange); Mix(b.mendPct); Mix(b.shield); Mix(b.shieldRegen);
                    foreach (var c in b.cost) { Mix(c.item); Mix(c.amount); }
                    if (b.ammos != null) foreach (var a in b.ammos) { Mix(a.item); Mix(a.perItem); Bullet(a.bullet); }
                }
                for (int a = 0; a < Content.Abysses.Length; a++)
                    foreach (int reg in new[] { 1, 15, 40, 100 })
                    {
                        var r = RegionRules.For(a, reg);
                        Mix((int)r.hazard); Mix(r.mapW); Mix(r.mapH); Mix(r.winWave); Mix(r.waveTicks); Mix(r.firstWaveTicks); Mix(r.enemyHealthMul); Mix(r.enemyDamageMul); Mix(r.enemyArmorMul);
                        foreach (var g in WaveGen.Generate(r)) { Mix(g.enemy.id); Mix(g.begin); Mix(g.spacing); Mix(g.amount); Mix(g.scaling); Mix(g.max); Mix(g.shields); Mix(g.shieldScaling); }
                    }
                return h;
            }
        }

        // ───────── 웨이브 미리보기 ─────────

        /// 그 회차에 나올 적 (무리 순서대로, 같은 무리끼리 묶어서)
        public void Preview(int w, List<WavePreview> into)
        {
            into.Clear();
            if (!rules.assault && w > rules.winWave) return;
            foreach (var g in groups)
            {
                int n = g.GetSpawned(w);
                if (n > 0) into.Add(new WavePreview { def = g.enemy, count = n, elite = g.elite, shield = g.shields > 0 || g.shieldScaling > 0 });
            }
        }

        /// 지금부터 몇 회차 뒤에 보스가 나오는가 (1 = 다음 회차, 없으면 0). ahead 회차까지만 본다
        public int BossIn(int ahead)
        {
            for (int k = 1; k <= ahead; k++)
            {
                if (!rules.assault && wave + k > rules.winWave) break;
                foreach (var g in groups) if (g.enemy.boss && g.GetSpawned(wave + k) > 0) return k;
            }
            return 0;
        }
    }
}
