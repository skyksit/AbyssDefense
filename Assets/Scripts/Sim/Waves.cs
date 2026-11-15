using System;
using System.Collections.Generic;

namespace AbyssDefense.Sim
{
    /// 지역 번호 하나에서 나오는 모든 규칙 (맵 크기, 웨이브 수, 시작 자원 …)
    public sealed class RegionRules
    {
        public int abyss, region;
        public float depth;              // 0(지역 1) ~ 1(지역 100)
        public long seed;
        public int mapW, mapH;
        public int winWave;
        public int waveTicks, firstWaveTicks;
        public int spawnCount;
        public float enemyHealthMul;
        public float enemyDamageMul, enemyArmorMul;   // 적의 공격력·방어력 배수
        public float late;               // 후반 보정의 바탕: 지역 10 까지 0, 그 뒤로 고르게 커져 지역 100 에서 1
        public bool hasBoss;             // 10의 배수 지역: 마지막 웨이브의 보스가 심연 군주다
        public int[] startItems;
        public bool assault;             // 적 거점 지역: 웨이브는 끝없이 오고, 적 핵을 모두 부숴야 점령한다

        /// 지역 15 부터 5 로 끝나는 지역 (15, 25, … 95)
        public static bool IsAssault(int region) => region >= 15 && region % 10 == 5;

        // 후반 보정: 출격 물자 한도가 크게 늘었으므로(최대 6400) 깊은 지역의 적을 그만큼 더 강하게 한다.
        // 지역 10 까지는 그대로고, 지역 100 에서 체력 ×(1 + LateHealth), 공격력 ×(1 + LateDamage), 방어력 ×(1 + LateArmor),
        // 적 거점의 건물 체력과 포탑 화력 ×(1 + LateBase) 가 된다. 값은 봇이 늘어난 한도로도 지역 100 을 못 넘게 맞췄다
        public const float LateHealth = 2.5f, LateDamage = 1.0f, LateArmor = 0.4f, LateBase = 2.5f;

        /// 지역 10 까지 0, 그 뒤로 고르게 커져 지역 100 에서 1
        public static float Late(float depth)
        {
            float t = Math.Max(0f, (depth - 0.1f) / 0.9f);
            return t;
        }

        public static RegionRules For(int abyssId, int region)
        {
            var a = Content.Abysses[abyssId];
            float d = (region - 1) / (float)(Content.RegionCount - 1);
            var r = new RegionRules
            {
                abyss = abyssId, region = region, depth = d,
                seed = abyssId * 1000L + region,
                mapW = 22 + (int)Math.Round(18 * d),
                mapH = 32 + (int)Math.Round(28 * d),
                winWave = 5 + region / 4,
                waveTicks = (int)((40 - 14 * d) * 60),
                firstWaveTicks = (int)((70 + 50 * d) * 60),
                spawnCount = region >= 45 ? 3 : region >= 15 ? 2 : 1,
                late = Late(d),
                enemyHealthMul = (1.3f + 11f * d) * (1 + LateHealth * Late(d)) * a.enemyHealthMul,
                enemyDamageMul = (1.3f + 0.7f * d) * (1 + LateDamage * Late(d)),
                enemyArmorMul = (1 + 2f * d) * (1 + LateArmor * Late(d)),
                hasBoss = region % 10 == 0,
                assault = IsAssault(region),
                startItems = new int[Content.Items.Length],
            };
            r.startItems[Content.Iron] = 150;   // 빈손으로 와도 포탑 몇 개는 지을 수 있는 기본 보급
            return r;
        }
    }

    /// 한 종류의 적이 어느 웨이브에 몇 마리 나오는지를 식 하나로 정한다
    public sealed class WaveGroup
    {
        public EnemyDef enemy;
        public int begin = 1, end = int.MaxValue, spacing = 1;
        public int amount = 1;
        public float scaling = 2;        // 이만큼의 회차마다 한 마리씩 늘어난다
        public int max = 40;
        public float shields;            // 이 무리의 적이 처음부터 두르는 보호막 (지역의 적 체력 배수를 따른다)
        public float shieldScaling;      // 회차가 지날 때마다 더해지는 보호막
        public bool elite;               // 정예: 체력 2.5배, 속도 1.15배, 전리품 3배

        /// 화면에 적는 이름: 정예 보행병, 보호막 철갑병 …
        public string Label => (elite ? "정예 " : shields > 0 || shieldScaling > 0 ? "보호막 " : "") + enemy.name;

        /// 이 회차에 나오는 적 한 마리가 무리에서 받는 보호막 (배수 적용 전)
        public float ShieldAt(int wave) => shields + shieldScaling * Math.Max(0, wave - begin);

        public int GetSpawned(int wave)
        {
            if (wave < begin || wave > end || (wave - begin) % spacing != 0) return 0;
            return Math.Min(amount + (int)(((wave - begin) / spacing) / scaling), max);
        }
    }

    public static class WaveGen
    {
        public static List<WaveGroup> Generate(RegionRules r)
        {
            var a = Content.Abysses[r.abyss];
            var rng = new Rng(r.seed * 31 + 7);
            float d = r.depth, cm = a.enemyCountMul * (r.assault ? 1.3f : 1f);
            var g = new List<WaveGroup>();

            g.Add(new WaveGroup { enemy = Content.Walker, amount = Count(4 + d * 8, cm), scaling = 1.5f });
            if (r.region >= 2)
                g.Add(new WaveGroup { enemy = Content.Runner, begin = rng.Range(2, 3), spacing = 2, amount = Count(3 + d * 9, cm), scaling = 2 });
            if (r.region >= 4)
                g.Add(new WaveGroup { enemy = Content.Breaker, begin = rng.Range(3, 4), spacing = rng.Range(2, 3), amount = Count(1 + d * 5, cm), scaling = 2.5f, max = 16 });
            if (r.region >= 6)
                g.Add(new WaveGroup { enemy = Content.Flyer, begin = rng.Range(3, 5), spacing = rng.Range(2, 3), amount = Count(3 + d * 8, cm), scaling = 2, max = 26 });
            if (r.region >= 12)   // 장갑이 두꺼워 약한 탄은 거의 튕겨 낸다
                g.Add(new WaveGroup { enemy = Content.Shell, begin = rng.Range(2, 4), spacing = 2, amount = Count(2 + d * 6, cm), scaling = 2.5f, max = 18 });
            // 능력이 있는 적: 깊은 지역일수록 종류가 늘어난다 (앞의 무리들과 난수 순서가 섞이지 않게 뒤에 둔다)
            if (r.region >= 9)    // 둘레의 적을 고친다
                g.Add(new WaveGroup { enemy = Content.Healer, begin = rng.Range(3, 4), spacing = rng.Range(2, 3), amount = Count(1 + d * 2, cm), scaling = 4, max = 8 });
            if (r.region >= 14)   // 속사포 밖에서 포탑을 친다
                g.Add(new WaveGroup { enemy = Content.Bombard, begin = rng.Range(4, 5), spacing = 3, amount = Count(1 + d * 2, cm), scaling = 6, max = 6 });
            if (r.region >= 16)   // 정예 보행병
                g.Add(new WaveGroup { enemy = Content.Walker, begin = rng.Range(4, 6), spacing = 3, amount = Count(1 + d * 2, cm), scaling = 4, max = 6, elite = true });
            if (r.region >= 18)   // 죽으면 셋으로 갈라진다
                g.Add(new WaveGroup { enemy = Content.Splitter, begin = rng.Range(3, 5), spacing = rng.Range(2, 3), amount = Count(1 + d * 3, cm), scaling = 3, max = 12 });
            if (r.region >= 22)   // 보호막이 다시 차오른다
                g.Add(new WaveGroup { enemy = Content.Guard, begin = rng.Range(4, 6), spacing = rng.Range(2, 3), amount = Count(1 + d * 3, cm), scaling = 4, max = 10 });
            if (r.region >= 28)   // 벽 너머에 보행병을 내려놓는다
                g.Add(new WaveGroup { enemy = Content.Dropship, begin = rng.Range(5, 7), spacing = 3, amount = Count(1 + d * 1.5f, cm), scaling = 5, max = 4 });
            if (r.region >= 35)   // 공장을 노린다
                g.Add(new WaveGroup { enemy = Content.Raider, begin = rng.Range(5, 7), spacing = rng.Range(2, 3), amount = Count(2 + d * 3, cm), scaling = 3, max = 12 });
            if (r.region >= 40)   // 회차가 지날수록 보호막이 두꺼워지는 철갑병
                g.Add(new WaveGroup { enemy = Content.Shell, begin = rng.Range(6, 8), spacing = 3, amount = Count(1 + d * 2, cm), scaling = 4, max = 6, shields = 20, shieldScaling = 6 });
            // 마지막 웨이브에는 언제나 보스: 보통 지역은 심연 대장 한 마리, 10의 배수 지역은 심연 군주
            if (r.assault)   // 적 거점: 마지막 웨이브가 없으니 다섯 회차마다 심연 대장이 나온다
                g.Add(new WaveGroup { enemy = Content.Chief, begin = 5, spacing = 5, amount = 1, scaling = 4, max = 4 });
            else if (r.hasBoss)
                g.Add(new WaveGroup { enemy = Content.Boss, begin = r.winWave, end = r.winWave, amount = 1 + r.region / 50 });
            else
                g.Add(new WaveGroup { enemy = Content.Chief, begin = r.winWave, end = r.winWave, amount = 1, max = 1 });
            return g;
        }

        static int Count(float v, float mul) => Math.Max(1, (int)Math.Round(v * mul));
    }
}
