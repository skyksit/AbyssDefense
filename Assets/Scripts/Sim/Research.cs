using System;

namespace AbyssDefense.Sim
{
    /// 연구로 얻는 보정값. 지역 한 판 내내 고정이다.
    public sealed class SimMods
    {
        public float damageMul = 1, rangeMul = 1, healthMul = 1, coreHealthMul = 1, ammoMul = 1, bountyMul = 1, buildSpeedMul = 1;
        public float unitMul = 1;                               // 유닛의 피해와 체력
        public int turretGrade = 1, buildGrade = 1, unitGrade = 1;   // 연구로 오른 등급: 포탑(포탑 화력), 그 밖의 건물(강화 장갑), 유닛(기체 강화)
        public int unitCap = Research.BaseUnitCap;              // 유닛 한도 (유닛마다 단계만큼 차지한다)
        public bool[] tech = new bool[Research.Defs.Length];   // 마친 연구 (건물·유닛 해금 확인용)
    }

    public sealed class ResearchDef
    {
        public int id;
        public string key, name, desc;
        public int maxLevel;
        public float perLevel;            // 단계마다 더해지는 양 (비율 또는 개수)
        public int reqRegion;             // 이 단계(전체 단계: 잿불 1~100, 서리 101~ …)의 지역을 직접 점령해야 연구할 수 있다 (0 = 처음부터)
        public bool expands;              // 서리 심연부터 군주·거점 지역을 점령할 때마다 최대 단계가 하나씩 늘어난다
        public int iron, sulfur, crystal; // 1단계 비용. crystal 은 crystalFrom 단계부터 든다
        public int crystalFrom = 3;
        public ItemStack[] extra = new ItemStack[0];   // 가공 자원 비용 (1단계 기준)
        public bool percent = true;
        public int parent = -1;           // 연구 트리에서 앞 연구 (-1 = 기지에서 바로 뻗는 가지). 앞 연구를 1단계 이상 마쳐야 드러난다
        public string icon;               // 트리 노드에 쓰는 그림 이름
    }

    /// 기지에서 하는 연구. 모든 심연에 함께 적용된다. 연구는 그것이 묶인 지역을 점령하면 열린다:
    /// 잿불 2·4·6·8 과, 지역 10 부터는 군주(10의 배수)·거점(15, 25, …) 지역.
    public static class Research
    {
        public const int Damage = 0, Health = 1, Loadout = 2, Mortar = 3, Core = 4, Range = 5, Mining = 6, Ammo = 7, Bounty = 8, Lance = 9, Build = 10,
            RobotFactory = 11, Airfield = 12, Tier2 = 13, Tier3 = 14, Command = 15, UnitPower = 16,
            Support = 17, Legs = 18, Shipyard = 19, Tier4 = 20, Tier5 = 21, Tank = 22, Hover = 23, Assembler = 24, Transcend = 25, SpecialTurret = 26, AdvTurret = 27, SupportBlock = 28;
        public const int BaseUnitCap = 4;

        public static readonly ResearchDef[] Defs =
        {
            new ResearchDef { key = "damage", name = "포탑 화력", desc = "모든 포탑의 피해가 늘어난다", expands = true, maxLevel = 11, perLevel = 0.15f, iron = 100, sulfur = 60, crystal = 40 },
            new ResearchDef { key = "health", name = "강화 장갑", desc = "방벽·포탑·공장의 체력이 늘어난다", expands = true, maxLevel = 11, perLevel = 0.2f, reqRegion = 2, iron = 110, sulfur = 20, crystal = 30 },
            new ResearchDef { key = "loadout", name = "출격 물자", desc = "출격할 때 가져갈 수 있는 자원 한도가 늘어난다. 단계가 오를수록 더 크게 늘어난다", maxLevel = 11, perLevel = 150, percent = false, reqRegion = 2, iron = 120, sulfur = 40, crystal = 30 },
            new ResearchDef { key = "mortar", name = "박격포 기술", desc = "박격포를 지을 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 2, iron = 150, sulfur = 100 },
            new ResearchDef { key = "core", name = "핵 보호막", desc = "심연 핵의 체력이 늘어난다", expands = true, maxLevel = 11, perLevel = 0.3f, reqRegion = 4, iron = 120, sulfur = 50, crystal = 40 },
            new ResearchDef { key = "range", name = "조준 장치", desc = "모든 포탑의 사거리가 늘어난다", maxLevel = 11, perLevel = 0.03f, reqRegion = 4, iron = 90, sulfur = 70, crystal = 40, crystalFrom = 2 },
            new ResearchDef { key = "mining", name = "채굴 기술", desc = "기지 채굴장의 생산량이 늘어난다", maxLevel = 11, perLevel = 0.1f, reqRegion = 6, iron = 160, sulfur = 60, crystal = 30 },
            new ResearchDef { key = "ammo", name = "탄약 절약", desc = "자원 하나로 더 많은 탄을 만든다", maxLevel = 11, perLevel = 0.10f, reqRegion = 6, iron = 70, sulfur = 80, crystal = 30 },
            new ResearchDef { key = "bounty", name = "전리품 회수", desc = "적을 처치하고 얻는 자원이 늘어난다", maxLevel = 11, perLevel = 0.2f, reqRegion = 8, iron = 130, sulfur = 70, crystal = 30 },
            new ResearchDef { key = "lance", name = "결정포 기술", desc = "결정포를 지을 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 10, iron = 300, sulfur = 200, crystal = 40, crystalFrom = 0 },
            new ResearchDef { key = "build", name = "건설 드론", desc = "지역에서 건물을 더 빨리 짓는다", maxLevel = 11, perLevel = 0.10f, reqRegion = 4, iron = 100, sulfur = 40, crystal = 30 },
            new ResearchDef { key = "robotfactory", name = "로봇 공장 기술", desc = "로봇 공장을 짓고 척후 로봇을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 6, iron = 200, sulfur = 100,
                extra = new[] { new ItemStack(Content.Steel, 40) } },
            new ResearchDef { key = "airfield", name = "비행장 기술", desc = "비행장을 짓고 정찰기를 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 8, iron = 250, sulfur = 150,
                extra = new[] { new ItemStack(Content.Steel, 60), new ItemStack(Content.Circuit, 40) } },
            new ResearchDef { key = "tier2", name = "기체 개량 I", desc = "2단계 유닛(돌격 로봇·폭격기)을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 10, iron = 300, sulfur = 200,
                extra = new[] { new ItemStack(Content.Steel, 100), new ItemStack(Content.Circuit, 80) } },
            new ResearchDef { key = "tier3", name = "기체 개량 II", desc = "3단계 유닛(중장 로봇·강습기)을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 25, iron = 500, sulfur = 300, crystal = 100, crystalFrom = 0,
                extra = new[] { new ItemStack(Content.Circuit, 150), new ItemStack(Content.Alloy, 60) } },
            new ResearchDef { key = "command", name = "지휘 체계", desc = "지역에서 거느릴 수 있는 유닛 한도가 늘어난다", maxLevel = 11, perLevel = 1, percent = false, reqRegion = 6, iron = 150, sulfur = 80,
                extra = new[] { new ItemStack(Content.Circuit, 20) } },
            new ResearchDef { key = "unitpower", name = "기체 강화", desc = "유닛의 피해와 체력이 늘어난다", expands = true, maxLevel = 11, perLevel = 0.12f, reqRegion = 15, iron = 150, sulfur = 100,
                extra = new[] { new ItemStack(Content.Steel, 40), new ItemStack(Content.Circuit, 30) } },
            new ResearchDef { key = "support", name = "지원 공방 기술", desc = "지원 공방을 짓고 정비 로봇·수리 드론을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 10, iron = 250, sulfur = 150,
                extra = new[] { new ItemStack(Content.Steel, 60), new ItemStack(Content.Circuit, 40) } },
            new ResearchDef { key = "legs", name = "다리 공방 기술", desc = "다리 공방을 짓고 자폭 벌레와 다리 로봇들을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 20, iron = 300, sulfur = 250,
                extra = new[] { new ItemStack(Content.Steel, 80), new ItemStack(Content.Powder, 60) } },
            new ResearchDef { key = "shipyard", name = "조선소 기술", desc = "물가에 조선소를 짓고 초계정을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 20, iron = 350, sulfur = 200,
                extra = new[] { new ItemStack(Content.Steel, 120), new ItemStack(Content.Circuit, 80) } },
            new ResearchDef { key = "tier4", name = "기체 개량 III", desc = "4단계 유닛(공성 로봇·전함)을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 45, iron = 800, sulfur = 500, crystal = 200, crystalFrom = 0,
                extra = new[] { new ItemStack(Content.Circuit, 250), new ItemStack(Content.Alloy, 150) } },
            new ResearchDef { key = "tier5", name = "기체 개량 IV", desc = "5단계 유닛(거신 로봇·모함)을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 70, iron = 1500, sulfur = 900, crystal = 400, crystalFrom = 0,
                extra = new[] { new ItemStack(Content.Circuit, 400), new ItemStack(Content.Alloy, 350) } },
            new ResearchDef { key = "tank", name = "전차 공장 기술", desc = "전차 공장을 짓고 장갑차를 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 15, iron = 300, sulfur = 150,
                extra = new[] { new ItemStack(Content.Steel, 100) } },
            new ResearchDef { key = "hover", name = "부양 공방 기술", desc = "부양 공방을 짓고 부양 척후·부양 정비를 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 20, iron = 350, sulfur = 200,
                extra = new[] { new ItemStack(Content.Steel, 100), new ItemStack(Content.Circuit, 80) } },
            new ResearchDef { key = "assembler", name = "심연 조립 기술", desc = "심연 조립소를 짓고 6단계 유닛(심연 거신·심연 모함·심연 거미)을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 75, iron = 2500, sulfur = 1500, crystal = 800, crystalFrom = 0,
                extra = new[] { new ItemStack(Content.Steel, 800), new ItemStack(Content.Circuit, 600), new ItemStack(Content.Alloy, 500) } },
            new ResearchDef { key = "transcend", name = "초월 개량", desc = "7단계 유닛(종말 전차·심연 요새·천벌기)을 만들 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 90, iron = 4000, sulfur = 2500, crystal = 1500, crystalFrom = 0,
                extra = new[] { new ItemStack(Content.Steel, 1200), new ItemStack(Content.Circuit, 900), new ItemStack(Content.Alloy, 900) } },
            new ResearchDef { key = "specialturret", name = "특수 포탑 기술", desc = "대공포와 점액탑을 지을 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 6, iron = 250, sulfur = 150,
                extra = new[] { new ItemStack(Content.Steel, 40) } },
            new ResearchDef { key = "advturret", name = "고급 포탑 기술", desc = "전격탑과 화염탑을 지을 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 15, iron = 400, sulfur = 250,
                extra = new[] { new ItemStack(Content.Circuit, 80), new ItemStack(Content.Powder, 60) } },
            new ResearchDef { key = "supportblock", name = "지원 건물 기술", desc = "수리탑·증폭탑·역장 발생기를 지을 수 있게 된다", maxLevel = 1, perLevel = 1, percent = false, reqRegion = 10, iron = 250, sulfur = 120,
                extra = new[] { new ItemStack(Content.Steel, 60), new ItemStack(Content.Circuit, 30) } },
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
            Link(Tank, RobotFactory, "icon_tankfactory");
            Link(Hover, RobotFactory, "icon_hovershop");
            Link(Assembler, Tier5, "icon_assembler");
            Link(Transcend, Assembler, "unit_doomtank");
            Link(SpecialTurret, Mortar, "icon_flak");
            Link(AdvTurret, Lance, "icon_tesla");
            Link(SupportBlock, Health, "icon_mender");
        }

        static void Link(int id, int parent, string icon) { Defs[id].parent = parent; Defs[id].icon = icon; }

        /// 앞 연구를 마쳐 트리에 드러났는가
        public static bool ParentDone(int[] lv, ResearchDef d) => d.parent < 0 || Level(lv, d.parent) > 0;

        // ───────── 비용 ─────────
        // 단계형 연구: L단계(1부터)는 철광 1K × 10^(L−1) (1K, 10K, … 1T, 10T). 다른 자원은 그 연구의 비율(iron·sulfur·crystal·extra)을 따른다.
        //   상한 확장으로 늘어난 12단계부터는 한 단계마다 ×ExpansionStep (모두 10T 이상).
        // 해금 연구(1단계뿐): Mindustry 의 연구비 규칙을 따른다
        //   - 건물을 여는 연구: 그 건물 비용의 자원마다 round(60 + 수량^1.11 × 20, 10)
        //   - 유닛을 여는 연구: 그 연구로 열리는 가장 싼 유닛 비용 × 50

        public const double FirstLevelCost = 1000, LevelStep = 10, ExpansionStep = 1.03;
        public const int StandardLevels = 11;
        public const double UnitResearchMul = 50;

        static double[][] unlockCost;

        /// level(0부터) 에서 다음 단계로 올리는 비용 (자원 번호 순)
        public static double[] Cost(ResearchDef d, int level)
        {
            if (d.maxLevel <= 1) return (double[])UnlockCost(d).Clone();
            var c = new double[Content.Items.Length];
            double iron = level < StandardLevels
                ? FirstLevelCost * Math.Pow(LevelStep, level)
                : FirstLevelCost * Math.Pow(LevelStep, StandardLevels - 1) * Math.Pow(ExpansionStep, level - StandardLevels + 1);
            double scale = iron / d.iron;
            c[Content.Iron] = Sig3(d.iron * scale);
            c[Content.Sulfur] = Sig3(d.sulfur * scale);
            if (d.crystal > 0 && level >= d.crystalFrom) c[Content.Crystal] = Sig3(d.crystal * scale);
            foreach (var e in d.extra) c[e.item] = Sig3(e.amount * scale);
            return c;
        }

        /// 해금 연구의 비용 (Mindustry 규칙). 여는 것이 없으면 지금 비율에 해금 지역의 경제 배수를 곱한다
        static double[] UnlockCost(ResearchDef d)
        {
            if (unlockCost == null) unlockCost = new double[Defs.Length][];
            if (unlockCost[d.id] != null) return unlockCost[d.id];
            var c = new double[Content.Items.Length];
            bool any = false;
            foreach (var b in Content.Blocks)
            {
                if (b.tech != d.id) continue;
                // 블록 연구식은 등급 배수를 곱하기 전의 비용에 쓰고, 그 뒤에 등급 배수를 곱한다 (큰 수에 1.11 제곱이 붙어 폭주하지 않게)
                foreach (var s in b.baseCost) c[s.item] += Math.Round((60 + Math.Pow(s.amount, 1.11) * 20) / 10) * 10 * Content.GradeCostMul(b.grade);
                any = true;
            }
            UnitDef cheapest = null; double cheapestSum = double.MaxValue;
            foreach (var u in Content.Units)
            {
                if (u.tech != d.id) continue;
                double sum = 0; foreach (var s in u.cost) sum += s.amount;
                if (sum < cheapestSum) { cheapestSum = sum; cheapest = u; }
            }
            if (cheapest != null) { foreach (var s in cheapest.cost) c[s.item] += s.amount * UnitResearchMul; any = true; }
            if (!any)
            {
                double m = Content.EconomyMul(d.reqRegion);
                c[Content.Iron] = d.iron * m; c[Content.Sulfur] = d.sulfur * m; c[Content.Crystal] = d.crystal * m;
                foreach (var e in d.extra) c[e.item] += e.amount * m;
            }
            for (int i = 0; i < c.Length; i++) c[i] = Sig3(c[i]);
            return unlockCost[d.id] = c;
        }

        /// 유효숫자 세 자리로 반올림 (1,234,567 → 1,230,000). 1,000 미만은 10 단위
        public static double Sig3(double v)
        {
            if (v <= 0) return 0;
            if (v < 1000) return Math.Max(10, Math.Round(v / 10) * 10);
            double p = Math.Pow(10, Math.Floor(Math.Log10(v)) - 2);
            return Math.Round(v / p) * p;
        }

        /// 첫 심연 다음의 심연마다 있는 군주·거점 지역의 수 (군주 10 + 거점 10). 그 지역을 하나 점령할 때마다 연구 상한이 하나 늘어난다
        public const int ExpansionsPerAbyss = 20;
        public static int MaxExpansions => (Content.Abysses.Length - 1) * ExpansionsPerAbyss;

        /// 연구의 최대 단계. expansions = 서리 심연부터 점령한 군주·거점 지역의 수
        public static int MaxLevel(ResearchDef d, int expansions) => d.maxLevel + (d.expands ? Math.Max(0, Math.Min(expansions, MaxExpansions)) : 0);

        /// 연구 단계. 단계는 그때의 상한 안에서만 살 수 있으므로, 여기서는 닿을 수 있는 가장 높은 상한으로만 자른다
        public static int Level(int[] lv, int id) => lv != null && id < lv.Length ? Math.Min(lv[id], MaxLevel(Defs[id], MaxExpansions)) : 0;

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
            // 연구 단계가 곧 등급: 포탑 화력 → 포탑, 강화 장갑 → 그 밖의 건물, 기체 강화 → 유닛
            m.turretGrade = Math.Max(1, Level(lv, Damage));
            m.buildGrade = Math.Max(1, Level(lv, Health));
            m.unitGrade = Math.Max(1, Level(lv, UnitPower));
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
        public double baseRate;           // 1단계의 분당 산출량
        public int reqRegion;             // 이 단계의 지역을 직접 점령해야 지을 수 있다
        public ItemStack[] buildCost, upgradeCost;
    }

    /// 기지: 심연과 상관없이 자원을 캐고 가공하고 쌓는 곳. 건물 단계와 점령한 지역 수로 분당 생산량이 정해진다.
    public static class BaseRules
    {
        public const int MaxMineLevel = 30, MaxStorageLevel = 30, MaxRefineryLevel = 30;
        public const int CrystalMineRegion = 10;     // 이 지역을 점령해야 결정 채굴장을 지을 수 있다
        public const int UpgradeRegionStep = 10;     // 기지 건물의 강화는 Lv 3 부터 단계 10 마다 한 단계씩 열린다
        public const double OutputGrowth = 1.8, CostGrowth = 2.0;   // 단계마다 산출 ×1.8, 강화 비용 ×2 (높은 단계일수록 본전을 뽑는 데 오래 걸린다)

        /// 기지 건물을 level → level+1 로 강화하려면 그만큼 깊이 점령해 두어야 하는 단계 (Lv 2 는 6, Lv 3 은 20, Lv 4 는 30, … Lv 10 은 90, Lv 30 은 290).
        /// 특정 지역이 아니라 "가장 깊이 점령한 단계"와 견준다: 거점 하나를 건너뛰어도 그 위 단계가 막히지 않게. 건설(0 → 1)은 건물마다 따로 정한 지역을 따른다
        public static int UpgradeRegion(int level) => level <= 0 ? 0 : level == 1 ? 6 : level * UpgradeRegionStep;
        public const int OfflineCapSeconds = 8 * 3600;
        public static readonly string[] MineNames = { "철광 채굴장", "황석 채굴장", "결정 채굴장" };
        static readonly double[] baseRate = { 30, 12, 6 };

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
            new RefineryDef { name = "화약 합성소", output = Content.Powder, baseRate = 4, reqRegion = 8,
                inputs = new[] { new ItemStack(Content.Sulfur, 2) },
                buildCost = new[] { new ItemStack(Content.Iron, 200), new ItemStack(Content.Sulfur, 250), new ItemStack(Content.Steel, 40) },
                upgradeCost = new[] { new ItemStack(Content.Iron, 120), new ItemStack(Content.Sulfur, 150), new ItemStack(Content.Steel, 30) } },
            new RefineryDef { name = "합금 융합로", output = Content.Alloy, baseRate = 2, reqRegion = 15,
                inputs = new[] { new ItemStack(Content.Steel, 4), new ItemStack(Content.Crystal, 2) },   // Mindustry 의 서지 합금처럼 원료가 무겁다
                buildCost = new[] { new ItemStack(Content.Iron, 400), new ItemStack(Content.Crystal, 200), new ItemStack(Content.Steel, 100), new ItemStack(Content.Circuit, 60) },
                upgradeCost = new[] { new ItemStack(Content.Iron, 200), new ItemStack(Content.Crystal, 60), new ItemStack(Content.Steel, 80), new ItemStack(Content.Circuit, 40) } },
        };

        static BaseRules() { for (int i = 0; i < Refineries.Length; i++) Refineries[i].id = i; }

        /// 채굴장 한 곳의 분당 생산량: 단계마다 ×1.8
        public static double MineRate(int item, int level) => level <= 0 || item >= Content.RawCount ? 0 : baseRate[item] * Math.Pow(OutputGrowth, level - 1);

        /// 채굴장을 level → level+1 로 올리는 비용: 단계마다 ×2
        public static double[] MineCost(int item, int level)
        {
            var c = new double[Content.Items.Length];
            if (level == 0) { c[0] = item == Content.Crystal ? 600 : 100; c[1] = item == Content.Crystal ? 400 : 50; return c; }
            double m = Math.Pow(CostGrowth, level - 1);
            if (item == Content.Iron) { c[0] = R(120 * m); c[1] = R(40 * m); }
            else if (item == Content.Sulfur) { c[0] = R(100 * m); c[1] = R(60 * m); }
            else { c[0] = R(200 * m); c[1] = R(150 * m); c[2] = R(30 * m); }
            return c;
        }

        /// 가공 건물의 분당 산출량: 단계마다 ×1.8
        public static double RefineryRate(RefineryDef d, int level) => level <= 0 ? 0 : d.baseRate * Math.Pow(OutputGrowth, level - 1);

        /// 가공 건물을 level → level+1 로 올리는 비용 (0 → 1 은 건설): 단계마다 ×2
        public static double[] RefineryCost(RefineryDef d, int level)
        {
            var c = new double[Content.Items.Length];
            if (level == 0) { foreach (var s in d.buildCost) c[s.item] = s.amount; return c; }
            double m = Math.Pow(CostGrowth, level - 1);
            foreach (var s in d.upgradeCost) c[s.item] = R(s.amount * m);
            return c;
        }

        /// 창고 한도: 단계마다 ×2.2 (Lv 30 에서 약 1.7 × 10^13)
        public static double StorageCap(int level) => R(2000 * Math.Pow(2.2, Math.Max(0, level - 1)));

        /// 창고 강화 비용: 지금 한도의 30% (철광)와 15% (황석)
        public static double[] StorageCost(int level)
        {
            var c = new double[Content.Items.Length];
            c[0] = R(StorageCap(level) * 0.3); c[1] = R(StorageCap(level) * 0.15);
            return c;
        }

        /// 출격할 때 자원 한 종류를 가져갈 수 있는 최대량
        public const long BaseLoadout = 400;
        /// 출격 물자 한도: 400 × 4^L (L = 출격 물자 연구 단계). 400 → 1.6K → 6.4K → … → 11단계 약 16.8억
        public const double LoadoutGrowth = 4;
        public static long LoadoutCapAt(int level) => (long)(BaseLoadout * Math.Pow(LoadoutGrowth, Math.Max(0, level)));
        public static long LoadoutCap(int[] research) => LoadoutCapAt(Research.Level(research, Research.Loadout));

        /// 지역 하나를 처음 점령했을 때 영구히 늘어나는 생산량: (자원, 분당 양)
        public static void RegionBonus(int abyss, int region, out int item, out double perMin)
        {
            int k = region % 3;
            if (k == 1) { item = Content.Iron; perMin = 3; }
            else if (k == 2) { item = Content.Sulfur; perMin = 1.5f; }
            else if (Content.Stage(abyss, region) >= CrystalMineRegion) { item = Content.Crystal; perMin = 0.75f; }
            else { item = Content.Iron; perMin = 3; }
            if (RegionRules.IsAssault(Content.Stage(abyss, region))) perMin *= 2;   // 적 거점을 부순 지역은 보상이 두 배
            perMin *= Content.EconomyMul(Content.Stage(abyss, region));          // 깊은 지역일수록 지수로 커진다
        }

        /// 점령한 지역들에서 들어오는 분당 생산량 (done[심연][지역] = 그 지역을 점령했는가). 건너뛴 지역은 세지 않는다
        public static double ConquestRate(int item, bool[][] done)
        {
            if (item >= Content.RawCount || done == null) return 0;
            double sum = 0;
            for (int a = 0; a < done.Length; a++)
                for (int r = 1; done[a] != null && r < done[a].Length; r++)
                {
                    if (!done[a][r]) continue;
                    RegionBonus(a, r, out int it, out double v);
                    if (it == item) sum += v;
                }
            return sum;
        }

        /// 심연마다 지역 1~n 을 모두 점령한 상태 (검사·봇용)
        public static bool[][] DoneUpTo(params int[] cleared)
        {
            var done = new bool[cleared.Length][];
            for (int a = 0; a < cleared.Length; a++)
            {
                done[a] = new bool[Content.RegionCount + 1];
                for (int r = 1; r <= cleared[a] && r <= Content.RegionCount; r++) done[a][r] = true;
            }
            return done;
        }

        /// 가장 깊이 점령한 전체 단계 (없으면 0)
        public static int BestStage(bool[][] done)
        {
            int best = 0;
            for (int a = 0; done != null && a < done.Length; a++)
                for (int r = 1; done[a] != null && r < done[a].Length; r++)
                    if (done[a][r]) best = Math.Max(best, Content.Stage(a, r));
            return best;
        }

        /// 가공 수율: 원료 한 묶음에서 나오는 가공 자원의 양. 깊이 점령할수록 커진다 (E(가장 깊은 단계)^RefineryYieldExp). 원료는 그대로 든다
        public const double RefineryYieldExp = 0.8;
        public static double RefineryYield(bool[][] done) => Math.Pow(Content.EconomyMul(Math.Max(1, BestStage(done))), RefineryYieldExp);

        /// 기지 채굴의 분당 총생산량 (가공 건물이 쓰는 양은 빼지 않은 값)
        public static double Rate(int item, int mineLevel, int[] research, bool[][] done) =>
            MineRate(item, mineLevel) * (1 + Research.Level(research, Research.Mining) * Research.Defs[Research.Mining].perLevel)
            + ConquestRate(item, done);

        /// 가공 건물이 모두 돌 때 자원별 분당 증감 (채굴 + 가공 산출 − 가공 원료)
        public static double[] NetRates(int[] mineLv, int[] refLv, bool[] refOn, int[] research, bool[][] done)
        {
            var net = new double[Content.Items.Length];
            double yield = RefineryYield(done);
            for (int i = 0; i < Content.RawCount; i++) net[i] = Rate(i, mineLv[i], research, done);
            for (int k = 0; k < Refineries.Length; k++)
            {
                if (!refOn[k]) continue;
                var d = Refineries[k];
                double rate = RefineryRate(d, refLv[k]);
                net[d.output] += rate * yield;
                foreach (var s in d.inputs) net[s.item] -= rate * s.amount;
            }
            return net;
        }

        /// 기지를 seconds 초만큼 돌린다: 채굴장이 캐고, 켜 둔 가공 건물이 원료가 있는 만큼 가공한다. 창고 한도까지만 쌓인다
        public static void Advance(double[] stock, int[] mineLv, int[] refLv, bool[] refOn, int[] research, bool[][] done, int storageLv, double seconds)
        {
            double cap = StorageCap(storageLv), yield = RefineryYield(done);
            var mine = new double[Content.RawCount];
            for (int i = 0; i < mine.Length; i++) mine[i] = Rate(i, mineLv[i], research, done) / 60.0;
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
                    double make = Math.Min(RefineryRate(d, refLv[k]) / 60.0 * step, (cap - stock[d.output]) / yield);
                    foreach (var s in d.inputs) make = Math.Min(make, stock[s.item] / s.amount);
                    if (make <= 0) continue;
                    foreach (var s in d.inputs) stock[s.item] -= make * s.amount;
                    stock[d.output] += make * yield;
                }
            }
        }

        static double R(double v) => Research.Sig3(v);
    }
}
