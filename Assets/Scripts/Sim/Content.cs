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

    public enum BlockKind { Core, Wall, Turret, Factory, EnemyCore, EnemyTurret, Support }

    public sealed class BulletDef
    {
        public float speed;          // 타일/틱
        public float damage;
        public float splash;         // 범위 피해 반지름 (0 = 없음)
        public int lifeTicks;
        public bool artillery;       // 목표 지점까지 날아가 터진다 (지상만)
        public int pierce;           // 관통 수 (0 = 첫 적에서 사라짐)
        public int chain;            // 전격: 맞은 적에서 가까운 다른 적으로 이만큼 더 건너뛴다 (건너뛸 때마다 피해 ×0.7)
        public float chainRange;     // 건너뛰는 거리
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

    /// 포탑에 넣는 탄 한 종류: 핵의 어떤 자원을 쓰고, 그 자원 하나로 몇 발이 되며, 어떤 탄이 나가는가
    public sealed class AmmoDef
    {
        public int item, perItem;
        public string name, desc;
        public BulletDef bullet;
    }

    /// 포탑이 사거리 안의 적 가운데 누구를 겨누는가
    public enum TargetMode { Nearest, Strongest, First, Air }

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
        public string ammoName = "기본탄";   // 기본 탄의 이름 (포탑 창에 나온다)
        public AmmoDef[] altAmmo = new AmmoDef[0];   // 기본 탄 말고 골라 넣을 수 있는 탄
        public AmmoDef[] ammos;          // [0] = 기본 탄(ammoItem·ammoPerItem·bullet), 그 뒤로 altAmmo. 등록할 때 채운다

        // 지원 건물: 범위 안의 아군 건물을 돕는다
        public float supportRange;       // 건물 가운데에서 잰 범위 (상대 건물의 가장자리까지)
        public float mendPct;            // (수리탑) mendTicks 마다 범위 안 건물을 최대 체력의 이 비율만큼 고친다
        public int mendTicks;
        public bool boost;               // (증폭탑) 범위 안 포탑이 3틱에 한 번 재장전을 한 틱 더 줄인다 (연사 약 1.33배, 겹쳐도 한 번만)
        public float shield;             // (역장 발생기) 범위 안 건물이 적에게 받는 피해를 이만큼까지 대신 받는다
        public float shieldRegen;        // 초당 차오르는 양
        public int shieldDelay;          // 마지막으로 피해를 받은 뒤 이 틱이 지나야 차오르기 시작한다

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
        public float regen;              // 자가 수복: 초당 이만큼 스스로 체력을 되찾는다
        public float boostRange;         // (증폭) 이 거리 안의 다른 아군 유닛은 3틱에 한 번 재장전이 한 틱 더 줄어든다 (연사 약 1.33배, 겹쳐도 한 번만)
        public float barrier;            // 자기 보호막: 받는 피해를 체력보다 먼저 이만큼 대신 받는다
        public float barrierRegen;       // 보호막이 초당 차오르는 양
        public int barrierDelay;         // 마지막으로 맞은 뒤 이 틱이 지나야 차오르기 시작한다
        public float crush;              // (전차) 몸에 닿은 지상의 적을 짓밟는다: 초당 이만큼의 피해 (방어력을 무시한다)
        public UnitDef spawns;           // (기함) 이 유닛을 스스로 띄운다. 띄운 유닛은 한도를 차지하지 않고 곁을 지킨다
        public int spawnMax, spawnTicks; // 한 번에 거느리는 수, 하나를 띄우는 데 걸리는 틱
        public BulletDef minelay;        // (기뢰 부설기) 깔고 다니는 지뢰: 지상의 적이 밟으면 이 피해·범위·둔화로 터진다
        public int mineEvery, mineMax;   // 지뢰 하나를 까는 데 걸리는 틱, 한 대가 깔아 둘 수 있는 수
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
            tier = 3, health = 1700, armor = 10, speed = 0.016f, radius = 0.48f, range = 5.5f, reloadTicks = 45, color = 0x2C7A7B, tech = Research.Tier3,
            bullet = new BulletDef { speed = 0.20f, damage = 72, splash = 1.2f, lifeTicks = 36, color = 0xE6FFFA, size = 0.3f },
            cost = new[] { new ItemStack(Steel, 80), new ItemStack(Circuit, 50), new ItemStack(Alloy, 25) }, buildTicks = 50 * 60, supply = 3,
        };
        public static readonly UnitDef Arcer = new UnitDef
        {
            key = "arcer", name = "전격 로봇", desc = "3단계 로봇. 전격이 맞은 적에서 2.2칸 안의 다른 적으로 네 번까지 건너뛴다. 건너뛸 때마다 피해가 30% 줄어든다.",
            tier = 3, health = 1200, armor = 7, speed = 0.020f, radius = 0.42f, range = 4.8f, reloadTicks = 36, color = 0x3BC9DB, tech = Research.Tier3,
            bullet = new BulletDef { speed = 0.50f, damage = 58, lifeTicks = 12, chain = 4, chainRange = 2.2f, color = 0xC5F6FA, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 70), new ItemStack(Circuit, 80), new ItemStack(Alloy, 25) }, buildTicks = 50 * 60, supply = 3,
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
            tier = 3, flying = true, health = 1000, armor = 5, speed = 0.035f, radius = 0.44f, range = 4.5f, reloadTicks = 14, color = 0x1C7ED6, tech = Research.Tier3,
            bullet = new BulletDef { speed = 0.30f, damage = 19, lifeTicks = 20, color = 0xA5D8FF, size = 0.18f },
            cost = new[] { new ItemStack(Steel, 50), new ItemStack(Circuit, 50), new ItemStack(Alloy, 30) }, buildTicks = 50 * 60, supply = 3,
        };
        public static readonly UnitDef Seeker = new UnitDef
        {
            key = "seeker", name = "유도탄기", desc = "멀리서 유도탄을 쏘는 3단계 비행 유닛. 유도탄은 겨눈 적을 쫓아가 작은 범위로 터진다. 장갑은 얇다.",
            tier = 3, flying = true, health = 680, armor = 3, speed = 0.038f, radius = 0.40f, range = 6.5f, reloadTicks = 55, color = 0x339AF0, tech = Research.Tier3,
            bullet = new BulletDef { speed = 0.20f, damage = 84, splash = 1.0f, lifeTicks = 55, homing = 0.12f, color = 0xFFD8A8, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 50), new ItemStack(Circuit, 60), new ItemStack(Powder, 40), new ItemStack(Alloy, 20) }, buildTicks = 50 * 60, supply = 3,
        };
        public static readonly UnitDef Mender = new UnitDef
        {
            key = "mender", name = "수리 폭격기", desc = "3단계 폭격기. 폭탄이 범위 안의 적을 때리면서 같은 범위의 아군 건물을 고친다. 공중은 못 쏜다.",
            tier = 3, flying = true, health = 1200, armor = 6, speed = 0.036f, radius = 0.46f, range = 0.9f, reloadTicks = 70, color = 0x4DD4AC, tech = Research.Tier3,
            targetAir = false,
            bullet = new BulletDef { speed = 0.05f, damage = 100, splash = 1.7f, lifeTicks = 30, artillery = true, heal = 84, color = 0xB2F2BB, size = 0.26f },
            cost = new[] { new ItemStack(Steel, 65), new ItemStack(Circuit, 65), new ItemStack(Powder, 40), new ItemStack(Alloy, 25) }, buildTicks = 55 * 60, supply = 3,
        };

        // 4·5단계
        public static readonly UnitDef Siege = new UnitDef
        {
            key = "siege", name = "공성 로봇", desc = "적 포탑 사거리 밖에서 포격한다. 건물에 강하고 공중은 못 쏜다.",
            tier = 4, health = 4400, armor = 14, speed = 0.013f, radius = 0.56f, range = 7.5f, reloadTicks = 80, color = 0x285E61, tech = Research.Tier4,
            targetAir = false, buildingMul = 2.5f,
            bullet = new BulletDef { speed = 0.10f, damage = 220, splash = 1.6f, lifeTicks = 90, artillery = true, color = 0xFFE8A3, size = 0.34f },
            cost = new[] { new ItemStack(Steel, 190), new ItemStack(Circuit, 130), new ItemStack(Powder, 95), new ItemStack(Alloy, 80) }, buildTicks = 115 * 60, supply = 4,
        };
        public static readonly UnitDef Titan = new UnitDef
        {
            key = "titan", name = "거신 로봇", desc = "가장 두꺼운 장갑과 범위 화력을 가진 5단계 로봇.",
            tier = 5, health = 14900, armor = 20, speed = 0.011f, radius = 0.72f, range = 6f, reloadTicks = 30, color = 0x1D4044, tech = Research.Tier5,
            bullet = new BulletDef { speed = 0.22f, damage = 200, splash = 1.4f, lifeTicks = 34, color = 0xB2F5EA, size = 0.36f },
            cost = new[] { new ItemStack(Steel, 500), new ItemStack(Circuit, 360), new ItemStack(Alloy, 280) }, buildTicks = 285 * 60, supply = 5,
        };
        public static readonly UnitDef Warship = new UnitDef
        {
            key = "warship", name = "전함", desc = "포문이 많은 4단계 비행 유닛. 지상과 공중 모두 쏜다.",
            tier = 4, flying = true, health = 4100, armor = 10, speed = 0.028f, radius = 0.58f, range = 5.5f, reloadTicks = 10, color = 0x1864AB, tech = Research.Tier4,
            bullet = new BulletDef { speed = 0.30f, damage = 35, lifeTicks = 24, color = 0xA5D8FF, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 160), new ItemStack(Circuit, 160), new ItemStack(Alloy, 95) }, buildTicks = 115 * 60, supply = 4,
        };
        public static readonly UnitDef Carrier = new UnitDef
        {
            key = "carrier", name = "모함", desc = "가장 큰 비행 유닛. 멀리서 범위 포격을 퍼붓는다.",
            tier = 5, flying = true, shape = "carrier", health = 13800, armor = 16, speed = 0.020f, radius = 0.76f, range = 7f, reloadTicks = 36, color = 0x0B4F8A, tech = Research.Tier5,
            bullet = new BulletDef { speed = 0.24f, damage = 215, splash = 1.5f, lifeTicks = 36, color = 0xD0EBFF, size = 0.36f },
            cost = new[] { new ItemStack(Steel, 440), new ItemStack(Circuit, 400), new ItemStack(Alloy, 320) }, buildTicks = 285 * 60, supply = 5,
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
            tier = 3, legs = true, shape = "legs", health = 1300, armor = 9, speed = 0.022f, radius = 0.46f, range = 3.2f, reloadTicks = 14, color = 0xD9480F, tech = Research.Tier3,
            drain = 0.4f,
            bullet = new BulletDef { speed = 0.40f, damage = 22, lifeTicks = 10, color = 0xDA77F2, size = 0.16f },
            cost = new[] { new ItemStack(Steel, 65), new ItemStack(Circuit, 50), new ItemStack(Alloy, 30) }, buildTicks = 50 * 60, supply = 3,
        };
        public static readonly UnitDef Shatter = new UnitDef
        {
            key = "shatter", name = "파편 로봇", desc = "벽을 타 넘는 4단계 다리 로봇. 곡사 포탄이 터지면서 파편 6개가 폭발 범위 밖으로 튄다. 파편은 공중의 적도 맞힌다.",
            tier = 4, legs = true, shape = "legs", health = 3900, armor = 12, speed = 0.016f, radius = 0.56f, range = 6.5f, reloadTicks = 70, color = 0xC2410C, tech = Research.Tier4,
            targetAir = false, buildingMul = 1.5f,
            bullet = new BulletDef
            {
                speed = 0.11f, damage = 130, splash = 1.5f, lifeTicks = 90, artillery = true, color = 0xFFC078, size = 0.3f, fragCount = 6,
                frag = new BulletDef { speed = 0.18f, damage = 41, lifeTicks = 12, color = 0xFFE8CC, size = 0.13f },
            },
            cost = new[] { new ItemStack(Steel, 175), new ItemStack(Circuit, 130), new ItemStack(Powder, 80), new ItemStack(Alloy, 70) }, buildTicks = 115 * 60, supply = 4,
        };
        public static readonly UnitDef Pyre = new UnitDef
        {
            key = "pyre", name = "화염 거미", desc = "벽을 타 넘는 5단계 다리 로봇. 가까이서 꿰뚫는 불길을 뿜어 적을 4초 동안 불태운다. 불은 방어력을 무시한다.",
            tier = 5, legs = true, shape = "legs", health = 12000, armor = 16, speed = 0.014f, radius = 0.70f, range = 3.6f, reloadTicks = 8, color = 0xA61E4D, tech = Research.Tier5,
            bullet = new BulletDef { speed = 0.20f, damage = 40, lifeTicks = 20, pierce = 4, burn = 54, burnTicks = 240, color = 0xFF922B, size = 0.24f },
            cost = new[] { new ItemStack(Steel, 440), new ItemStack(Circuit, 300), new ItemStack(Powder, 240), new ItemStack(Alloy, 260) }, buildTicks = 275 * 60, supply = 5,
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
            tier = 3, shape = "tank", health = 2500, armor = 15, speed = 0.015f, radius = 0.52f, range = 5f, reloadTicks = 55, color = 0x5C6F45, tech = Research.Tier3,
            targetAir = false, crush = 48,
            bullet = new BulletDef { speed = 0.22f, damage = 90, splash = 1.1f, lifeTicks = 30, color = 0xE9F5C9, size = 0.28f },
            cost = new[] { new ItemStack(Steel, 115), new ItemStack(Circuit, 50), new ItemStack(Alloy, 25) }, buildTicks = 55 * 60, supply = 3,
        };
        public static readonly UnitDef Bulwark = new UnitDef
        {
            key = "bulwark", name = "방호 전차", desc = "보호막을 두른 3단계 전차. 보호막 910 이 피해를 먼저 받고, 3초 동안 맞지 않으면 다시 차오른다.",
            tier = 3, shape = "tank", health = 2000, armor = 12, speed = 0.016f, radius = 0.50f, range = 4.2f, reloadTicks = 24, color = 0x5C7F8A, tech = Research.Tier3,
            barrier = 910, barrierRegen = 78, barrierDelay = 3 * 60, crush = 36,
            bullet = new BulletDef { speed = 0.26f, damage = 24, lifeTicks = 22, color = 0xC5F6FA, size = 0.17f },
            cost = new[] { new ItemStack(Steel, 105), new ItemStack(Circuit, 80), new ItemStack(Alloy, 30) }, buildTicks = 55 * 60, supply = 3,
        };
        public static readonly UnitDef PiercerTank = new UnitDef
        {
            key = "piercertank", name = "철갑 전차", desc = "4단계 전차. 멀리서 방어력을 무시하는 철갑탄을 쏜다. 느리게 쏘고 공중은 못 쏜다.",
            tier = 4, shape = "tank", health = 5400, armor = 18, speed = 0.013f, radius = 0.58f, range = 7f, reloadTicks = 100, color = 0x485737, tech = Research.Tier4,
            targetAir = false, buildingMul = 1.5f, crush = 87,
            bullet = new BulletDef { speed = 0.45f, damage = 465, lifeTicks = 20, pierceArmor = true, color = 0xFFF3BF, size = 0.26f },
            cost = new[] { new ItemStack(Steel, 240), new ItemStack(Circuit, 130), new ItemStack(Powder, 65), new ItemStack(Alloy, 95) }, buildTicks = 125 * 60, supply = 4,
        };
        public static readonly UnitDef Juggernaut = new UnitDef
        {
            key = "juggernaut", name = "분쇄 전차", desc = "가장 단단한 5단계 전차. 범위 피해 주포를 쏘며 닿은 적을 갈아 버린다.",
            tier = 5, shape = "tank", health = 18400, armor = 24, speed = 0.010f, radius = 0.76f, range = 5.5f, reloadTicks = 26, color = 0x364228, tech = Research.Tier5,
            crush = 270,
            bullet = new BulletDef { speed = 0.22f, damage = 170, splash = 1.3f, lifeTicks = 32, color = 0xE9F5C9, size = 0.34f },
            cost = new[] { new ItemStack(Steel, 600), new ItemStack(Circuit, 320), new ItemStack(Alloy, 300) }, buildTicks = 300 * 60, supply = 5,
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
            tier = 3, naval = true, shape = "boat", health = 3600, armor = 14, speed = 0.016f, radius = 0.56f, range = 9f, reloadTicks = 60, color = 0x0B7285, tech = Research.Tier3,
            targetAir = false, buildingMul = 1.5f,
            bullet = new BulletDef { speed = 0.11f, damage = 155, splash = 1.8f, lifeTicks = 100, artillery = true, color = 0xE3FAFC, size = 0.32f },
            cost = new[] { new ItemStack(Steel, 145), new ItemStack(Circuit, 105), new ItemStack(Alloy, 50) }, buildTicks = 70 * 60, supply = 3,
        };
        public static readonly UnitDef Shockboat = new UnitDef
        {
            key = "shockboat", name = "충격함", desc = "3단계 함정. 충격탄이 터지면서 범위 안의 적을 0.9칸 뒤로 밀어낸다. 보스는 절반만 밀린다.",
            tier = 3, naval = true, shape = "boat", health = 2900, armor = 12, speed = 0.018f, radius = 0.50f, range = 7.5f, reloadTicks = 45, color = 0x22B8CF, tech = Research.Tier3,
            bullet = new BulletDef { speed = 0.30f, damage = 60, splash = 1.2f, lifeTicks = 30, knockback = 0.9f, color = 0xFFFFFF, size = 0.24f },
            cost = new[] { new ItemStack(Steel, 115), new ItemStack(Circuit, 90), new ItemStack(Powder, 50), new ItemStack(Alloy, 40) }, buildTicks = 65 * 60, supply = 3,
        };
        public static readonly UnitDef Arcship = new UnitDef
        {
            key = "arcship", name = "방전함", desc = "4단계 함정. 포 대신 전류를 뿜어 5.5칸 안의 적 8기를 지상·공중 가리지 않고 한꺼번에 치고, 곁의 아군을 고친다.",
            tier = 4, naval = true, shape = "boat", health = 7100, armor = 16, speed = 0.014f, radius = 0.62f, range = 5.5f, reloadTicks = 40, color = 0x15AABF, tech = Research.Tier4,
            fieldDamage = 58, fieldTargets = 8, heal = 12, healRange = 4f,
            cost = new[] { new ItemStack(Steel, 240), new ItemStack(Circuit, 190), new ItemStack(Alloy, 110) }, buildTicks = 135 * 60, supply = 4,
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
            tier = 5, naval = true, shape = "boat", health = 16100, armor = 20, speed = 0.012f, radius = 0.74f, range = 10f, reloadTicks = 90, color = 0x0C8599, tech = Research.Tier5,
            buildingMul = 1.5f, spawns = Escort, spawnMax = 2, spawnTicks = 15 * 60,
            bullet = new BulletDef { speed = 0.5f, damage = 305, lifeTicks = 24, pierce = 5, color = 0xE3FAFC, size = 0.3f },
            cost = new[] { new ItemStack(Steel, 520), new ItemStack(Circuit, 400), new ItemStack(Alloy, 340) }, buildTicks = 310 * 60, supply = 5,
        };

        // ───────── 공장마다 12종이 되도록 더한 유닛 ─────────

        // 로봇 공장 (추가)
        public static readonly UnitDef Torch = new UnitDef
        {
            key = "torch", name = "화염 로봇", desc = "가까이서 불길을 뿜는 2단계 로봇. 맞은 적은 3초 동안 불탄다.",
            tier = 2, health = 380, armor = 4, speed = 0.026f, radius = 0.34f, range = 2.3f, reloadTicks = 8, color = 0xFF8787, tech = Research.Tier2,
            bullet = new BulletDef { speed = 0.18f, damage = 6, lifeTicks = 14, pierce = 2, burn = 14, burnTicks = 180, color = 0xFF922B, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 30), new ItemStack(Circuit, 15), new ItemStack(Powder, 15) }, buildTicks = 16 * 60, supply = 2,
        };
        public static readonly UnitDef Sniper = new UnitDef
        {
            key = "sniper", name = "저격 로봇", desc = "아주 멀리서 한 발씩 쏘는 2단계 로봇. 느리게 쏘고 몸은 약하다.",
            tier = 2, health = 240, armor = 2, speed = 0.022f, radius = 0.30f, range = 8f, reloadTicks = 95, color = 0x20C997, tech = Research.Tier2,
            bullet = new BulletDef { speed = 0.6f, damage = 95, lifeTicks = 16, color = 0xE6FCF5, size = 0.16f },
            cost = new[] { new ItemStack(Steel, 25), new ItemStack(Circuit, 35) }, buildTicks = 18 * 60, supply = 2,
        };
        public static readonly UnitDef Breacher = new UnitDef
        {
            key = "breacher", name = "돌파 로봇", desc = "적 건물을 부수는 3단계 로봇. 붙어서 치고 건물에 3배 피해를 준다. 공중은 못 쏜다.",
            tier = 3, health = 2200, armor = 12, speed = 0.020f, radius = 0.46f, range = 1.8f, reloadTicks = 30, color = 0x0CA678, tech = Research.Tier3,
            targetAir = false, buildingMul = 3f,
            bullet = new BulletDef { speed = 0.2f, damage = 54, lifeTicks = 12, color = 0xFFE066, size = 0.24f },
            cost = new[] { new ItemStack(Steel, 105), new ItemStack(Circuit, 40), new ItemStack(Alloy, 25) }, buildTicks = 50 * 60, supply = 3,
        };
        public static readonly UnitDef Suppressor = new UnitDef
        {
            key = "suppressor", name = "제압 로봇", desc = "쉬지 않고 쏘는 4단계 로봇. 맞은 적은 1초 동안 30% 느려진다.",
            tier = 4, health = 4100, armor = 12, speed = 0.016f, radius = 0.54f, range = 5.2f, reloadTicks = 6, color = 0x099268, tech = Research.Tier4,
            bullet = new BulletDef { speed = 0.3f, damage = 23, lifeTicks = 20, slow = 0.7f, slowTicks = 60, color = 0xA9E34B, size = 0.15f },
            cost = new[] { new ItemStack(Steel, 175), new ItemStack(Circuit, 175), new ItemStack(Alloy, 90) }, buildTicks = 115 * 60, supply = 4,
        };
        public static readonly UnitDef Warlord = new UnitDef
        {
            key = "warlord", name = "유도 거신", desc = "보호막을 두르고 범위 유도탄을 쏘는 5단계 로봇.",
            tier = 5, health = 12000, armor = 16, speed = 0.012f, radius = 0.70f, range = 7.5f, reloadTicks = 40, color = 0x087F5B, tech = Research.Tier5,
            barrier = 3400, barrierRegen = 185, barrierDelay = 4 * 60,
            bullet = new BulletDef { speed = 0.2f, damage = 270, splash = 1.4f, lifeTicks = 60, homing = 0.12f, color = 0xFFD8A8, size = 0.3f },
            cost = new[] { new ItemStack(Steel, 480), new ItemStack(Circuit, 400), new ItemStack(Powder, 160), new ItemStack(Alloy, 280) }, buildTicks = 285 * 60, supply = 5,
        };

        // 비행장 (추가)
        public static readonly UnitDef Interceptor = new UnitDef
        {
            key = "interceptor", name = "요격기", desc = "공중의 적만 쏘는 빠른 2단계 비행 유닛.",
            tier = 2, health = 260, armor = 2, speed = 0.065f, radius = 0.30f, range = 3.6f, reloadTicks = 10, color = 0xA5D8FF, tech = Research.Tier2,
            flying = true, targetGround = false,
            bullet = new BulletDef { speed = 0.34f, damage = 14, lifeTicks = 14, color = 0xE7F5FF, size = 0.13f },
            cost = new[] { new ItemStack(Steel, 20), new ItemStack(Circuit, 30) }, buildTicks = 16 * 60, supply = 2,
        };
        public static readonly UnitDef Thumper = new UnitDef
        {
            key = "thumper", name = "충격기", desc = "충격탄으로 적을 뒤로 밀어내는 2단계 비행 유닛.",
            tier = 2, health = 300, armor = 3, speed = 0.042f, radius = 0.34f, range = 3.4f, reloadTicks = 40, color = 0x91A7FF, tech = Research.Tier2,
            flying = true,
            bullet = new BulletDef { speed = 0.26f, damage = 22, splash = 0.9f, lifeTicks = 16, knockback = 0.6f, color = 0xFFFFFF, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 30), new ItemStack(Circuit, 25), new ItemStack(Powder, 10) }, buildTicks = 18 * 60, supply = 2,
        };
        public static readonly UnitDef Firebomber = new UnitDef
        {
            key = "firebomber", name = "소이 폭격기", desc = "불타는 폭탄을 떨어뜨리는 3단계 폭격기. 범위 안 지상의 적이 4초 동안 불탄다. 공중은 못 쏜다.",
            tier = 3, health = 990, armor = 5, speed = 0.038f, radius = 0.44f, range = 0.8f, reloadTicks = 75, color = 0xFF8787, tech = Research.Tier3,
            flying = true, targetAir = false,
            bullet = new BulletDef { speed = 0.05f, damage = 60, splash = 1.8f, lifeTicks = 30, artillery = true, burn = 26, burnTicks = 240, color = 0xFF922B, size = 0.26f },
            cost = new[] { new ItemStack(Steel, 60), new ItemStack(Circuit, 60), new ItemStack(Powder, 65), new ItemStack(Alloy, 20) }, buildTicks = 50 * 60, supply = 3,
        };
        public static readonly UnitDef Stormwing = new UnitDef
        {
            key = "stormwing", name = "전격기", desc = "적에서 적으로 다섯 번 건너뛰는 전격을 쏘는 4단계 비행 유닛.",
            tier = 4, health = 3200, armor = 9, speed = 0.030f, radius = 0.56f, range = 5f, reloadTicks = 28, color = 0x3BC9DB, tech = Research.Tier4,
            flying = true,
            bullet = new BulletDef { speed = 0.5f, damage = 87, lifeTicks = 12, chain = 5, chainRange = 2.4f, color = 0xC5F6FA, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 145), new ItemStack(Circuit, 190), new ItemStack(Alloy, 95) }, buildTicks = 115 * 60, supply = 4,
        };
        public static readonly UnitDef Skyfort = new UnitDef
        {
            key = "skyfort", name = "공중 요새", desc = "보호막을 두른 5단계 비행 유닛. 12초마다 함재기를 띄워 셋까지 거느린다.",
            tier = 5, health = 11500, armor = 15, speed = 0.018f, radius = 0.78f, range = 6f, reloadTicks = 22, color = 0x4263EB, tech = Research.Tier5,
            flying = true, shape = "carrier", barrier = 4600, barrierRegen = 230, barrierDelay = 4 * 60, spawns = Escort, spawnMax = 3, spawnTicks = 12 * 60,
            bullet = new BulletDef { speed = 0.3f, damage = 99, lifeTicks = 24, color = 0xDBE4FF, size = 0.22f },
            cost = new[] { new ItemStack(Steel, 480), new ItemStack(Circuit, 440), new ItemStack(Alloy, 340) }, buildTicks = 300 * 60, supply = 5,
        };

        // 지원 공방 (추가)
        public static readonly UnitDef Medic = new UnitDef
        {
            key = "medic", name = "야전 정비 로봇", desc = "4.5칸 안의 아군 유닛과 건물을 빠르게 고치는 3단계 로봇.",
            tier = 3, health = 1800, armor = 9, speed = 0.020f, radius = 0.44f, range = 3.2f, reloadTicks = 30, color = 0x51CF66, tech = Research.Tier3,
            shape = "support", heal = 48, healRange = 4.5f,
            bullet = new BulletDef { speed = 0.25f, damage = 12, lifeTicks = 16, color = 0xD3F9D8, size = 0.14f },
            cost = new[] { new ItemStack(Steel, 90), new ItemStack(Circuit, 90), new ItemStack(Alloy, 25) }, buildTicks = 50 * 60, supply = 3,
        };
        public static readonly UnitDef BoostDrone = new UnitDef
        {
            key = "boostdrone", name = "증폭 드론", desc = "날아다니며 4.5칸 안의 다른 아군 유닛을 1.33배 빨리 쏘게 하는 3단계 드론.",
            tier = 3, health = 550, armor = 2, speed = 0.045f, radius = 0.28f, range = 3.5f, reloadTicks = 30, color = 0xFAB005, tech = Research.Tier3,
            flying = true, shape = "drone", boostRange = 4.5f,
            bullet = new BulletDef { speed = 0.26f, damage = 6, lifeTicks = 16, color = 0xFFF3BF, size = 0.12f },
            cost = new[] { new ItemStack(Steel, 65), new ItemStack(Circuit, 90), new ItemStack(Alloy, 20) }, buildTicks = 50 * 60, supply = 3,
        };
        public static readonly UnitDef Excavator = new UnitDef
        {
            key = "excavator", name = "굴착 드론", desc = "큰 채굴 드론. 벽을 6초 캐서 철광 30(세 번에 한 번은 황석 15)을 핵으로 나른다. 싸우지 못한다.",
            tier = 3, health = 550, armor = 3, speed = 0.040f, radius = 0.34f, color = 0xB08968, tech = Research.Tier3,
            flying = true, shape = "drone", mine = 30, mineTicks = 6 * 60,
            cost = new[] { new ItemStack(Steel, 80), new ItemStack(Circuit, 65), new ItemStack(Alloy, 20) }, buildTicks = 45 * 60, supply = 3,
        };
        public static readonly UnitDef Aegis = new UnitDef
        {
            key = "aegis", name = "수호 로봇", desc = "4.5칸 안의 아군 유닛이 받는 피해를 절반으로 줄이는 4단계 로봇. 자기도 보호막을 두른다.",
            tier = 4, health = 4400, armor = 14, speed = 0.016f, radius = 0.54f, range = 3f, reloadTicks = 36, color = 0x2B8A3E, tech = Research.Tier4,
            shape = "support", shieldRange = 4.5f, shieldMul = 0.5f, barrier = 2000, barrierRegen = 120, barrierDelay = 4 * 60,
            bullet = new BulletDef { speed = 0.24f, damage = 20, lifeTicks = 16, color = 0xD3F9D8, size = 0.16f },
            cost = new[] { new ItemStack(Steel, 210), new ItemStack(Circuit, 145), new ItemStack(Alloy, 95) }, buildTicks = 115 * 60, supply = 4,
        };
        public static readonly UnitDef Hauler = new UnitDef
        {
            key = "hauler", name = "대형 수송기", desc = "걸어가는 유닛을 한도 14칸까지 실어 나르는 4단계 수송기.",
            tier = 4, health = 3700, armor = 10, speed = 0.034f, radius = 0.80f, color = 0x1B7A36, tech = Research.Tier4,
            flying = true, shape = "transport", capacity = 14,
            cost = new[] { new ItemStack(Steel, 225), new ItemStack(Circuit, 145), new ItemStack(Alloy, 65) }, buildTicks = 105 * 60, supply = 4,
        };

        // 다리 공방 (추가)
        public static readonly UnitDef Hopper = new UnitDef
        {
            key = "hopper", name = "도약 로봇", desc = "값싼 1단계 다리 로봇. 약하지만 벽을 타 넘는다.",
            tier = 1, health = 150, armor = 1, speed = 0.034f, radius = 0.26f, range = 3f, reloadTicks = 22, color = 0xFFD43B,
            legs = true, shape = "legs",
            bullet = new BulletDef { speed = 0.25f, damage = 8, lifeTicks = 16, color = 0xFFF3BF, size = 0.13f },
            cost = new[] { new ItemStack(Iron, 30), new ItemStack(Circuit, 10) }, buildTicks = 9 * 60, supply = 1,
        };
        public static readonly UnitDef Slimebug = new UnitDef
        {
            key = "slimebug", name = "산성 벌레", desc = "붙어서 터지는 2단계 벌레. 넓은 범위의 적을 4초 동안 60% 늦춘다.",
            tier = 2, health = 200, armor = 0, speed = 0.045f, radius = 0.28f, range = 0.3f, reloadTicks = 0, color = 0x94D82D, tech = Research.Tier2,
            shape = "bug", suicide = true,
            bullet = new BulletDef { damage = 60, splash = 2.2f, slow = 0.4f, slowTicks = 240, color = 0xA9E34B },
            cost = new[] { new ItemStack(Steel, 15), new ItemStack(Circuit, 10), new ItemStack(Powder, 15) }, buildTicks = 12 * 60, supply = 2,
        };
        public static readonly UnitDef Blaster = new UnitDef
        {
            key = "blaster", name = "폭뢰 벌레", desc = "크게 터지는 3단계 벌레. 건물에 3배 피해.",
            tier = 3, health = 680, armor = 4, speed = 0.040f, radius = 0.36f, range = 0.3f, reloadTicks = 0, color = 0xF59F00, tech = Research.Tier3,
            shape = "bug", suicide = true, buildingMul = 3f,
            bullet = new BulletDef { damage = 505, splash = 2.6f, color = 0xFFD43B },
            cost = new[] { new ItemStack(Steel, 50), new ItemStack(Powder, 90), new ItemStack(Alloy, 20) }, buildTicks = 35 * 60, supply = 3,
        };
        public static readonly UnitDef Stalker = new UnitDef
        {
            key = "stalker", name = "저격 거미", desc = "벽을 타 넘는 3단계 다리 로봇. 멀리서 방어력을 무시하는 한 발을 쏜다. 공중은 못 쏜다.",
            tier = 3, health = 910, armor = 6, speed = 0.022f, radius = 0.40f, range = 7.5f, reloadTicks = 85, color = 0xE8590C, tech = Research.Tier3,
            legs = true, shape = "legs", targetAir = false,
            bullet = new BulletDef { speed = 0.5f, damage = 155, lifeTicks = 18, pierceArmor = true, color = 0xFFF3BF, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 65), new ItemStack(Circuit, 70), new ItemStack(Alloy, 30) }, buildTicks = 50 * 60, supply = 3,
        };
        public static readonly UnitDef Sparker = new UnitDef
        {
            key = "sparker", name = "전격 거미", desc = "벽을 타 넘는 4단계 다리 로봇. 적에서 적으로 네 번 건너뛰는 전격을 쏜다.",
            tier = 4, health = 3600, armor = 11, speed = 0.018f, radius = 0.54f, range = 4.6f, reloadTicks = 24, color = 0xD9480F, tech = Research.Tier4,
            legs = true, shape = "legs",
            bullet = new BulletDef { speed = 0.5f, damage = 84, lifeTicks = 12, chain = 4, chainRange = 2.4f, color = 0xC5F6FA, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 160), new ItemStack(Circuit, 190), new ItemStack(Alloy, 80) }, buildTicks = 115 * 60, supply = 4,
        };
        public static readonly UnitDef Devourer = new UnitDef
        {
            key = "devourer", name = "포식 거미", desc = "벽을 타 넘는 5단계 다리 로봇. 범위 피해를 주고, 직격으로 준 피해의 35% 만큼 스스로를 고친다.",
            tier = 5, health = 13800, armor = 18, speed = 0.013f, radius = 0.72f, range = 4.2f, reloadTicks = 20, color = 0x862E9C, tech = Research.Tier5,
            legs = true, shape = "legs", drain = 0.35f,
            bullet = new BulletDef { speed = 0.24f, damage = 125, splash = 1.2f, lifeTicks = 20, color = 0xDA77F2, size = 0.26f },
            cost = new[] { new ItemStack(Steel, 480), new ItemStack(Circuit, 340), new ItemStack(Alloy, 320) }, buildTicks = 285 * 60, supply = 5,
        };

        // 조선소 (추가)
        public static readonly UnitDef Tender = new UnitDef
        {
            key = "tender", name = "수리정", desc = "5칸 안의 아군 유닛과 건물을 고치는 2단계 함정.",
            tier = 2, health = 700, armor = 6, speed = 0.024f, radius = 0.38f, range = 5f, reloadTicks = 30, color = 0x63E6BE, tech = Research.Tier2,
            naval = true, shape = "boat", heal = 18, healRange = 5f,
            bullet = new BulletDef { speed = 0.26f, damage = 9, lifeTicks = 22, color = 0xC3FAE8, size = 0.13f },
            cost = new[] { new ItemStack(Steel, 45), new ItemStack(Circuit, 40) }, buildTicks = 20 * 60, supply = 2,
        };
        public static readonly UnitDef Missileboat = new UnitDef
        {
            key = "missileboat", name = "유도탄정", desc = "멀리서 유도탄을 쏘는 2단계 함정.",
            tier = 2, health = 620, armor = 5, speed = 0.024f, radius = 0.38f, range = 7.5f, reloadTicks = 60, color = 0x66D9E8, tech = Research.Tier2,
            naval = true, shape = "boat",
            bullet = new BulletDef { speed = 0.2f, damage = 48, splash = 0.9f, lifeTicks = 60, homing = 0.12f, color = 0xFFD8A8, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 40), new ItemStack(Circuit, 40), new ItemStack(Powder, 20) }, buildTicks = 22 * 60, supply = 2,
        };
        public static readonly UnitDef Mireship = new UnitDef
        {
            key = "mireship", name = "산성함", desc = "산성 곡사포를 쏘는 3단계 함정. 넓은 범위 지상의 적을 3초 넘게 절반 속도로 늦춘다. 공중은 못 쏜다.",
            tier = 3, health = 2600, armor = 11, speed = 0.017f, radius = 0.50f, range = 8.5f, reloadTicks = 70, color = 0x82C91E, tech = Research.Tier3,
            naval = true, shape = "boat", targetAir = false,
            bullet = new BulletDef { speed = 0.11f, damage = 54, splash = 1.9f, lifeTicks = 100, artillery = true, slow = 0.5f, slowTicks = 200, color = 0xA9E34B, size = 0.28f },
            cost = new[] { new ItemStack(Steel, 115), new ItemStack(Circuit, 90), new ItemStack(Powder, 40), new ItemStack(Alloy, 30) }, buildTicks = 65 * 60, supply = 3,
        };
        public static readonly UnitDef Wardship = new UnitDef
        {
            key = "wardship", name = "방패함", desc = "5칸 안의 아군 유닛이 받는 피해를 40% 줄이는 3단계 함정. 자기도 보호막을 두른다.",
            tier = 3, health = 3100, armor = 13, speed = 0.016f, radius = 0.52f, range = 5.5f, reloadTicks = 34, color = 0x15AABF, tech = Research.Tier3,
            naval = true, shape = "boat", shieldRange = 5f, shieldMul = 0.6f, barrier = 1000, barrierRegen = 78, barrierDelay = 4 * 60,
            bullet = new BulletDef { speed = 0.26f, damage = 26, lifeTicks = 24, color = 0xC5F6FA, size = 0.16f },
            cost = new[] { new ItemStack(Steel, 145), new ItemStack(Circuit, 90), new ItemStack(Alloy, 45) }, buildTicks = 65 * 60, supply = 3,
        };
        public static readonly UnitDef Lancership = new UnitDef
        {
            key = "lancership", name = "철갑함", desc = "아주 멀리서 방어력을 무시하는 철갑탄을 쏘는 4단계 함정. 적 셋까지 꿰뚫는다. 공중은 못 쏜다.",
            tier = 4, health = 5800, armor = 15, speed = 0.014f, radius = 0.60f, range = 9.5f, reloadTicks = 110, color = 0x1098AD, tech = Research.Tier4,
            naval = true, shape = "boat", targetAir = false, buildingMul = 1.5f,
            bullet = new BulletDef { speed = 0.5f, damage = 405, lifeTicks = 22, pierce = 2, pierceArmor = true, color = 0xFFF3BF, size = 0.26f },
            cost = new[] { new ItemStack(Steel, 255), new ItemStack(Circuit, 175), new ItemStack(Powder, 80), new ItemStack(Alloy, 110) }, buildTicks = 135 * 60, supply = 4,
        };
        public static readonly UnitDef Infernoship = new UnitDef
        {
            key = "infernoship", name = "화염 전함", desc = "불타는 곡사포를 쏘는 5단계 함정. 포탄이 터지며 파편 8개가 튀고, 범위 안 지상의 적이 불탄다. 공중은 못 겨눈다.",
            tier = 5, health = 14900, armor = 19, speed = 0.012f, radius = 0.74f, range = 9f, reloadTicks = 55, color = 0xC92A2A, tech = Research.Tier5,
            naval = true, shape = "boat", targetAir = false,
            bullet = new BulletDef { speed = 0.11f, damage = 200, splash = 2.0f, lifeTicks = 100, artillery = true, burn = 63, burnTicks = 240, color = 0xFF922B, size = 0.34f, fragCount = 8, frag = new BulletDef { speed = 0.18f, damage = 54, lifeTicks = 12, color = 0xFFE8CC, size = 0.13f } },
            cost = new[] { new ItemStack(Steel, 500), new ItemStack(Circuit, 380), new ItemStack(Powder, 240), new ItemStack(Alloy, 320) }, buildTicks = 305 * 60, supply = 5,
        };

        // 전차 공장 (추가)
        public static readonly UnitDef Flametank = new UnitDef
        {
            key = "flametank", name = "화염 전차", desc = "가까이서 불길을 뿜는 2단계 전차. 맞은 적은 3초 동안 불탄다.",
            tier = 2, health = 700, armor = 9, speed = 0.022f, radius = 0.40f, range = 2.4f, reloadTicks = 8, color = 0xB5651D, tech = Research.Tier2,
            shape = "tank", crush = 25,
            bullet = new BulletDef { speed = 0.18f, damage = 7, lifeTicks = 15, pierce = 2, burn = 16, burnTicks = 180, color = 0xFF922B, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 50), new ItemStack(Circuit, 15), new ItemStack(Powder, 20) }, buildTicks = 18 * 60, supply = 2,
        };
        public static readonly UnitDef Flaktank = new UnitDef
        {
            key = "flaktank", name = "대공 전차", desc = "공중의 적만 쏘는 2단계 전차. 터지는 탄으로 뭉친 비행체를 함께 잡는다.",
            tier = 2, health = 560, armor = 8, speed = 0.022f, radius = 0.40f, range = 6f, reloadTicks = 26, color = 0x7C8B99, tech = Research.Tier2,
            shape = "tank", targetGround = false, crush = 15,
            bullet = new BulletDef { speed = 0.34f, damage = 26, splash = 1.1f, lifeTicks = 20, color = 0xE7F5FF, size = 0.18f },
            cost = new[] { new ItemStack(Steel, 45), new ItemStack(Circuit, 30) }, buildTicks = 18 * 60, supply = 2,
        };
        public static readonly UnitDef Howitzer = new UnitDef
        {
            key = "howitzer", name = "자주포", desc = "아주 멀리 곡사포를 쏘는 3단계 전차. 장갑이 얇고 공중은 못 쏜다. 적 건물에 1.5배.",
            tier = 3, health = 1400, armor = 9, speed = 0.014f, radius = 0.50f, range = 8.5f, reloadTicks = 90, color = 0x6B705C, tech = Research.Tier3,
            shape = "tank", targetAir = false, buildingMul = 1.5f, crush = 30,
            bullet = new BulletDef { speed = 0.11f, damage = 145, splash = 1.8f, lifeTicks = 100, artillery = true, color = 0xFFE8A3, size = 0.32f },
            cost = new[] { new ItemStack(Steel, 105), new ItemStack(Circuit, 65), new ItemStack(Powder, 50), new ItemStack(Alloy, 25) }, buildTicks = 55 * 60, supply = 3,
        };
        public static readonly UnitDef Ramtank = new UnitDef
        {
            key = "ramtank", name = "충각 전차", desc = "빠르게 달려들어 짓밟는 3단계 전차. 충격탄으로 적을 밀어낸다.",
            tier = 3, health = 2700, armor = 14, speed = 0.024f, radius = 0.50f, range = 2.6f, reloadTicks = 40, color = 0x8D6E63, tech = Research.Tier3,
            shape = "tank", crush = 110,
            bullet = new BulletDef { speed = 0.26f, damage = 48, splash = 0.8f, lifeTicks = 12, knockback = 0.7f, color = 0xFFFFFF, size = 0.22f },
            cost = new[] { new ItemStack(Steel, 130), new ItemStack(Circuit, 50), new ItemStack(Alloy, 30) }, buildTicks = 55 * 60, supply = 3,
        };
        public static readonly UnitDef Workshop = new UnitDef
        {
            key = "workshop", name = "정비 전차", desc = "3.5칸 안의 아군 유닛과 건물을 고치는 3단계 전차.",
            tier = 3, health = 2200, armor = 13, speed = 0.016f, radius = 0.50f, range = 3.4f, reloadTicks = 30, color = 0x5C940D, tech = Research.Tier3,
            shape = "tank", heal = 30, healRange = 3.5f, crush = 36,
            bullet = new BulletDef { speed = 0.26f, damage = 14, lifeTicks = 16, color = 0xD3F9D8, size = 0.15f },
            cost = new[] { new ItemStack(Steel, 105), new ItemStack(Circuit, 80), new ItemStack(Alloy, 25) }, buildTicks = 55 * 60, supply = 3,
        };
        public static readonly UnitDef Teslatank = new UnitDef
        {
            key = "teslatank", name = "방전 전차", desc = "포 대신 전류로 4칸 안의 적 6기를 한꺼번에 치는 4단계 전차.",
            tier = 4, health = 5100, armor = 17, speed = 0.013f, radius = 0.58f, range = 4f, reloadTicks = 40, color = 0x3D5A80, tech = Research.Tier4,
            shape = "tank", fieldDamage = 65, fieldTargets = 6, crush = 87,
            cost = new[] { new ItemStack(Steel, 225), new ItemStack(Circuit, 210), new ItemStack(Alloy, 95) }, buildTicks = 125 * 60, supply = 4,
        };

        // 부양 공방: 떠서 달려 호수를 가로지르는 유닛. 빠르고 얇다
        public static readonly UnitDef Dart = new UnitDef
        {
            key = "dart", name = "부양 척후", desc = "값싸고 아주 빠른 1단계 부양 유닛.",
            tier = 1, hover = true, shape = "hover", health = 110, armor = 1, speed = 0.046f, radius = 0.26f, range = 3f, reloadTicks = 20, color = 0x96F2D7,
            bullet = new BulletDef { speed = 0.27f, damage = 7, lifeTicks = 16, color = 0xC3FAE8, size = 0.13f },
            cost = new[] { new ItemStack(Iron, 25), new ItemStack(Circuit, 10) }, buildTicks = 8 * 60, supply = 1,
        };
        public static readonly UnitDef Patch = new UnitDef
        {
            key = "patch", name = "부양 정비", desc = "3칸 안의 아군 유닛과 건물을 고치는 1단계 부양 유닛.",
            tier = 1, hover = true, shape = "hover", health = 180, armor = 1, speed = 0.040f, radius = 0.28f, range = 3f, reloadTicks = 30, color = 0x8CE99A,
            heal = 10, healRange = 3f,
            bullet = new BulletDef { speed = 0.25f, damage = 4, lifeTicks = 16, color = 0xD3F9D8, size = 0.12f },
            cost = new[] { new ItemStack(Iron, 35), new ItemStack(Circuit, 15) }, buildTicks = 12 * 60, supply = 1,
        };
        public static readonly UnitDef Minelayer = new UnitDef
        {
            key = "minelayer", name = "기뢰 부설기", desc = "싸우지 않고 돌아다니며 5초마다 지뢰를 깐다 (다섯 개까지). 지상의 적이 밟으면 터진다.",
            tier = 2, hover = true, shape = "hover", health = 340, armor = 3, speed = 0.040f, radius = 0.32f, color = 0xFFA94D, tech = Research.Tier2,
            mineEvery = 5 * 60, mineMax = 5, minelay = new BulletDef { damage = 70, splash = 1.4f, color = 0xFFA94D },
            cost = new[] { new ItemStack(Steel, 30), new ItemStack(Circuit, 20), new ItemStack(Powder, 25) }, buildTicks = 18 * 60, supply = 2,
        };
        public static readonly UnitDef Lancer = new UnitDef
        {
            key = "lancer", name = "부양 포수", desc = "사거리가 긴 2단계 부양 유닛.",
            tier = 2, hover = true, shape = "hover", health = 360, armor = 4, speed = 0.038f, radius = 0.32f, range = 4.5f, reloadTicks = 28, color = 0x38D9A9, tech = Research.Tier2,
            bullet = new BulletDef { speed = 0.3f, damage = 24, lifeTicks = 18, color = 0xC3FAE8, size = 0.16f },
            cost = new[] { new ItemStack(Steel, 30), new ItemStack(Circuit, 25) }, buildTicks = 16 * 60, supply = 2,
        };
        public static readonly UnitDef Scorch = new UnitDef
        {
            key = "scorch", name = "부양 화염", desc = "빠르게 붙어 불길을 뿜는 2단계 부양 유닛. 맞은 적은 3초 동안 불탄다.",
            tier = 2, hover = true, shape = "hover", health = 420, armor = 4, speed = 0.042f, radius = 0.32f, range = 2.3f, reloadTicks = 8, color = 0xFF8787, tech = Research.Tier2,
            bullet = new BulletDef { speed = 0.18f, damage = 6, lifeTicks = 14, pierce = 2, burn = 14, burnTicks = 180, color = 0xFF922B, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 30), new ItemStack(Circuit, 15), new ItemStack(Powder, 15) }, buildTicks = 16 * 60, supply = 2,
        };
        public static readonly UnitDef Bumper = new UnitDef
        {
            key = "bumper", name = "부양 충격", desc = "충격탄으로 적을 밀어내는 3단계 부양 유닛.",
            tier = 3, hover = true, shape = "hover", health = 1200, armor = 7, speed = 0.034f, radius = 0.42f, range = 4f, reloadTicks = 36, color = 0x74C0FC, tech = Research.Tier3,
            bullet = new BulletDef { speed = 0.26f, damage = 41, splash = 1.0f, lifeTicks = 18, knockback = 0.8f, color = 0xFFFFFF, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 70), new ItemStack(Circuit, 60), new ItemStack(Powder, 25), new ItemStack(Alloy, 20) }, buildTicks = 50 * 60, supply = 3,
        };
        public static readonly UnitDef Homer = new UnitDef
        {
            key = "homer", name = "부양 유도탄", desc = "멀리서 유도탄을 쏘는 3단계 부양 유닛.",
            tier = 3, hover = true, shape = "hover", health = 990, armor = 5, speed = 0.034f, radius = 0.42f, range = 6.5f, reloadTicks = 50, color = 0xFFD8A8, tech = Research.Tier3,
            bullet = new BulletDef { speed = 0.2f, damage = 74, splash = 1.0f, lifeTicks = 55, homing = 0.12f, color = 0xFFD8A8, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 60), new ItemStack(Circuit, 70), new ItemStack(Powder, 40), new ItemStack(Alloy, 20) }, buildTicks = 50 * 60, supply = 3,
        };
        public static readonly UnitDef Screen = new UnitDef
        {
            key = "screen", name = "부양 방패", desc = "3.5칸 안의 아군 유닛이 받는 피해를 35% 줄이는 3단계 부양 유닛. 자기도 보호막을 두른다.",
            tier = 3, hover = true, shape = "hover", health = 1400, armor = 8, speed = 0.032f, radius = 0.44f, range = 3f, reloadTicks = 36, color = 0x69DB7C, tech = Research.Tier3,
            shieldRange = 3.5f, shieldMul = 0.65f, barrier = 650, barrierRegen = 65, barrierDelay = 4 * 60,
            bullet = new BulletDef { speed = 0.24f, damage = 12, lifeTicks = 16, color = 0xD3F9D8, size = 0.14f },
            cost = new[] { new ItemStack(Steel, 90), new ItemStack(Circuit, 65), new ItemStack(Alloy, 25) }, buildTicks = 50 * 60, supply = 3,
        };
        public static readonly UnitDef Jolt = new UnitDef
        {
            key = "jolt", name = "부양 전격", desc = "적에서 적으로 네 번 건너뛰는 전격을 쏘는 4단계 부양 유닛.",
            tier = 4, hover = true, shape = "hover", health = 3200, armor = 10, speed = 0.030f, radius = 0.54f, range = 4.8f, reloadTicks = 26, color = 0x3BC9DB, tech = Research.Tier4,
            bullet = new BulletDef { speed = 0.5f, damage = 81, lifeTicks = 12, chain = 4, chainRange = 2.4f, color = 0xC5F6FA, size = 0.2f },
            cost = new[] { new ItemStack(Steel, 145), new ItemStack(Circuit, 190), new ItemStack(Alloy, 90) }, buildTicks = 115 * 60, supply = 4,
        };
        public static readonly UnitDef Lobber = new UnitDef
        {
            key = "lobber", name = "부양 포격", desc = "곡사 포탄이 터지며 파편 6개가 튀는 4단계 부양 유닛. 공중은 못 겨눈다.",
            tier = 4, hover = true, shape = "hover", health = 2900, armor = 9, speed = 0.028f, radius = 0.54f, range = 7.5f, reloadTicks = 75, color = 0xFFC078, tech = Research.Tier4,
            targetAir = false, buildingMul = 1.5f,
            bullet = new BulletDef { speed = 0.11f, damage = 125, splash = 1.5f, lifeTicks = 90, artillery = true, color = 0xFFC078, size = 0.3f, fragCount = 6, frag = new BulletDef { speed = 0.18f, damage = 38, lifeTicks = 12, color = 0xFFE8CC, size = 0.13f } },
            cost = new[] { new ItemStack(Steel, 160), new ItemStack(Circuit, 130), new ItemStack(Powder, 80), new ItemStack(Alloy, 70) }, buildTicks = 120 * 60, supply = 4,
        };
        public static readonly UnitDef Seeder = new UnitDef
        {
            key = "seeder", name = "산성 기뢰 부설기", desc = "싸우지 않고 4초마다 큰 산성 지뢰를 깐다 (여덟 개까지). 터지면 범위 안의 적이 3초 동안 절반 속도로 느려진다.",
            tier = 4, hover = true, shape = "hover", health = 2600, armor = 8, speed = 0.032f, radius = 0.50f, color = 0xA9E34B, tech = Research.Tier4,
            mineEvery = 4 * 60, mineMax = 8, minelay = new BulletDef { damage = 275, splash = 1.9f, slow = 0.5f, slowTicks = 180, color = 0xA9E34B },
            cost = new[] { new ItemStack(Steel, 145), new ItemStack(Circuit, 110), new ItemStack(Powder, 130), new ItemStack(Alloy, 55) }, buildTicks = 110 * 60, supply = 4,
        };
        public static readonly UnitDef Leviathan = new UnitDef
        {
            key = "leviathan", name = "부양 요새", desc = "보호막을 두르고 범위 포를 빠르게 쏘는 5단계 부양 유닛.",
            tier = 5, hover = true, shape = "hover", health = 12000, armor = 15, speed = 0.022f, radius = 0.72f, range = 6f, reloadTicks = 16, color = 0x12B886, tech = Research.Tier5,
            barrier = 3400, barrierRegen = 185, barrierDelay = 4 * 60,
            bullet = new BulletDef { speed = 0.3f, damage = 110, splash = 1.0f, lifeTicks = 22, color = 0xC3FAE8, size = 0.24f },
            cost = new[] { new ItemStack(Steel, 480), new ItemStack(Circuit, 380), new ItemStack(Alloy, 300) }, buildTicks = 285 * 60, supply = 5,
        };

        // 심연 조립소: 6·7단계의 첨단 유닛. 다른 공장의 5단계보다 훨씬 강하고, 심연 결정까지 드는 큰 비용이 든다. 모두 스스로 체력을 되찾는다
        public static readonly UnitDef Colossus = new UnitDef
        {
            key = "colossus", name = "심연 거신", desc = "6단계 로봇. 보호막 9000 을 두르고 큰 범위 포를 쏜다. 스스로 체력을 되찾는다.",
            tier = 6, health = 42000, armor = 28, speed = 0.010f, radius = 0.90f, range = 7f, reloadTicks = 22, color = 0x7048E8, tech = Research.Assembler,
            barrier = 9000, barrierRegen = 450, barrierDelay = 5 * 60, regen = 120,
            bullet = new BulletDef { speed = 0.26f, damage = 460, splash = 1.6f, lifeTicks = 30, color = 0xD0BFFF, size = 0.4f },
            cost = new[] { new ItemStack(Steel, 940), new ItemStack(Circuit, 860), new ItemStack(Alloy, 720), new ItemStack(Crystal, 360) }, buildTicks = 360 * 60, supply = 6,
        };
        public static readonly UnitDef Mothership = new UnitDef
        {
            key = "mothership", name = "심연 모함", desc = "6단계 비행 유닛. 보호막 9000, 건너뛰는 전격, 8초마다 띄우는 함재기 다섯. 스스로 체력을 되찾는다.",
            tier = 6, flying = true, shape = "carrier", health = 36000, armor = 22, speed = 0.016f, radius = 0.95f, range = 7f, reloadTicks = 14, color = 0x5F3DC4, tech = Research.Assembler,
            barrier = 9000, barrierRegen = 450, barrierDelay = 5 * 60, regen = 105, spawns = Escort, spawnMax = 5, spawnTicks = 8 * 60,
            bullet = new BulletDef { speed = 0.5f, damage = 200, lifeTicks = 16, chain = 4, chainRange = 2.6f, color = 0xE5DBFF, size = 0.24f },
            cost = new[] { new ItemStack(Steel, 900), new ItemStack(Circuit, 940), new ItemStack(Alloy, 720), new ItemStack(Crystal, 360) }, buildTicks = 360 * 60, supply = 6,
        };
        public static readonly UnitDef Ravager = new UnitDef
        {
            key = "ravager", name = "심연 거미", desc = "벽을 타 넘는 6단계 다리 유닛. 사거리 8.5 의 곡사 포탄이 범위 안을 불태우고 파편 10개로 갈라진다. 공중은 못 겨눈다. 스스로 체력을 되찾는다.",
            tier = 6, legs = true, shape = "legs", health = 39000, armor = 24, speed = 0.012f, radius = 0.88f, range = 8.5f, reloadTicks = 70, color = 0x9C36B5, tech = Research.Assembler,
            targetAir = false, buildingMul = 2f, regen = 120,
            bullet = new BulletDef
            {
                speed = 0.11f, damage = 705, splash = 2.2f, lifeTicks = 100, artillery = true, burn = 88, burnTicks = 240, color = 0xF783AC, size = 0.4f, fragCount = 10,
                frag = new BulletDef { speed = 0.18f, damage = 99, lifeTicks = 14, color = 0xFFDEEB, size = 0.15f },
            },
            cost = new[] { new ItemStack(Steel, 940), new ItemStack(Circuit, 810), new ItemStack(Powder, 340), new ItemStack(Alloy, 720), new ItemStack(Crystal, 360) }, buildTicks = 360 * 60, supply = 6,
        };
        public static readonly UnitDef Doomtank = new UnitDef
        {
            key = "doomtank", name = "종말 전차", desc = "7단계 전차. 사거리 9 에서 방어력을 무시하고 넷까지 꿰뚫는 철갑탄 2340 을 쏘고, 닿은 적을 초당 1040 으로 짓밟는다. 공중은 못 쏜다. 스스로 체력을 되찾는다.",
            tier = 7, shape = "tank", health = 105000, armor = 36, speed = 0.009f, radius = 1.0f, range = 9f, reloadTicks = 80, color = 0x343A40, tech = Research.Transcend,
            targetAir = false, buildingMul = 2f, crush = 1040, regen = 280,
            bullet = new BulletDef { speed = 0.5f, damage = 2340, lifeTicks = 20, pierce = 3, pierceArmor = true, color = 0xFFF3BF, size = 0.4f },
            cost = new[] { new ItemStack(Steel, 2080), new ItemStack(Circuit, 1730), new ItemStack(Alloy, 1610), new ItemStack(Crystal, 1030) }, buildTicks = 420 * 60, supply = 7,
        };
        public static readonly UnitDef Bastion = new UnitDef
        {
            key = "bastion", name = "심연 요새", desc = "떠서 다니는 7단계 유닛. 보호막 21000, 6칸 안의 적 12기를 한꺼번에 치는 전류, 6칸 안 아군의 수리와 피해 −40%. 스스로 체력을 되찾는다.",
            tier = 7, hover = true, shape = "hover", health = 77000, armor = 26, speed = 0.016f, radius = 0.95f, range = 6f, reloadTicks = 30, color = 0x1098AD, tech = Research.Transcend,
            fieldDamage = 310, fieldTargets = 12, heal = 105, healRange = 6f, shieldRange = 6f, shieldMul = 0.6f,
            barrier = 21000, barrierRegen = 875, barrierDelay = 5 * 60, regen = 210,
            cost = new[] { new ItemStack(Steel, 1960), new ItemStack(Circuit, 1850), new ItemStack(Alloy, 1610), new ItemStack(Crystal, 1030) }, buildTicks = 420 * 60, supply = 7,
        };
        public static readonly UnitDef Nemesis = new UnitDef
        {
            key = "nemesis", name = "천벌기", desc = "7단계 비행 유닛. 사거리 9 에서 범위 유도탄 675 를 잇달아 쏘고, 직격한 적을 불태운다. 스스로 체력을 되찾는다.",
            tier = 7, flying = true, shape = "carrier", health = 70000, armor = 28, speed = 0.018f, radius = 1.0f, range = 9f, reloadTicks = 24, color = 0xC2255C, tech = Research.Transcend,
            regen = 210,
            bullet = new BulletDef { speed = 0.22f, damage = 675, splash = 1.8f, lifeTicks = 60, homing = 0.14f, burn = 78, burnTicks = 180, color = 0xFFDEEB, size = 0.34f },
            cost = new[] { new ItemStack(Steel, 1960), new ItemStack(Circuit, 1900), new ItemStack(Alloy, 1610), new ItemStack(Crystal, 1030) }, buildTicks = 420 * 60, supply = 7,
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
            Scout, Assault, Skimmer, Heavy, Arcer, Siege, Titan, Recon, Bomber, Striker, Seeker, Mender, Warship, Carrier,
            Mechanic, Guardian, Booster, RepairDrone, Transport, Miner, Rebuilder, Bug, Climber, Spitter, Leech, Shatter, Pyre, Patrol, Gunboat, Destroyer, Shockboat, Arcship, Flagship, Escort, ArmoredCar, LightTank, HeavyTank, Bulwark, PiercerTank, Juggernaut,
            Torch, Sniper, Breacher, Suppressor, Warlord, Interceptor, Thumper, Firebomber, Stormwing, Skyfort,
            Medic, BoostDrone, Excavator, Aegis, Hauler, Hopper, Slimebug, Blaster, Stalker, Sparker, Devourer,
            Tender, Missileboat, Mireship, Wardship, Lancership, Infernoship, Flametank, Flaktank, Howitzer, Ramtank, Workshop, Teslatank,
            Dart, Patch, Minelayer, Lancer, Scorch, Bumper, Homer, Screen, Jolt, Lobber, Seeder, Leviathan,
            Colossus, Mothership, Ravager, Doomtank, Bastion, Nemesis,
            Commander,
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
            ammoName = "철광탄",
            altAmmo = new[]
            {
                new AmmoDef { item = Steel, perItem = 8, name = "철갑탄", desc = "적의 방어력을 무시한다",
                    bullet = new BulletDef { speed = 0.24f, damage = 13, lifeTicks = 30, pierceArmor = true, color = 0xDEE2E6 } },
                new AmmoDef { item = Powder, perItem = 6, name = "소이탄", desc = "맞은 적을 불태운다",
                    bullet = new BulletDef { speed = 0.22f, damage = 8, lifeTicks = 32, burn = 10, burnTicks = 180, color = 0xFF922B } },
                new AmmoDef { item = Circuit, perItem = 8, name = "유도탄", desc = "겨눈 적을 쫓아간다",
                    bullet = new BulletDef { speed = 0.20f, damage = 12, lifeTicks = 50, homing = 0.12f, color = 0x8CE99A } },
            },
        };

        public static readonly BlockDef MortarTurret = new BlockDef
        {
            key = "mortar", name = "박격포", desc = "황석 포탄으로 넓은 범위를 때린다. 지상만 쏜다.",
            kind = BlockKind.Turret, size = 2, health = 260, color = 0xE76F51, tech = Research.Mortar,
            cost = new[] { new ItemStack(Iron, 60), new ItemStack(Sulfur, 40) },
            range = 8.5f, reloadTicks = 100, ammoItem = Sulfur, ammoPerItem = 2, targetAir = false,
            bullet = new BulletDef { speed = 0.11f, damage = 38, splash = 1.7f, lifeTicks = 90, artillery = true, color = 0xFFE066, size = 0.28f },
            ammoName = "황석 포탄",
            altAmmo = new[]
            {
                new AmmoDef { item = Powder, perItem = 2, name = "파편 포탄", desc = "터지며 사방으로 파편이 튄다",
                    bullet = new BulletDef { speed = 0.11f, damage = 34, splash = 1.7f, lifeTicks = 90, artillery = true, color = 0xFF922B, size = 0.28f, fragCount = 6,
                        frag = new BulletDef { speed = 0.18f, damage = 14, lifeTicks = 12, color = 0xFFE8CC, size = 0.13f } } },
                new AmmoDef { item = Alloy, perItem = 3, name = "점착 포탄", desc = "넓은 범위의 적을 느리게 한다",
                    bullet = new BulletDef { speed = 0.11f, damage = 26, splash = 2.4f, lifeTicks = 90, artillery = true, slow = 0.5f, slowTicks = 150, color = 0xF06292, size = 0.3f } },
            },
        };

        public static readonly BlockDef CrystalTurret = new BlockDef
        {
            key = "lance", name = "결정포", desc = "심연 결정을 쏘아 적을 꿰뚫는다.",
            kind = BlockKind.Turret, size = 2, health = 300, color = 0x9C6BFF, tech = Research.Lance,
            cost = new[] { new ItemStack(Iron, 80), new ItemStack(Sulfur, 50), new ItemStack(Crystal, 30) },
            range = 9.5f, reloadTicks = 55, ammoItem = Crystal, ammoPerItem = 4,
            bullet = new BulletDef { speed = 0.45f, damage = 70, lifeTicks = 26, pierce = 3, color = 0xD0BFFF, size = 0.24f },
            ammoName = "결정탄",
            altAmmo = new[]
            {
                new AmmoDef { item = Alloy, perItem = 4, name = "합금 관통탄", desc = "적 여섯을 꿰뚫고 방어력을 무시한다",
                    bullet = new BulletDef { speed = 0.45f, damage = 85, lifeTicks = 26, pierce = 6, pierceArmor = true, color = 0xF8A5C2, size = 0.26f } },
            },
        };

        // ───────── 특수 포탑 ─────────

        public static readonly BlockDef FlakTurret = new BlockDef
        {
            key = "flak", name = "대공포", desc = "공중의 적만 쏜다. 터지며 둘레의 비행체까지 때린다.",
            kind = BlockKind.Turret, health = 200, color = 0x74C0FC, tech = Research.SpecialTurret,
            cost = new[] { new ItemStack(Iron, 50), new ItemStack(Steel, 15) },
            range = 8f, reloadTicks = 30, ammoItem = Iron, ammoPerItem = 4, targetGround = false,
            bullet = new BulletDef { speed = 0.30f, damage = 16, splash = 1.1f, lifeTicks = 30, color = 0xA5D8FF, size = 0.2f },
            ammoName = "철광 대공탄",
            altAmmo = new[]
            {
                new AmmoDef { item = Powder, perItem = 4, name = "화약 대공탄", desc = "더 세고 더 넓게 터진다",
                    bullet = new BulletDef { speed = 0.30f, damage = 22, splash = 1.5f, lifeTicks = 30, color = 0xFFC078, size = 0.22f } },
            },
        };

        public static readonly BlockDef SlimeTurret = new BlockDef
        {
            key = "slime", name = "점액탑", desc = "점액을 뿌려 둘레의 적을 느리게 한다. 피해는 거의 없다.",
            kind = BlockKind.Turret, health = 180, color = 0x94D82D, tech = Research.SpecialTurret,
            cost = new[] { new ItemStack(Iron, 45), new ItemStack(Sulfur, 25) },
            range = 5f, reloadTicks = 45, ammoItem = Sulfur, ammoPerItem = 5,
            bullet = new BulletDef { speed = 0.20f, damage = 5, splash = 1.3f, lifeTicks = 28, slow = 0.55f, slowTicks = 120, color = 0xC0EB75, size = 0.22f },
            ammoName = "황석 점액",
        };

        public static readonly BlockDef TeslaTurret = new BlockDef
        {
            key = "tesla", name = "전격탑", desc = "번개가 맞은 적에서 가까운 적으로 건너뛴다. 떼로 오는 적에게 세다.",
            kind = BlockKind.Turret, size = 2, health = 280, color = 0x3BC9DB, tech = Research.AdvTurret,
            cost = new[] { new ItemStack(Iron, 90), new ItemStack(Steel, 40), new ItemStack(Circuit, 40) },
            range = 6f, reloadTicks = 50, ammoItem = Circuit, ammoPerItem = 10,
            bullet = new BulletDef { speed = 0.5f, damage = 30, lifeTicks = 14, chain = 4, chainRange = 2.2f, color = 0xC5F6FA, size = 0.2f },
            ammoName = "회로 방전",
            altAmmo = new[]
            {
                new AmmoDef { item = Alloy, perItem = 12, name = "합금 방전", desc = "더 세고 일곱 번까지 건너뛴다",
                    bullet = new BulletDef { speed = 0.5f, damage = 42, lifeTicks = 14, chain = 7, chainRange = 2.6f, color = 0xFCC2D7, size = 0.22f } },
            },
        };

        public static readonly BlockDef FlameTurret = new BlockDef
        {
            key = "flame", name = "화염탑", desc = "가까운 적을 꿰뚫는 불길로 태운다. 사거리가 짧다.",
            kind = BlockKind.Turret, health = 240, color = 0xFF6B35, tech = Research.AdvTurret,
            cost = new[] { new ItemStack(Iron, 60), new ItemStack(Powder, 30) },
            range = 3.2f, reloadTicks = 8, ammoItem = Powder, ammoPerItem = 40,
            bullet = new BulletDef { speed = 0.2f, damage = 4, lifeTicks = 17, pierce = 3, burn = 12, burnTicks = 150, color = 0xFF922B, size = 0.22f },
            ammoName = "화약 불길",
        };

        // ───────── 지원 건물 ─────────

        public static readonly BlockDef MendTower = new BlockDef
        {
            key = "mender", name = "수리탑", desc = "2초마다 둘레 4칸 안의 건물을 최대 체력의 5% 씩 고친다.",
            kind = BlockKind.Support, health = 200, color = 0x51CF66, tech = Research.SupportBlock,
            cost = new[] { new ItemStack(Iron, 40), new ItemStack(Steel, 20) },
            supportRange = 4f, mendPct = 0.05f, mendTicks = 120,
        };

        public static readonly BlockDef Amplifier = new BlockDef
        {
            key = "amplifier", name = "증폭탑", desc = "둘레 4칸 안의 포탑이 1.33배 빨리 쏜다. 겹쳐도 한 번만 듣는다.",
            kind = BlockKind.Support, health = 220, color = 0xFCC419, tech = Research.SupportBlock,
            cost = new[] { new ItemStack(Iron, 60), new ItemStack(Circuit, 40) },
            supportRange = 4f, boost = true,
        };

        public static readonly BlockDef ShieldGen = new BlockDef
        {
            key = "shieldgen", name = "역장 발생기", desc = "둘레 4.5칸 안의 건물이 받는 피해를 역장이 먼저 받는다. 5초 동안 맞지 않으면 다시 차오른다.",
            kind = BlockKind.Support, size = 2, health = 500, color = 0x66D9E8, tech = Research.SupportBlock,
            cost = new[] { new ItemStack(Iron, 100), new ItemStack(Steel, 60), new ItemStack(Circuit, 50) },
            supportRange = 4.5f, shield = 600, shieldRegen = 40, shieldDelay = 300,
        };

        public static readonly BlockDef RobotFactory = new BlockDef
        {
            key = "robotfactory", name = "로봇 공장", desc = "핵의 자원으로 공격 로봇을 만든다.",
            kind = BlockKind.Factory, size = 2, health = 500, color = 0x38B2AC, tech = Research.RobotFactory,
            cost = new[] { new ItemStack(Iron, 120), new ItemStack(Steel, 40) },
            units = new[] { Scout, Assault, Torch, Sniper, Skimmer, Heavy, Breacher, Arcer, Suppressor, Siege, Warlord, Titan },
        };

        public static readonly BlockDef Airfield = new BlockDef
        {
            key = "airfield", name = "비행장", desc = "핵의 자원으로 비행 유닛을 만든다.",
            kind = BlockKind.Factory, size = 2, health = 420, color = 0x4DABF7, tech = Research.Airfield,
            cost = new[] { new ItemStack(Iron, 100), new ItemStack(Steel, 30), new ItemStack(Circuit, 30) },
            units = new[] { Recon, Interceptor, Thumper, Bomber, Striker, Seeker, Firebomber, Mender, Stormwing, Warship, Skyfort, Carrier },
        };

        public static readonly BlockDef SupportShop = new BlockDef
        {
            key = "supportshop", name = "지원 공방", desc = "수리·보호막·증폭·수송·채굴·재건 유닛을 만든다.",
            kind = BlockKind.Factory, size = 2, health = 450, color = 0x37B24D, tech = Research.Support,
            cost = new[] { new ItemStack(Iron, 100), new ItemStack(Steel, 40), new ItemStack(Circuit, 30) },
            units = new[] { Mechanic, RepairDrone, Miner, Rebuilder, Guardian, Booster, Transport, Medic, BoostDrone, Excavator, Aegis, Hauler },
        };

        public static readonly BlockDef LegShop = new BlockDef
        {
            key = "legshop", name = "다리 공방", desc = "자폭 벌레와 벽을 넘는 다리 로봇들을 만든다.",
            kind = BlockKind.Factory, size = 2, health = 450, color = 0xF08C00, tech = Research.Legs,
            cost = new[] { new ItemStack(Iron, 100), new ItemStack(Steel, 30), new ItemStack(Powder, 30) },
            units = new[] { Bug, Hopper, Slimebug, Climber, Spitter, Blaster, Stalker, Leech, Sparker, Shatter, Devourer, Pyre },
        };

        public static readonly BlockDef TankFactory = new BlockDef
        {
            key = "tankfactory", name = "전차 공장", desc = "느리고 단단한 전차를 만든다. 전차는 닿은 적을 짓밟는다.",
            kind = BlockKind.Factory, size = 2, health = 600, color = 0x74875A, tech = Research.Tank,
            cost = new[] { new ItemStack(Iron, 140), new ItemStack(Steel, 80) },
            units = new[] { ArmoredCar, Flametank, Flaktank, LightTank, Howitzer, Ramtank, Workshop, HeavyTank, Bulwark, Teslatank, PiercerTank, Juggernaut },
        };

        public static readonly BlockDef HoverShop = new BlockDef
        {
            key = "hovershop", name = "부양 공방", desc = "떠서 달리는 유닛을 만든다. 빠르고 얇으며 호수를 가로지른다.",
            kind = BlockKind.Factory, size = 2, health = 450, color = 0x38D9A9, tech = Research.Hover,
            cost = new[] { new ItemStack(Iron, 120), new ItemStack(Steel, 50), new ItemStack(Circuit, 40) },
            units = new[] { Dart, Patch, Minelayer, Lancer, Scorch, Bumper, Homer, Screen, Jolt, Lobber, Seeder, Leviathan },
        };

        public static readonly BlockDef Assembler = new BlockDef
        {
            key = "assembler", name = "심연 조립소", desc = "가장 강한 6·7단계 유닛을 조립한다. 짓는 데도 유닛에도 심연 결정까지 드는 큰 비용이 든다.",
            kind = BlockKind.Factory, size = 3, health = 2500, armor = 6, color = 0x7048E8, tech = Research.Assembler,
            cost = new[] { new ItemStack(Iron, 600), new ItemStack(Steel, 400), new ItemStack(Circuit, 300), new ItemStack(Alloy, 200), new ItemStack(Crystal, 150) },
            units = new[] { Colossus, Mothership, Ravager, Doomtank, Bastion, Nemesis },
        };

        public static readonly BlockDef Shipyard = new BlockDef
        {
            key = "shipyard", name = "조선소", desc = "물가에만 지을 수 있다. 호수 위를 다니는 함정을 만든다.",
            kind = BlockKind.Factory, size = 2, health = 500, color = 0x1098AD, tech = Research.Shipyard, needsWater = true,
            cost = new[] { new ItemStack(Iron, 120), new ItemStack(Steel, 50), new ItemStack(Circuit, 20) },
            units = new[] { Patrol, Tender, Missileboat, Gunboat, Mireship, Wardship, Destroyer, Shockboat, Lancership, Arcship, Infernoship, Flagship },
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

        public static readonly BlockDef[] Blocks = { Core, Wall, RapidTurret, MortarTurret, CrystalTurret, SteelWall, RobotFactory, Airfield, EnemyCore, EnemyTurret, EnemyWall, SupportShop, LegShop, Shipyard, TankFactory, HoverShop, Assembler, FlakTurret, SlimeTurret, TeslaTurret, FlameTurret, MendTower, Amplifier, ShieldGen };
        /// 건설 바에 나오는 순서
        public static readonly BlockDef[] Buildable = { RapidTurret, Wall, MortarTurret, FlakTurret, SlimeTurret, CrystalTurret, TeslaTurret, FlameTurret, MendTower, Amplifier, ShieldGen, RobotFactory, Airfield, SupportShop, LegShop, TankFactory, HoverShop, Shipyard, Assembler, SteelWall };

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
                var bd = Blocks[i];
                if (bd.kind == BlockKind.Turret)
                {
                    bd.ammos = new AmmoDef[1 + bd.altAmmo.Length];
                    bd.ammos[0] = new AmmoDef { item = bd.ammoItem, perItem = bd.ammoPerItem, bullet = bd.bullet, name = bd.ammoName, desc = "기본 탄" };
                    System.Array.Copy(bd.altAmmo, 0, bd.ammos, 1, bd.altAmmo.Length);
                }
            }
            for (int i = 0; i < Units.Length; i++) Units[i].id = i;
            for (int i = 0; i < Enemies.Length; i++) Enemies[i].id = i;
            for (int i = 0; i < Abysses.Length; i++) Abysses[i].id = i;
        }
    }
}
