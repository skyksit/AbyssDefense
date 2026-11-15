using System;
using System.Collections.Generic;
using System.Text;
using AbyssDefense.Sim;

/// 시뮬레이션 점검: 단위 검사 + 자동 플레이 결과.
///   dotnet run -c Release            전체 검사
///   dotnet run -c Release -- map 0 1  지역 맵을 글자로 출력 (abyss, region)
static class Program
{
    static int fails, checks;

    static void Check(bool ok, string name)
    {
        checks++;
        if (!ok) { fails++; Console.WriteLine("  FAIL  " + name); }
    }

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length >= 3 && args[0] == "trace")
        {
            int ab = int.Parse(args[1]), rg = int.Parse(args[2]);
            var research = ExpectedResearch(rg);
            int cap = BaseRules.LoadoutCap(research);
            var take = new int[Content.Items.Length];
            take[0] = cap; take[1] = cap;
            if (rg > BaseRules.CrystalMineRegion) take[2] = cap / 2;
            foreach (var rf in BaseRules.Refineries) if (rg > rf.reqRegion) take[rf.output] = rf.output == Content.Alloy ? cap / 4 : cap / 2;
            var sim = new Sim(ab, rg, take, Research.Mods(research));
            var bot = new AutoBot(sim);
            if (args.Length >= 4)
            {
                FactoryBuilding hf = null;
                for (int y = sim.Core.y + 4; y < sim.world.H && hf == null; y++)
                    for (int x = 2; x < sim.world.W - 3 && hf == null; x++)
                        if (sim.Place(Content.RobotFactory, x, y) == PlaceResult.Ok) hf = (FactoryBuilding)sim.world.BuildAt(x, y);
                int n = int.Parse(args[3]);
                for (int i = 0; i < n; i++) sim.SpawnUnit(i % 2 == 0 ? Content.Assault : Content.Bomber, hf);
                sim.stance = UnitStance.Attack;
            }
            while (sim.state == SimState.Playing && sim.tick < 60 * 60 * 40)
            {
                bot.Step(); sim.Tick();
                if (sim.tick % (60 * 60) == 0)
                {
                    int t = 0, f = 0; string st = "";
                    foreach (var b in sim.world.buildings) { if (b is TurretBuilding) t++; if (b is FactoryBuilding fb) { f++; st += fb.status + "/" + fb.product + " "; } }
                    Console.WriteLine($"{sim.tick / 3600}분 웨이브 {sim.wave} 적 {sim.enemies.Count} 포탑 {t} 공장 {f} [{st}] 유닛 {sim.units.Count} ({sim.SupplyUsed}/{sim.UnitCap}) {sim.stance} 만든 {sim.unitsBuilt} 잃은 {sim.unitsLost} 적핵 {sim.EnemyCores} 부순 {sim.structuresDestroyed} 자원 {string.Join(",", sim.Core.items)}");
                }
            }
            Console.WriteLine(sim.state);
            return 0;
        }
        if (args.Length >= 3 && args[0] == "map") { PrintMap(int.Parse(args[1]), int.Parse(args[2])); return 0; }

        Construction();
        BuildTime();
        Combat();
        Armor();
        Turrets();
        SupportBlocks();
        Units();
        Assault();
        NewUnits();
        WaveFormula();
        BaseEconomy();
        MapChecks();
        Determinism();
        Console.WriteLine($"검사 {checks}개 중 실패 {fails}개");

        Console.WriteLine("\n자동 플레이 (봇, 그 깊이에 맞는 연구와 출격 물자 한도만큼 가져감):  결과, 웨이브, 처치, 핵 체력, 걸린 시간");
        var idle = Play(0, 1, false);
        Console.WriteLine($"  아무것도 안 지음  잿불 1 → {idle}");
        Check(idle.StartsWith("Lost"), "아무것도 안 지으면 진다");
        var bare = Play(0, 1, true, false);
        Console.WriteLine($"  빈손 출격        잿불 1 → {bare}");
        Check(bare.StartsWith("Won"), "빈손(기본 보급 150)으로도 지역 1 을 봇이 이긴다");
        int[] regions = { 1, 2, 5, 10, 15, 20, 45, 50, 95, 100 };
        for (int a = 0; a < Content.Abysses.Length; a++)
            foreach (int r in regions)
            {
                string res = Play(a, r, true);
                Console.WriteLine($"  {Content.Abysses[a].name} {r,3} → {res}");
                if (r == 1) Check(res.StartsWith("Won"), Content.Abysses[a].name + " 지역 1 을 봇이 이긴다");
            }
        Console.WriteLine($"\n검사 {checks}개 중 실패 {fails}개");
        return fails == 0 ? 0 : 1;
    }

    /// 그 지역쯤이면 해 두었을 법한 연구 단계: 해금된 연구를 지역 60 에서 모두 끝낸다고 본다
    static int[] ExpectedResearch(int region)
    {
        var lv = new int[Research.Defs.Length];
        float f = Math.Min(1f, (region - 1) / 59f);
        for (int i = 0; i < lv.Length; i++)
        {
            var d = Research.Defs[i];
            if (region <= d.reqRegion) continue;
            lv[i] = d.maxLevel == 1 ? 1 : (int)Math.Round(d.maxLevel * f);
        }
        return lv;
    }

    static string Play(int abyss, int region, bool bot, bool loadout = true)
    {
        var research = ExpectedResearch(region);
        int cap = BaseRules.LoadoutCap(research);
        int[] take = null;
        if (loadout)
        {
            // 가공 자원은 그 가공 건물이 열린 뒤부터 한도의 절반씩 가져간다
            take = new int[Content.Items.Length];
            take[Content.Iron] = cap; take[Content.Sulfur] = cap;
            if (region > BaseRules.CrystalMineRegion) take[Content.Crystal] = cap / 2;
            foreach (var rf in BaseRules.Refineries)
                if (region > rf.reqRegion) take[rf.output] = rf.output == Content.Alloy ? cap / 4 : cap / 2;
        }
        var sim = new Sim(abyss, region, take, loadout ? Research.Mods(research) : null);
        var b = bot ? new AutoBot(sim) : null;
        int limit = 60 * 60 * 60;
        while (sim.state == SimState.Playing && sim.tick < limit)
        {
            b?.Step();
            sim.Tick();
        }
        int turrets = 0, factories = 0;
        foreach (var bd in sim.world.buildings) { if (bd is TurretBuilding) turrets++; if (bd is FactoryBuilding) factories++; }
        float hp = sim.Core.health <= 0 ? 0 : sim.Core.health / sim.Core.maxHealth * 100;
        return $"{sim.state,-7} {(sim.rules.assault ? $"거점(남은 적 핵 {sim.EnemyCores})" : "")} 웨이브 {sim.wave}/{sim.rules.winWave}  처치 {sim.kills,4}  핵 {hp,3:0}%  {sim.tick / 60 / 60}분 {sim.tick / 60 % 60:00}초  (포탑 {turrets}, 공장 {factories}, 유닛 {sim.unitsBuilt}대 중 {sim.unitsLost} 잃음, 가져감 {(take == null ? 0 : take[0])}, 남은 철 {sim.Core.items[0]} 황 {sim.Core.items[1]}, 전리품 철 {sim.looted[0]})";
    }

    /// 벽 없는 빈 맵
    static Sim EmptySim(int[] loadout = null, SimMods mods = null)
    {
        var sim = new Sim(0, 1, loadout ?? new[] { 1000, 1000, 1000 }, mods);
        var w = sim.world;
        for (int y = 1; y < w.H - 1; y++)
            for (int x = 1; x < w.W - 1; x++) { w.tiles[w.Idx(x, y)].wall = false; w.tiles[w.Idx(x, y)].water = false; }
        sim.waveCountdown = int.MaxValue;
        sim.DisableCommander();   // 지휘기가 끼어들지 않게 (지휘기는 따로 검사한다)
        sim.autoRebuild = false;  // 자동 재건도 따로 검사한다
        return sim;
    }

    static void Construction()
    {
        Console.WriteLine("건설");
        var sim = EmptySim();
        var core = sim.Core;
        int iron = core.items[Content.Iron];
        bool placed = sim.Place(Content.RapidTurret, 3, 20) == PlaceResult.Ok;
        Finish(sim);
        Check(placed && core.items[Content.Iron] == iron - 35, "포탑을 지으면 핵 재고에서 비용이 빠진다");
        Check(sim.Place(Content.Wall, 3, 20) == PlaceResult.Blocked, "건물 위에는 못 짓는다");
        Check(sim.Place(Content.MortarTurret, 6, 20) == PlaceResult.Locked, "연구하지 않은 박격포는 못 짓는다");
        int sp = sim.world.spawns[0];
        Check(sim.Place(Content.Wall, sp % sim.world.W + 1, sp / sim.world.W) == PlaceResult.NearSpawn, "출현 지점 옆에는 못 짓는다");
        iron = core.items[Content.Iron];
        sim.Remove(3, 20);
        Check(core.items[Content.Iron] == iron + (int)(35 * Sim.RefundRatio), "철거하면 75% 를 돌려받는다");

        var lv = new int[Research.Defs.Length];
        lv[Research.Mortar] = 1; lv[Research.Health] = 2;
        var s2 = EmptySim(null, Research.Mods(lv));
        Check(s2.Place(Content.MortarTurret, 6, 20) == PlaceResult.Ok, "박격포 기술을 연구하면 지을 수 있다");
        Check(Math.Abs(s2.world.BuildAt(6, 20).maxHealth - Content.MortarTurret.health * 1.4f) < 0.01f, "강화 장갑 2단계 = 체력 +40%");

        var poor = EmptySim(new[] { 0, 0, 0 });
        Check(poor.Core.items[Content.Iron] == 150, "빈손으로 출격해도 기본 보급 철광 150");
        for (int i = 0; i < 4; i++) poor.Place(Content.RapidTurret, 3 + i, 20);
        Check(poor.Place(Content.RapidTurret, 8, 20) == PlaceResult.NoResources, "자원이 모자라면 못 짓는다");
    }

    /// 건물을 놓고 다 지어질 때까지 틱을 돌린다
    static void Finish(Sim sim)
    {
        for (int i = 0; i < 60 * 600; i++)
        {
            bool all = true;
            foreach (var b in sim.world.buildings) if (!b.Built) all = false;
            if (all) return;
            sim.Tick();
        }
    }

    static void BuildTime()
    {
        Console.WriteLine("건설 시간");
        var sim = EmptySim(new[] { 850, 0, 0 });   // 기본 보급 150 을 더해 철광 1000
        var core = sim.Core;
        int ticks = Content.RapidTurret.buildTicks;
        Check(ticks == (20 + 35) * 5 && Content.Wall.buildTicks == (20 + 8) * 5, $"건설 시간 = (20 + 비용) × 5 틱 (속사포 {ticks / 60f:0.0}초, 방벽 {Content.Wall.buildTicks / 60f:0.0}초)");
        Check(Content.MortarTurret.buildTicks > ticks * 2, $"비싼 건물일수록 오래 걸린다 (박격포 {Content.MortarTurret.buildTicks / 60f:0.0}초, 결정포 {Content.CrystalTurret.buildTicks / 60f:0.0}초)");

        sim.Place(Content.RapidTurret, 3, 20);
        var t = sim.world.BuildAt(3, 20);
        Check(!t.Built && core.items[Content.Iron] == 1000, "놓는 순간에는 비용이 나가지 않는다");
        Check(Math.Abs(t.health - t.maxHealth * Building.SiteHealth) < 0.01f, "건설 터의 체력은 15%");
        for (int i = 0; i < ticks / 2; i++) sim.Tick();
        Check(!t.Built && Math.Abs(t.progress - 0.5f) < 0.02f && Math.Abs((1000 - core.items[Content.Iron]) - 17) <= 1, $"절반 지으면 비용도 절반 ({1000 - core.items[Content.Iron]})");
        for (int i = 0; i < ticks; i++) sim.Tick();
        Check(t.Built && core.items[Content.Iron] == 965 && t.health == t.maxHealth, "다 지어지면 비용 35 를 모두 내고 체력이 가득 찬다");

        // 다 지어지기 전에는 쏘지 않는다
        var s2 = EmptySim(new[] { 850, 0, 0 });
        s2.Place(Content.RapidTurret, (int)s2.Core.Cx, s2.Core.y + 6);
        var e = new Enemy { def = Content.Walker, x = s2.Core.Cx + 3, y = s2.Core.y + 6.5f, health = 9999, maxHealth = 9999 };
        s2.enemies.Add(e);
        for (int i = 0; i < ticks - 5; i++) s2.Tick();
        Check(s2.bullets.Count == 0 && e.health == 9999, "짓는 중인 포탑은 쏘지 않는다");
        for (int i = 0; i < 60; i++) s2.Tick();
        Check(e.health < 9999, "다 지어지면 쏘기 시작한다");

        // 한 번에 두 곳까지만 짓는다
        var s3 = EmptySim(new[] { 850, 0, 0 });
        for (int i = 0; i < 3; i++) s3.Place(Content.RapidTurret, 3 + i, 20);
        for (int i = 0; i < 30; i++) s3.Tick();
        Check(s3.world.BuildAt(3, 20).constructing && s3.world.BuildAt(4, 20).constructing && !s3.world.BuildAt(5, 20).constructing && s3.world.BuildAt(5, 20).progress == 0, "세 번째 건물은 순서를 기다린다");
        Finish(s3);
        Check(s3.tick >= ticks * 2 && s3.tick <= ticks * 2 + 5, $"세 곳을 짓는 데 두 배 시간 ({s3.tick} 틱)");

        // 예약된 비용만큼은 다른 건물에 쓸 수 없다, 자원이 떨어지면 멈춘다
        var s4 = EmptySim(new[] { 0, 0, 0 });   // 철광 150
        for (int i = 0; i < 4; i++) s4.Place(Content.RapidTurret, 3 + i, 20);
        Check(s4.Place(Content.RapidTurret, 8, 20) == PlaceResult.NoResources, "짓고 있는 건물 몫을 빼고 남은 자원으로만 놓을 수 있다 (150 = 35 × 4 + 10)");
        Check(s4.Place(Content.Wall, 8, 20) == PlaceResult.Ok, "남은 10 으로 방벽(8)은 놓을 수 있다");
        s4.Core.items[Content.Iron] = 20;
        for (int i = 0; i < ticks * 3; i++) s4.Tick();
        var stalled = s4.world.BuildAt(3, 20);
        Check(!stalled.Built && !stalled.constructing && s4.Core.items[Content.Iron] >= 0, "핵에 자원이 없으면 건설이 멈춘다");

        // 짓던 건물을 취소하면 낸 만큼 돌려받는다
        var s5 = EmptySim(new[] { 850, 0, 0 });
        s5.Place(Content.RapidTurret, 3, 20);
        for (int i = 0; i < ticks / 2; i++) s5.Tick();
        s5.Remove(3, 20);
        Check(s5.Core.items[Content.Iron] == 1000, "건설 취소는 전액 환불");

        // 건설 드론 연구
        var lv = new int[Research.Defs.Length]; lv[Research.Build] = 4;
        var s6 = EmptySim(new[] { 850, 0, 0 }, Research.Mods(lv));
        s6.Place(Content.RapidTurret, 3, 20);
        Finish(s6);
        Check(Math.Abs(s6.tick - ticks / 2) <= 2, $"건설 드론 4단계 = 2배 빠르다 ({s6.tick} 틱)");
    }

    /// 포탑 하나와 적 하나를 직접 놓고 탄약·전리품 규칙을 본다
    static void Combat()
    {
        Console.WriteLine("전투");
        var sim = EmptySim(new[] { 100, 0, 0 });
        var core = sim.Core;
        sim.Place(Content.RapidTurret, (int)core.Cx, core.y + 6);
        Finish(sim);
        int iron = core.items[Content.Iron];
        var e = new Enemy { def = Content.Walker, x = core.Cx, y = core.y + 10, health = 42, maxHealth = 42 };
        sim.enemies.Add(e);
        for (int i = 0; i < 600 && !e.dead; i++) sim.Tick();
        var t = (TurretBuilding)sim.world.BuildAt((int)core.Cx, core.y + 6);
        Check(e.dead && sim.kills == 1, "포탑이 적을 처치한다");
        Check(core.items[Content.Iron] == iron - 1 + 4, $"탄약으로 철광 1 을 쓰고 전리품 4 를 얻는다 ({iron} → {core.items[Content.Iron]})");
        Check(t.ammo == Content.RapidTurret.ammoPerItem - 4, $"철광 1 = 탄 6발, 4발로 처치 (남은 탄 {t.ammo})");
        Check(sim.looted[Content.Iron] == 4, "전리품 집계");

        // 핵에 탄약 자원이 없으면 쏘지 못한다
        var dry = EmptySim(new[] { 0, 0, 0 });
        dry.Place(Content.RapidTurret, (int)dry.Core.Cx, dry.Core.y + 6);
        Finish(dry);
        dry.Core.items[Content.Iron] = 0;
        var e2 = new Enemy { def = Content.Walker, x = dry.Core.Cx + 4, y = dry.Core.y + 6.5f, health = 42, maxHealth = 42 };
        dry.enemies.Add(e2);
        for (int i = 0; i < 120; i++) dry.Tick();
        Check(!e2.dead && dry.bullets.Count == 0 && ((TurretBuilding)dry.world.BuildAt((int)dry.Core.Cx, dry.Core.y + 6)).Starved, "핵에 철광이 없으면 속사포가 멈춘다");

        // 전리품 회수 연구
        var lv = new int[Research.Defs.Length]; lv[Research.Bounty] = 5;
        var rich = EmptySim(new[] { 100, 0, 0 }, Research.Mods(lv));
        rich.Place(Content.RapidTurret, (int)rich.Core.Cx, rich.Core.y + 6);
        Finish(rich);
        var e3 = new Enemy { def = Content.Walker, x = rich.Core.Cx, y = rich.Core.y + 10, health = 42, maxHealth = 42 };
        rich.enemies.Add(e3);
        for (int i = 0; i < 600 && !e3.dead; i++) rich.Tick();
        Check(rich.looted[Content.Iron] == 8, "전리품 회수 5단계 = 2배 (" + rich.looted[Content.Iron] + ")");
    }

    static readonly int[] Full = { 1000, 1000, 1000, 1000, 1000, 1000, 1000 };

    static SimMods Mods(params int[] idLevel)
    {
        var lv = new int[Research.Defs.Length];
        for (int i = 0; i < idLevel.Length; i += 2) lv[idLevel[i]] = idLevel[i + 1];
        return Research.Mods(lv);
    }

    static void Armor()
    {
        Console.WriteLine("방어력");
        Check(Sim.Armored(11, 6) == 5 && Math.Abs(Sim.Armored(11, 20) - 2.2f) < 0.001f, "피해 − 방어력, 최소 20%");

        // 장갑 6 인 철갑병은 속사포(11)에 5 씩만 맞는다
        var sim = EmptySim(new[] { 100, 0, 0 });
        sim.Place(Content.RapidTurret, (int)sim.Core.Cx, sim.Core.y + 6);
        Finish(sim);
        var e = new Enemy { def = Content.Shell, x = sim.Core.Cx + 3, y = sim.Core.y + 6.5f, health = 110, maxHealth = 110, armor = 6 };
        sim.enemies.Add(e);
        for (int i = 0; i < 300 && e.health == 110; i++) sim.Tick();
        Check(e.health == 105, $"철갑병(방어력 6)은 속사포에 5 씩 맞는다 ({110 - e.health})");

        // 강철 방벽(방어력 5)은 보행병(9)에게 4 씩 맞는다
        var s2 = EmptySim(Full);
        s2.Place(Content.SteelWall, 3, 20);
        s2.Place(Content.Wall, 5, 20);
        Finish(s2);
        Check(s2.Core.items[Content.Steel] == 990, "강철 방벽은 강철 10 이 든다");
        s2.enemies.Add(new Enemy { def = Content.Walker, x = 3.5f, y = 20.5f, health = 9999, maxHealth = 9999 });
        s2.enemies.Add(new Enemy { def = Content.Walker, x = 5.5f, y = 20.5f, health = 9999, maxHealth = 9999 });
        s2.Tick();
        var sw = s2.world.BuildAt(3, 20); var nw = s2.world.BuildAt(5, 20);
        Check(Math.Abs(sw.maxHealth - sw.health - 6.7f) < 0.01f && Math.Abs(nw.maxHealth - nw.health - 11.7f) < 0.01f, $"보행병(9 × 1.3 = 11.7)에게 강철 방벽은 6.7, 보통 방벽은 11.7 을 받는다 ({sw.maxHealth - sw.health}, {nw.maxHealth - nw.health})");
        Check(RegionRules.For(0, 1).depth == 0 && Content.Shell.armor > Content.Walker.armor, "적의 방어력은 깊이에 따라 커진다 (지역 1 은 기본값)");
    }

    /// 제자리에 서서 맞기만 하는 적
    static Enemy Dummy(Sim sim, EnemyDef def, float x, float y, float health = 90000, float armor = 0)
    {
        var e = new Enemy { def = def, x = x, y = y, health = health, maxHealth = health, armor = armor, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
        sim.enemies.Add(e);
        return e;
    }

    static void Turrets()
    {
        Console.WriteLine("포탑 (탄종·특수 포탑·표적 기준)");
        var all = Mods(Research.Mortar, 1, Research.Lance, 1, Research.SpecialTurret, 1, Research.AdvTurret, 1);

        // 철갑탄: 방어력을 무시하고, 강철을 쓴다
        var sim = EmptySim(Full, all);
        int tx = (int)sim.Core.Cx, ty = sim.Core.y + 6;
        sim.Place(Content.RapidTurret, tx, ty);
        Finish(sim);
        var t = (TurretBuilding)sim.world.BuildAt(tx, ty);
        var shell = Dummy(sim, Content.Shell, tx + 3.5f, ty + 0.5f, 1000, 6);
        for (int i = 0; i < 300 && shell.health == 1000; i++) sim.Tick();
        int loaded = t.ammo;
        bool swapped = t.SetAmmo(1);
        Check(swapped && loaded > 0 && t.ammo == 0 && !t.SetAmmo(1) && !t.SetAmmo(9), "탄종을 바꾸면 장전해 둔 탄을 버린다 (같은 탄·없는 탄은 무시)");
        float before = shell.health; int steel = sim.Core.items[Content.Steel], iron = sim.Core.items[Content.Iron];
        for (int i = 0; i < 300 && shell.health == before; i++) sim.Tick();
        Check(before - shell.health == 13 && sim.Core.items[Content.Steel] == steel - 1 && sim.Core.items[Content.Iron] == iron,
            $"철갑탄은 철갑병(방어력 6)에게 13 그대로 들어가고 강철을 쓴다 ({before - shell.health})");

        // 소이탄: 불이 붙는다
        t.SetAmmo(2);
        for (int i = 0; i < 300 && shell.burnTicks == 0; i++) sim.Tick();
        Check(shell.burnTicks > 0 && shell.burnDps == 10, "소이탄에 맞은 적은 불탄다");

        // 유도탄: 겨눈 적을 쫓는다
        t.SetAmmo(3);
        sim.bullets.Clear();
        for (int i = 0; i < 300 && sim.bullets.Count == 0; i++) sim.Tick();
        Check(sim.bullets.Count > 0 && sim.bullets[0].target == shell && sim.bullets[0].def.homing > 0, "유도탄은 겨눈 적을 쫓는다");

        // 탄약 자원이 없으면 그 탄은 못 쏜다
        sim.Core.items[Content.Circuit] = 0; t.ammo = 0; sim.bullets.Clear();
        for (int i = 0; i < 120; i++) sim.Tick();
        Check(t.Starved && sim.bullets.Count == 0, "고른 탄의 자원이 핵에 없으면 포탑이 멈춘다");

        // 표적 기준
        var s2 = EmptySim(Full, all);
        float cx = s2.Core.Cx, cy = s2.Core.y + 8.5f;
        var near = Dummy(s2, Content.Walker, cx + 1, cy + 1, 50);          // 가장 가깝다
        var strong = Dummy(s2, Content.Walker, cx - 3, cy + 2, 500);       // 가장 튼튼하다
        var first = Dummy(s2, Content.Walker, cx + 2, cy - 3, 60);         // 핵에 가장 가깝다
        var air = Dummy(s2, Content.Flyer, cx, cy + 4, 30);                // 비행체
        s2.Tick();
        Check(s2.PickEnemy(cx, cy, 6, true, true, TargetMode.Nearest) == near
            && s2.PickEnemy(cx, cy, 6, true, true, TargetMode.Strongest) == strong
            && s2.PickEnemy(cx, cy, 6, true, true, TargetMode.First) == first
            && s2.PickEnemy(cx, cy, 6, true, true, TargetMode.Air) == air, "표적 기준 넷이 저마다 맞는 적을 고른다 (가까운·튼튼한·핵에 가까운·비행)");
        Check(s2.PickEnemy(cx, cy, 6, false, true, TargetMode.Air) == near && s2.PickEnemy(cx, cy, 6, true, false, TargetMode.Strongest) == air,
            "표적 기준은 포탑이 쏠 수 있는 적 안에서만 고른다");

        // 대공포는 지상을 쏘지 않는다
        var s3 = EmptySim(Full, all);
        tx = (int)s3.Core.Cx; ty = s3.Core.y + 6;
        Check(EmptySim(Full).Place(Content.FlakTurret, tx, ty) == PlaceResult.Locked && s3.Place(Content.FlakTurret, tx, ty) == PlaceResult.Ok, "대공포는 특수 포탑 기술을 연구해야 짓는다");
        Finish(s3);
        var g = Dummy(s3, Content.Walker, tx + 3.5f, ty + 0.5f);
        for (int i = 0; i < 240; i++) s3.Tick();
        bool groundSafe = g.health == 90000;
        var fl = Dummy(s3, Content.Flyer, tx - 3f, ty + 0.5f); var fl2 = Dummy(s3, Content.Flyer, tx - 3.6f, ty + 0.5f);
        for (int i = 0; i < 240; i++) s3.Tick();
        Check(groundSafe && g.health == 90000 && fl.health < 90000 && fl2.health < 90000, "대공포는 비행체만 쏘고, 터지며 곁의 비행체도 때린다");

        // 모든 포탑의 모든 탄: 사거리까지 닿고, 겨눌 수 있는 적에게 피해를 준다
        string bad = ""; int kinds = 0;
        foreach (var def in Content.Buildable)
        {
            if (def.kind != BlockKind.Turret) continue;
            for (int k = 0; k < def.ammos.Length; k++)
            {
                kinds++;
                var am = def.ammos[k]; var bl = am.bullet;
                if (am.perItem <= 0 || string.IsNullOrEmpty(am.name) || (!bl.artillery && bl.speed * bl.lifeTicks < def.range)) bad += $" {def.key}:{k}";
                var s = EmptySim(Full, all);
                int px = (int)s.Core.Cx, py = s.Core.y + 6;
                s.Place(def, px, py);
                Finish(s);
                var tb = (TurretBuilding)s.world.BuildAt(px, py);
                tb.SetAmmo(k);
                float reach = Math.Min(def.range - 0.5f, 3f) + def.size * 0.5f;
                var foe = Dummy(s, def.targetGround ? Content.Walker : Content.Flyer, tb.Cx + reach, tb.Cy);
                int had = s.Core.items[am.item];
                for (int i = 0; i < 600; i++) s.Tick();
                if (foe.health >= 90000 || s.Core.items[am.item] >= had) bad += $" 못때림:{def.key}:{k}";
            }
        }
        Check(bad == "" && kinds == 15, $"포탑 7종의 탄 {kinds}가지가 모두 사거리까지 닿고 피해를 준다" + bad);

        // 전격탑: 번개가 곁의 적으로 건너뛴다
        var s4 = EmptySim(Full, all);
        tx = (int)s4.Core.Cx; ty = s4.Core.y + 6;
        s4.Place(Content.TeslaTurret, tx, ty);
        Finish(s4);
        var c1 = Dummy(s4, Content.Walker, tx + 4f, ty + 1f); var c2 = Dummy(s4, Content.Walker, tx + 5.2f, ty + 1f); var c3 = Dummy(s4, Content.Walker, tx + 6.4f, ty + 1f);
        for (int i = 0; i < 200 && c3.health == 90000; i++) s4.Tick();
        Check(c1.health < c2.health && c2.health < c3.health && c3.health < 90000, "전격탑의 번개는 건너뛸 때마다 약해지며 곁의 적을 친다");

        // 점액탑: 범위 안의 적을 느리게 한다
        var s5 = EmptySim(Full, all);
        tx = (int)s5.Core.Cx; ty = s5.Core.y + 6;
        s5.Place(Content.SlimeTurret, tx, ty);
        Finish(s5);
        var w1 = new Enemy { def = Content.Walker, x = tx + 3f, y = ty + 0.5f, health = 90000, maxHealth = 90000, cooldown = 1 << 20 };
        var w2 = new Enemy { def = Content.Walker, x = tx + 3.6f, y = ty + 0.5f, health = 90000, maxHealth = 90000, cooldown = 1 << 20 };
        s5.enemies.Add(w1); s5.enemies.Add(w2);
        for (int i = 0; i < 200 && w2.slowTicks == 0; i++) s5.Tick();
        Check(w1.slowTicks > 0 && w2.slowTicks > 0 && w1.slowMul == 0.55f, "점액탑은 맞은 적과 곁의 적을 느리게 한다");
    }

    static void SupportBlocks()
    {
        Console.WriteLine("지원 건물 (수리탑·증폭탑·역장 발생기)과 자동 재건");
        var all = Mods(Research.SupportBlock, 1);
        var sim = EmptySim(Full, all);
        Check(EmptySim(Full).Place(Content.MendTower, 5, 20) == PlaceResult.Locked, "지원 건물은 지원 건물 기술을 연구해야 짓는다");

        // 수리탑: 범위 안의 건물만 고친다
        sim.Place(Content.MendTower, 5, 20);
        sim.Place(Content.Wall, 7, 20); sim.Place(Content.Wall, 12, 20);
        Finish(sim);
        var nearW = sim.world.BuildAt(7, 20); var farW = sim.world.BuildAt(12, 20); var mender = sim.world.BuildAt(5, 20);
        nearW.health = 100; farW.health = 100; mender.health = 50;
        for (int i = 0; i < Content.MendTower.mendTicks; i++) sim.Tick();
        Check(Math.Abs(nearW.health - (100 + 420 * 0.05f)) < 0.01f && farW.health == 100 && mender.health == 50,
            $"수리탑은 2초마다 범위 안의 건물을 5% 고친다 (자기 자신과 범위 밖은 아니다: {nearW.health}, {farW.health})");

        // 증폭탑: 범위 안의 포탑이 빨리 쏘고, 둘이 겹쳐도 한 번만 듣는다
        int Shots(int amps)
        {
            var s = EmptySim(Full, all);
            int px = (int)s.Core.Cx, py = s.Core.y + 6;
            s.Place(Content.RapidTurret, px, py);
            if (amps >= 1) s.Place(Content.Amplifier, px - 2, py);
            if (amps >= 2) s.Place(Content.Amplifier, px - 2, py + 2);
            Finish(s);
            Dummy(s, Content.Walker, px + 3.5f, py + 0.5f);
            var tb = (TurretBuilding)s.world.BuildAt(px, py);
            int shots = 0, last = tb.reload;
            for (int i = 0; i < 600; i++) { s.Tick(); if (tb.reload > last) shots++; last = tb.reload; }
            return shots;
        }
        int plain = Shots(0), one = Shots(1), two = Shots(2);
        Check(plain == 30 && one == 40 && two == one, $"증폭탑 곁의 포탑은 1.33배 빨리 쏘고, 겹쳐도 한 번만 듣는다 (10초에 {plain} → {one} → {two}발)");

        // 역장 발생기: 범위 안 건물이 받는 피해를 먼저 받고, 맞지 않으면 다시 차오른다
        var s2 = EmptySim(Full, all);
        s2.Place(Content.ShieldGen, 5, 20);
        s2.Place(Content.Wall, 8, 20); s2.Place(Content.Wall, 14, 20);
        Finish(s2);
        var gen = (SupportBuilding)s2.world.BuildAt(5, 20);
        for (int i = 0; i < 60 * 20; i++) s2.Tick();
        bool full = gen.shieldHp == 600;
        var inW = s2.world.BuildAt(8, 20); var outW = s2.world.BuildAt(14, 20);
        var a1 = new Enemy { def = Content.Walker, x = 8.5f, y = 20.5f, health = 9999, maxHealth = 9999 };
        var a2 = new Enemy { def = Content.Walker, x = 14.5f, y = 20.5f, health = 9999, maxHealth = 9999 };
        s2.enemies.Add(a1); s2.enemies.Add(a2);
        s2.Tick();
        Check(full && inW.health == inW.maxHealth && Math.Abs(gen.shieldHp - (600 - 11.7f)) < 0.01f && Math.Abs(outW.maxHealth - outW.health - 11.7f) < 0.01f,
            $"역장은 범위 안 건물의 피해를 대신 받는다 (역장 {gen.shieldHp}, 범위 밖 방벽 −{outW.maxHealth - outW.health})");
        gen.shieldHp = 5; a1.cooldown = 0;
        s2.Tick();
        Check(gen.shieldHp == 0 && Math.Abs(inW.maxHealth - inW.health - 6.7f) < 0.01f, $"역장이 다 닳으면 남은 피해는 건물이 받는다 (−{inW.maxHealth - inW.health})");
        s2.enemies.Clear();
        for (int i = 0; i < Content.ShieldGen.shieldDelay - 2; i++) s2.Tick();
        bool waiting = gen.shieldHp == 0;
        for (int i = 0; i < 62; i++) s2.Tick();
        Check(waiting && gen.shieldHp > 30 && gen.shieldHp < 45, $"역장은 5초 동안 맞지 않아야 다시 차오른다 (1초 뒤 {gen.shieldHp:0.0})");

        // 자동 재건: 적에게 부서진 건물을 잠시 뒤 같은 자리에 다시 놓는다
        var s3 = EmptySim(new[] { 100, 0, 0 });
        s3.autoRebuild = true;
        s3.Place(Content.RapidTurret, 5, 20); s3.Place(Content.Wall, 8, 20);
        Finish(s3);
        ((TurretBuilding)s3.world.BuildAt(5, 20)).SetAmmo(0);
        s3.world.BuildAt(5, 20).health = 0;
        s3.Tick();
        s3.Remove(8, 20);
        int ironNow = s3.Core.items[Content.Iron];
        bool noted = s3.ruins.Count == 1 && s3.world.BuildAt(5, 20) == null;
        for (int i = 0; i < Sim.AutoRebuildDelay - 40; i++) s3.Tick();
        bool waits = s3.world.BuildAt(5, 20) == null;
        for (int i = 0; i < 80; i++) s3.Tick();
        var site = s3.world.BuildAt(5, 20);
        Finish(s3);
        Check(noted && waits && site != null && site.def == Content.RapidTurret && site.Built && s3.ruins.Count == 0 && s3.Core.items[Content.Iron] == ironNow - 35 && s3.world.BuildAt(8, 20) == null,
            "자동 재건: 부서진 포탑은 10초 뒤 정가로 다시 지어지고, 직접 철거한 건물은 다시 짓지 않는다");
        s3.Core.items[Content.Iron] = 0;
        s3.world.BuildAt(5, 20).health = 0;
        for (int i = 0; i < Sim.AutoRebuildDelay + 120; i++) s3.Tick();
        bool poor = s3.world.BuildAt(5, 20) == null && s3.ruins.Count == 1;
        s3.Core.items[Content.Iron] = 100;
        for (int i = 0; i < 60; i++) s3.Tick();
        Check(poor && s3.world.BuildAt(5, 20) != null, "자동 재건: 자원이 없으면 기다렸다가 생기면 놓는다");
        s3.world.BuildAt(5, 20).health = 0;
        s3.Tick();
        bool gone = s3.Remove(5, 20) && s3.ruins.Count == 0;
        s3.autoRebuild = false;
        s3.Place(Content.Wall, 5, 20); Finish(s3);
        s3.world.BuildAt(5, 20).health = 0; s3.Tick();
        Check(gone && s3.ruins.Count == 0, "부서진 자리는 철거로 지울 수 있고, 자동 재건을 끄면 적어 두지 않는다");
    }

    static void Units()
    {
        Console.WriteLine("유닛");
        var locked = EmptySim(Full);
        Check(locked.Place(Content.RobotFactory, 3, 20) == PlaceResult.Locked, "연구하지 않으면 로봇 공장을 못 짓는다");

        var sim = EmptySim(Full, Mods(Research.RobotFactory, 1, Research.Airfield, 1));
        var core = sim.Core;
        Check(sim.Place(Content.RobotFactory, 3, 20) == PlaceResult.Ok, "로봇 공장 기술을 연구하면 지을 수 있다");
        Finish(sim);
        sim.Tick();
        var f = (FactoryBuilding)sim.world.BuildAt(3, 20);
        Check(f.making == Content.Scout && sim.units.Count == 0, "다 지어진 공장은 바로 척후 로봇 생산을 시작한다");
        Check(core.items[Content.Iron] == 1150 - 120 - 25 && core.items[Content.Circuit] == 1000 - 8 && core.items[Content.Steel] == 1000 - 40, "유닛 비용은 생산을 시작할 때 핵에서 나간다");
        Check(sim.SupplyUsed == 1, "만들고 있는 유닛도 한도를 차지한다");
        for (int i = 0; i < Content.Scout.buildTicks; i++) sim.Tick();
        Check(sim.units.Count == 1, $"척후 로봇은 {Content.Scout.buildTicks / 60}초에 한 대 나온다");
        for (int i = 0; i < Content.Scout.buildTicks * 4; i++) sim.Tick();
        Check(sim.units.Count == 4 && f.status == FactoryStatus.Cap && sim.UnitCap == 4, $"유닛 한도 4 가 차면 생산이 멈춘다 ({sim.units.Count}대)");
        Check(core.items[Content.Iron] == 1150 - 120 - 100, "한도가 찬 뒤에는 자원을 더 쓰지 않는다");
        for (int i = 0; i < 60 * 60; i++) sim.Tick();   // 공장에서 핵 옆까지 걸어갈 시간
        bool gathered = true;
        foreach (var u in sim.units) if (Math.Abs(u.x - f.rallyX) > 1.2f || Math.Abs(u.y - f.rallyY) > 1.2f) gathered = false;
        Check(gathered, "유닛은 집결 지점에 모인다");

        // 전투: 탄약 없이 싸우고 전리품은 핵으로
        int iron = core.items[Content.Iron];
        var e = new Enemy { def = Content.Walker, x = f.rallyX + 2, y = f.rallyY, health = 42, maxHealth = 42 };
        sim.enemies.Add(e);
        for (int i = 0; i < 300 && !e.dead; i++) sim.Tick();
        Check(e.dead && core.items[Content.Iron] == iron + 4, "유닛이 적을 처치한다 (탄약은 쓰지 않고 전리품 4 를 얻는다)");

        // 태세
        var far = new Enemy { def = Content.Walker, x = f.rallyX + 12, y = f.rallyY, health = 99999, maxHealth = 99999 };
        sim.enemies.Add(far);
        for (int i = 0; i < 20; i++) sim.Tick();
        bool idle = true; foreach (var u in sim.units) if (u.target != null) idle = false;
        Check(idle, "방어 태세: 집결 지점에서 먼 적은 쫓지 않는다");
        Check(sim.Core.DistTo(f.rallyX, f.rallyY) <= 2.5f, "기본 집결 지점은 핵 바로 옆이다 (방어 태세 = 핵 수비)");
        int sp0 = sim.world.spawns[0];
        float spx = sp0 % sim.world.W + 0.5f, spy = sp0 / sim.world.W + 0.5f;
        float before = 0; foreach (var u in sim.units) before += Math.Abs(u.x - spx) + Math.Abs(u.y - spy);
        sim.stance = UnitStance.Attack;
        for (int i = 0; i < 120; i++) sim.Tick();
        float after = 0; bool ignore = true;
        foreach (var u in sim.units) { after += Math.Abs(u.x - spx) + Math.Abs(u.y - spy); if (u.target != null) ignore = false; }
        Check(after < before - 8 && ignore, "공격 태세: 출현 지점으로 나아가고, 가는 길에서 먼 적은 쫓지 않는다");
        var atSpawn = new Enemy { def = Content.Walker, x = spx + 2, y = spy - 2, health = 99999, maxHealth = 99999 };
        sim.enemies.Add(atSpawn);
        for (int i = 0; i < 20; i++) sim.Tick();
        bool chase = true; foreach (var u in sim.units) if (u.target != atSpawn) chase = false;
        Check(chase, "공격 태세: 출현 지점 둘레의 적을 노린다");
        sim.enemies.Clear();
        sim.stance = UnitStance.Defend;
        for (int i = 0; i < 60 * 60; i++) sim.Tick();
        bool home = true; foreach (var u in sim.units) if (Math.Abs(u.x - f.rallyX) > 1.2f || Math.Abs(u.y - f.rallyY) > 1.2f) home = false;
        Check(home, "방어 태세로 돌리면 핵 옆 집결 지점으로 돌아온다");

        // 유닛을 잃으면 다시 만들고, 연구하지 않은 단계는 못 만든다
        sim.units[0].dead = true;
        sim.Tick(); sim.Tick();
        Check(sim.units.Count == 3 && f.making == Content.Scout, "유닛을 잃으면 공장이 다시 만든다");
        f.product = 1;
        for (int i = 0; i < Content.Scout.buildTicks + 2; i++) sim.Tick();
        sim.units[0].dead = true;
        sim.Tick(); sim.Tick();
        Check(f.status == FactoryStatus.Locked && f.making == null, "기체 개량 I 을 연구하지 않으면 돌격 로봇은 못 만든다");
        f.product = -1;
        sim.Tick();
        Check(f.status == FactoryStatus.Stopped, "생산 정지");

        // 적이 유닛을 친다: 보행병 9 − 척후 로봇 방어력 2 = 7
        var s2 = EmptySim(Full, Mods(Research.RobotFactory, 1));
        s2.Place(Content.RobotFactory, 3, 20);
        var f2 = (FactoryBuilding)s2.world.BuildAt(3, 20);
        s2.SpawnUnit(Content.Scout, f2);
        var scout = s2.units[0];
        s2.enemies.Add(new Enemy { def = Content.Walker, x = scout.x + 0.3f, y = scout.y, health = 99999, maxHealth = 99999 });
        s2.Tick();
        Check(Math.Abs(scout.maxHealth - scout.health - 9.7f) < 0.01f, $"보행병(11.7)은 방어력 2 인 척후 로봇에게 9.7 을 준다 ({scout.maxHealth - scout.health})");
        for (int i = 0; i < 60 * 40 && !scout.dead; i++) s2.Tick();
        Check(scout.dead && s2.unitsLost == 1 && s2.units.Count == 0, "체력이 다하면 유닛을 잃는다");

        // 지상 적은 비행 유닛을 못 친다
        var s3 = EmptySim(Full, Mods(Research.Airfield, 1));
        s3.Place(Content.Airfield, 3, 20);
        var f3 = (FactoryBuilding)s3.world.BuildAt(3, 20);
        s3.SpawnUnit(Content.Recon, f3);
        var recon = s3.units[0];
        s3.enemies.Add(new Enemy { def = Content.Walker, x = recon.x + 0.3f, y = recon.y, health = 99999, maxHealth = 99999 });
        for (int i = 0; i < 120; i++) s3.Tick();
        Check(recon.health == recon.maxHealth, "보행병은 비행 유닛을 못 친다");
        s3.enemies.Clear();
        s3.SpawnUnit(Content.Bomber, f3);
        var bomber = s3.units[1];
        s3.enemies.Add(new Enemy { def = Content.Flyer, x = f3.rallyX + 1, y = f3.rallyY + 1, health = 99999, maxHealth = 99999 });
        for (int i = 0; i < 30; i++) s3.Tick();
        Check(bomber.target == null && recon.target != null, "폭격기는 공중의 적을 노리지 않는다");

        // 기체 강화·지휘 체계 연구
        var s4 = EmptySim(Full, Mods(Research.RobotFactory, 1, Research.UnitPower, 5, Research.Command, 3));
        s4.Place(Content.RobotFactory, 3, 20);
        s4.SpawnUnit(Content.Scout, (FactoryBuilding)s4.world.BuildAt(3, 20));
        Check(Math.Abs(s4.units[0].maxHealth - 130 * 1.6f) < 0.01f && s4.UnitCap == 10, "기체 강화 5단계 = 체력 +60%, 지휘 체계 3단계 = 한도 10");

        // 실제 맵: 지상 유닛은 벽을 돌아서 가고, 비행 유닛은 넘어간다
        var real = new Sim(0, 30, Full, Mods(Research.RobotFactory, 1, Research.Airfield, 1));
        real.waveCountdown = int.MaxValue;
        var w = real.world;
        FactoryBuilding rf = null, af = null;
        for (int y = real.Core.y - 6; y < real.Core.y + 12 && af == null; y++)
            for (int x = 1; x < w.W - 2 && af == null; x++)
            {
                var def = rf == null ? Content.RobotFactory : Content.Airfield;
                if (real.Place(def, x, y) != PlaceResult.Ok) continue;
                if (rf == null) rf = (FactoryBuilding)w.BuildAt(x, y); else af = (FactoryBuilding)w.BuildAt(x, y);
            }
        int sp = w.spawns[0];
        float sx = sp % w.W + 0.5f, sy = sp / w.W + 0.5f;
        int wallTile = -1;
        for (int i = 0; i < w.tiles.Length && wallTile < 0; i++) if (w.tiles[i].wall && i % w.W > 0 && i / w.W > 0) wallTile = i;
        Check(!real.SetRally(rf, wallTile % w.W + 0.5f, wallTile / w.W + 0.5f) && real.SetRally(af, wallTile % w.W + 0.5f, wallTile / w.W + 0.5f), "집결 지점: 로봇 공장은 벽 위에 못 두고 비행장은 둘 수 있다");
        Check(real.SetRally(rf, sx, sy) && real.SetRally(af, sx, sy), "집결 지점을 출현 지점으로 옮긴다");
        real.SpawnUnit(Content.Scout, rf);
        real.SpawnUnit(Content.Recon, af);
        Unit g = real.units[0], a = real.units[1];
        g.ox = g.oy = a.ox = a.oy = 0;
        bool onWall = false; int gTicks = -1, aTicks = -1;
        for (int i = 0; i < 60 * 240 && (gTicks < 0 || aTicks < 0); i++)
        {
            real.Tick();
            if (w.Solid((int)g.x, (int)g.y)) onWall = true;
            if (gTicks < 0 && Math.Abs(g.x - sx) < 0.5f && Math.Abs(g.y - sy) < 0.5f) gTicks = i;
            if (aTicks < 0 && Math.Abs(a.x - sx) < 0.5f && Math.Abs(a.y - sy) < 0.5f) aTicks = i;
        }
        Check(gTicks > 0 && !onWall, $"지상 유닛은 벽을 밟지 않고 출현 지점까지 간다 ({gTicks / 60f:0}초)");
        Check(aTicks > 0 && aTicks < gTicks, $"비행 유닛은 곧장 날아가 먼저 도착한다 ({aTicks / 60f:0}초)");
    }

    static void Assault()
    {
        Console.WriteLine("적 거점");
        Check(RegionRules.IsAssault(15) && RegionRules.IsAssault(95) && !RegionRules.IsAssault(5) && !RegionRules.IsAssault(20) && !RegionRules.IsAssault(10), "적 거점 지역은 15, 25, … 95");
        var sim = new Sim(0, 15, Full, Mods(Research.RobotFactory, 1, Research.Airfield, 1));
        var w = sim.world;
        int cores = 0, turrets = 0, walls = 0;
        foreach (var b in w.buildings) { if (b is EnemyCoreBuilding) cores++; if (b is EnemyTurretBuilding) turrets++; if (b.def == Content.EnemyWall) walls++; }
        bool onSpawn = true;
        foreach (int s in w.spawns) if (!(w.tiles[s].build is EnemyCoreBuilding) || sim.field.cost[s] == float.MaxValue) onSpawn = false;
        Check(cores == sim.rules.spawnCount && sim.EnemyCores == cores && onSpawn, $"출현 지점마다 적 핵이 서 있고 내 핵과 길이 이어진다 (적 핵 {cores})");
        Check(turrets >= 2 * cores && walls > 0, $"적 핵 둘레에 적 포탑과 적 방벽이 있다 (포탑 {turrets}, 방벽 {walls})");
        int sx = w.spawns[0] % w.W, sy = w.spawns[0] / w.W;
        bool near = true, farOk = false;
        for (int y = 1; y < w.H - 1; y++)
            for (int x = 1; x < w.W - 1; x++)
            {
                var r = sim.CanPlace(Content.Wall, x, y, false);
                bool inside = false;
                foreach (int s in w.spawns) if (Math.Abs(s % w.W - x) <= Sim.EnemyCoreSafeRadius && Math.Abs(s / w.W - y) <= Sim.EnemyCoreSafeRadius) inside = true;
                if (inside && r == PlaceResult.Ok) near = false;
                if (!inside && r == PlaceResult.Ok) farOk = true;
            }
        Check(near && farOk, $"적 핵에서 {Sim.EnemyCoreSafeRadius}칸 안에는 건물을 지을 수 없다");
        var et = w.buildings.Find(b => b is EnemyTurretBuilding);
        Check(sim.Place(Content.EnemyCore, 3, 3) == PlaceResult.Blocked && !sim.Remove(et.x, et.y), "적 건물은 짓거나 철거할 수 없다");
        sim.wave = 999;
        Check(!sim.AllWavesSent, "적 거점 지역은 웨이브가 끝나지 않는다");
        sim.wave = 0;

        // 적은 자기 건물을 치지 않고 지나간다. 아무것도 안 하면 진다
        var idle = new Sim(0, 15);
        idle.DisableCommander();
        float hp0 = 0; foreach (var b in idle.world.buildings) if (b.def.enemy) hp0 += b.health;
        while (idle.state == SimState.Playing && idle.tick < 60 * 60 * 30) idle.Tick();
        float hp1 = 0; foreach (var b in idle.world.buildings) if (b.def.enemy) hp1 += b.health;
        Check(idle.state == SimState.Lost && hp0 == hp1 && idle.kills == 0, "적은 자기 거점을 치지 않고 내 핵으로 온다");

        // 유닛만 적 건물을 부술 수 있다: 방어 태세면 가지 않고, 공격 태세면 적 핵까지 부순다
        sim.waveCountdown = int.MaxValue;
        FactoryBuilding rf = null;
        for (int y = sim.Core.y + 4; y < w.H && rf == null; y++)
            for (int x = 2; x < w.W - 3 && rf == null; x++)
                if (sim.Place(Content.RobotFactory, x, y) == PlaceResult.Ok) rf = (FactoryBuilding)w.BuildAt(x, y);
        rf.product = -1;
        for (int i = 0; i < 4; i++) sim.SpawnUnit(Content.Heavy, rf);
        for (int i = 0; i < 2; i++) sim.SpawnUnit(Content.Bomber, rf);
        for (int i = 0; i < 60 * 30; i++) sim.Tick();
        Check(sim.structuresDestroyed == 0 && sim.EnemyCores == cores, "방어 태세의 유닛은 적 거점으로 가지 않는다");
        sim.stance = UnitStance.Attack;
        int coresBefore = sim.EnemyCores, spawnsBefore = w.spawns.Count, crystal = sim.Core.items[Content.Crystal];
        for (int i = 0; i < 60 * 60 * 20 && sim.EnemyCores == coresBefore; i++) sim.Tick();
        Check(sim.EnemyCores == coresBefore - 1 && w.spawns.Count == spawnsBefore - 1, $"공격 태세의 유닛이 적 핵 하나를 부수면 그 출현 지점이 사라진다 ({sim.tick / 60}초, 부순 건물 {sim.structuresDestroyed})");
        Check(sim.Core.items[Content.Crystal] >= crystal + 30, "적 핵을 부수면 전리품이 들어온다");
        bool hurt = sim.unitsLost > 0; foreach (var u in sim.units) if (u.health < u.maxHealth) hurt = true;
        Check(hurt, "적 포탑이 유닛을 쏜다");
        for (int i = 0; i < 60 * 60 * 30 && sim.state == SimState.Playing; i++) sim.Tick();
        Check(sim.state == SimState.Won && sim.EnemyCores == 0, $"적 핵을 모두 부수면 점령 ({sim.tick / 60}초, 잃은 유닛 {sim.unitsLost})");

        BaseRules.RegionBonus(25, out int item, out float perMin);
        BaseRules.RegionBonus(22, out int item2, out float perMin2);
        Check(item == Content.Iron && perMin == 6 && perMin2 == 3, "적 거점 지역의 점령 보상은 두 배");
    }

    static float Dist(Unit u, float x, float y) => (float)Math.Sqrt((u.x - x) * (u.x - x) + (u.y - y) * (u.y - y));

    /// 공장에서 만드는 모든 유닛을 하나씩 빈 맵에 세워 30초 돌려 본다: 정의가 말이 되는지, 무기가 있으면 겨눌 수 있는 적에게 피해를 주는지
    static void AllUnits()
    {
        var lvAll = new int[Research.Defs.Length];
        foreach (int id in new[] { Research.RobotFactory, Research.Airfield, Research.Support, Research.Legs, Research.Shipyard, Research.Tank, Research.Hover, Research.Assembler, Research.Transcend, Research.Tier2, Research.Tier3, Research.Tier4, Research.Tier5 }) lvAll[id] = 1;
        var mods = Research.Mods(lvAll);
        var keys = new HashSet<string>(); var names = new HashSet<string>();
        bool unique = true, sane = true, fights = true; string bad = "";
        foreach (var u in Content.Units) { if (!keys.Add(u.key) || !names.Add(u.name)) { unique = false; bad += " " + u.key; } }
        int made = 0;
        foreach (var fdef in new[] { Content.RobotFactory, Content.Airfield, Content.SupportShop, Content.LegShop, Content.TankFactory, Content.HoverShop, Content.Shipyard, Content.Assembler })
        {
            int lastTier = 0;
            foreach (var u in fdef.units)
            {
                made++;
                bool weapon = u.bullet != null || u.fieldDamage > 0;
                bool lays = u.minelay != null;
                if (u.tier < lastTier || u.supply != u.tier || u.cost.Length == 0 || u.buildTicks <= 0 || u.health <= 0 || u.speed <= 0 || string.IsNullOrEmpty(u.desc)
                    || (weapon && !u.suicide && (u.range <= 0 || u.reloadTicks <= 0)) || (weapon && !u.targetAir && !u.targetGround)
                    || (u.bullet != null && !u.suicide && !u.bullet.artillery && u.bullet.speed * u.bullet.lifeTicks < u.range) || (u.tier >= 2 && u != Content.Climber && u.tech < 0))
                { sane = false; bad += " " + u.key; }
                lastTier = u.tier;

                var sim = EmptySim(Full, mods);
                if (!sim.IsUnlocked(u)) { sane = false; bad += " 잠김:" + u.key; }
                sim.Place(Content.SupportShop, 3, 20);
                Finish(sim);
                var shop = (FactoryBuilding)sim.world.BuildAt(3, 20);
                shop.product = -1; shop.making = null;
                sim.units.Clear();
                sim.SpawnUnit(u, shop);
                var unit = sim.units[0];
                unit.x = 10.5f; unit.y = 30.5f; unit.ox = unit.oy = 0; sim.SetRally(shop, 10.5f, 30.5f);
                float reach = Math.Min(2f, u.range > 0 ? u.range : 2f);
                var ground = new Enemy { def = Content.Walker, x = unit.x + reach, y = unit.y, health = 90000, maxHealth = 90000, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
                var air = new Enemy { def = Content.Flyer, x = unit.x - reach, y = unit.y, health = 90000, maxHealth = 90000, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
                if (u.targetGround) sim.enemies.Add(ground); else sim.enemies.Add(air);
                for (int i = 0; i < 60 * 30; i++)
                {
                    sim.Tick();
                    ground.slowTicks = air.slowTicks = 1 << 20; ground.slowMul = air.slowMul = 0;   // 둔화 유닛이 덮어써도 제자리에 둔다
                }
                float dealt = 180000 - ground.health - air.health;
                if (weapon && dealt <= 0) { fights = false; bad += " 못때림:" + u.key; }
                if (!weapon && !lays && dealt > 0) { fights = false; bad += " 때림:" + u.key; }
                if (unit.dead != u.suicide) { fights = false; bad += " 생사:" + u.key; }
            }
        }
        Check(unique, "유닛의 key 와 이름은 겹치지 않는다" + bad);
        Check(made == 90 && sane, "공장 유닛 90종의 정의가 말이 된다 (단계 순, 한도 = 단계, 비용·연구·사거리)" + bad);
        Check(fights, "무기가 있는 유닛은 모두 겨눌 수 있는 적에게 피해를 주고, 없는 유닛은 주지 않는다" + bad);
    }

    static void NewUnits()
    {
        Console.WriteLine("새 유닛 (지원·다리·해상·수송·지휘기)");
        bool twelve = true;
        foreach (var fb in new[] { Content.RobotFactory, Content.Airfield, Content.SupportShop, Content.LegShop, Content.TankFactory, Content.HoverShop, Content.Shipyard }) if (fb.units.Length != 12) twelve = false;
        if (Content.Assembler.units.Length != 6) twelve = false;
        Check(Content.Units.Length == 92 && twelve, $"유닛 {Content.Units.Length}종 (공장 일곱 곳에 12종씩, 심연 조립소에 6종, 함재기와 지휘기)");
        var all = Mods(Research.RobotFactory, 1, Research.Airfield, 1, Research.Support, 1, Research.Legs, 1, Research.Shipyard, 1, Research.Tier2, 1, Research.Tier3, 1);
        var lockedSim = EmptySim(Full, all);
        lockedSim.Place(Content.RobotFactory, 3, 20);
        Check(!lockedSim.IsUnlocked(Content.Siege) && !lockedSim.IsUnlocked(Content.Carrier) && lockedSim.IsUnlocked(Content.Heavy), "4·5단계는 기체 개량 III·IV 를 연구해야 한다");

        // 호수: 벽 덩어리 안에만 생기고, 지상 길은 그대로다
        int withWater = 0, bad = 0;
        for (int a = 0; a < Content.Abysses.Length; a++)
            for (int r = 1; r <= 100; r += 3)
            {
                var m = new Sim(a, r);
                int water = 0;
                foreach (var t in m.world.tiles) { if (t.water) { water++; if (!t.wall) bad++; } }
                if (water >= 10) withWater++;
            }
        Check(withWater >= 95 && bad == 0, $"거의 모든 맵에 호수가 있고(102개 중 {withWater}개), 물 칸은 지상에서 벽이다");

        // 조선소와 함정
        var sim = new Sim(0, 30, Full, all);
        sim.DisableCommander();
        sim.waveCountdown = int.MaxValue;
        var w = sim.world;
        int shoreX = -1, shoreY = -1, dryX = -1, dryY = -1;
        for (int y = 1; y < w.H - 2; y++)
            for (int x = 1; x < w.W - 2; x++)
            {
                var r = sim.CanPlace(Content.Shipyard, x, y, false);
                if (r == PlaceResult.Ok && shoreX < 0 && Math.Abs(y - sim.Core.y) < 25) { shoreX = x; shoreY = y; }
                if (r == PlaceResult.NeedsWater && dryX < 0) { dryX = x; dryY = y; }
            }
        Check(shoreX >= 0 && dryX >= 0 && sim.Place(Content.Shipyard, dryX, dryY) == PlaceResult.NeedsWater && sim.Place(Content.Shipyard, shoreX, shoreY) == PlaceResult.Ok, "조선소는 물가에만 지을 수 있다");
        var yard = (FactoryBuilding)w.BuildAt(shoreX, shoreY);
        Check(w.tiles[w.Idx((int)yard.rallyX, (int)yard.rallyY)].water && !sim.SetRally(yard, sim.Core.Cx, sim.Core.Cy + 3), "조선소의 집결 지점은 물 위여야 한다");
        sim.SpawnUnit(Content.Patrol, yard);
        var boat = sim.units[0];
        // 호수의 가장 먼 물 칸으로 보낸다
        int farTile = -1; float farD = -1;
        for (int i = 0; i < w.tiles.Length; i++)
            if (w.tiles[i].water) { float dd = Dist(boat, i % w.W + 0.5f, i / w.W + 0.5f); if (dd > farD) { farD = dd; farTile = i; } }
        sim.SetRally(yard, farTile % w.W + 0.5f, farTile / w.W + 0.5f);
        bool offWater = false; float startX = boat.x, startY = boat.y;
        for (int i = 0; i < 60 * 60; i++) { sim.Tick(); if (!w.tiles[w.Idx((int)boat.x, (int)boat.y)].water) offWater = true; }
        Check(!offWater && w.tiles[w.Idx((int)startX, (int)startY)].water, "함정은 물 위에서 나오고 물 밖으로 나가지 않는다");
        var e = new Enemy { def = Content.Walker, x = boat.x + 3, y = boat.y, health = 60, maxHealth = 60 };
        sim.enemies.Add(e);
        for (int i = 0; i < 400 && !e.dead; i++) sim.Tick();
        Check(e.dead, "함정이 사거리 안의 적을 쏜다");

        // 등반 로봇은 벽을 타 넘는다
        FactoryBuilding leg = null;
        for (int y = sim.Core.y + 4; y < w.H && leg == null; y++)
            for (int x = 2; x < w.W - 3 && leg == null; x++)
                if (sim.Place(Content.LegShop, x, y) == PlaceResult.Ok) leg = (FactoryBuilding)w.BuildAt(x, y);
        int sp = w.spawns[0];
        float sx = sp % w.W + 0.5f, sy = sp / w.W + 0.5f;
        int wallTile = -1;
        for (int i = 0; i < w.tiles.Length && wallTile < 0; i++) if (w.tiles[i].wall && i % w.W > 1 && i / w.W > 1) wallTile = i;
        Check(sim.SetRally(leg, wallTile % w.W + 0.5f, wallTile / w.W + 0.5f) && sim.SetRally(leg, sx, sy), "다리 공방의 집결 지점은 벽 위에도 둘 수 있다");
        sim.SpawnUnit(Content.Climber, leg);
        var climber = sim.units[sim.units.Count - 1];
        climber.ox = climber.oy = 0;
        bool overWall = false; int arrive = -1;
        for (int i = 0; i < 60 * 120 && arrive < 0; i++)
        {
            sim.Tick();
            if (w.Solid((int)climber.x, (int)climber.y)) overWall = true;
            if (Dist(climber, sx, sy) < 0.5f) arrive = i;
        }
        float straight = Dist(new Unit { x = leg.Cx, y = leg.Cy }, sx, sy) / Content.Climber.speed;
        Check(arrive > 0 && arrive <= straight + 5, $"등반 로봇은 곧장 간다 ({arrive / 60f:0}초, 벽을 넘음 {overWall})");

        // 자폭 벌레
        var s2 = EmptySim(Full, all);
        s2.Place(Content.LegShop, 3, 20);
        var shop = (FactoryBuilding)s2.world.BuildAt(3, 20);
        s2.SpawnUnit(Content.Bug, shop);
        var bug = s2.units[0];
        for (int i = 0; i < 3; i++) s2.enemies.Add(new Enemy { def = Content.Walker, x = shop.rallyX + 2 + i * 0.4f, y = shop.rallyY, health = 55, maxHealth = 55 });
        for (int i = 0; i < 600 && !bug.dead; i++) s2.Tick();
        s2.Tick();
        Check(bug.dead && s2.kills == 3 && s2.units.Count == 0 && s2.unitsLost == 0, $"자폭 벌레가 붙어서 터져 둘레의 적을 함께 잡는다 (처치 {s2.kills})");

        // 흡수 로봇: 준 피해의 일부만큼 체력을 되찾고, 벽을 타 넘는다
        var s2b = EmptySim(Full, all);
        s2b.DisableCommander();
        s2b.Place(Content.LegShop, 3, 20);
        Finish(s2b);
        var shop2 = (FactoryBuilding)s2b.world.BuildAt(3, 20);
        shop2.product = -1; shop2.making = null;   // 공장이 만들던 자폭 벌레가 끼어들지 않게
        Check(s2b.IsUnlocked(Content.Leech) && !EmptySim(Full, Mods(Research.Legs, 1)).IsUnlocked(Content.Leech) && Content.Leech.legs, "흡수 로봇은 기체 개량 II 를 연구해야 한다");
        s2b.SpawnUnit(Content.Leech, shop2);
        var leech = s2b.units[0];
        leech.health = 300;
        var prey = new Enemy { def = Content.Walker, x = shop2.rallyX + 2, y = shop2.rallyY, health = 5000, maxHealth = 5000 };
        s2b.enemies.Add(prey);
        for (int i = 0; i < 60 * 60 && prey.health > 4500; i++) { s2b.Tick(); prey.cooldown = 99; }
        float dealt = 5000 - prey.health, gained = leech.health - 300;
        Check(dealt > 400 && Math.Abs(gained - dealt * Content.Leech.drain) < 1f, $"흡수 로봇은 준 피해의 40% 를 되찾는다 (피해 {dealt:0}, 회복 {gained:0})");
        leech.health = leech.maxHealth - 1;
        for (int i = 0; i < 120; i++) { s2b.Tick(); prey.cooldown = 99; }
        Check(leech.health == leech.maxHealth, "흡수 로봇의 체력은 최대치를 넘지 않는다");

        // 산성 로봇: 범위 안 지상의 적을 느리게 한다
        var s2c = EmptySim(Full, all);
        s2c.Place(Content.LegShop, 3, 20);
        Finish(s2c);
        var shop3 = (FactoryBuilding)s2c.world.BuildAt(3, 20);
        shop3.product = -1; shop3.making = null;
        s2c.units.Clear();
        s2c.SpawnUnit(Content.Spitter, shop3);
        var spit = s2c.units[0];
        spit.x = shop3.rallyX; spit.y = shop3.rallyY + 6;
        Enemy Far(EnemyDef def, float dx) => new Enemy { def = def, x = shop3.rallyX + dx, y = shop3.rallyY + 9.5f, health = 9000, maxHealth = 9000, cooldown = 1 << 20 };
        var slowA = Far(Content.Walker, 0); var slowB = Far(Content.Walker, 0.8f); var slowBoss = Far(Content.Chief, -0.8f); var slowAir = Far(Content.Flyer, 0.3f);
        s2c.enemies.AddRange(new[] { slowA, slowB, slowBoss, slowAir });
        for (int i = 0; i < 60 * 10 && slowA.slowTicks == 0; i++) s2c.Tick();
        Check(slowA.slowTicks > 0 && slowB.slowTicks > 0 && slowA.slowMul == 0.5f && slowBoss.slowMul == 0.75f && slowAir.slowTicks == 0 && slowAir.health == 9000,
            $"산성 로봇은 범위 안 지상의 적을 늦춘다 (보스는 절반만, 공중은 안 맞음: {slowA.slowMul}, {slowBoss.slowMul})");
        var free = new Enemy { def = Content.Walker, x = 3.5f, y = 3.5f, health = 9000, maxHealth = 9000 };
        var held = new Enemy { def = Content.Walker, x = 3.5f, y = 3.5f, health = 9000, maxHealth = 9000, slowTicks = 60, slowMul = 0.5f };
        s2c.units.Clear(); s2c.enemies.Clear(); s2c.enemies.Add(free); s2c.enemies.Add(held);
        float fy0 = free.y;
        for (int i = 0; i < 60; i++) s2c.Tick();
        float movedFree = Dist(new Unit { x = free.x, y = free.y }, 3.5f, fy0), movedHeld = Dist(new Unit { x = held.x, y = held.y }, 3.5f, fy0);
        Check(movedFree > 0.5f && Math.Abs(movedHeld - movedFree * 0.5f) < 0.05f && held.slowTicks == 0, $"느려진 적은 절반 속도로 걷는다 ({movedHeld:0.00} / {movedFree:0.00}칸)");

        // 채굴 드론: 집결 지점에서 가장 가까운 벽을 캐서 핵으로 나른다
        var s2d = EmptySim(Full, all);
        s2d.Place(Content.SupportShop, 3, 20);
        Finish(s2d);
        var shop4 = (FactoryBuilding)s2d.world.BuildAt(3, 20);
        shop4.product = -1; shop4.making = null;
        s2d.units.Clear();
        int rockTile = s2d.world.Idx((int)s2d.Core.Cx + 4, s2d.Core.y + 1);
        s2d.world.tiles[rockTile].wall = true;
        s2d.SetRally(shop4, s2d.Core.Cx + 3.5f, s2d.Core.y + 1.5f);
        s2d.SpawnUnit(Content.Miner, shop4);
        var miner = s2d.units[0];
        int iron0 = s2d.Core.items[Content.Iron], sulfur0 = s2d.Core.items[Content.Sulfur];
        for (int i = 0; i < 60 * 60 && miner.mineTrips < 3; i++) s2d.Tick();
        Check(miner.mineTile < 0 && s2d.Core.items[Content.Iron] - iron0 == 16 && s2d.Core.items[Content.Sulfur] - sulfur0 == 4 && s2d.mined[Content.Iron] == 16,
            $"채굴 드론은 벽을 캐서 핵으로 나른다 (세 번에 철광 {s2d.Core.items[Content.Iron] - iron0}, 황석 {s2d.Core.items[Content.Sulfur] - sulfur0}, {s2d.tick / 60}초)");
        var raider = new Enemy { def = Content.Walker, x = miner.x + 0.3f, y = miner.y, health = 50, maxHealth = 50, cooldown = 1 << 20 };
        s2d.enemies.Add(raider);
        for (int i = 0; i < 300; i++) s2d.Tick();
        Check(raider.health == 50 && !raider.dead, "채굴 드론은 싸우지 못한다");

        // 방전함: 사거리 안의 적 여럿을 한꺼번에 친다 (물이 없는 맵이라 태어난 자리에 서 있다)
        var s2e = EmptySim(Full, Mods(Research.Support, 1, Research.Shipyard, 1, Research.Tier2, 1, Research.Tier3, 1, Research.Tier4, 1));
        Check(!s2c.IsUnlocked(Content.Arcship) && s2e.IsUnlocked(Content.Arcship), "방전함은 기체 개량 III 을 연구해야 한다");
        s2e.Place(Content.SupportShop, 3, 20);
        Finish(s2e);
        var shop5 = (FactoryBuilding)s2e.world.BuildAt(3, 20);
        shop5.product = -1; shop5.making = null;
        s2e.units.Clear();
        s2e.SpawnUnit(Content.Arcship, shop5);
        var arc = s2e.units[0];
        var ring = new List<Enemy>();
        for (int i = 0; i < 10; i++)
        {
            float a = i * 0.6283f, rr = 2.5f + i * 0.2f;
            ring.Add(new Enemy { def = i == 0 ? Content.Flyer : Content.Walker, x = arc.x + (float)Math.Cos(a) * rr, y = arc.y + (float)Math.Sin(a) * rr, health = 1000, maxHealth = 1000, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 });
        }
        var outOfRange = new Enemy { def = Content.Walker, x = arc.x + 8, y = arc.y, health = 1000, maxHealth = 1000, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
        s2e.enemies.AddRange(ring); s2e.enemies.Add(outOfRange);
        for (int i = 0; i < 120 && ring[0].health == 1000; i++) s2e.Tick();
        int zapped = 0; bool nearestFirst = true;
        for (int i = 0; i < 10; i++) { if (ring[i].health == 1000 - Content.Arcship.fieldDamage) zapped++; else if (i < 8) nearestFirst = false; }
        Check(zapped == 8 && nearestFirst && ring[8].health == 1000 && outOfRange.health == 1000 && s2e.bullets.Count == 0,
            $"방전함은 사거리 안의 가까운 적 8기를 지상·공중 가리지 않고 한꺼번에 친다 ({zapped}기)");
        arc.health = arc.maxHealth; s2e.SpawnUnit(Content.Scout, shop5);
        var hurt = s2e.units[1]; hurt.x = arc.x + 1; hurt.y = arc.y; hurt.health = 50;
        for (int i = 0; i < 60; i++) { s2e.Tick(); hurt.x = arc.x + 1; hurt.y = arc.y; }
        Check(hurt.health > 50, "방전함은 곁의 아군을 고친다");

        // 파편 로봇: 곡사 포탄이 터지면서 파편이 사방으로 튄다
        s2e.units.Clear(); s2e.enemies.Clear(); s2e.bullets.Clear();
        s2e.SpawnUnit(Content.Shatter, shop5);
        var shat = s2e.units[0];
        Enemy Still(EnemyDef def, float dx, float dy) => new Enemy { def = def, x = shat.x + dx, y = shat.y + dy, health = 5000, maxHealth = 5000, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
        var aimed = Still(Content.Walker, 5, 0);
        s2e.enemies.Add(aimed);
        var around = new List<Enemy>();
        for (int i = 0; i < 16; i++) around.Add(Still(Content.Flyer, 5 + 2.6f * (float)Math.Cos(i * 0.3927f), 2.6f * (float)Math.Sin(i * 0.3927f)));
        s2e.enemies.AddRange(around);
        for (int i = 0; i < 60 * 30 && aimed.health == 5000; i++) s2e.Tick();
        int maxBullets = s2e.bullets.Count;
        for (int i = 0; i < 20; i++) s2e.Tick();
        int fragHits = 0;
        foreach (var fe in around) if (fe.health < 5000) fragHits++;
        Check(aimed.health <= 5000 - Content.Shatter.bullet.damage * 0.88f && aimed.health >= 5000 - Content.Shatter.bullet.damage && maxBullets == Content.Shatter.bullet.fragCount && fragHits >= 4 && fragHits <= 6 && around.TrueForAll(fe => fe.health == 5000 || fe.health == 5000 - Content.Shatter.bullet.frag.damage),
            $"파편 로봇의 포탄은 터지면서 파편 6개로 갈라져 공중의 적도 맞힌다 (파편 {maxBullets}개, 맞은 비행체 {fragHits})");

        // 기함: 관통 전자포를 쏘고 함재기를 띄운다 (물이 없는 맵이라 태어난 자리에 서 있다)
        var s2f = EmptySim(Full, Mods(Research.Support, 1, Research.Tier2, 1, Research.Tier3, 1, Research.Tier4, 1, Research.Tier5, 1));
        Check(!s2e.IsUnlocked(Content.Flagship) && s2f.IsUnlocked(Content.Flagship), "기함은 기체 개량 IV 를 연구해야 한다");
        s2f.Place(Content.SupportShop, 3, 20);
        Finish(s2f);
        var shop6 = (FactoryBuilding)s2f.world.BuildAt(3, 20);
        shop6.product = -1; shop6.making = null;
        s2f.units.Clear();
        s2f.SpawnUnit(Content.Flagship, shop6);
        var flag = s2f.units[0];
        var row = new List<Enemy>();
        for (int i = 0; i < 4; i++) row.Add(new Enemy { def = Content.Walker, x = flag.x + 4 + i, y = flag.y, health = 150, maxHealth = 150, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 });
        s2f.enemies.AddRange(row);
        for (int i = 0; i < 300 && !row[0].dead; i++) s2f.Tick();
        for (int i = 0; i < 15; i++) s2f.Tick();
        Check(row.TrueForAll(re => re.dead) && s2f.kills == 4 && s2f.units.Count == 1, $"기함의 전자포 한 발이 한 줄로 선 적 넷을 꿰뚫는다 (처치 {s2f.kills})");
        for (int i = 0; i < Content.Flagship.spawnTicks + 5; i++) s2f.Tick();
        int after1 = s2f.units.Count;
        for (int i = 0; i < Content.Flagship.spawnTicks * 3; i++) s2f.Tick();
        Check(after1 == 2 && s2f.units.Count == 3 && s2f.units[1].def == Content.Escort && s2f.SupplyUsed == 5 && Dist(s2f.units[1], flag.x, flag.y) < 2.5f,
            $"기함은 15초마다 함재기를 띄워 둘까지 거느리고, 함재기는 한도를 차지하지 않는다 (유닛 {s2f.units.Count}, 한도 {s2f.SupplyUsed})");
        s2f.stance = UnitStance.Attack;
        for (int i = 0; i < 60 * 10; i++) s2f.Tick();
        Check(Dist(s2f.units[1], flag.x, flag.y) < 2.5f && Dist(s2f.units[2], flag.x, flag.y) < 2.5f, "함재기는 공격 태세에서도 기함 곁을 지킨다");
        s2f.stance = UnitStance.Defend;
        s2f.HurtUnit(s2f.units[1], 99999, 0);
        int lostBefore = s2f.unitsLost;
        for (int i = 0; i < Content.Flagship.spawnTicks + 5; i++) s2f.Tick();
        Check(s2f.units.Count == 3 && lostBefore == 0, "잃은 함재기는 다시 띄우고, 잃은 유닛으로 세지 않는다");
        s2f.HurtUnit(flag, 999999, 0);
        s2f.Tick(); s2f.Tick();
        Check(s2f.units.Count == 0 && s2f.unitsLost == 1, "기함을 잃으면 함재기도 함께 떨어진다");

        // 유도탄기: 유도탄은 쏜 뒤에 자리를 옮긴 적도 쫓아가 맞힌다
        s2f.enemies.Clear(); s2f.bullets.Clear();
        s2f.SpawnUnit(Content.Seeker, shop6);
        var seeker = s2f.units[0];
        seeker.x = 12; seeker.y = 30; s2f.SetRally(shop6, 12, 30); seeker.ox = seeker.oy = 0;
        var dodger = new Enemy { def = Content.Walker, x = 17, y = 30, health = 5000, maxHealth = 5000, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
        s2f.enemies.Add(dodger);
        for (int i = 0; i < 200 && s2f.bullets.Count == 0; i++) s2f.Tick();
        bool fired = s2f.bullets.Count == 1 && s2f.bullets[0].target == dodger;
        seeker.reload = 1 << 20;
        dodger.x = 16; dodger.y = 33.5f;   // 탄이 날아가는 동안 옆으로 비켜난다
        for (int i = 0; i < 60 && dodger.health == 5000; i++) s2f.Tick();
        Check(fired && dodger.health < 5000 - 60 && s2f.bullets.Count == 0, $"유도탄은 자리를 옮긴 적을 쫓아가 맞힌다 (피해 {5000 - dodger.health:0})");

        // 부양 로봇: 호수를 가로지른다 (맵을 세로로 가르는 호수 띠를 만든다). 맨 벽은 못 넘는다
        for (int pass = 0; pass < 2; pass++)
        {
            var sh = EmptySim(Full, all);
            for (int y = 1; y < sh.world.H - 1; y++)
            {
                var st = sh.world.tiles[sh.world.Idx(9, y)];
                if (st.build != null) continue;
                st.wall = true; st.water = pass == 0;
            }
            sh.Place(Content.RobotFactory, 3, 20);
            Finish(sh);
            var rf = (FactoryBuilding)sh.world.BuildAt(3, 20);
            rf.product = -1; rf.making = null;
            sh.units.Clear();
            sh.SetRally(rf, 10.5f, 21.5f);
            sh.SpawnUnit(Content.Skimmer, rf); sh.SpawnUnit(Content.Scout, rf);
            var skimmer = sh.units[0]; var walker = sh.units[1];
            skimmer.ox = skimmer.oy = walker.ox = walker.oy = 0;
            bool onWater = false;
            for (int i = 0; i < 60 * 20; i++) { sh.Tick(); if (sh.world.tiles[sh.world.Idx((int)skimmer.x, (int)skimmer.y)].water) onWater = true; }
            if (pass == 0) Check(onWater && Dist(skimmer, 10.5f, 21.5f) < 0.3f && walker.x < 9, $"부양 로봇은 호수를 가로지르고, 걷는 로봇은 못 건넌다 (부양 x {skimmer.x:0.0}, 척후 x {walker.x:0.0})");
            else Check(skimmer.x < 9 && walker.x < 9, $"부양 로봇도 맨 벽은 못 넘는다 (x {skimmer.x:0.0})");
        }

        // 재건 드론: 적에게 부서진 건물을 같은 자리에 다시 놓는다
        var s2g = EmptySim(Full, all);
        s2g.Place(Content.SupportShop, 3, 20);
        int rwx = (int)s2g.Core.Cx + 5, rwy = s2g.Core.y + 6;
        s2g.Place(Content.Wall, rwx, rwy); s2g.Place(Content.RapidTurret, rwx + 1, rwy); s2g.Place(Content.Wall, rwx + 2, rwy);
        Finish(s2g);
        var shop7 = (FactoryBuilding)s2g.world.BuildAt(3, 20);
        shop7.product = -1; shop7.making = null;
        s2g.units.Clear();
        s2g.world.BuildAt(rwx + 2, rwy).health = 0;
        s2g.Tick();
        Check(s2g.ruins.Count == 0 && s2g.world.BuildAt(rwx + 2, rwy) == null, "재건 드론이 없으면 부서진 자리를 적어 두지 않는다");
        s2g.SpawnUnit(Content.Rebuilder, shop7);
        s2g.Remove(rwx, rwy);
        s2g.world.BuildAt(rwx + 1, rwy).health = 0;
        s2g.Tick();
        int ruinCount = s2g.ruins.Count, ironBefore = s2g.Core.items[Content.Iron];
        for (int i = 0; i < 60 * 40; i++) s2g.Tick();
        var again = s2g.world.BuildAt(rwx + 1, rwy);
        Check(ruinCount == 1 && s2g.ruins.Count == 0 && again != null && again.def == Content.RapidTurret && again.Built && s2g.Core.items[Content.Iron] <= ironBefore - 35,
            $"재건 드론은 부서진 속사포를 같은 자리에 다시 짓고 비용을 낸다 (철광 {ironBefore} → {s2g.Core.items[Content.Iron]})");
        Check(s2g.world.BuildAt(rwx, rwy) == null && s2g.world.BuildAt(rwx + 2, rwy) == null, "직접 철거한 건물과 드론이 없을 때 부서진 건물은 다시 짓지 않는다");

        // 화염 거미: 맞은 적은 불타고, 불은 방어력을 무시한다
        s2f.units.Clear(); s2f.enemies.Clear(); s2f.bullets.Clear();
        s2f.SpawnUnit(Content.Pyre, shop6);
        var pyre = s2f.units[0];
        var plated = new Enemy { def = Content.Shell, x = pyre.x + 2.5f, y = pyre.y, health = 5000, maxHealth = 5000, armor = 500, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
        s2f.enemies.Add(plated);
        for (int i = 0; i < 300 && plated.burnTicks == 0; i++) s2f.Tick();
        bool lit = plated.burnTicks > 0;
        s2f.units.Clear(); s2f.bullets.Clear();
        plated.burnTicks = Content.Pyre.bullet.burnTicks;
        float burnFrom = plated.health;
        for (int i = 0; i < Content.Pyre.bullet.burnTicks + 30; i++) s2f.Tick();
        float burned = burnFrom - plated.health;
        Check(lit && plated.burnTicks == 0 && Math.Abs(burned - Content.Pyre.bullet.burn * 4) < 0.5f, $"화염 거미에 맞은 적은 4초 동안 방어력과 상관없이 불탄다 (피해 {burned:0.#})");

        // 전차 공장: 연구해야 지을 수 있고, 전차는 닿은 적을 짓밟는다
        Check(EmptySim(Full, all).Place(Content.TankFactory, 3, 20) == PlaceResult.Locked, "전차 공장은 전차 공장 기술을 연구해야 지을 수 있다");
        var tk = EmptySim(Full, Mods(Research.Tank, 1, Research.Tier2, 1, Research.Tier3, 1, Research.Tier4, 1));
        Check(tk.Place(Content.TankFactory, 3, 20) == PlaceResult.Ok && tk.IsUnlocked(Content.ArmoredCar) && tk.IsUnlocked(Content.PiercerTank) && !tk.IsUnlocked(Content.Juggernaut), "전차도 단계마다 기체 개량 연구가 필요하다");
        Finish(tk);
        var tf = (FactoryBuilding)tk.world.BuildAt(3, 20);
        tf.product = -1; tf.making = null;
        tk.units.Clear();
        tk.SpawnUnit(Content.LightTank, tf);
        var tank = tk.units[0];
        tank.x = tf.rallyX; tank.y = tf.rallyY; tank.ox = tank.oy = 0; tank.reload = 1 << 20;
        Enemy Armored(EnemyDef def, float dx) => new Enemy { def = def, x = tank.x + dx, y = tank.y, health = 5000, maxHealth = 5000, armor = 500, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
        var touching = Armored(Content.Walker, 0.9f); var apart = Armored(Content.Walker, 2.5f); var above = Armored(Content.Flyer, 0.5f);
        tk.enemies.AddRange(new[] { touching, apart, above });
        for (int i = 0; i < 240; i++) { tk.Tick(); tank.reload = 1 << 20; }
        Check(Math.Abs(5000 - touching.health - Content.LightTank.crush * 4) < 0.5f && apart.health == 5000 && above.health == 5000,
            $"전차는 닿은 지상의 적만 방어력과 상관없이 짓밟는다 (4초에 {5000 - touching.health:0.#})");

        // 철갑 전차: 철갑탄은 방어력을 무시한다
        tk.units.Clear(); tk.enemies.Clear(); tk.bullets.Clear();
        tk.SpawnUnit(Content.PiercerTank, tf);
        var piercer = tk.units[0];
        piercer.x = tf.rallyX; piercer.y = tf.rallyY; piercer.ox = piercer.oy = 0;
        var thick = new Enemy { def = Content.Shell, x = piercer.x + 5, y = piercer.y, health = 5000, maxHealth = 5000, armor = 500, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
        tk.enemies.Add(thick);
        for (int i = 0; i < 90 && thick.health == 5000; i++) tk.Tick();
        Check(thick.health == 5000 - Content.PiercerTank.bullet.damage, $"철갑탄은 방어력을 무시한다 (피해 {5000 - thick.health:0})");

        // 방호 전차: 보호막이 피해를 먼저 받고, 맞지 않으면 다시 차오른다
        tk.units.Clear(); tk.enemies.Clear(); tk.bullets.Clear();
        tk.SpawnUnit(Content.Bulwark, tf);
        var bul = tk.units[0];
        float fullHp = bul.health;
        float bar = Content.Bulwark.barrier;
        tk.HurtUnit(bul, 200 + Content.Bulwark.armor, 0);
        bool soaked = bul.barrier == bar - 200 && bul.health == fullHp;
        tk.HurtUnit(bul, bar + 100 + Content.Bulwark.armor, 0);
        bool broke = bul.barrier == 0 && bul.health == fullHp - 300;
        for (int i = 0; i < Content.Bulwark.barrierDelay - 5; i++) tk.Tick();
        bool waits = bul.barrier == 0;
        for (int i = 0; i < 65; i++) tk.Tick();
        bool regrows = bul.barrier > Content.Bulwark.barrierRegen * 0.85f && bul.barrier < Content.Bulwark.barrierRegen * 1.15f;
        for (int i = 0; i < 60 * 15; i++) tk.Tick();
        Check(soaked && broke && waits && regrows && bul.barrier == bar && bul.health == fullHp - 300,
            $"방호 전차의 보호막은 피해를 먼저 받고, 3초 뒤부터 다시 차오른다 ({soaked} {broke} {waits} {regrows}, 보호막 {bul.barrier:0})");

        // 충격함: 충격탄은 적을 날아가던 쪽으로 밀어낸다 (물이 없는 맵이라 태어난 자리에 서 있다)
        for (int pass = 0; pass < 2; pass++)
        {
            s2g.units.Clear(); s2g.enemies.Clear(); s2g.bullets.Clear(); s2g.ruins.Clear();
            s2g.SpawnUnit(Content.Shockboat, shop7);
            var shock = s2g.units[0];
            float ex = (int)(shock.x + 5) + 0.5f;
            var shoved = new Enemy { def = Content.Walker, x = ex, y = shock.y, health = 5000, maxHealth = 5000, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
            var beside = new Enemy { def = Content.Chief, x = ex, y = shock.y + 0.9f, health = 5000, maxHealth = 5000, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
            s2g.enemies.Add(shoved); s2g.enemies.Add(beside);
            var behind = s2g.world.tiles[s2g.world.Idx((int)ex + 1, (int)shock.y)];
            behind.wall = pass == 1;
            for (int i = 0; i < 200 && shoved.health == 5000; i++) s2g.Tick();
            if (pass == 0) Check(Math.Abs(shoved.x - ex - 0.9f) < 0.05f && Math.Abs(beside.x - ex - 0.45f) < 0.1f && Math.Abs(shoved.y - shock.y) < 0.05f,
                $"충격탄은 범위 안의 적을 뒤로 밀어낸다 (보행병 {shoved.x - ex:0.00}칸, 보스 {beside.x - ex:0.00}칸)");
            else Check(shoved.health < 5000 && shoved.x == ex, "충격탄은 지상의 적을 벽 안으로 밀지 않는다");
            behind.wall = false;
        }

        // 수리 폭격기: 폭탄이 적을 때리면서 같은 범위의 아군 건물을 고친다 (유닛은 고치지 않는다)
        s2g.units.Clear(); s2g.enemies.Clear(); s2g.bullets.Clear(); s2g.ruins.Clear();
        s2g.Remove(rwx + 1, rwy);
        s2g.Place(Content.Wall, rwx + 2, rwy);
        Finish(s2g);
        var patched = s2g.world.BuildAt(rwx + 2, rwy);
        patched.health = 100;
        var bombed = new Enemy { def = Content.Walker, x = patched.Cx + 1.0f, y = patched.Cy, health = 5000, maxHealth = 5000, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
        s2g.enemies.Add(bombed);
        s2g.SetRally(shop7, patched.Cx, patched.Cy + 1.5f);
        s2g.SpawnUnit(Content.Mender, shop7); s2g.SpawnUnit(Content.Scout, shop7);
        var mender = s2g.units[0]; var friend = s2g.units[1];
        friend.reload = 1 << 20; friend.health = 30;
        for (int i = 0; i < 60 * 40 && bombed.health == 5000; i++) { s2g.Tick(); friend.x = patched.Cx + 1.0f; friend.y = patched.Cy + 0.8f; friend.reload = 1 << 20; }
        Check(bombed.health < 5000 - 60 && patched.health == 100 + Content.Mender.bullet.heal && friend.health == 30 && mender.health == mender.maxHealth,
            $"수리 폭격기의 폭탄은 적을 때리면서 아군 건물만 고친다 (적 −{5000 - bombed.health:0}, 방벽 {patched.health:0}, 척후 로봇 {friend.health:0})");

        // 증폭 로봇: 곁의 다른 아군 유닛이 1.33배 빨리 쏜다 (겹쳐도 한 번만, 자기 자신은 아니다)
        s2g.units.Clear(); s2g.enemies.Clear(); s2g.bullets.Clear(); s2g.ruins.Clear();
        s2g.SpawnUnit(Content.Scout, shop7); s2g.SpawnUnit(Content.Scout, shop7); s2g.SpawnUnit(Content.Booster, shop7); s2g.SpawnUnit(Content.Booster, shop7);
        var fastScout = s2g.units[0]; var plainScout = s2g.units[1];
        Enemy Dummy(float x, float y) => new Enemy { def = Content.Walker, x = x, y = y, health = 90000, maxHealth = 90000, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
        var nearDummy = Dummy(6, 30); var farDummy = Dummy(22, 30);
        s2g.enemies.Add(nearDummy); s2g.enemies.Add(farDummy);
        for (int i = 0; i < 60 * 30; i++)
        {
            fastScout.x = 4; fastScout.y = 30; plainScout.x = 20; plainScout.y = 30;
            for (int k = 2; k < 4; k++) { s2g.units[k].x = 4; s2g.units[k].y = 31 + (k - 2) * 0.5f; s2g.units[k].reload = 1 << 20; }
            s2g.Tick();
        }
        float boostRatio = (90000 - nearDummy.health) / (90000 - farDummy.health);
        Check(boostRatio > 1.28f && boostRatio < 1.38f, $"증폭 로봇 곁의 유닛은 1.33배 빨리 쏜다 (피해 비율 {boostRatio:0.00})");

        // 전격 로봇: 전격이 가까운 적으로 건너뛰며 피해가 줄어든다
        s2g.units.Clear(); s2g.enemies.Clear(); s2g.bullets.Clear(); s2g.ruins.Clear();
        s2g.SpawnUnit(Content.Arcer, shop7);
        var arcer = s2g.units[0];
        arcer.x = 4; arcer.y = 30; s2g.SetRally(shop7, 4, 30); arcer.ox = arcer.oy = 0;
        var chainRow = new List<Enemy>();
        for (int i = 0; i < 7; i++) chainRow.Add(Dummy(7 + i * 1.5f, 30));
        chainRow[6].x += 3;   // 마지막 하나는 건너뛸 수 없게 떨어뜨린다
        s2g.enemies.AddRange(chainRow);
        for (int i = 0; i < 200 && chainRow[0].health == 90000; i++) s2g.Tick();
        arcer.reload = 1 << 20;
        bool chainOk = true; float want = Content.Arcer.bullet.damage;
        for (int i = 0; i < 5; i++) { if (Math.Abs(90000 - chainRow[i].health - want) > 0.01f) chainOk = false; want *= 0.7f; }
        Check(chainOk && chainRow[5].health == 90000 && chainRow[6].health == 90000,
            $"전격은 가까운 적으로 네 번 건너뛰며 피해가 30% 씩 줄어든다 ({90000 - chainRow[0].health:0.#} → {90000 - chainRow[4].health:0.#})");

        // 부양 공방과 지뢰
        Check(EmptySim(Full, all).Place(Content.HoverShop, 3, 20) == PlaceResult.Locked, "부양 공방은 부양 공방 기술을 연구해야 지을 수 있다");
        var hv = EmptySim(Full, Mods(Research.Hover, 1, Research.Tier2, 1));
        Check(hv.Place(Content.HoverShop, 3, 20) == PlaceResult.Ok && Content.Dart.hover && Content.Leviathan.hover, "부양 공방의 유닛은 모두 떠서 다닌다");
        Finish(hv);
        var hs = (FactoryBuilding)hv.world.BuildAt(3, 20);
        hs.product = -1; hs.making = null;
        hv.units.Clear();
        hv.SetRally(hs, 12.5f, 30.5f);
        hv.SpawnUnit(Content.Minelayer, hs);
        var layer = hv.units[0];
        layer.x = 12.5f; layer.y = 30.5f;
        for (int i = 0; i < 60 * 60; i++) hv.Tick();
        bool spread = false;
        foreach (var mn in hv.mines) if (Math.Abs(mn.x - hv.mines[0].x) + Math.Abs(mn.y - hv.mines[0].y) > 0.5f) spread = true;
        Check(hv.mines.Count == Content.Minelayer.mineMax && spread, $"기뢰 부설기는 돌아다니며 지뢰를 다섯 개까지 깐다 ({hv.mines.Count}개)");
        var firstMine = hv.mines[0];
        var overhead = new Enemy { def = Content.Flyer, x = firstMine.x, y = firstMine.y, health = 5000, maxHealth = 5000, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
        hv.enemies.Add(overhead);
        hv.Tick();
        bool flyerSafe = hv.mines.Contains(firstMine) && overhead.health == 5000;
        var stepper = new Enemy { def = Content.Walker, x = firstMine.x + 0.3f, y = firstMine.y, health = 5000, maxHealth = 5000, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
        hv.enemies.Add(stepper);
        hv.Tick();
        Check(flyerSafe && !hv.mines.Contains(firstMine) && stepper.health < 5000 - 50 && overhead.health == 5000,
            $"지뢰는 지상의 적이 밟으면 터지고 비행체는 밟지 않는다 (피해 {5000 - stepper.health:0})");
        hv.HurtUnit(layer, 99999, 0);
        int leftMines = hv.mines.Count;
        hv.Tick();
        Check(hv.mines.Count == leftMines && leftMines > 0, "부설기를 잃어도 깔아 둔 지뢰는 남는다");

        // 심연 조립소: 연구 두 단계로 열리고, 유닛은 다른 공장의 5단계보다 훨씬 강하고 비싸며 스스로 체력을 되찾는다
        var noTech = EmptySim(Full, Mods(Research.RobotFactory, 1, Research.Tier2, 1, Research.Tier3, 1, Research.Tier4, 1, Research.Tier5, 1));
        var asmMods = Mods(Research.Assembler, 1);
        var richLoad = new[] { 5000, 5000, 5000, 5000, 5000, 5000, 5000 };
        var asm = EmptySim(richLoad, asmMods);
        Check(noTech.Place(Content.Assembler, 3, 20) == PlaceResult.Locked && asm.Place(Content.Assembler, 3, 20) == PlaceResult.Ok
            && asm.IsUnlocked(Content.Colossus) && !asm.IsUnlocked(Content.Doomtank) && EmptySim(Full, Mods(Research.Assembler, 1, Research.Transcend, 1)).IsUnlocked(Content.Doomtank),
            "심연 조립소와 6단계는 심연 조립 기술로, 7단계는 초월 개량으로 열린다");
        float Worth(ItemStack[] c) { float t = 0; foreach (var k in c) t += k.amount * (k.item == Content.Iron || k.item == Content.Sulfur ? 1 : k.item == Content.Alloy || k.item == Content.Crystal ? 4 : 2); return t; }
        float topWorth = 0, topHealth = 0;
        foreach (var fdef in new[] { Content.RobotFactory, Content.Airfield, Content.SupportShop, Content.LegShop, Content.TankFactory, Content.HoverShop, Content.Shipyard })
            foreach (var fu in fdef.units) { topWorth = Math.Max(topWorth, Worth(fu.cost)); topHealth = Math.Max(topHealth, fu.health + fu.barrier); }
        bool mighty = true;
        foreach (var hu in Content.Assembler.units)
            if (Worth(hu.cost) < topWorth * 1.2f || hu.health + hu.barrier < topHealth * 1.5f || hu.regen <= 0 || hu.tier < 6 || Array.FindIndex(hu.cost, k => k.item == Content.Crystal) < 0) mighty = false;
        Check(mighty && Worth(Content.Assembler.cost) > 3 * Worth(Content.RobotFactory.cost) && Content.Assembler.size == 3,
            $"심연 조립소의 유닛은 다른 공장의 가장 센 유닛보다 1.5배 넘게 단단하고 1.2배 넘게 비싸며 심연 결정이 든다 (다른 공장 최고 체력 {topHealth:0}, 값 {topWorth:0})");
        Finish(asm);
        var ab = (FactoryBuilding)asm.world.BuildAt(3, 20);
        Check(ab != null && ab.Built && ab.def.size == 3, "심연 조립소는 3×3 칸으로 지어진다");
        ab.product = -1; ab.making = null;
        asm.units.Clear();
        asm.SpawnUnit(Content.Colossus, ab);
        var giant = asm.units[0];
        asm.HurtUnit(giant, Content.Colossus.barrier + 5000 + Content.Colossus.armor, 0);
        float hurtTo = giant.health;
        for (int i = 0; i < 600; i++) asm.Tick();
        Check(hurtTo == giant.maxHealth - 5000 && Math.Abs(giant.health - hurtTo - Content.Colossus.regen * 10) < 1f && giant.barrier > 0,
            $"심연 조립소의 유닛은 스스로 체력을 되찾는다 (10초에 +{giant.health - hurtTo:0})");

        AllUnits();

        // 정비 로봇: 유닛과 건물을 고친다
        var s3 = EmptySim(Full, all);
        s3.Place(Content.SupportShop, 3, 20);
        s3.Place(Content.Wall, (int)s3.Core.Cx, s3.Core.y + 5);
        Finish(s3);
        var sup = (FactoryBuilding)s3.world.BuildAt(3, 20);
        sup.product = -1;
        s3.SpawnUnit(Content.Mechanic, sup);
        s3.SpawnUnit(Content.Scout, sup);
        var wall = s3.world.BuildAt((int)s3.Core.Cx, s3.Core.y + 5);
        s3.SetRally(sup, wall.Cx, wall.Cy - 1.5f);
        for (int i = 0; i < 60 * 30; i++) s3.Tick();
        s3.units[1].health = 40; wall.health = 100;
        for (int i = 0; i < 60 * 6; i++) s3.Tick();
        Check(s3.units[1].health > 100 && wall.health > 150, $"정비 로봇이 둘레의 유닛과 건물을 고친다 (유닛 {s3.units[1].health:0}, 방벽 {wall.health:0})");

        // 방패 로봇: 보행병 11.7 × 0.65 − 방어력 2
        s3.SpawnUnit(Content.Guardian, sup);
        for (int i = 0; i < 60 * 30; i++) s3.Tick();
        var sc = s3.units[1];
        sc.health = sc.maxHealth;
        s3.units[0].dead = true;   // 정비 로봇이 고쳐 버리지 않게
        s3.enemies.Add(new Enemy { def = Content.Walker, x = sc.x + 0.3f, y = sc.y, health = 99999, maxHealth = 99999 });
        s3.Tick();
        Check(Math.Abs(sc.maxHealth - sc.health - (11.7f * 0.65f - 2)) < 0.02f, $"방패 로봇 곁에서는 피해가 35% 줄어든다 ({sc.maxHealth - sc.health:0.00})");
        s3.enemies.Clear();

        // 수리 드론: 멀리 있는 상한 건물로 날아가 고친다
        s3.Place(Content.Wall, 18, 28);
        Finish(s3);
        var farWall = s3.world.BuildAt(18, 28);
        s3.SpawnUnit(Content.RepairDrone, sup);
        farWall.health = 50;
        for (int i = 0; i < 60 * 25; i++) s3.Tick();
        Check(farWall.health == farWall.maxHealth, $"수리 드론이 상한 건물을 찾아가 고친다 ({farWall.health:0}/{farWall.maxHealth:0})");

        // 수송기: 공격 태세에서 느린 유닛을 실어 출현 지점 앞에 내려놓는다
        var s4 = new Sim(0, 30, Full, all);
        s4.DisableCommander();
        s4.waveCountdown = int.MaxValue;
        FactoryBuilding f4 = null;
        for (int y = s4.Core.y + 4; y < s4.world.H && f4 == null; y++)
            for (int x = 2; x < s4.world.W - 3 && f4 == null; x++)
                if (s4.Place(Content.SupportShop, x, y) == PlaceResult.Ok) f4 = (FactoryBuilding)s4.world.BuildAt(x, y);
        f4.product = -1;
        int sp4 = s4.world.spawns[0];
        float sx4 = sp4 % s4.world.W + 0.5f, sy4 = sp4 / s4.world.W + 0.5f;
        s4.SpawnUnit(Content.Heavy, f4);
        s4.SpawnUnit(Content.Transport, f4);
        var heavy = s4.units[0];
        s4.stance = UnitStance.Attack;
        bool carried = false; int near4 = -1;
        for (int i = 0; i < 60 * 60 && near4 < 0; i++)
        {
            s4.Tick();
            if (heavy.carrier != null) carried = true;
            if (heavy.carrier == null && carried && Dist(heavy, sx4, sy4) < 12) near4 = i;
        }
        float walk = Dist(new Unit { x = f4.Cx, y = f4.Cy }, sx4, sy4) / Content.Heavy.speed;
        Check(carried && near4 > 0 && near4 < walk * 0.6f && !s4.world.Solid((int)heavy.x, (int)heavy.y), $"수송기가 중장 로봇을 실어 출현 지점 앞 바닥에 내려놓는다 ({near4 / 60f:0}초, 걸어가면 적어도 {walk / 60f:0}초)");
        s4.stance = UnitStance.Defend;
        for (int i = 0; i < 60 * 5; i++) s4.Tick();
        Check(heavy.carrier == null, "방어 태세에서는 싣지 않는다");

        // 지휘기
        var s5 = new Sim(0, 1, new[] { 850, 0, 0 });
        s5.waveCountdown = int.MaxValue;
        s5.Tick();
        var cmd = s5.commander;
        Check(cmd != null && s5.units.Count == 0 && s5.SupplyUsed == 0, "지휘기는 핵에서 나오고 유닛 한도를 차지하지 않는다");
        s5.SetCommand(s5.Core.Cx + 3, s5.Core.Cy + 6);
        for (int i = 0; i < 60 * 10; i++) s5.Tick();
        Check(Dist(cmd, s5.commandX, s5.commandY) < 0.5f, "지휘기는 찍어 준 곳으로 간다");
        var e5 = new Enemy { def = Content.Walker, x = cmd.x + 2, y = cmd.y, health = 42 * 1.3f, maxHealth = 42 * 1.3f };
        s5.enemies.Add(e5);
        for (int i = 0; i < 600 && !e5.dead; i++) s5.Tick();
        Check(e5.dead, "지휘기가 곁의 적을 쏜다");
        // 곁의 건설은 1.5배 빠르다
        int bx = (int)s5.commandX, by = (int)s5.commandY + 1;
        bool placedNear = s5.Place(Content.RapidTurret, bx, by) == PlaceResult.Ok;
        int t0 = s5.tick;
        Finish(s5);
        int nearTicks = s5.tick - t0;
        Check(placedNear && Math.Abs(nearTicks - Content.RapidTurret.buildTicks / 1.5f) <= 3, $"지휘기 곁의 건설은 1.5배 빠르다 ({nearTicks} 틱, 보통 {Content.RapidTurret.buildTicks})");
        s5.HurtUnit(cmd, 99999, 0);
        s5.Tick();
        Check(cmd.dead && s5.unitsLost == 0, "지휘기를 잃어도 유닛 손실로 세지 않는다");
        for (int i = 0; i < Sim.CommanderRespawnTicks + 2; i++) s5.Tick();
        Check(s5.commander != cmd && s5.commander != null && !s5.commander.dead, "지휘기는 20초 뒤 핵에서 다시 나온다");

        // 공성 로봇은 적 건물에 2.5배
        var s6 = new Sim(0, 15, Full, all);
        s6.DisableCommander();
        s6.waveCountdown = int.MaxValue;
        FactoryBuilding f6 = null;
        for (int y = s6.Core.y + 4; y < s6.world.H && f6 == null; y++)
            for (int x = 2; x < s6.world.W - 3 && f6 == null; x++)
                if (s6.Place(Content.RobotFactory, x, y) == PlaceResult.Ok) f6 = (FactoryBuilding)s6.world.BuildAt(x, y);
        f6.product = -1;
        for (int i = 0; i < 3; i++) s6.SpawnUnit(Content.Siege, f6);
        for (int i = 0; i < 4; i++) s6.SpawnUnit(Content.Bug, f6);
        s6.stance = UnitStance.Attack;
        for (int i = 0; i < 60 * 60 * 15 && s6.state == SimState.Playing; i++) s6.Tick();
        Check(s6.state == SimState.Won, $"공성 로봇 3대와 자폭 벌레로 거점 15 를 부순다 ({s6.tick / 60}초, 잃은 유닛 {s6.unitsLost})");
    }

    static void WaveFormula()
    {
        Console.WriteLine("웨이브");
        var g = new WaveGroup { begin = 2, spacing = 3, amount = 2, scaling = 2, max = 4 };
        Check(g.GetSpawned(1) == 0 && g.GetSpawned(2) == 2 && g.GetSpawned(3) == 0 && g.GetSpawned(5) == 2, "시작·간격");
        Check(g.GetSpawned(8) == 3 && g.GetSpawned(14) == 4 && g.GetSpawned(50) == 4, "증가·상한");
        var r10 = RegionRules.For(0, 10);
        bool boss = false;
        foreach (var grp in WaveGen.Generate(r10)) if (grp.enemy.boss && grp.GetSpawned(r10.winWave) > 0) boss = true;
        Check(boss, "지역 10 마지막 웨이브에 보스");
        bool always = true;
        for (int a = 0; a < Content.Abysses.Length; a++)
            for (int reg = 1; reg <= 100; reg++)
            {
                var rr = RegionRules.For(a, reg);
                if (rr.assault) continue;
                int last = 0, early = 0;
                foreach (var grp in WaveGen.Generate(rr))
                    if (grp.enemy.boss) { last += grp.GetSpawned(rr.winWave); for (int wv = 1; wv < rr.winWave; wv++) early += grp.GetSpawned(wv); }
                if (early != 0 || last < 1 || (reg % 10 != 0 && last != 1)) always = false;
            }
        Check(always, "모든 지역의 마지막 웨이브에만 보스가 나온다 (보통 지역은 정확히 한 마리)");
        Check(RegionRules.For(0, 1).winWave == 5 && RegionRules.For(0, 100).winWave == 30, "승리 웨이브 5 ~ 30");
    }

    static void BaseEconomy()
    {
        Console.WriteLine("기지");
        var none = new[] { 0, 0, 0 };
        Check(BaseRules.MineRate(Content.Iron, 1) == 30 && BaseRules.MineRate(Content.Crystal, 0) == 0, "채굴장 1단계 철광 분당 30, 안 지은 채굴장은 0");
        bool grows = true;
        for (int lv = 1; lv < BaseRules.MaxMineLevel; lv++)
            if (BaseRules.MineRate(Content.Iron, lv + 1) <= BaseRules.MineRate(Content.Iron, lv)) grows = false;
        Check(grows, "채굴장 단계가 오르면 생산량이 늘어난다");
        Check(BaseRules.ConquestRate(Content.Iron, none) == 0 && BaseRules.ConquestRate(Content.Iron, new[] { 1, 0, 0 }) == 3, "지역 1 점령 = 철광 분당 +3");
        Check(BaseRules.ConquestRate(Content.Sulfur, new[] { 2, 2, 0 }) == 3, "지역 2 를 두 심연에서 점령 = 황석 분당 +3");
        Check(BaseRules.ConquestRate(Content.Crystal, new[] { 9, 0, 0 }) == 0 && BaseRules.ConquestRate(Content.Crystal, new[] { 12, 0, 0 }) == 0.75f, "결정 보너스는 지역 12 부터");
        var lv5 = new int[Research.Defs.Length]; lv5[Research.Mining] = 5; lv5[Research.Loadout] = 2;
        Check(Math.Abs(BaseRules.Rate(Content.Iron, 1, lv5, new[] { 1, 0, 0 }) - (30 * 1.5f + 3)) < 0.01f, "총생산량 = 채굴장 × 채굴 기술 + 점령 보너스");
        var lv10 = new int[Research.Defs.Length]; lv10[Research.Loadout] = 10;
        var lv1 = new int[Research.Defs.Length]; lv1[Research.Loadout] = 1;
        Check(BaseRules.LoadoutCap(null) == 400 && BaseRules.LoadoutCap(lv1) == 550 && BaseRules.LoadoutCap(lv5) == 800 && BaseRules.LoadoutCap(lv10) == 6400 && Research.Defs[Research.Loadout].maxLevel == 10,
            "출격 한도 400, 연구 단계가 오를수록 더 크게 늘어 10단계에 6400");
        int capMax = BaseRules.LoadoutCap(lv10); bool fits = true;
        foreach (var hu in Content.Assembler.units)
            foreach (var hc in hu.cost)
            {
                int need = hc.amount;
                foreach (var bc in Content.Assembler.cost) if (bc.item == hc.item) need += bc.amount;
                if (need > capMax * 0.6f) fits = false;
            }
        Check(fits, "출격 한도를 끝까지 올리면 조립소와 7단계 한 대를 짓고도 자원이 넉넉히 남는다");
        bool affordable = true;
        for (int lv = 1; lv < BaseRules.MaxStorageLevel; lv++)
        {
            int cap = BaseRules.StorageCap(lv);
            foreach (int c in BaseRules.StorageCost(lv)) if (c > cap) affordable = false;
        }
        Check(affordable, "창고 업그레이드 비용은 지금 창고 한도 안이다");
        // 가공 건물
        var smelter = BaseRules.Refineries[0];
        int[] noMine = { 0, 0, 0 }, noRes = new int[Research.Defs.Length];
        int[] refLv = { 1, 0, 0, 0 }; bool[] on = { true, true, true, true };
        var st = new double[] { 300, 0, 0, 0, 0, 0, 0 };
        BaseRules.Advance(st, noMine, refLv, on, noRes, none, 1, 600);
        Check(Math.Abs(st[Content.Steel] - 60) < 0.01 && Math.Abs(st[Content.Iron] - 120) < 0.01, $"제련소 1단계: 10분에 철광 180 → 강철 60 ({st[Content.Iron]:0}, {st[Content.Steel]:0})");
        BaseRules.Advance(st, noMine, refLv, on, noRes, none, 1, 6000);
        Check(Math.Abs(st[Content.Steel] - 100) < 0.01 && st[Content.Iron] < 0.01, "원료가 떨어지면 가공이 멈춘다");
        st[Content.Iron] = 300;
        BaseRules.Advance(st, noMine, refLv, new[] { false, true, true, true }, noRes, none, 1, 600);
        Check(st[Content.Iron] == 300, "꺼 둔 가공 건물은 원료를 쓰지 않는다");
        st = new double[] { 99999, 0, 0, 1495, 0, 0, 0 };
        BaseRules.Advance(st, noMine, refLv, on, noRes, none, 1, 6000);
        Check(Math.Abs(st[Content.Steel] - 1500) < 0.01 && Math.Abs(st[Content.Iron] - (99999 - 15)) < 0.01, "가공 자원도 창고 한도까지만 쌓인다");
        var net = BaseRules.NetRates(new[] { 1, 1, 0 }, refLv, on, noRes, none);
        Check(net[Content.Iron] == 12 && net[Content.Steel] == 6 && net[Content.Sulfur] == 12, "순생산량: 철광 30 − 18, 강철 +6");
        st = new double[] { 0, 0, 0, 0, 0, 0, 0 };
        BaseRules.Advance(st, new[] { 1, 1, 0 }, new[] { 1, 1, 0, 0 }, on, noRes, none, 1, 8 * 3600);
        Check(Math.Abs(st[Content.Steel] - 1500) < 1 && Math.Abs(st[Content.Circuit] - 1500) < 1 && st[Content.Iron] > 0, $"8시간 비우면 가공 자원이 한도까지 차고 남는 철광이 쌓인다 (철광 {st[Content.Iron]:0})");
        Check(BaseRules.RefineryCost(smelter, 0)[Content.Iron] == 200 && BaseRules.RefineryRate(smelter, 2) > 12, "제련소 건설 비용·단계별 산출량");
        var rc = Research.Cost(Research.Defs[Research.RobotFactory], 0);
        Check(rc[Content.Steel] == 40 && rc[Content.Iron] == 200 && rc.Length == Content.Items.Length, "로봇 공장 기술은 강철이 든다");
        // 연구 트리: 앞 연구는 자기보다 늦게 열리지 않고, 거슬러 올라가면 반드시 뿌리에 닿는다
        bool treeOk = true; int treeRoots = 0;
        foreach (var d in Research.Defs)
        {
            if (d.parent < 0) treeRoots++;
            else if (Research.Defs[d.parent].reqRegion > d.reqRegion) treeOk = false;
            int steps = 0;
            for (var c = d; c.parent >= 0 && steps <= Research.Defs.Length; c = Research.Defs[c.parent]) steps++;
            if (steps > Research.Defs.Length || d.icon == null) treeOk = false;
        }
        Check(treeOk && treeRoots == 4, $"연구 트리가 뿌리 가지 4개로 이어져 있다 (뿌리 {treeRoots})");
        var treeLv = new int[Research.Defs.Length];
        Check(Research.ParentDone(treeLv, Research.Defs[Research.Damage]) && !Research.ParentDone(treeLv, Research.Defs[Research.Mortar]), "앞 연구(포탑 화력)를 마치기 전에는 박격포 기술이 드러나지 않는다");
        treeLv[Research.Damage] = 1;
        Check(Research.ParentDone(treeLv, Research.Defs[Research.Mortar]) && !Research.ParentDone(treeLv, Research.Defs[Research.Lance]), "포탑 화력 1단계면 박격포 기술이 드러난다");
        Check(Research.Defs[Research.Mortar].reqRegion == 2 && Research.Defs[Research.Lance].reqRegion == 10, "박격포 기술은 지역 2, 결정포 기술은 지역 10 점령으로 열린다");
    }

    static void MapChecks()
    {
        Console.WriteLine("맵 생성 (300개)");
        int bad = 0;
        for (int a = 0; a < Content.Abysses.Length; a++)
            for (int r = 1; r <= 100; r++)
            {
                var sim = new Sim(a, r);
                var w = sim.world;
                foreach (int s in w.spawns)
                    if (sim.field.cost[s] == float.MaxValue || w.tiles[s].wall) bad++;
                if (w.spawns.Count != sim.rules.spawnCount) bad++;
            }
        Check(bad == 0, "모든 출현 지점이 핵과 이어진다 (실패 " + bad + ")");
    }

    static void Determinism()
    {
        Console.WriteLine("재현성");
        string a = Play(1, 7, true), b = Play(1, 7, true);
        Check(a == b, "같은 지역을 두 번 돌리면 결과가 같다");
    }

    static void PrintMap(int abyss, int region)
    {
        var sim = new Sim(abyss, region, new[] { 400, 400, 0 });
        var bot = new AutoBot(sim);
        for (int i = 0; i < 60 * 90; i++) { bot.Step(); sim.Tick(); }
        var w = sim.world;
        for (int y = w.H - 1; y >= 0; y--)
        {
            var sb = new StringBuilder();
            for (int x = 0; x < w.W; x++)
            {
                var t = w.tiles[w.Idx(x, y)];
                char c = t.wall ? '#' : '.';
                if (w.spawns.Contains(w.Idx(x, y))) c = 'X';
                var b = t.build;
                if (b is CoreBuilding) c = 'C';
                else if (b is TurretBuilding) c = 'T';
                else if (b != null) c = 'W';
                sb.Append(c);
            }
            Console.WriteLine(sb);
        }
        Console.WriteLine($"{w.W}x{w.H}  tick {sim.tick}  wave {sim.wave}  철 {sim.Core.items[0]} 황 {sim.Core.items[1]}");
    }
}
