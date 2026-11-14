namespace AbyssDefense.Sim
{
    /// 길이 단위는 타일(1 = 한 칸), 시간 단위는 틱(60틱 = 1초).
    public struct ItemStack
    {
        public int item, amount;
        public ItemStack(int item, int amount) { this.item = item; this.amount = amount; }
    }

    public sealed class ItemDef
    {
        public int id;
        public string key, name;
        public uint color;   // 0xRRGGBB
    }

    public enum BlockKind { Core, Wall, Turret, Factory, EnemyCore, EnemyTurret }

    public sealed class BulletDef
    {
        public float speed;          // 타일/틱
        public float damage;
        public float splash;         // 범위 피해 반지름 (0 = 없음)
        public int lifeTicks;
        public bool artillery;       // 목표 지점까지 날아가 터진다 (지상만)
        public int pierce;           // 관통 수 (0 = 첫 적에서 사라짐)
        public float heal;           // 터질 때 범위 안의 아군 건물을 이만큼 고친다 (곡사탄)
        public float knockback;      // 맞은 적(범위 피해가 있으면 범위 안의 적 모두)을 탄이 날아가던 쪽으로 이만큼 밀어낸다. 보스는 절반
        public bool pierceArmor;     // 철갑탄: 직격한 적과 적 건물의 방어력을 무시한다
        public float burn;           // 맞은 적을 불태운다: 초당 이만큼의 피해 (방어력을 무시한다)
        public int burnTicks;        // 불타는 시간
        public float homing;         // 겨눈 적을 쫓아 틱마다 이만큼(라디안)까지 방향을 튼다 (0 = 곧게 난다)
        public BulletDef frag;       // 곡사탄이 떨어져 터질 때 사방으로 튀는 파편탄
        public int fragCount;
        public float slow;           // 맞은 적의 속도에 곱한다 (0 = 없음). 범위 피해가 있으면 범위 안의 적 모두
        public int slowTicks;        // 느려지는 시간
        public uint color;
        public float size = 0.18f;
    }

    public sealed class BlockDef
    {
        public int id;
        public string key, name, desc;
        public BlockKind kind;
        public int size = 1;
        public float health = 100;
        public float armor;              // 받는 피해에서 이만큼 뺀다 (최소 20% 는 들어온다)
        public ItemStack[] cost = new ItemStack[0];
        public int buildTicks;           // 건설에 걸리는 틱 (비용에서 계산)
        public int tech = -1;            // 이 연구를 마쳐야 지을 수 있다 (Research 의 id, -1 = 처음부터)
        public uint color;

        // 포탑
        public float range;
        public int reloadTicks;
        public int ammoItem = -1;        // 탄약이 떨어지면 핵 재고에서 하나씩 가져온다
        public int ammoPerItem;
        public bool targetAir = true, targetGround = true;
        public BulletDef bullet;

        // 유닛 공장
        public UnitDef[] units;          // 만들 수 있는 유닛 (단계 순)
        public bool needsWater;          // 물가에만 지을 수 있다 (조선소)

        // 적 거점의 건물
        public bool enemy;               // 적의 건물 (플레이어는 지을 수 없고, 유닛으로만 부술 수 있다)
        public ItemStack[] bounty = new ItemStack[0];   // 부수면 핵으로 들어오는 자원
    }

    /// 공장에서 만드는 아군 유닛
    public sealed class UnitDef
    {
        public int id;
        public string key, name, desc;
        public int tier = 1;
        public bool flying;              // 지형과 건물을 무시하고 날아간다
        public bool legs;                // 다리로 지형 벽을 타 넘는다 (지상의 적에게는 맞는다)
        public bool naval;               // 물 위만 다닌다
        public bool hover;               // 떠서 다닌다: 땅과 호수를 모두 지난다 (지형 벽은 못 넘는다)
        public string shape;             // 그림 종류: mech, air, support, drone, bug, legs, boat, carrier, commander, hover, tank
        public float buildingMul = 1;    // 적 건물에 주는 피해 배수
        public bool suicide;             // 붙어서 터진다 (bullet 의 피해·범위를 쓴다)
        public float drain;              // 탄이 맞혀서 준 피해의 이 비율만큼 자기 체력을 되찾는다
        public float heal, healRange;    // 둘레의 아군 유닛·건물을 초당 heal 만큼 고친다
        public float shieldRange, shieldMul = 1;   // 둘레 아군이 받는 피해에 곱한다
        public int capacity;             // (수송기) 실을 수 있는 유닛의 한도 합
        public float fieldDamage;        // (방전) 탄 대신 사거리 안의 적 여럿을 한꺼번에 친다. 한 번에 한 적에게 주는 피해
        public int fieldTargets;         // (방전) 한 번에 치는 적의 수 (가까운 순)
        public float boostRange;         // (증폭) 이 거리 안의 다른 아군 유닛은 3틱에 한 번 재장전이 한 틱 더 줄어든다 (연사 약 1.33배, 겹쳐도 한 번만)
        public float barrier;            // 자기 보호막: 받는 피해를 체력보다 먼저 이만큼 대신 받는다
        public float barrierRegen;       // 보호막이 초당 차오르는 양
        public int barrierDelay;         // 마지막으로 맞은 뒤 이 틱이 지나야 차오르기 시작한다
        public float crush;              // (전차) 몸에 닿은 지상의 적을 짓밟는다: 초당 이만큼의 피해 (방어력을 무시한다)
        public UnitDef spawns;           // (기함) 이 유닛을 스스로 띄운다. 띄운 유닛은 한도를 차지하지 않고 곁을 지킨다
        public int spawnMax, spawnTicks; // 한 번에 거느리는 수, 하나를 띄우는 데 걸리는 틱
        public bool rebuild;             // (재건 드론) 적에게 부서진 아군 건물의 자리로 가서 같은 건물을 다시 놓는다
        public int mine;                 // (채굴 드론) 벽에서 한 번에 캐 오는 양. 세 번에 한 번은 황석을 절반만큼 캔다
        public int mineTicks;            // 한 번 캐는 데 걸리는 틱
        public bool commander;           // 핵에서 나오는 지휘기
        /// 걸어서만 다니는 유닛 (수송기가 실어 나를 수 있다)
        public bool GroundBound => !flying && !legs && !naval;
        public float health, armor, speed, radius;
        public float range;
        public int reloadTicks;
        public bool targetAir = true, targetGround = true;
        public BulletDef bullet;
        public ItemStack[] cost = new ItemStack[0];
        public int buildTicks;           // 생산에 걸리는 틱
        public int supply = 1;           // 유닛 한도에서 차지하는 칸
        public int tech = -1;            // 이 연구를 마쳐야 생산할 수 있다
        public uint color;
    }

    public sealed class EnemyDef
    {
        public int id;
        public string key, name;
        public float health, speed, radius;
        public float armor;          // 깊은 지역일수록 커진다
        public float damage;
        public int attackTicks;
        public float range;          // 공격 사거리 (건물 가장자리까지)
        public bool flying;          // 길을 무시하고 핵으로 직진
        public bool siege;           // 사거리 안의 건물을 멈춰서 공격
        public bool boss;
        public ItemStack[] bounty = new ItemStack[0];   // 처치하면 핵으로 바로 들어오는 자원
        public uint color;
        /// 비행 유닛을 때릴 수 있는가
        public bool HitsAir => flying || siege || boss;
    }

    public sealed class AbyssDef
    {
        public int id;
        public string key, name, desc;
        public float enemyHealthMul = 1, enemySpeedMul = 1, enemyCountMul = 1, bountyMul = 1;
        public uint floorColor, wallColor, accent;
    }

    /// 게임 내용물 등록소. 모든 정의는 여기 한 곳에 있다.
    public static class Content
    {
        // 원광 (기지 채굴장) · 가공 자원 (기지 가공 건물)
        public const int Iron = 0, Sulfur = 1, Crystal = 2, Steel = 3, Circuit = 4, Powder = 5, Alloy = 6;
        public const int RawCount = 3;

        public static readonly ItemDef[] Items =
        {
            new ItemDef { id = Iron, key = "iron", name = "철광", color = 0xC9A27A },
            new ItemDef { id = Sulfur, key = "sulfur", name = "황석", color = 0xE8D44D },
            new ItemDef { id = Crystal, key = "crystal", name = "심연 결정", color = 0x9C6BFF },
            new ItemDef { id = Steel, key = "steel", name = "강철", color = 0x9FB4C7 },
            new ItemDef { id = Circuit, key = "circuit", name = "회로", color = 0x5FD38D },
            new ItemDef { id = Powder, key = "powder", name = "화약", color = 0xE8703A },
            new ItemDef { id = Alloy, key = "alloy", name = "심연 합금", color = 0xF06292 },
        };

        // ───────── 유닛 ─────────

        public static readonly UnitDef Scout = new UnitDef
        {
            key = "scout", name = "척후 로봇", desc = "값싼 1단계 로봇. 지상과 공중을 쏜다.",
            tier = 1, health = 130, armor = 2, speed = 0.030f, radius = 0.28f, range = 3.2f, reloadTicks = 24, color = 0x4FD1C5,
            bullet = new BulletDef { speed = 0.25f, damage = 8, lifeTicks = 18, color = 0xB2F5EA, size = 0.14f },
            cost = new[] { new ItemStack(Iron, 25), new ItemStack(Circuit, 8) }, buildTicks = 8 * 60, supply = 1,
        };
        public static readonly UnitDef Assault = new UnitDef
        {
            key = "assault", name = "돌격 로봇", desc = "가까이 붙어 꿰뚫는 불꽃을 뿜는다. 튼튼하다.",
            tier = 2, health = 420, armor = 5, speed = 0.024f, radius = 0.36f, range = 2.0f, reloadTicks = 10, color = 0x38B2AC, tech = Research.Tier2,
            bullet = new BulletDef { speed = 0.20f, damage = 9, lifeTicks = 13, pierce = 2, color = 0xFFB347, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 30), new ItemStack(Circuit, 20) }, buildTicks = 16 * 60, supply = 2,
        };
        public static readonly UnitDef Skimmer = new UnitDef
        {
            key = "skimmer", name = "부양 로봇", desc = "땅에서 조금 떠서 달리는 빠른 2단계 로봇. 호수를 가로질러 지름길로 간다. 장갑은 얇다.",
            tier = 2, hover = true, shape = "hover", health = 300, armor = 3, speed = 0.040f, radius = 0.32f, range = 3.8f, reloadTicks = 16, color = 0x63E6BE, tech = Research.Tier2,
            bullet = new BulletDef { speed = 0.27f, damage = 11, lifeTicks = 18, color = 0xC3FAE8, size = 0.14f },
            cost = new[] { new ItemStack(Steel, 25), new ItemStack(Circuit, 25) }, buildTicks = 14 * 60, supply = 2,
        };
        public static readonly UnitDef Heavy = new UnitDef
        {
            key = "heavy", name = "중장 로봇", desc = "두꺼운 장갑과 범위 피해 주포를 가진 3단계 로봇.",
            tier = 3, health = 1300, armor = 10, speed = 0.016f, radius = 0.48f, range = 5.5f, reloadTicks = 45, color = 0x2C7A7B, tech = Research.Tier3,
            bullet = new BulletDef { speed = 0.20f, damage = 60, splash = 1.2f, lifeTicks = 36, color = 0xE6FFFA, size = 0.3f },
            cost = new[] { new ItemStack(Steel, 60), new ItemStack(Circuit, 40), new ItemStack(Alloy, 20) }, buildTicks = 35 * 60, supply = 3,
        };
        public static readonly UnitDef Recon = new UnitDef
        {
            key = "recon", name = "정찰기", desc = "빠른 1단계 비행 유닛. 약하지만 어디든 간다.",
            tier = 1, flying = true, health = 70, armor = 0, speed = 0.060f, radius = 0.26f, range = 2.6f, reloadTicks = 18, color = 0x74C0FC,
            bullet = new BulletDef { speed = 0.28f, damage = 6, lifeTicks = 14, color = 0xD0EBFF, size = 0.13f },
            cost = new[] { new ItemStack(Iron, 20), new ItemStack(Circuit, 10) }, buildTicks = 8 * 60, supply = 1,
        };
        public static readonly UnitDef Bomber = new UnitDef
        {
            key = "bomber", name = "폭격기", desc = "적 머리 위에서 폭탄을 떨어뜨린다. 공중은 못 쏜다.",
            tier = 2, flying = true, health = 240, armor = 2, speed = 0.045f, radius = 0.34f, range = 0.8f, reloadTicks = 60, color = 0x4DABF7, tech = Research.Tier2,
            targetAir = false,
            bullet = new BulletDef { speed = 0.05f, damage = 45, splash = 1.4f, lifeTicks = 30, artillery = true, color = 0xFFA94D, size = 0.24f },
            cost = new[] { new ItemStack(Steel, 20), new ItemStack(Circuit, 20), new ItemStack(Powder, 25) }, buildTicks = 18 * 60, supply = 2,
        };
        public static readonly UnitDef Striker = new UnitDef
        {
            key = "striker", name = "강습기", desc = "장갑을 두른 3단계 비행 유닛. 연사가 빠르다.",
            tier = 3, flying = true, health = 800, armor = 5, speed = 0.035f, radius = 0.44f, range = 4.5f, reloadTicks = 14, color = 0x1C7ED6, tech = Research.Tier3,
            bullet = new BulletDef { speed = 0.30f, damage = 16, lifeTicks = 20, color = 0xA5D8FF, size = 0.18f },
            cost = new[] { new ItemStack(Steel, 40), new ItemStack(Circuit, 40), new ItemStack(Alloy, 25) }, buildTicks = 35 * 60, supply = 3,
        };
        public static readonly UnitDef Seeker = new UnitDef
        {
            key = "seeker", name = "유도탄기", desc = "멀리서 유도탄을 쏘는 3단계 비행 유닛. 유도탄은 겨눈 적을 쫓아가 작은 범위로 터진다. 장갑은 얇다.",
            tier = 3, flying = true, health = 520, armor = 3, speed = 0.038f, radius = 0.40f, range = 6.5f, reloadTicks = 55, color = 0x339AF0, tech = Research.Tier3,
            bullet = new BulletDef { speed = 0.20f, damage = 70, splash = 1.0f, lifeTicks = 55, homing = 0.12f, color = 0xFFD8A8, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 40), new ItemStack(Circuit, 45), new ItemStack(Powder, 30), new ItemStack(Alloy, 15) }, buildTicks = 35 * 60, supply = 3,
        };
        public static readonly UnitDef Mender = new UnitDef
        {
            key = "mender", name = "수리 폭격기", desc = "3단계 폭격기. 폭탄이 범위 안의 적을 때리면서 같은 범위의 아군 건물을 고친다. 공중은 못 쏜다.",
            tier = 3, flying = true, health = 900, armor = 6, speed = 0.036f, radius = 0.46f, range = 0.9f, reloadTicks = 70, color = 0x4DD4AC, tech = Research.Tier3,
            targetAir = false,
            bullet = new BulletDef { speed = 0.05f, damage = 85, splash = 1.7f, lifeTicks = 30, artillery = true, heal = 70, color = 0xB2F2BB, size = 0.26f },
            cost = new[] { new ItemStack(Steel, 50), new ItemStack(Circuit, 50), new ItemStack(Powder, 30), new ItemStack(Alloy, 20) }, buildTicks = 38 * 60, supply = 3,
        };

        // 4·5단계
        public static readonly UnitDef Siege = new UnitDef
        {
            key = "siege", name = "공성 로봇", desc = "적 포탑 사거리 밖에서 포격한다. 건물에 강하고 공중은 못 쏜다.",
            tier = 4, health = 2600, armor = 14, speed = 0.013f, radius = 0.56f, range = 7.5f, reloadTicks = 80, color = 0x285E61, tech = Research.Tier4,
            targetAir = false, buildingMul = 2.5f,
            bullet = new BulletDef { speed = 0.10f, damage = 150, splash = 1.6f, lifeTicks = 90, artillery = true, color = 0xFFE8A3, size = 0.34f },
            cost = new[] { new ItemStack(Steel, 120), new ItemStack(Circuit, 80), new ItemStack(Powder, 60), new ItemStack(Alloy, 50) }, buildTicks = 60 * 60, supply = 4,
        };
        public static readonly UnitDef Titan = new UnitDef
        {
            key = "titan", name = "거신 로봇", desc = "가장 두꺼운 장갑과 범위 화력을 가진 5단계 로봇.",
            tier = 5, health = 6500, armor = 20, speed = 0.011f, radius = 0.72f, range = 6f, reloadTicks = 30, color = 0x1D4044, tech = Research.Tier5,
            bullet = new BulletDef { speed = 0.22f, damage = 110, splash = 1.4f, lifeTicks = 34, color = 0xB2F5EA, size = 0.36f },
            cost = new[] { new ItemStack(Steel, 250), new ItemStack(Circuit, 180), new ItemStack(Alloy, 140) }, buildTicks = 110 * 60, supply = 5,
        };
        public static readonly UnitDef Warship = new UnitDef
        {
            key = "warship", name = "전함", desc = "포문이 많은 4단계 비행 유닛. 지상과 공중 모두 쏜다.",
            tier = 4, flying = true, health = 2400, armor = 10, speed = 0.028f, radius = 0.58f, range = 5.5f, reloadTicks = 10, color = 0x1864AB, tech = Research.Tier4,
            bullet = new BulletDef { speed = 0.30f, damage = 24, lifeTicks = 24, color = 0xA5D8FF, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 100), new ItemStack(Circuit, 100), new ItemStack(Alloy, 60) }, buildTicks = 60 * 60, supply = 4,
        };
        public static readonly UnitDef Carrier = new UnitDef
        {
            key = "carrier", name = "모함", desc = "가장 큰 비행 유닛. 멀리서 범위 포격을 퍼붓는다.",
            tier = 5, flying = true, shape = "carrier", health = 6000, armor = 16, speed = 0.020f, radius = 0.76f, range = 7f, reloadTicks = 36, color = 0x0B4F8A, tech = Research.Tier5,
            bullet = new BulletDef { speed = 0.24f, damage = 120, splash = 1.5f, lifeTicks = 36, color = 0xD0EBFF, size = 0.36f },
            cost = new[] { new ItemStack(Steel, 220), new ItemStack(Circuit, 200), new ItemStack(Alloy, 160) }, buildTicks = 110 * 60, supply = 5,
        };

        // 지원 공방
        public static readonly UnitDef Mechanic = new UnitDef
        {
            key = "mechanic", name = "정비 로봇", desc = "둘레의 아군 유닛과 건물을 고친다.",
            tier = 1, shape = "support", health = 260, armor = 3, speed = 0.026f, radius = 0.30f, range = 3f, reloadTicks = 30, color = 0x69DB7C,
            heal = 14, healRange = 3.2f,
            bullet = new BulletDef { speed = 0.25f, damage = 6, lifeTicks = 16, color = 0xD3F9D8, size = 0.13f },
            cost = new[] { new ItemStack(Iron, 40), new ItemStack(Circuit, 20) }, buildTicks = 14 * 60, supply = 1,
        };
        public static readonly UnitDef Guardian = new UnitDef
        {
            key = "guardian", name = "방패 로봇", desc = "3칸 안의 아군 유닛이 받는 피해를 35% 줄인다.",
            tier = 2, shape = "support", health = 900, armor = 8, speed = 0.020f, radius = 0.40f, range = 2.5f, reloadTicks = 40, color = 0x37B24D, tech = Research.Tier2,
            shieldRange = 3f, shieldMul = 0.65f,
            bullet = new BulletDef { speed = 0.22f, damage = 8, lifeTicks = 16, color = 0xD3F9D8, size = 0.15f },
            cost = new[] { new ItemStack(Steel, 50), new ItemStack(Circuit, 30) }, buildTicks = 22 * 60, supply = 2,
        };
        public static readonly UnitDef Booster = new UnitDef
        {
            key = "booster", name = "증폭 로봇", desc = "3.5칸 안의 다른 아군 유닛이 1.33배 빨리 쏘게 한다. 여럿이 겹쳐도 효과는 한 번이다.",
            tier = 2, shape = "support", health = 600, armor = 6, speed = 0.022f, radius = 0.38f, range = 3f, reloadTicks = 34, color = 0xFFD43B, tech = Research.Tier2,
            boostRange = 3.5f,
            bullet = new BulletDef { speed = 0.24f, damage = 8, lifeTicks = 16, color = 0xFFF3BF, size = 0.14f },
            cost = new[] { new ItemStack(Steel, 45), new ItemStack(Circuit, 45) }, buildTicks = 22 * 60, supply = 2,
        };
        public static readonly UnitDef RepairDrone = new UnitDef
        {
            key = "repairdrone", name = "수리 드론", desc = "가장 많이 상한 건물로 날아가 고친다. 싸우지 못한다.",
            tier = 1, flying = true, shape = "drone", health = 120, armor = 0, speed = 0.050f, radius = 0.24f, color = 0x8CE99A,
            heal = 20, healRange = 2.5f,
            cost = new[] { new ItemStack(Iron, 30), new ItemStack(Circuit, 15) }, buildTicks = 12 * 60, supply = 1,
        };
        public static readonly UnitDef Transport = new UnitDef
        {
            key = "transport", name = "수송기", desc = "공격 태세일 때 걸어가는 유닛을 실어 적 출현 지점 앞에 내려놓는다.",
            tier = 2, flying = true, shape = "transport", health = 700, armor = 6, speed = 0.040f, radius = 0.6f, color = 0x2F9E44, tech = Research.Tier2,
            capacity = 6,
            cost = new[] { new ItemStack(Steel, 60), new ItemStack(Circuit, 40) }, buildTicks = 25 * 60, supply = 2,
        };
        public static readonly UnitDef Miner = new UnitDef
        {
            key = "miner", name = "채굴 드론", desc = "집결 지점에서 가장 가까운 벽을 캐서 철광(세 번에 한 번은 황석)을 핵으로 나른다. 싸우지 못한다.",
            tier = 1, flying = true, shape = "drone", health = 110, armor = 0, speed = 0.045f, radius = 0.24f, color = 0xC9A27A,
            mine = 8, mineTicks = 5 * 60,
            cost = new[] { new ItemStack(Iron, 40), new ItemStack(Circuit, 15) }, buildTicks = 12 * 60, supply = 1,
        };
        public static readonly UnitDef Rebuilder = new UnitDef
        {
            key = "rebuilder", name = "재건 드론", desc = "적에게 부서진 건물의 자리로 날아가 같은 건물을 다시 놓는다. 비용은 평소처럼 핵에서 나간다. 싸우지 못한다.",
            tier = 1, flying = true, shape = "drone", health = 140, armor = 0, speed = 0.050f, radius = 0.24f, color = 0xB2F2BB,
            rebuild = true,
            cost = new[] { new ItemStack(Iron, 35), new ItemStack(Steel, 10), new ItemStack(Circuit, 20) }, buildTicks = 14 * 60, supply = 1,
        };

        // 다리 공방
        public static readonly UnitDef Bug = new UnitDef
        {
            key = "bug", name = "자폭 벌레", desc = "싸고 빠르다. 적이나 적 건물에 붙어 터진다. 건물에 3배 피해.",
            tier = 1, shape = "bug", health = 90, armor = 0, speed = 0.050f, radius = 0.24f, range = 0.3f, color = 0xFCC419,
            suicide = true, buildingMul = 3f,
            bullet = new BulletDef { damage = 120, splash = 1.8f, color = 0xFFD43B },
            cost = new[] { new ItemStack(Iron, 15), new ItemStack(Powder, 12) }, buildTicks = 5 * 60, supply = 1,
        };
        public static readonly UnitDef Climber = new UnitDef
        {
            key = "climber", name = "등반 로봇", desc = "긴 다리로 지형 벽을 타 넘어 지름길로 간다.",
            tier = 2, legs = true, shape = "legs", health = 520, armor = 6, speed = 0.028f, radius = 0.38f, range = 3.6f, reloadTicks = 22, color = 0xF08C00,
            bullet = new BulletDef { speed = 0.24f, damage = 20, lifeTicks = 20, color = 0xFFE066, size = 0.17f },
            cost = new[] { new ItemStack(Steel, 40), new ItemStack(Circuit, 25) }, buildTicks = 18 * 60, supply = 2,
        };
        public static readonly UnitDef Spitter = new UnitDef
        {
            key = "spitter", name = "산성 로봇", desc = "벽을 타 넘는 2단계 다리 로봇. 산성 덩어리를 던져 범위 안 지상의 적을 2.5초 동안 절반 속도로 늦춘다. 공중은 못 쏜다.",
            tier = 2, legs = true, shape = "legs", health = 460, armor = 5, speed = 0.026f, radius = 0.36f, range = 4.5f, reloadTicks = 50, color = 0xE67700, tech = Research.Tier2,
            targetAir = false,
            bullet = new BulletDef { speed = 0.12f, damage = 22, splash = 1.3f, lifeTicks = 60, artillery = true, slow = 0.5f, slowTicks = 150, color = 0xA9E34B, size = 0.22f },
            cost = new[] { new ItemStack(Steel, 35), new ItemStack(Circuit, 25), new ItemStack(Powder, 10) }, buildTicks = 18 * 60, supply = 2,
        };
        public static readonly UnitDef Leech = new UnitDef
        {
            key = "leech", name = "흡수 로봇", desc = "벽을 타 넘는 3단계 다리 로봇. 가까이서 쏘아 준 피해의 40% 만큼 스스로를 고친다.",
            tier = 3, legs = true, shape = "legs", health = 1000, armor = 9, speed = 0.022f, radius = 0.46f, range = 3.2f, reloadTicks = 14, color = 0xD9480F, tech = Research.Tier3,
            drain = 0.4f,
            bullet = new BulletDef { speed = 0.40f, damage = 18, lifeTicks = 10, color = 0xDA77F2, size = 0.16f },
            cost = new[] { new ItemStack(Steel, 50), new ItemStack(Circuit, 40), new ItemStack(Alloy, 25) }, buildTicks = 35 * 60, supply = 3,
        };
        public static readonly UnitDef Shatter = new UnitDef
        {
            key = "shatter", name = "파편 로봇", desc = "벽을 타 넘는 4단계 다리 로봇. 곡사 포탄이 터지면서 파편 6개가 폭발 범위 밖으로 튄다. 파편은 공중의 적도 맞힌다.",
            tier = 4, legs = true, shape = "legs", health = 2300, armor = 12, speed = 0.016f, radius = 0.56f, range = 6.5f, reloadTicks = 70, color = 0xC2410C, tech = Research.Tier4,
            targetAir = false, buildingMul = 1.5f,
            bullet = new BulletDef
            {
                speed = 0.11f, damage = 90, splash = 1.5f, lifeTicks = 90, artillery = true, color = 0xFFC078, size = 0.3f, fragCount = 6,
                frag = new BulletDef { speed = 0.18f, damage = 28, lifeTicks = 12, color = 0xFFE8CC, size = 0.13f },
            },
            cost = new[] { new ItemStack(Steel, 110), new ItemStack(Circuit, 80), new ItemStack(Powder, 50), new ItemStack(Alloy, 45) }, buildTicks = 60 * 60, supply = 4,
        };
        public static readonly UnitDef Pyre = new UnitDef
        {
            key = "pyre", name = "화염 거미", desc = "벽을 타 넘는 5단계 다리 로봇. 가까이서 꿰뚫는 불길을 뿜어 적을 4초 동안 불태운다. 불은 방어력을 무시한다.",
            tier = 5, legs = true, shape = "legs", health = 5200, armor = 16, speed = 0.014f, radius = 0.70f, range = 3.6f, reloadTicks = 8, color = 0xA61E4D, tech = Research.Tier5,
            bullet = new BulletDef { speed = 0.20f, damage = 22, lifeTicks = 20, pierce = 4, burn = 30, burnTicks = 240, color = 0xFF922B, size = 0.24f },
            cost = new[] { new ItemStack(Steel, 220), new ItemStack(Circuit, 150), new ItemStack(Powder, 120), new ItemStack(Alloy, 130) }, buildTicks = 105 * 60, supply = 5,
        };

        // 전차 공장: 느리고 단단한 지상 유닛. 몸에 닿은 지상의 적을 짓밟는다
        public static readonly UnitDef ArmoredCar = new UnitDef
        {
            key = "armoredcar", name = "장갑차", desc = "값싼 1단계 전차. 작지만 장갑이 있고, 닿은 적을 짓밟는다.",
            tier = 1, shape = "tank", health = 260, armor = 5, speed = 0.026f, radius = 0.32f, range = 3.4f, reloadTicks = 22, color = 0x94A37A,
            crush = 10,
            bullet = new BulletDef { speed = 0.26f, damage = 10, lifeTicks = 18, color = 0xE9F5C9, size = 0.14f },
            cost = new[] { new ItemStack(Iron, 35), new ItemStack(Steel, 10) }, buildTicks = 10 * 60, supply = 1,
        };
        public static readonly UnitDef LightTank = new UnitDef
        {
            key = "lighttank", name = "경전차", desc = "2단계 전차. 같은 단계의 로봇보다 느리지만 훨씬 단단하다.",
            tier = 2, shape = "tank", health = 620, armor = 9, speed = 0.022f, radius = 0.40f, range = 4.2f, reloadTicks = 30, color = 0x74875A, tech = Research.Tier2,
            crush = 20,
            bullet = new BulletDef { speed = 0.26f, damage = 26, lifeTicks = 22, color = 0xE9F5C9, size = 0.18f },
            cost = new[] { new ItemStack(Steel, 45), new ItemStack(Circuit, 20) }, buildTicks = 18 * 60, supply = 2,
        };
        public static readonly UnitDef HeavyTank = new UnitDef
        {
            key = "heavytank", name = "중전차", desc = "범위 피해 주포를 단 3단계 전차. 공중은 못 쏜다.",
            tier = 3, shape = "tank", health = 1900, armor = 15, speed = 0.015f, radius = 0.52f, range = 5f, reloadTicks = 55, color = 0x5C6F45, tech = Research.Tier3,
            targetAir = false, crush = 40,
            bullet = new BulletDef { speed = 0.22f, damage = 75, splash = 1.1f, lifeTicks = 30, color = 0xE9F5C9, size = 0.28f },
            cost = new[] { new ItemStack(Steel, 90), new ItemStack(Circuit, 40), new ItemStack(Alloy, 20) }, buildTicks = 38 * 60, supply = 3,
        };
        public static readonly UnitDef Bulwark = new UnitDef
        {
            key = "bulwark", name = "방호 전차", desc = "보호막을 두른 3단계 전차. 보호막 700 이 피해를 먼저 받고, 3초 동안 맞지 않으면 다시 차오른다.",
            tier = 3, shape = "tank", health = 1500, armor = 12, speed = 0.016f, radius = 0.50f, range = 4.2f, reloadTicks = 24, color = 0x5C7F8A, tech = Research.Tier3,
            barrier = 700, barrierRegen = 60, barrierDelay = 3 * 60, crush = 30,
            bullet = new BulletDef { speed = 0.26f, damage = 20, lifeTicks = 22, color = 0xC5F6FA, size = 0.17f },
            cost = new[] { new ItemStack(Steel, 80), new ItemStack(Circuit, 60), new ItemStack(Alloy, 25) }, buildTicks = 38 * 60, supply = 3,
        };
        public static readonly UnitDef PiercerTank = new UnitDef
        {
            key = "piercertank", name = "철갑 전차", desc = "4단계 전차. 멀리서 방어력을 무시하는 철갑탄을 쏜다. 느리게 쏘고 공중은 못 쏜다.",
            tier = 4, shape = "tank", health = 3200, armor = 18, speed = 0.013f, radius = 0.58f, range = 7f, reloadTicks = 100, color = 0x485737, tech = Research.Tier4,
            targetAir = false, buildingMul = 1.5f, crush = 60,
            bullet = new BulletDef { speed = 0.45f, damage = 320, lifeTicks = 20, pierceArmor = true, color = 0xFFF3BF, size = 0.26f },
            cost = new[] { new ItemStack(Steel, 150), new ItemStack(Circuit, 80), new ItemStack(Powder, 40), new ItemStack(Alloy, 60) }, buildTicks = 65 * 60, supply = 4,
        };
        public static readonly UnitDef Juggernaut = new UnitDef
        {
            key = "juggernaut", name = "분쇄 전차", desc = "가장 단단한 5단계 전차. 범위 피해 주포를 쏘며 닿은 적을 갈아 버린다.",
            tier = 5, shape = "tank", health = 8000, armor = 24, speed = 0.010f, radius = 0.76f, range = 5.5f, reloadTicks = 26, color = 0x364228, tech = Research.Tier5,
            crush = 150,
            bullet = new BulletDef { speed = 0.22f, damage = 95, splash = 1.3f, lifeTicks = 32, color = 0xE9F5C9, size = 0.34f },
            cost = new[] { new ItemStack(Steel, 300), new ItemStack(Circuit, 160), new ItemStack(Alloy, 150) }, buildTicks = 115 * 60, supply = 5,
        };

        // 조선소 (해상): 호수 안에서만 움직이지만 사거리가 길다
        public static readonly UnitDef Patrol = new UnitDef
        {
            key = "patrol", name = "초계정", desc = "물 위만 다니는 1단계 함정.",
            tier = 1, naval = true, shape = "boat", health = 320, armor = 3, speed = 0.030f, radius = 0.32f, range = 5f, reloadTicks = 18, color = 0x3BC9DB,
            bullet = new BulletDef { speed = 0.28f, damage = 10, lifeTicks = 24, color = 0xC5F6FA, size = 0.14f },
            cost = new[] { new ItemStack(Iron, 40), new ItemStack(Circuit, 12) }, buildTicks = 10 * 60, supply = 1,
        };
        public static readonly UnitDef Gunboat = new UnitDef
        {
            key = "gunboat", name = "포함", desc = "범위 피해 포를 실은 2단계 함정.",
            tier = 2, naval = true, shape = "boat", health = 950, armor = 8, speed = 0.022f, radius = 0.42f, range = 7f, reloadTicks = 50, color = 0x1098AD, tech = Research.Tier2,
            bullet = new BulletDef { speed = 0.22f, damage = 55, splash = 1.3f, lifeTicks = 40, color = 0x99E9F2, size = 0.26f },
            cost = new[] { new ItemStack(Steel, 50), new ItemStack(Circuit, 35), new ItemStack(Powder, 20) }, buildTicks = 22 * 60, supply = 2,
        };
        public static readonly UnitDef Destroyer = new UnitDef
        {
            key = "destroyer", name = "구축함", desc = "아주 멀리 곡사포를 쏘는 3단계 함정. 공중은 못 쏜다.",
            tier = 3, naval = true, shape = "boat", health = 2800, armor = 14, speed = 0.016f, radius = 0.56f, range = 9f, reloadTicks = 60, color = 0x0B7285, tech = Research.Tier3,
            targetAir = false, buildingMul = 1.5f,
            bullet = new BulletDef { speed = 0.11f, damage = 130, splash = 1.8f, lifeTicks = 100, artillery = true, color = 0xE3FAFC, size = 0.32f },
            cost = new[] { new ItemStack(Steel, 110), new ItemStack(Circuit, 80), new ItemStack(Alloy, 40) }, buildTicks = 50 * 60, supply = 3,
        };
        public static readonly UnitDef Shockboat = new UnitDef
        {
            key = "shockboat", name = "충격함", desc = "3단계 함정. 충격탄이 터지면서 범위 안의 적을 0.9칸 뒤로 밀어낸다. 보스는 절반만 밀린다.",
            tier = 3, naval = true, shape = "boat", health = 2200, armor = 12, speed = 0.018f, radius = 0.50f, range = 7.5f, reloadTicks = 45, color = 0x22B8CF, tech = Research.Tier3,
            bullet = new BulletDef { speed = 0.30f, damage = 50, splash = 1.2f, lifeTicks = 30, knockback = 0.9f, color = 0xFFFFFF, size = 0.24f },
            cost = new[] { new ItemStack(Steel, 90), new ItemStack(Circuit, 70), new ItemStack(Powder, 40), new ItemStack(Alloy, 30) }, buildTicks = 45 * 60, supply = 3,
        };
        public static readonly UnitDef Arcship = new UnitDef
        {
            key = "arcship", name = "방전함", desc = "4단계 함정. 포 대신 전류를 뿜어 5.5칸 안의 적 8기를 지상·공중 가리지 않고 한꺼번에 치고, 곁의 아군을 고친다.",
            tier = 4, naval = true, shape = "boat", health = 4200, armor = 16, speed = 0.014f, radius = 0.62f, range = 5.5f, reloadTicks = 40, color = 0x15AABF, tech = Research.Tier4,
            fieldDamage = 40, fieldTargets = 8, heal = 8, healRange = 4f,
            cost = new[] { new ItemStack(Steel, 150), new ItemStack(Circuit, 120), new ItemStack(Alloy, 70) }, buildTicks = 70 * 60, supply = 4,
        };
        /// 기함이 스스로 띄우는 호위기. 공장에서는 만들 수 없고, 기함을 잃으면 함께 떨어진다
        public static readonly UnitDef Escort = new UnitDef
        {
            key = "escort", name = "함재기", desc = "기함이 띄우는 작은 호위기. 기함 곁을 지킨다.",
            tier = 1, flying = true, health = 150, armor = 1, speed = 0.055f, radius = 0.22f, range = 2.8f, reloadTicks = 16, color = 0x66D9E8,
            bullet = new BulletDef { speed = 0.28f, damage = 9, lifeTicks = 14, color = 0xC5F6FA, size = 0.12f },
            supply = 0,
        };
        public static readonly UnitDef Flagship = new UnitDef
        {
            key = "flagship", name = "기함", desc = "5단계 함정. 사거리 10 의 관통 전자포를 쏘고, 15초마다 함재기를 띄워 둘까지 거느린다.",
            tier = 5, naval = true, shape = "boat", health = 7000, armor = 20, speed = 0.012f, radius = 0.74f, range = 10f, reloadTicks = 90, color = 0x0C8599, tech = Research.Tier5,
            buildingMul = 1.5f, spawns = Escort, spawnMax = 2, spawnTicks = 15 * 60,
            bullet = new BulletDef { speed = 0.5f, damage = 170, lifeTicks = 24, pierce = 5, color = 0xE3FAFC, size = 0.3f },
            cost = new[] { new ItemStack(Steel, 260), new ItemStack(Circuit, 200), new ItemStack(Alloy, 170) }, buildTicks = 120 * 60, supply = 5,
        };

        /// 핵에서 나오는 코어 유닛. 플레이어가 갈 곳을 찍어 주고, 곁의 건설을 빠르게 한다
        public static readonly UnitDef Commander = new UnitDef
        {
            key = "commander", name = "지휘기", desc = "핵에서 나온다. 찍어 준 곳을 지키고, 곁의 건설을 1.5배 빠르게 한다.",
            tier = 1, flying = true, shape = "commander", commander = true, health = 320, armor = 2, speed = 0.050f, radius = 0.34f, range = 3.5f, reloadTicks = 20, color = 0xFFD43B,
            bullet = new BulletDef { speed = 0.28f, damage = 7, lifeTicks = 16, color = 0xFFF3BF, size = 0.14f },
            supply = 0,
        };

        public static readonly UnitDef[] Units =
        {
            Scout, Assault, Skimmer, Heavy, Siege, Titan, Recon, Bomber, Striker, Seeker, Mender, Warship, Carrier,
            Mechanic, Guardian, Booster, RepairDrone, Transport, Miner, Rebuilder, Bug, Climber, Spitter, Leech, Shatter, Pyre, Patrol, Gunboat, Destroyer, Shockboat, Arcship, Flagship, Escort, ArmoredCar, LightTank, HeavyTank, Bulwark, PiercerTank, Juggernaut, Commander,
        };

        // ───────── 건물 ─────────

        public static readonly BlockDef Core = new BlockDef
        {
            key = "core", name = "심연 핵", desc = "가져온 자원이 담긴 곳. 부서지면 패배한다.",
            kind = BlockKind.Core, size = 3, health = 1500, color = 0x4FD1C5,
        };

        public static readonly BlockDef Wall = new BlockDef
        {
            key = "wall", name = "방벽", desc = "적의 길을 막고 공격을 받아낸다.",
            kind = BlockKind.Wall, health = 420, color = 0x9A8C98,
            cost = new[] { new ItemStack(Iron, 8) },
        };

        public static readonly BlockDef SteelWall = new BlockDef
        {
            key = "steelwall", name = "강철 방벽", desc = "강철로 만든 방벽. 장갑이 있어 약한 공격은 거의 막는다.",
            kind = BlockKind.Wall, health = 1100, armor = 5, color = 0x9FB4C7,
            cost = new[] { new ItemStack(Steel, 10) },
        };

        public static readonly BlockDef RapidTurret = new BlockDef
        {
            key = "rapid", name = "속사포", desc = "철광을 탄으로 쓴다. 지상과 공중 모두 쏜다.",
            kind = BlockKind.Turret, health = 160, color = 0xF4A261,
            cost = new[] { new ItemStack(Iron, 35) },
            range = 5.5f, reloadTicks = 20, ammoItem = Iron, ammoPerItem = 6,
            bullet = new BulletDef { speed = 0.22f, damage = 11, lifeTicks = 32, color = 0xFFD8A8 },
        };

        public static readonly BlockDef MortarTurret = new BlockDef
        {
            key = "mortar", name = "박격포", desc = "황석 포탄으로 넓은 범위를 때린다. 지상만 쏜다.",
            kind = BlockKind.Turret, size = 2, health = 260, color = 0xE76F51, tech = Research.Mortar,
            cost = new[] { new ItemStack(Iron, 60), new ItemStack(Sulfur, 40) },
            range = 8.5f, reloadTicks = 100, ammoItem = Sulfur, ammoPerItem = 2, targetAir = false,
            bullet = new BulletDef { speed = 0.11f, damage = 38, splash = 1.7f, lifeTicks = 90, artillery = true, color = 0xFFE066, size = 0.28f },
        };

        public static readonly BlockDef CrystalTurret = new BlockDef
        {
            key = "lance", name = "결정포", desc = "심연 결정을 쏘아 적을 꿰뚫는다.",
            kind = BlockKind.Turret, size = 2, health = 300, color = 0x9C6BFF, tech = Research.Lance,
            cost = new[] { new ItemStack(Iron, 80), new ItemStack(Sulfur, 50), new ItemStack(Crystal, 30) },
            range = 9.5f, reloadTicks = 55, ammoItem = Crystal, ammoPerItem = 4,
            bullet = new BulletDef { speed = 0.45f, damage = 70, lifeTicks = 26, pierce = 3, color = 0xD0BFFF, size = 0.24f },
        };

        public static readonly BlockDef RobotFactory = new BlockDef
        {
            key = "robotfactory", name = "로봇 공장", desc = "핵의 자원으로 공격 로봇을 만든다.",
            kind = BlockKind.Factory, size = 2, health = 500, color = 0x38B2AC, tech = Research.RobotFactory,
            cost = new[] { new ItemStack(Iron, 120), new ItemStack(Steel, 40) },
            units = new[] { Scout, Assault, Skimmer, Heavy, Siege, Titan },
        };

        public static readonly BlockDef Airfield = new BlockDef
        {
            key = "airfield", name = "비행장", desc = "핵의 자원으로 비행 유닛을 만든다.",
            kind = BlockKind.Factory, size = 2, health = 420, color = 0x4DABF7, tech = Research.Airfield,
            cost = new[] { new ItemStack(Iron, 100), new ItemStack(Steel, 30), new ItemStack(Circuit, 30) },
            units = new[] { Recon, Bomber, Striker, Seeker, Mender, Warship, Carrier },
        };

        public static readonly BlockDef SupportShop = new BlockDef
        {
            key = "supportshop", name = "지원 공방", desc = "수리·보호막·증폭·수송·채굴·재건 유닛을 만든다.",
            kind = BlockKind.Factory, size = 2, health = 450, color = 0x37B24D, tech = Research.Support,
            cost = new[] { new ItemStack(Iron, 100), new ItemStack(Steel, 40), new ItemStack(Circuit, 30) },
            units = new[] { Mechanic, Guardian, Booster, RepairDrone, Transport, Miner, Rebuilder },
        };

        public static readonly BlockDef LegShop = new BlockDef
        {
            key = "legshop", name = "다리 공방", desc = "자폭 벌레와 벽을 넘는 다리 로봇들을 만든다.",
            kind = BlockKind.Factory, size = 2, health = 450, color = 0xF08C00, tech = Research.Legs,
            cost = new[] { new ItemStack(Iron, 100), new ItemStack(Steel, 30), new ItemStack(Powder, 30) },
            units = new[] { Bug, Climber, Spitter, Leech, Shatter, Pyre },
        };

        public static readonly BlockDef TankFactory = new BlockDef
        {
            key = "tankfactory", name = "전차 공장", desc = "느리고 단단한 전차를 만든다. 전차는 닿은 적을 짓밟는다.",
            kind = BlockKind.Factory, size = 2, health = 600, color = 0x74875A, tech = Research.Tank,
            cost = new[] { new ItemStack(Iron, 140), new ItemStack(Steel, 80) },
            units = new[] { ArmoredCar, LightTank, HeavyTank, Bulwark, PiercerTank, Juggernaut },
        };

        public static readonly BlockDef Shipyard = new BlockDef
        {
            key = "shipyard", name = "조선소", desc = "물가에만 지을 수 있다. 호수 위를 다니는 함정을 만든다.",
            kind = BlockKind.Factory, size = 2, health = 500, color = 0x1098AD, tech = Research.Shipyard, needsWater = true,
            cost = new[] { new ItemStack(Iron, 120), new ItemStack(Steel, 50), new ItemStack(Circuit, 20) },
            units = new[] { Patrol, Gunboat, Destroyer, Shockboat, Arcship, Flagship },
        };

        // 적 거점 지역에만 나오는 적의 건물. 체력과 화력은 깊이에 따라 커진다
        public static readonly BlockDef EnemyCore = new BlockDef
        {
            key = "enemycore", name = "적 핵", desc = "적이 쏟아져 나오는 곳. 모두 부수면 지역을 점령한다.",
            kind = BlockKind.EnemyCore, size = 3, health = 3000, armor = 4, color = 0xC1121F, enemy = true,
            bounty = new[] { new ItemStack(Iron, 150), new ItemStack(Sulfur, 100), new ItemStack(Crystal, 30) },
        };
        public static readonly BlockDef EnemyTurret = new BlockDef
        {
            key = "enemyturret", name = "적 포탑", desc = "가까이 온 유닛을 쏜다.",
            kind = BlockKind.EnemyTurret, health = 450, armor = 3, color = 0xE5383B, enemy = true,
            range = 5.5f, reloadTicks = 40,
            bullet = new BulletDef { damage = 12, color = 0xFF8FA3 },
            bounty = new[] { new ItemStack(Iron, 15), new ItemStack(Sulfur, 5) },
        };
        public static readonly BlockDef EnemyWall = new BlockDef
        {
            key = "enemywall", name = "적 방벽", desc = "적 거점을 두른 벽.",
            kind = BlockKind.Wall, health = 700, armor = 4, color = 0x7F1D1D, enemy = true,
            bounty = new[] { new ItemStack(Iron, 3) },
        };

        public static readonly BlockDef[] Blocks = { Core, Wall, RapidTurret, MortarTurret, CrystalTurret, SteelWall, RobotFactory, Airfield, EnemyCore, EnemyTurret, EnemyWall, SupportShop, LegShop, Shipyard, TankFactory };
        /// 건설 바에 나오는 순서
        public static readonly BlockDef[] Buildable = { RapidTurret, Wall, MortarTurret, CrystalTurret, RobotFactory, Airfield, SupportShop, LegShop, TankFactory, Shipyard, SteelWall };

        // ───────── 적 ─────────

        public static readonly EnemyDef Walker = new EnemyDef
        {
            key = "walker", name = "보행병", health = 42, speed = 0.020f, radius = 0.30f,
            damage = 9, attackTicks = 50, range = 0.65f, color = 0xD64550,
            bounty = new[] { new ItemStack(Iron, 4) },
        };
        public static readonly EnemyDef Runner = new EnemyDef
        {
            key = "runner", name = "질주병", health = 20, speed = 0.038f, radius = 0.22f,
            damage = 5, attackTicks = 35, range = 0.6f, color = 0xFF8FA3,
            bounty = new[] { new ItemStack(Iron, 2), new ItemStack(Sulfur, 1) },
        };
        public static readonly EnemyDef Breaker = new EnemyDef
        {
            key = "breaker", name = "파쇄병", health = 190, speed = 0.012f, radius = 0.42f, armor = 2,
            damage = 26, attackTicks = 80, range = 2.4f, siege = true, color = 0x8B1E3F,
            bounty = new[] { new ItemStack(Iron, 8), new ItemStack(Sulfur, 4) },
        };
        public static readonly EnemyDef Flyer = new EnemyDef
        {
            key = "flyer", name = "비행체", health = 30, speed = 0.030f, radius = 0.26f,
            damage = 7, attackTicks = 45, range = 1.0f, flying = true, color = 0xB5179E,
            bounty = new[] { new ItemStack(Iron, 3), new ItemStack(Sulfur, 1) },
        };
        public static readonly EnemyDef Boss = new EnemyDef
        {
            key = "boss", name = "심연 군주", health = 3400, speed = 0.009f, radius = 0.8f, armor = 3,
            damage = 60, attackTicks = 90, range = 2.2f, siege = true, boss = true, color = 0x5A189A,
            bounty = new[] { new ItemStack(Iron, 120), new ItemStack(Sulfur, 80), new ItemStack(Crystal, 40) },
        };
        public static readonly EnemyDef Shell = new EnemyDef
        {
            key = "shell", name = "철갑병", health = 110, speed = 0.015f, radius = 0.36f, armor = 6,
            damage = 16, attackTicks = 60, range = 0.7f, color = 0x6C757D,
            bounty = new[] { new ItemStack(Iron, 7), new ItemStack(Sulfur, 3) },
        };

        /// 모든 지역의 마지막 웨이브에 한 마리 나오는 보스 (10의 배수 지역은 더 강한 심연 군주가 대신 나온다)
        public static readonly EnemyDef Chief = new EnemyDef
        {
            key = "chief", name = "심연 대장", health = 900, speed = 0.011f, radius = 0.6f, armor = 2,
            damage = 34, attackTicks = 85, range = 2.0f, siege = true, boss = true, color = 0x7B2CBF,
            bounty = new[] { new ItemStack(Iron, 40), new ItemStack(Sulfur, 25) },
        };

        public static readonly EnemyDef[] Enemies = { Walker, Runner, Breaker, Flyer, Boss, Shell, Chief };

        public static readonly AbyssDef[] Abysses =
        {
            new AbyssDef
            {
                key = "ember", name = "잿불 심연", desc = "타다 남은 재가 흩날리는 균형 잡힌 심연.",
                floorColor = 0x3A2E2B, wallColor = 0x1C1514, accent = 0xF4A261,
            },
            new AbyssDef
            {
                key = "frost", name = "서리 심연", desc = "적은 느리지만 단단하고, 전리품이 적다.",
                enemyHealthMul = 1.45f, enemySpeedMul = 0.8f, enemyCountMul = 0.9f, bountyMul = 0.85f,
                floorColor = 0x2B3A4A, wallColor = 0x131C26, accent = 0x90E0EF,
            },
            new AbyssDef
            {
                key = "mire", name = "독늪 심연", desc = "적이 빠르게 떼로 몰려오지만 전리품이 많다.",
                enemyHealthMul = 0.8f, enemySpeedMul = 1.25f, enemyCountMul = 1.4f, bountyMul = 1.15f,
                floorColor = 0x2F3B2A, wallColor = 0x161D13, accent = 0xB5E48C,
            },
        };

        public const int RegionCount = 100;

        static Content()
        {
            // 건설 시간은 비용에 비례한다: (20 + 자원별 가중 합) × 5 틱
            float[] weight = { 1f, 1.5f, 3f, 2.5f, 3f, 2.5f, 6f };
            for (int i = 0; i < Blocks.Length; i++)
            {
                Blocks[i].id = i;
                float sum = 20;
                foreach (var s in Blocks[i].cost) sum += s.amount * weight[s.item];
                Blocks[i].buildTicks = (int)(sum * 5);
            }
            for (int i = 0; i < Units.Length; i++) Units[i].id = i;
            for (int i = 0; i < Enemies.Length; i++) Enemies[i].id = i;
            for (int i = 0; i < Abysses.Length; i++) Abysses[i].id = i;
        }
    }
}
