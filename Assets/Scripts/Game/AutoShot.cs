#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using AbyssDefense.Sim;
using UnityEngine;

namespace AbyssDefense
{
    /// 점검용: 실행 인자 -autoshot <폴더> 가 있으면 화면을 차례로 돌며 스크린샷을 찍고 끝낸다.
    /// 시험용 세이브(save_test.json)를 쓰므로 진짜 세이브는 건드리지 않는다.
    public sealed class AutoShot : MonoBehaviour
    {
        public string dir;
        int errors;

        void Note(string s) => File.AppendAllText(Path.Combine(dir, "_checks.txt"), s + "\n");

        IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type != LogType.Exception && type != LogType.Error) return;
                errors++;
                File.AppendAllText(Path.Combine(dir, "_errors.txt"), msg + "\n" + stack + "\n");
            };
            var gm = GetComponent<GameManager>();
            gm.ResetSave();
            var sv = gm.save;
            yield return new WaitForSeconds(0.5f);
            yield return Shot("01_title");

            // 기지: 처음 상태, 채굴장 강화
            gm.ShowBase();
            yield return Shot("02_base");
            float rate0 = gm.Rate(Content.Iron);
            bool up = gm.UpgradeMine(Content.Iron);
            Note($"iron mine upgrade {up} (expect True), rate {rate0} -> {gm.Rate(Content.Iron)} (expect 30 -> 66), iron left {(int)sv.stock[0]} (expect 380)");
            Note($"crystal mine buildable {gm.CanBuildMine(Content.Crystal)} (expect False), mortar research open {gm.ResearchOpen(Research.Defs[Research.Mortar])} (expect False)");
            // 생산: 10분 전에 마지막으로 정산했다고 치면 분당 생산량 × 10 이 쌓여야 한다
            double before = sv.stock[0];
            sv.lastTime -= 600;
            gm.Accrue();
            Note($"10 min of production: iron +{sv.stock[0] - before:0} (expect +660)");
            sv.lastTime -= 100 * 3600;
            gm.Accrue();
            Note($"after 100 h away iron {(int)sv.stock[0]} (expect storage cap {gm.StorageCap})");
            gm.ShowBase();
            yield return Shot("03_base_upgraded");
            gm.ShowResearch();
            yield return Shot("04_research");

            gm.ShowAbyssSelect();
            yield return Shot("05_abyss");
            gm.ShowRegionMap(0);
            yield return Shot("06_regions");

            // 출격 물자: 철광 250 만 가져간다
            sv.loadout[0] = 250; sv.loadout[1] = 100; sv.loadout[2] = 0;
            gm.ShowRegionSheet(0, 1);
            yield return Shot("07_region_sheet");
            double ironBefore = sv.stock[0], sulfurBefore = sv.stock[1];
            gm.StartRegion(0, 1);
            Note($"sortie took iron {gm.Taken[0]} sulfur {gm.Taken[1]} (expect 250 100), base iron {ironBefore:0} -> {sv.stock[0]:0}, core iron {gm.sim.Core.items[0]} (expect 400 = 250 + 150 supply)");
            yield return Shot("08_play_start");

            // 건설 조작: 방벽을 끌어서 한 줄, 포탑을 탭해서 하나
            var play = FindAnyObjectByType<PlayController>();
            var cam = Camera.main;
            var core = gm.sim.Core;
            Vector2 p0 = cam.WorldToScreenPoint(new Vector3(core.x - 1.5f, core.y + 4.5f));
            Vector2 p1 = cam.WorldToScreenPoint(new Vector3(core.x + 1.5f, core.y + 4.5f));
            Vector2 p2 = cam.WorldToScreenPoint(new Vector3(core.x + 1.5f, core.y + 5.5f));
            play.Select(Content.Wall);
            play.DebugPointer(p0, true, false);
            play.DebugPointer(p1, false, false);
            yield return Shot("08b_drag_ghost");
            play.DebugPointer(p1, false, true);
            play.Select(Content.RapidTurret);
            play.DebugPointer(p2, true, false);
            yield return Shot("08c_turret_ghost");
            play.DebugPointer(p2, false, true);
            // 연구하지 않은 박격포는 선택되지 않고, 건설 바 위를 눌러도 설치가 시작되지 않아야 한다
            play.Select(Content.Wall);
            play.DebugPointer(new Vector2(Screen.width * 0.5f, Screen.height * 0.05f), true, false);
            play.DebugPointer(new Vector2(Screen.width * 0.5f, Screen.height * 0.05f), false, true);
            play.Cancel();
            // 건설 중: 놓은 직후에는 아직 지어지지 않았고 비용도 거의 나가지 않았다
            yield return new WaitForSeconds(1.2f);
            int unbuilt = 0;
            foreach (var b in gm.sim.world.buildings) if (!b.Built) unbuilt++;
            Note($"1.2 s after placing: unfinished {unbuilt} (expect 5: a wall takes 2.3 s), core iron {gm.sim.Core.items[0]} (expect between 333 and 400)");
            yield return Shot("08d_constructing");
            int startTick = gm.sim.tick;
            while (true)
            {
                bool all = true;
                foreach (var b in gm.sim.world.buildings) if (!b.Built) all = false;
                if (all) break;
                yield return null;
            }
            int walls = 0, tur = 0;
            foreach (var b in gm.sim.world.buildings) { if (b is WallBuilding) walls++; if (b is TurretBuilding) tur++; }
            Note($"placed walls {walls} (expect 4), turrets {tur} (expect 1), all built at {gm.sim.tick / 60f:0.0}s, core iron {gm.sim.Core.items[0]} (expect 400 - 32 - 35 = 333)");

            // 지역 1 을 봇이 플레이
            gm.bot = new AutoBot(gm.sim);
            gm.SetSpeed(2);
            while (gm.sim.state == SimState.Playing && !(gm.sim.wave >= 2 && gm.sim.enemies.Count >= 2 && gm.sim.bullets.Count >= 1)) yield return null;
            yield return Shot("09_play_wave");
            gm.SetSpeed(24);
            while (gm.sim != null && gm.sim.state == SimState.Playing) yield return null;
            int coreIron = gm.sim.Core.items[0];
            double baseIron = sv.stock[0];
            yield return new WaitForSeconds(0.3f);
            yield return Shot("10_result");
            Note($"region 1 {gm.sim.state} (expect Won), cleared {sv.abysses[0].cleared} (expect 1), returned iron {coreIron}: base {baseIron:0} -> {sv.stock[0]:0}");
            Note($"conquest bonus: iron rate {gm.Rate(Content.Iron)} (expect 69 = 66 + 3), health research open {gm.ResearchOpen(Research.Defs[Research.Health])} (expect True)");

            // 점령 뒤: 기지, 연구
            gm.ShowBase();
            yield return Shot("11_base_after");
            sv.stock[0] = 600; sv.stock[1] = 300;
            bool b1 = gm.BuyResearch(Research.Defs[Research.Damage]);
            bool b2 = gm.BuyResearch(Research.Defs[Research.Mortar]);   // 지역 2 를 점령해야 열린다
            sv.abysses[0].cleared = 12;                                  // 깊이 점령한 상태
            bool b3 = gm.BuyResearch(Research.Defs[Research.Mortar]);
            Note($"research damage {b1} mortar(locked) {b2} mortar(after region 2) {b3} (expect True False True), stock {(int)sv.stock[0]}/{(int)sv.stock[1]} (expect 350/140)");
            // 가진 만큼만 넣기: 강화 장갑(110 / 20)에 철광 50 만 있을 때
            double keepIron = sv.stock[0];
            sv.stock[0] = 50;
            var hd = Research.Defs[Research.Health];
            bool partial = gm.BuyResearch(hd);
            Note($"partial research: leveled {partial} (expect False), iron left {(int)sv.stock[0]} (expect 0), remaining iron {gm.ResearchRemaining(hd)[0]} sulfur {gm.ResearchRemaining(hd)[1]} (expect 60 0), progress {gm.ResearchProgress(hd):0.00} (expect 0.54)");
            sv.stock[0] = 60;
            bool finished = gm.BuyResearch(hd);
            Note($"partial research finished {finished} (expect True), health level {sv.research[Research.Health]} (expect 1), lance visible {Research.ParentDone(sv.research, Research.Defs[Research.Lance])} (expect True: mortar done), tier2 visible {Research.ParentDone(sv.research, Research.Defs[Research.Tier2])} (expect False)");
            sv.stock[0] = keepIron;
            gm.SelectResearch(Research.Lance);
            gm.ShowResearch();
            yield return Shot("12_research_after");
            gm.SelectResearch(-1);
            gm.ShowBase();
            yield return Shot("13_base_region12");

            // 후퇴: 남은 자원의 절반만 돌아온다
            sv.stock[0] = 2000; sv.stock[1] = 1000; sv.stock[2] = 200;
            sv.loadout[0] = 400; sv.loadout[1] = 400; sv.loadout[2] = 100;
            gm.ShowRegionMap(0);
            gm.ShowRegionSheet(0, 13);
            yield return Shot("14_region_sheet_13");
            gm.StartRegion(0, 13);
            Note($"region 13 mods damage x{gm.sim.world.mods.damageMul} (expect 1.15), mortar unlocked {gm.sim.IsUnlocked(Content.MortarTurret)} (expect True), lance unlocked {gm.sim.IsUnlocked(Content.CrystalTurret)} (expect False)");
            double ironBase = sv.stock[0];
            int ironCore = gm.sim.Core.items[0];
            gm.Retreat();
            Note($"retreat: core iron {ironCore}, base iron {ironBase:0} -> {sv.stock[0]:0} (expect +{ironCore / 2})");
            yield return Shot("15_after_retreat");

            // 가공 건물: 제련소를 짓고 10분 돌린다, 멈추면 원료를 쓰지 않는다
            sv.stock[0] = 3000; sv.stock[1] = 2000;
            bool rf1 = gm.UpgradeRefinery(0), rf2 = gm.UpgradeRefinery(3);
            double steel0 = sv.stock[Content.Steel], iron0 = sv.stock[0];
            sv.lastTime -= 600;
            gm.Accrue();
            Note($"smelter built {rf1}, fusion (needs region 14) {rf2} (expect True False), 10 min: steel +{sv.stock[Content.Steel] - steel0:0} (expect +60), iron {sv.stock[0] - iron0:+0;-0} (expect capped at storage or -180 + mining)");
            gm.ToggleRefinery(0);
            steel0 = sv.stock[Content.Steel];
            sv.lastTime -= 600;
            gm.Accrue();
            Note($"smelter stopped: steel +{sv.stock[Content.Steel] - steel0:0} (expect +0)");
            gm.ToggleRefinery(0);
            gm.SetScroll("base", 700);
            gm.ShowBase();
            yield return Shot("17_base_refinery");
            sv.research[Research.RobotFactory] = 1; sv.research[Research.Tier2] = 1; sv.research[Research.Damage] = 8;
            gm.SelectResearch(Research.Command);
            gm.ShowResearch();
            yield return Shot("18_research_units");
            gm.SelectResearch(-1);
            sv.research[Research.Damage] = 1;

            // 유닛: 공장을 짓고, 만들 유닛을 고르고, 집결 지점을 옮기고, 태세를 바꾼다
            sv.research[Research.RobotFactory] = 1; sv.research[Research.Airfield] = 1; sv.research[Research.Tier2] = 1; sv.research[Research.Command] = 2;
            for (int i = 0; i < sv.stock.Length; i++) { sv.stock[i] = 5000; sv.loadout[i] = 400; }
            gm.ShowRegionMap(0);
            gm.ShowRegionSheet(0, 13);
            yield return Shot("19_region_sheet_all_items");
            gm.StartRegion(0, 13);
            play = FindAnyObjectByType<PlayController>();
            var s13 = gm.sim;
            FactoryBuilding robots = null, air = null;
            for (int y = s13.Core.y + 4; y < s13.world.H && air == null; y++)
                for (int x = s13.Core.x - 3; x < s13.world.W - 2 && air == null; x++)
                {
                    var def = robots == null ? Content.RobotFactory : Content.Airfield;
                    if (s13.Place(def, x, y) != PlaceResult.Ok) continue;
                    if (robots == null) robots = (FactoryBuilding)s13.world.BuildAt(x, y); else air = (FactoryBuilding)s13.world.BuildAt(x, y);
                }
            gm.SetSpeed(8);
            while (robots == null || air == null || !robots.Built || !air.Built) yield return null;
            gm.SetSpeed(1);
            play.SelectFactory(robots);
            gm.SetProduct(1);
            gm.CycleFactoryStance(); gm.CycleFactoryStance();
            yield return Shot("20_factory_panel");
            Note($"factory stance: robots {robots.stance} (expect Attack), a robot unit would {s13.StanceOf(new Unit { home = robots })} (expect Attack) while everyone else is {s13.stance} (expect Defend)");
            gm.CycleFactoryStance();
            float rally0 = robots.rallyY;
            play.BeginRally();
            Vector2 rp = Camera.main.WorldToScreenPoint(new Vector3(s13.Core.Cx + 0.5f, s13.Core.y + 6.5f));
            play.DebugPointer(rp, true, false);
            play.DebugPointer(rp, false, true);
            Note($"factories built: robots {robots.Built} air {air.Built}, robot product {robots.def.units[robots.product].name} (expect 돌격 로봇), rally moved {rally0:0.0} -> {robots.rallyY:0.0} (expect {s13.Core.y + 6.5f:0.0}), rally mode off {!play.RallyMode}, unit cap {s13.UnitCap} (expect 8)");
            gm.SetSpeed(8);
            while (s13.units.Count < 4) yield return null;
            gm.SetSpeed(1);
            yield return Shot("21_units");
            gm.ToggleStance();
            Note($"units {s13.units.Count}, supply {s13.SupplyUsed}/{s13.UnitCap}, stance {s13.stance} (expect Attack)");
            gm.ToggleStance();
            play.Cancel();
            gm.bot = new AutoBot(s13);
            gm.SetSpeed(8);
            while (s13.state == SimState.Playing && !(s13.wave >= 3 && s13.enemies.Count >= 4 && s13.bullets.Count >= 2)) yield return null;
            yield return Shot("22_units_fight");
            Note($"region 13 with units: wave {s13.wave}, kills {s13.kills}, units built {s13.unitsBuilt} lost {s13.unitsLost}");
            gm.Retreat();

            // 적 거점 지역 15: 적 핵 둘레에는 못 짓고, 유닛을 공격 태세로 보내 적 핵을 부순다
            sv.abysses[0].cleared = 14;
            sv.research[Research.Tier3] = 1; sv.research[Research.Command] = 6;
            for (int i = 0; i < sv.stock.Length; i++) { sv.stock[i] = 5000; sv.loadout[i] = 400; }
            gm.ShowRegionMap(0);
            gm.ShowRegionSheet(0, 15);
            yield return Shot("23_stronghold_sheet");
            gm.StartRegion(0, 15);
            play = FindAnyObjectByType<PlayController>();
            var s15 = gm.sim;
            int ecx = s15.world.spawns[0] % s15.world.W, ecy = s15.world.spawns[0] / s15.world.W;
            var nearRes = s15.CanPlace(Content.Wall, ecx, ecy - 5, false);
            Camera.main.transform.position = new Vector3(ecx + 0.5f, ecy - 3f, -10);
            yield return Shot("24_stronghold_base");
            FactoryBuilding f15 = null;
            for (int y = s15.Core.y + 4; y < s15.world.H && f15 == null; y++)
                for (int x = s15.Core.x - 3; x < s15.world.W - 2 && f15 == null; x++)
                    if (s15.Place(Content.RobotFactory, x, y) == PlaceResult.Ok) f15 = (FactoryBuilding)s15.world.BuildAt(x, y);
            for (int i = 0; i < 8; i++) s15.SpawnUnit(Content.Heavy, f15);
            int cores0 = s15.EnemyCores;
            gm.ToggleStance();
            gm.bot = new AutoBot(s15);
            gm.SetSpeed(8);
            while (s15.state == SimState.Playing && s15.structuresDestroyed < 3) yield return null;
            gm.SetSpeed(1);
            if (s15.units.Count > 0) Camera.main.transform.position = new Vector3(s15.units[0].x, s15.units[0].y, -10);
            yield return Shot("25_stronghold_attack");
            gm.SetSpeed(24);
            while (gm.sim != null && gm.sim.state == SimState.Playing) yield return null;
            yield return new WaitForSeconds(0.3f);
            yield return Shot("26_stronghold_result");
            Note($"stronghold 15: enemy cores {cores0} (expect 2), build next to enemy core {nearRes} (expect NearSpawn), result {s15.state} (expect Won), enemy cores left {s15.EnemyCores} (expect 0), structures destroyed {s15.structuresDestroyed}, units lost {s15.unitsLost}, cleared {sv.abysses[0].cleared} (expect 15), iron conquest bonus doubled {BaseRules.ConquestRate(Content.Iron, new[] { 15, 0, 0 }) - BaseRules.ConquestRate(Content.Iron, new[] { 14, 0, 0 })} (expect 0: region 15 gives crystal) crystal {BaseRules.ConquestRate(Content.Crystal, new[] { 15, 0, 0 }) - BaseRules.ConquestRate(Content.Crystal, new[] { 14, 0, 0 })} (expect 1.5)");

            // 새 유닛: 지원 공방·다리 공방·조선소, 해상·수송·지휘기
            foreach (int id in new[] { Research.Support, Research.Legs, Research.Shipyard, Research.Tier4, Research.Tier5, Research.Mortar, Research.Lance, Research.SpecialTurret, Research.AdvTurret, Research.SupportBlock }) sv.research[id] = 1;
            for (int i = 0; i < sv.stock.Length; i++) { sv.stock[i] = 5000; sv.loadout[i] = 400; }
            gm.SelectResearch(Research.Tier4);
            gm.ShowResearch();
            yield return Shot("27_research_tree_full");
            gm.SelectResearch(-1);
            gm.StartRegion(0, 30);
            play = FindAnyObjectByType<PlayController>();
            var s30 = gm.sim;
            s30.waveCountdown = 60 * 600;
            FactoryBuilding yard = null, robo = null, sup = null, leg = null;
            for (int y = 1; y < s30.world.H - 2 && yard == null; y++)
                for (int x = 1; x < s30.world.W - 2 && yard == null; x++)
                    if (Mathf.Abs(y - s30.Core.y) < 14 && s30.Place(Content.Shipyard, x, y) == PlaceResult.Ok) yard = (FactoryBuilding)s30.world.BuildAt(x, y);
            for (int y = s30.Core.y + 4; y < s30.world.H && leg == null; y++)
                for (int x = s30.Core.x - 4; x < s30.world.W - 2 && leg == null; x++)
                {
                    var def = robo == null ? Content.RobotFactory : sup == null ? Content.SupportShop : Content.LegShop;
                    if (s30.Place(def, x, y) != PlaceResult.Ok) continue;
                    var fb = (FactoryBuilding)s30.world.BuildAt(x, y);
                    if (robo == null) robo = fb; else if (sup == null) sup = fb; else leg = fb;
                }
            foreach (var fb in new[] { yard, robo, sup, leg }) if (fb != null) fb.product = -1;
            gm.SetSpeed(8);
            while (robo == null || !robo.Built || !sup.Built || !leg.Built || (yard != null && !yard.Built)) yield return null;
            gm.SetSpeed(1);
            foreach (var ud in new[] { Content.Skimmer, Content.Arcer, Content.Siege, Content.Titan }) s30.SpawnUnit(ud, robo);
            foreach (var ud in new[] { Content.Mechanic, Content.Guardian, Content.Booster, Content.RepairDrone, Content.Transport, Content.Miner, Content.Rebuilder, Content.Seeker, Content.Mender, Content.Warship, Content.Carrier }) s30.SpawnUnit(ud, sup);
            foreach (var ud in new[] { Content.Bug, Content.Climber, Content.Spitter, Content.Leech, Content.Shatter, Content.Pyre }) s30.SpawnUnit(ud, leg);
            if (yard != null) foreach (var ud in new[] { Content.Patrol, Content.Gunboat, Content.Destroyer, Content.Shockboat, Content.Arcship, Content.Flagship }) s30.SpawnUnit(ud, yard);
            // 핵을 탭하고 다른 곳을 탭하면 지휘기가 그리로 간다
            Vector2 corePt = Camera.main.WorldToScreenPoint(new Vector3(s30.Core.Cx, s30.Core.Cy));
            play.DebugPointer(corePt, true, false); play.DebugPointer(corePt, false, true);
            bool cmdMode = play.CommandMode;
            Vector2 cmdPt = Camera.main.WorldToScreenPoint(new Vector3(s30.Core.Cx - 2.5f, s30.Core.Cy + 5.5f));
            play.DebugPointer(cmdPt, true, false); play.DebugPointer(cmdPt, false, true);
            gm.SetSpeed(4);
            yield return new WaitForSeconds(4f);
            gm.SetSpeed(1);
            play.SelectFactory(robo);
            yield return Shot("28_new_units");
            var cm = s30.commander;
            Note($"new units: shipyard placed {yard != null}, units {s30.units.Count} (expect 27 with shipyard, plus any escorts the flagship has launched), commander mode after tapping core {cmdMode} (expect True), command point {s30.commandX:0.0},{s30.commandY:0.0} (expect {s30.Core.Cx - 2.5f:0.0},{s30.Core.Cy + 5.5f:0.0}), commander there {cm != null && Mathf.Abs(cm.x - s30.commandX) < 0.6f && Mathf.Abs(cm.y - s30.commandY) < 0.6f}");
            if (yard != null)
            {
                play.Cancel();
                Camera.main.transform.position = new Vector3(yard.Cx, yard.Cy + 2, -10);
                bool onWater = true;
                foreach (var u in s30.units) if (u.def.naval && !s30.world.tiles[s30.world.Idx((int)u.x, (int)u.y)].water) onWater = false;
                Note($"naval units on water {onWater} (expect True)");
                yield return Shot("29_shipyard");
            }
            // 포탑: 일곱 종을 짓고, 하나를 탭해 탄종과 표적 기준을 바꾼다
            play.Cancel();
            for (int i = 0; i < s30.Core.items.Length; i++) s30.Core.items[i] += 3000;
            var turrets =new System.Collections.Generic.List<TurretBuilding>();
            BlockDef[] tdefs = { Content.RapidTurret, Content.MortarTurret, Content.CrystalTurret, Content.FlakTurret, Content.SlimeTurret, Content.TeslaTurret, Content.FlameTurret };
            for (int y = s30.Core.y + 4; y < s30.world.H && turrets.Count < tdefs.Length; y += 3)
                for (int x = 2; x < s30.world.W - 3 && turrets.Count < tdefs.Length; x += 3)
                    if (s30.Place(tdefs[turrets.Count], x, y) == PlaceResult.Ok) turrets.Add((TurretBuilding)s30.world.BuildAt(x, y));
            gm.SetSpeed(8);
            while (turrets.Exists(tb => !tb.Built)) yield return null;
            gm.SetSpeed(1);
            var rapid = turrets[0];
            Camera.main.transform.position = new Vector3(rapid.Cx, rapid.Cy + 3, -10);
            yield return null;
            Vector2 tpt = Camera.main.WorldToScreenPoint(new Vector3(rapid.Cx, rapid.Cy));
            play.DebugPointer(tpt, true, false); play.DebugPointer(tpt, false, true);
            bool picked = play.Turret == rapid;
            gm.SetAmmo(1);
            gm.SetTargetMode(TargetMode.Strongest);
            gm.ToggleOvercharge();
            yield return Shot("30_turret_panel");
            Note($"turrets: built {turrets.Count} (expect 7), tap selects turret {picked} (expect True), ammo {rapid.Ammo.name} (expect 철갑탄), target mode {rapid.mode} (expect Strongest), overcharge {rapid.overcharge} (expect True)");
            play.Cancel();

            // 지원 건물 셋과 부서진 자리(자동 재건 대기)
            BlockDef[] sdefs = { Content.MendTower, Content.Amplifier, Content.ShieldGen };
            var supports = new System.Collections.Generic.List<Building>();
            for (int y = (int)rapid.Cy + 2; y < s30.world.H && supports.Count < sdefs.Length; y += 3)
                for (int x = 2; x < s30.world.W - 3 && supports.Count < sdefs.Length; x += 3)
                    if (s30.Place(sdefs[supports.Count], x, y) == PlaceResult.Ok) supports.Add(s30.world.BuildAt(x, y));
            gm.SetSpeed(8);
            while (supports.Exists(sb => !sb.Built)) yield return null;
            gm.SetSpeed(1);
            var genB = supports[2] as SupportBuilding;
            turrets[1].health = 0;
            yield return new WaitForSeconds(0.3f);
            Camera.main.transform.position = new Vector3(supports[1].Cx, supports[1].Cy + 2, -10);
            yield return Shot("31_support_blocks");
            Note($"support blocks: built {supports.Count} (expect 3), shield charging {genB.shieldHp > 0} (expect True), auto rebuild {s30.autoRebuild} (expect True), ruins waiting {s30.ruins.Count} (expect 1)");
            gm.ToggleAutoRebuild();
            Note($"auto rebuild off: {!s30.autoRebuild} (expect True), ruins {s30.ruins.Count} (expect 0)");

            // 적 14종을 한 줄로 세워 그림과 표시(정예·보호막·실은 적·상태 이상)를 본다
            s30.units.Clear();
            float ex0 = s30.Core.Cx - 6f, ey0 = s30.Core.Cy + 9f;
            for (int i = 0; i < Content.Enemies.Length; i++)
            {
                var ed = Content.Enemies[i];
                var en = new Enemy { def = ed, x = ex0 + (i % 7) * 2f, y = ey0 + (i / 7) * 2.2f, health = 99999, maxHealth = 99999, cooldown = 1 << 20, slowTicks = 1 << 20, slowMul = 0 };
                if (ed == Content.Walker) en.elite = true;
                if (ed == Content.Guard || ed == Content.Shell) { en.barrierMax = 100; en.barrier = 60; }
                if (ed == Content.Dropship) en.cargo = 4;
                if (ed == Content.Runner) en.acidTicks = 1 << 20;
                if (ed == Content.Breaker) en.wetTicks = 1 << 20;
                s30.enemies.Add(en);
            }
            Camera.main.transform.position = new Vector3(s30.Core.Cx, ey0 + 1f, -10);
            yield return new WaitForSeconds(0.2f);
            yield return Shot("32_enemies");
            s30.enemies.Clear();
            gm.Retreat();
            sv.abysses[0].cleared = 44;
            gm.ShowRegionMap(0);
            gm.ShowRegionSheet(0, 45);
            yield return Shot("33_region_sheet_45");

            // 깊은 지역 전투
            sv.abysses[2].cleared = 29;
            sv.research[Research.Damage] = 4; sv.research[Research.Loadout] = 3; sv.research[Research.Lance] = 1;
            sv.stock[0] = 5000; sv.stock[1] = 5000; sv.stock[2] = 2000;
            sv.loadout[0] = 2000; sv.loadout[1] = 2000; sv.loadout[2] = 2000;
            gm.StartRegion(2, 30);
            Note($"loadout cap with research 3: took iron {gm.Taken[0]} (expect 1150)");
            gm.bot = new AutoBot(gm.sim);
            gm.SetSpeed(8);
            while (gm.sim.state == SimState.Playing && !(gm.sim.wave >= 4 && gm.sim.enemies.Count >= 8 && gm.sim.bullets.Count >= 1)) yield return null;
            yield return Shot("16_play_region30");

            gm.Retreat();

            // 도감 세 쪽
            gm.ShowBase();
            yield return Shot("34_base_with_dex");
            for (int tab = 0; tab < 3; tab++) { gm.ShowDex(tab); yield return Shot("35_dex_" + tab); }

            // 진행 중 저장과 이어하기: 명령으로 짓고, 저장하고, 세이브를 다시 읽어 이어한다
            for (int i = 0; i < sv.stock.Length; i++) { sv.stock[i] = 5000; sv.loadout[i] = 400; }
            gm.StartRegion(0, 12);
            var live = gm.sim;
            int lx = (int)live.Core.Cx, ly = live.Core.y;
            for (int i = -2; i <= 2; i++) live.CmdPlace(i == 0 ? Content.MortarTurret : Content.RapidTurret, lx + i * 2, ly + 5);
            gm.SetSpeed(8);
            while (live.tick < 60 * 100 && live.state == SimState.Playing) yield return null;
            live.CmdCallWave();
            while (live.tick < 60 * 150 && live.state == SimState.Playing) yield return null;
            gm.SetSpeed(1);
            gm.TogglePause();
            yield return Shot("36_preview_minimap");
            gm.Save();
            long liveHash = live.StateHash(); int liveTick = live.tick, liveCmds = live.log.Count;
            gm.ReloadSave();
            sv = gm.save;
            bool can = gm.CanResume;
            gm.ShowTitle();
            yield return Shot("37_title_resume");
            gm.ResumeRun();
            while (gm.sim == null) yield return null;
            yield return Shot("38_resumed");
            Note($"resume: can resume {can} (expect True), commands {liveCmds} (expect 5 or 6), tick {gm.sim.tick} (expect {liveTick}), same state {gm.sim.StateHash() == liveHash} (expect True), paused {gm.Paused} (expect True)");
            gm.Retreat();
            Note($"after retreat: can resume {gm.CanResume} (expect False)");

            // 심연 규칙: 잿불의 용암 틈, 서리의 눈보라. 난이도
            for (int i = 0; i < sv.stock.Length; i++) { sv.stock[i] = 5000; sv.loadout[i] = 400; }
            sv.difficulty = 2;
            gm.ShowRegionMap(0);
            gm.ShowRegionSheet(0, 30);
            yield return Shot("39_region_sheet_hard");
            gm.StartRegion(0, 30);
            play = FindAnyObjectByType<PlayController>();
            var hs = gm.sim;
            int hazards = 0, firstHz = -1;
            for (int i = 0; i < hs.world.tiles.Length; i++) if (hs.world.tiles[i].hazard) { hazards++; if (firstHz < 0 && i / hs.world.W > hs.Core.y + 8) firstHz = i; }
            if (firstHz < 0) firstHz = hs.world.Idx((int)hs.Core.Cx, hs.Core.y + 10);
            Camera.main.transform.position = new Vector3(firstHz % hs.world.W + 0.5f, firstHz / hs.world.W + 2.5f, -10);
            var onHz = hs.CanPlace(Content.Wall, firstHz % hs.world.W, firstHz / hs.world.W, false);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("40_lava");
            Note($"hazards: ember region 30 hard, hazard tiles {hazards} (expect > 0), build on hazard {onHz} (expect Hazard), difficulty {hs.rules.difficulty} (expect 2), enemy health x{hs.rules.enemyHealthMul / RegionRules.For(0, 30).enemyHealthMul:0.00} (expect 1.40)");
            gm.Retreat();
            sv.difficulty = 1;
            sv.abysses[1].cleared = 29;
            for (int i = 0; i < sv.stock.Length; i++) { sv.stock[i] = 5000; sv.loadout[i] = 400; }
            gm.StartRegion(1, 30);
            var fs = gm.sim;
            fs.waveCountdown = 60 * 600;
            gm.SetSpeed(24);
            while (!fs.Blizzard && fs.state == SimState.Playing) yield return null;
            gm.SetSpeed(1);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("41_blizzard");
            Note($"blizzard: frost region 30 blowing {fs.Blizzard} (expect True), left {fs.blizzardLeft / 60}s (expect about 19)");
            gm.Retreat();

            // 덮어 짓기와 힌트
            for (int i = 0; i < sv.stock.Length; i++) { sv.stock[i] = 5000; sv.loadout[i] = 400; }
            int hintsSeen = sv.hints.Count;
            sv.hints.Clear();
            gm.StartRegion(0, 3);
            var rs = gm.sim;
            yield return new WaitForSeconds(1f);
            play = FindAnyObjectByType<PlayController>();   // 앞 지역의 조작 객체가 치워진 뒤에 찾는다
            yield return Shot("42_hint_build");
            int wx = (int)rs.Core.Cx - 3, wy = rs.Core.y + 5;
            rs.CmdPlace(Content.Wall, wx, wy);
            gm.SetSpeed(8);
            while (rs.world.BuildAt(wx, wy) == null || !rs.world.BuildAt(wx, wy).Built) yield return null;
            gm.SetSpeed(1);
            play.Select(Content.SteelWall);
            Camera.main.transform.position = new Vector3(wx + 0.5f, wy + 3f, -10);
            yield return null;
            Vector2 wpt = Camera.main.WorldToScreenPoint(new Vector3(wx + 0.5f, wy + 0.5f));
            play.DebugPointer(wpt, true, false); play.DebugPointer(wpt, false, true);
            var swapped = rs.world.BuildAt(wx, wy);
            play.Cancel();
            Note($"hints and replace: hints seen during this run {hintsSeen} (expect > 3), tap steel wall on wall -> {swapped.def.name} (expect 강철 방벽), building {!swapped.Built} (expect True)");
            gm.Retreat();

            File.WriteAllText(Path.Combine(dir, "_done.txt"), "errors " + errors);
            Application.Quit();
        }

        IEnumerator Shot(string name)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return null;
            yield return null;
        }
    }
}
#endif
