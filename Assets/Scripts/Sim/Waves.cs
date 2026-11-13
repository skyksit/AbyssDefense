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
        public bool hasBoss;             // 10의 배수 지역: 마지막 웨이브의 보스가 심연 군주다
        public int[] startItems;
        public bool assault;             // 적 거점 지역: 웨이브는 끝없이 오고, 적 핵을 모두 부숴야 점령한다

        /// 지역 15 부터 5 로 끝나는 지역 (15, 25, … 95)
        public static bool IsAssault(int region) => region >= 15 && region % 10 == 5;

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
                enemyHealthMul = (1.3f + 11f * d) * a.enemyHealthMul,
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
