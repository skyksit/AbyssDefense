using System;

namespace AbyssDefense.Sim
{
    /// 연구로 얻는 보정값. 지역 한 판 내내 고정이다.
    public sealed class SimMods
    {
        public float damageMul = 1, rangeMul = 1, healthMul = 1, coreHealthMul = 1, ammoMul = 1, bountyMul = 1, buildSpeedMul = 1;
        public float unitMul = 1;                               // 유닛의 피해와 체력
        public int unitCap = Research.BaseUnitCap;              // 유닛 한도 (유닛마다 단계만큼 차지한다)
        public bool[] tech = new bool[Research.Defs.Length];   // 마친 연구 (건물·유닛 해금 확인용)
    }

    public sealed class ResearchDef
    {
        public int id;
        public string key, name, desc;
        public int maxLevel;
        public float perLevel;            // 단계마다 더해지는 양 (비율 또는 개수)
        public int reqRegion;             // 어느 심연에서든 이 지역을 점령해야 연구할 수 있다 (0 = 처음부터)
        public int iron, sulfur, crystal; // 1단계 비용. crystal 은 crystalFrom 단계부터 든다
        public int crystalFrom = 3;
        public ItemStack[] extra = new ItemStack[0];   // 가공 자원 비용 (1단계 기준)
        public bool percent = true;
        public int parent = -1;           // 연구 트리에서 앞 연구 (-1 = 기지에서 바로 뻗는 가지). 앞 연구를 1단계 이상 마쳐야 드러난다
        public string icon;               // 트리 노드에 쓰는 그림 이름
    }

    /// 기지에서 하는 연구. 모든 심연에 함께 적용된다. 지역을 점령하면 새 연구가 열린다.
    public static class Research
    {
        public const int Damage = 0, Health = 1, Loadout = 2, Mortar = 3, Core = 4, Range = 5, Mining = 6, Ammo = 7, Bounty = 8, Lance = 9, Build = 10,
            RobotFactory = 11, Airfield = 12, Tier2 = 13, Tier3 = 14, Command = 15, UnitPower = 16,
            Support = 17, Legs = 18, Shipyard = 19, Tier4 = 20, Tier5 = 21;
        public const int BaseUnitCap = 4;

        public static readonly ResearchDef[] Defs =
        {
            new ResearchDef { key = "damage", name = "포탑 화력", desc = "모든 포탑의 피해가 늘어난다", maxLevel = 8, perLevel = 0.15f, iron = 100, sulfur = 60, crystal = 40 },
            new ResearchDef { key = "health", name = "강화 장갑", desc = "방벽·포탑·공장의 체력이 늘어난다", maxLevel = 5, perLevel = 0.2f, reqRegion = 1, iron = 110, sulfur = 20, crystal = 30 },
            new ResearchDef { key = "loadout", name = "출격 물자", desc = "출격할 때 가져갈 수 있는 자원 한도가 늘어난다", maxLevel = 6, perLevel = 150, percent = false, reqRegion = 1, iron = 120, sulfur = 40, crystal = 30 },
            new ResearchDef { key = "mortar", name = "박격포 기술", desc = "박격포를 지을 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 2, iron = 150, sulfur = 100 },
            new ResearchDef { key = "core", name = "핵 보호막", desc = "심연 핵의 체력이 늘어난다", maxLevel = 5, perLevel = 0.3f, reqRegion = 3, iron = 120, sulfur = 50, crystal = 40 },
            new ResearchDef { key = "range", name = "조준 장치", desc = "모든 포탑의 사거리가 늘어난다", maxLevel = 4, perLevel = 0.07f, reqRegion = 4, iron = 90, sulfur = 70, crystal = 40, crystalFrom = 2 },
            new ResearchDef { key = "mining", name = "채굴 기술", desc = "기지 채굴장의 생산량이 늘어난다", maxLevel = 5, perLevel = 0.1f, reqRegion = 5, iron = 160, sulfur = 60, crystal = 30 },
            new ResearchDef { key = "ammo", name = "탄약 절약", desc = "자원 하나로 더 많은 탄을 만든다", maxLevel = 4, perLevel = 0.25f, reqRegion = 6, iron = 70, sulfur = 80, crystal = 30 },
            new ResearchDef { key = "bounty", name = "전리품 회수", desc = "적을 처치하고 얻는 자원이 늘어난다", maxLevel = 5, perLevel = 0.2f, reqRegion = 8, iron = 130, sulfur = 70, crystal = 30 },
            new ResearchDef { key = "lance", name = "결정포 기술", desc = "결정포를 지을 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 10, iron = 300, sulfur = 200, crystal = 40, crystalFrom = 0 },
            new ResearchDef { key = "build", name = "건설 드론", desc = "지역에서 건물을 더 빨리 짓는다", maxLevel = 4, perLevel = 0.25f, reqRegion = 3, iron = 100, sulfur = 40, crystal = 30 },
            new ResearchDef { key = "robotfactory", name = "로봇 공장 기술", desc = "로봇 공장을 짓고 척후 로봇을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 5, iron = 200, sulfur = 100,
                extra = new[] { new ItemStack(Content.Steel, 40) } },
            new ResearchDef { key = "airfield", name = "비행장 기술", desc = "비행장을 짓고 정찰기를 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 8, iron = 250, sulfur = 150,
                extra = new[] { new ItemStack(Content.Steel, 60), new ItemStack(Content.Circuit, 40) } },
            new ResearchDef { key = "tier2", name = "기체 개량 I", desc = "2단계 유닛(돌격 로봇·폭격기)을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 12, iron = 300, sulfur = 200,
                extra = new[] { new ItemStack(Content.Steel, 100), new ItemStack(Content.Circuit, 80) } },
            new ResearchDef { key = "tier3", name = "기체 개량 II", desc = "3단계 유닛(중장 로봇·강습기)을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 25, iron = 500, sulfur = 300, crystal = 100, crystalFrom = 0,
                extra = new[] { new ItemStack(Content.Circuit, 150), new ItemStack(Content.Alloy, 60) } },
            new ResearchDef { key = "command", name = "지휘 체계", desc = "지역에서 거느릴 수 있는 유닛 한도가 늘어난다", maxLevel = 6, perLevel = 2, percent = false, reqRegion = 6, iron = 150, sulfur = 80,
                extra = new[] { new ItemStack(Content.Circuit, 20) } },
            new ResearchDef { key = "unitpower", name = "기체 강화", desc = "유닛의 피해와 체력이 늘어난다", maxLevel = 5, perLevel = 0.12f, reqRegion = 16, iron = 150, sulfur = 100,
                extra = new[] { new ItemStack(Content.Steel, 40), new ItemStack(Content.Circuit, 30) } },
            new ResearchDef { key = "support", name = "지원 공방 기술", desc = "지원 공방을 짓고 정비 로봇·수리 드론을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 10, iron = 250, sulfur = 150,
                extra = new[] { new ItemStack(Content.Steel, 60), new ItemStack(Content.Circuit, 40) } },
            new ResearchDef { key = "legs", name = "다리 공방 기술", desc = "다리 공방을 짓고 자폭 벌레·등반 로봇을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 18, iron = 300, sulfur = 250,
                extra = new[] { new ItemStack(Content.Steel, 80), new ItemStack(Content.Powder, 60) } },
            new ResearchDef { key = "shipyard", name = "조선소 기술", desc = "물가에 조선소를 짓고 초계정을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 20, iron = 350, sulfur = 200,
                extra = new[] { new ItemStack(Content.Steel, 120), new ItemStack(Content.Circuit, 80) } },
            new ResearchDef { key = "tier4", name = "기체 개량 III", desc = "4단계 유닛(공성 로봇·전함)을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 45, iron = 800, sulfur = 500, crystal = 200, crystalFrom = 0,
                extra = new[] { new ItemStack(Content.Circuit, 250), new ItemStack(Content.Alloy, 150) } },
            new ResearchDef { key = "tier5", name = "기체 개량 IV", desc = "5단계 유닛(거신 로봇·모함)을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 70, iron = 1500, sulfur = 900, crystal = 400, crystalFrom = 0,
                extra = new[] { new ItemStack(Content.Circuit, 400), new ItemStack(Content.Alloy, 350) } },
        };

        static Research()
        {
            for (int i = 0; i < Defs.Length; i++) Defs[i].id = i;
            // 연구 트리: 위쪽 가지는 전투(포탑·방어), 아래쪽 가지는 보급과 유닛
            Link(Damage, -1, "icon_rapid");
            Link(Mortar, Damage, "icon_mortar");
            Link(Lance, Mortar, "icon_lance");
            Link(Range, Damage, "fx_ring");
            Link(Ammo, Range, "item_sulfur");
            Link(Health, -1, "icon_wall");
            Link(Core, Health, "icon_core");
            Link(Build, Health, "icon_steelwall");
            Link(Loadout, -1, "item_iron");
            Link(Mining, Loadout, "item_crystal");
            Link(Bounty, Mining, "enemy_walker");
            Link(RobotFactory, -1, "icon_robotfactory");
            Link(Airfield, RobotFactory, "icon_airfield");
            Link(Tier2, RobotFactory, "unit_assault");
            Link(Tier3, Tier2, "unit_heavy");
            Link(UnitPower, Tier2, "unit_scout");
            Link(Command, RobotFactory, "unit_commander");
            Link(Shipyard, Airfield, "icon_shipyard");
            Link(Tier4, Tier3, "unit_siege");
            Link(Tier5, Tier4, "unit_titan");
            Link(Support, RobotFactory, "icon_supportshop");
            Link(Legs, RobotFactory, "icon_legshop");
        }

        static void Link(int id, int parent, string icon) { Defs[id].parent = parent; Defs[id].icon = icon; }

        /// 앞 연구를 마쳐 트리에 드러났는가
        public static bool ParentDone(int[] lv, ResearchDef d) => d.parent < 0 || Level(lv, d.parent) > 0;

        /// level(0부터) 에서 다음 단계로 올리는 비용 (자원 번호 순)
        public static int[] Cost(ResearchDef d, int level)
        {
            float m = 1 + level * 0.9f;
            var c = new int[Content.Items.Length];
            c[Content.Iron] = Round10(d.iron * m);
            c[Content.Sulfur] = Round10(d.sulfur * m);
            if (d.crystal > 0 && level >= d.crystalFrom) c[Content.Crystal] = Round10(d.crystal * (level - d.crystalFrom + 1));
            foreach (var e in d.extra) c[e.item] = Round10(e.amount * m);
            return c;
        }

        static int Round10(float v) => (int)Math.Round(v / 10f) * 10;

        public static int Level(int[] lv, int id) => lv != null && id < lv.Length ? Math.Min(lv[id], Defs[id].maxLevel) : 0;

        public static SimMods Mods(int[] lv)
        {
            var m = new SimMods();
            if (lv == null) return m;
            m.damageMul = 1 + Level(lv, Damage) * Defs[Damage].perLevel;
            m.rangeMul = 1 + Level(lv, Range) * Defs[Range].perLevel;
            m.healthMul = 1 + Level(lv, Health) * Defs[Health].perLevel;
            m.coreHealthMul = 1 + Level(lv, Core) * Defs[Core].perLevel;
            m.ammoMul = 1 + Level(lv, Ammo) * Defs[Ammo].perLevel;
            m.bountyMul = 1 + Level(lv, Bounty) * Defs[Bounty].perLevel;
            m.buildSpeedMul = 1 + Level(lv, Build) * Defs[Build].perLevel;
            m.unitMul = 1 + Level(lv, UnitPower) * Defs[UnitPower].perLevel;
            m.unitCap = BaseUnitCap + (int)(Level(lv, Command) * Defs[Command].perLevel);
            for (int i = 0; i < Defs.Length; i++) m.tech[i] = Level(lv, i) > 0;
            return m;
        }
    }

    /// 기지의 가공 건물: 분당 일정량의 원료를 가공 자원으로 바꾼다
    public sealed class RefineryDef
    {
        public int id;
        public string name;
        public int output;
        public ItemStack[] inputs;        // 산출물 하나에 드는 원료
        public float baseRate;            // 1단계의 분당 산출량
        public int reqRegion;             // 이 지역을 점령해야 지을 수 있다
        public ItemStack[] buildCost, upgradeCost;
    }

    /// 기지: 심연과 상관없이 자원을 캐고 가공하고 쌓는 곳. 건물 단계와 점령한 지역 수로 분당 생산량이 정해진다.
    public static class BaseRules
    {
        public const int MaxMineLevel = 10, MaxStorageLevel = 10, MaxRefineryLevel = 8;
        public const int CrystalMineRegion = 10;     // 이 지역을 점령해야 결정 채굴장을 지을 수 있다
        public const int OfflineCapSeconds = 8 * 3600;
        public static readonly string[] MineNames = { "철광 채굴장", "황석 채굴장", "결정 채굴장" };
        static readonly float[] baseRate = { 30, 12, 6 };

        public static readonly RefineryDef[] Refineries =
        {
            new RefineryDef { name = "제련소", output = Content.Steel, baseRate = 6, reqRegion = 2,
                inputs = new[] { new ItemStack(Content.Iron, 3) },
                buildCost = new[] { new ItemStack(Content.Iron, 200), new ItemStack(Content.Sulfur, 80) },
                upgradeCost = new[] { new ItemStack(Content.Iron, 180), new ItemStack(Content.Sulfur, 60) } },
            new RefineryDef { name = "회로 조립소", output = Content.Circuit, baseRate = 4, reqRegion = 4,
                inputs = new[] { new ItemStack(Content.Iron, 2), new ItemStack(Content.Sulfur, 1) },
                buildCost = new[] { new ItemStack(Content.Iron, 250), new ItemStack(Content.Sulfur, 150), new ItemStack(Content.Steel, 30) },
                upgradeCost = new[] { new ItemStack(Content.Iron, 150), new ItemStack(Content.Sulfur, 100), new ItemStack(Content.Steel, 20) } },
            new RefineryDef { name = "화약 합성소", output = Content.Powder, baseRate = 4, reqRegion = 7,
                inputs = new[] { new ItemStack(Content.Sulfur, 2) },
                buildCost = new[] { new ItemStack(Content.Iron, 200), new ItemStack(Content.Sulfur, 250), new ItemStack(Content.Steel, 40) },
                upgradeCost = new[] { new ItemStack(Content.Iron, 120), new ItemStack(Content.Sulfur, 150), new ItemStack(Content.Steel, 30) } },
            new RefineryDef { name = "합금 융합로", output = Content.Alloy, baseRate = 2, reqRegion = 14,
                inputs = new[] { new ItemStack(Content.Steel, 2), new ItemStack(Content.Crystal, 1) },
                buildCost = new[] { new ItemStack(Content.Iron, 400), new ItemStack(Content.Crystal, 200), new ItemStack(Content.Steel, 100), new ItemStack(Content.Circuit, 60) },
                upgradeCost = new[] { new ItemStack(Content.Iron, 200), new ItemStack(Content.Crystal, 60), new ItemStack(Content.Steel, 80), new ItemStack(Content.Circuit, 40) } },
        };

        static BaseRules() { for (int i = 0; i < Refineries.Length; i++) Refineries[i].id = i; }

        /// 채굴장 한 곳의 분당 생산량
        public static float MineRate(int item, int level) => level <= 0 || item >= Content.RawCount ? 0 : baseRate[item] * level * (1 + 0.1f * (level - 1));

        /// 채굴장을 level → level+1 로 올리는 비용
        public static int[] MineCost(int item, int level)
        {
            var c = new int[Content.Items.Length];
            if (level == 0) { c[0] = item == Content.Crystal ? 600 : 100; c[1] = item == Content.Crystal ? 400 : 50; return c; }
            float m = (float)Math.Pow(level, 1.7);
            if (item == Content.Iron) { c[0] = R(120 * m); c[1] = R(40 * m); }
            else if (item == Content.Sulfur) { c[0] = R(100 * m); c[1] = R(60 * m); }
            else { c[0] = R(200 * m); c[1] = R(150 * m); c[2] = R(30 * m); }
            return c;
        }

        /// 가공 건물의 분당 산출량
        public static float RefineryRate(RefineryDef d, int level) => level <= 0 ? 0 : d.baseRate * level * (1 + 0.1f * (level - 1));

        /// 가공 건물을 level → level+1 로 올리는 비용 (0 → 1 은 건설)
        public static int[] RefineryCost(RefineryDef d, int level)
        {
            var c = new int[Content.Items.Length];
            if (level == 0) { foreach (var s in d.buildCost) c[s.item] = s.amount; return c; }
            float m = (float)Math.Pow(level, 1.7);
            foreach (var s in d.upgradeCost) c[s.item] = R(s.amount * m);
            return c;
        }

        public static int StorageCap(int level) => R(1500 * (float)Math.Pow(1.6, Math.Max(0, level - 1)), 100);

        public static int[] StorageCost(int level)
        {
            float m = (float)Math.Pow(1.7, level - 1);
            var c = new int[Content.Items.Length];
            c[0] = R(250 * m); c[1] = R(120 * m);
            return c;
        }

        /// 출격할 때 자원 한 종류를 가져갈 수 있는 최대량
        public static int LoadoutCap(int[] research) => 400 + (int)(Research.Level(research, Research.Loadout) * Research.Defs[Research.Loadout].perLevel);

        /// 지역 하나를 처음 점령했을 때 영구히 늘어나는 생산량: (자원, 분당 양)
        public static void RegionBonus(int region, out int item, out float perMin)
        {
            int k = region % 3;
            if (k == 1) { item = Content.Iron; perMin = 3; }
            else if (k == 2) { item = Content.Sulfur; perMin = 1.5f; }
            else if (region >= CrystalMineRegion) { item = Content.Crystal; perMin = 0.75f; }
            else { item = Content.Iron; perMin = 3; }
            if (RegionRules.IsAssault(region)) perMin *= 2;   // 적 거점을 부순 지역은 보상이 두 배
        }

        /// 점령한 지역들에서 들어오는 분당 생산량 (cleared = 심연별 점령한 가장 깊은 지역)
        public static float ConquestRate(int item, int[] cleared)
        {
            if (item >= Content.RawCount) return 0;
            float sum = 0;
            foreach (int c in cleared)
                for (int r = 1; r <= c; r++)
                {
                    RegionBonus(r, out int it, out float v);
                    if (it == item) sum += v;
                }
            return sum;
        }

        /// 기지 채굴의 분당 총생산량 (가공 건물이 쓰는 양은 빼지 않은 값)
        public static float Rate(int item, int mineLevel, int[] research, int[] cleared) =>
            MineRate(item, mineLevel) * (1 + Research.Level(research, Research.Mining) * Research.Defs[Research.Mining].perLevel)
            + ConquestRate(item, cleared);

        /// 가공 건물이 모두 돌 때 자원별 분당 증감 (채굴 + 가공 산출 − 가공 원료)
        public static float[] NetRates(int[] mineLv, int[] refLv, bool[] refOn, int[] research, int[] cleared)
        {
            var net = new float[Content.Items.Length];
            for (int i = 0; i < Content.RawCount; i++) net[i] = Rate(i, mineLv[i], research, cleared);
            for (int k = 0; k < Refineries.Length; k++)
            {
                if (!refOn[k]) continue;
                var d = Refineries[k];
                float rate = RefineryRate(d, refLv[k]);
                net[d.output] += rate;
                foreach (var s in d.inputs) net[s.item] -= rate * s.amount;
            }
            return net;
        }

        /// 기지를 seconds 초만큼 돌린다: 채굴장이 캐고, 켜 둔 가공 건물이 원료가 있는 만큼 가공한다. 창고 한도까지만 쌓인다
        public static void Advance(double[] stock, int[] mineLv, int[] refLv, bool[] refOn, int[] research, int[] cleared, int storageLv, double seconds)
        {
            int cap = StorageCap(storageLv);
            var mine = new double[Content.RawCount];
            for (int i = 0; i < mine.Length; i++) mine[i] = Rate(i, mineLv[i], research, cleared) / 60.0;
            double left = seconds;
            while (left > 0)
            {
                double step = Math.Min(left, 30);
                left -= step;
                for (int i = 0; i < mine.Length; i++)
                    if (stock[i] < cap) stock[i] = Math.Min(cap, stock[i] + mine[i] * step);   // 한도를 넘겨 들고 온 자원은 그대로 둔다
                for (int k = 0; k < Refineries.Length; k++)
                {
                    if (!refOn[k] || refLv[k] <= 0) continue;
                    var d = Refineries[k];
                    double make = Math.Min(RefineryRate(d, refLv[k]) / 60.0 * step, cap - stock[d.output]);
                    foreach (var s in d.inputs) make = Math.Min(make, stock[s.item] / s.amount);
                    if (make <= 0) continue;
                    foreach (var s in d.inputs) stock[s.item] -= make * s.amount;
                    stock[d.output] += make;
                }
            }
        }

        static int R(float v, int step = 10) => Math.Max(step, (int)Math.Round(v / step) * step);
    }
}
