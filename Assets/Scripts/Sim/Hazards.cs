using System;

namespace AbyssDefense.Sim
{
    /// 심연마다 다른 규칙: 잿불의 용암 틈, 독늪의 산성 웅덩이(위험 바닥), 서리의 눈보라(날씨)
    public sealed partial class Sim
    {
        public const float LavaPct = 0.02f;                 // 용암 틈 위에서 초마다 잃는 최대 체력의 비율 (보스는 절반)
        public const float PoolArmor = 0.5f;                // 산성 웅덩이 위에서의 방어력 배수 (보스는 0.75)
        public const int BlizzardEvery = 75 * 60, BlizzardTicks = 20 * 60, BlizzardWarn = 10 * 60;
        public const float BlizzardFly = 0.6f;              // 눈보라 속 비행체(양쪽)의 속도 배수
        const float HazardCoreClear = 7.5f, HazardSpawnClear = 5f;

        public int blizzardLeft;     // 눈보라가 부는 남은 틱 (0 = 안 분다)
        public int blizzardIn;       // 다음 눈보라까지 남은 틱
        public bool Blizzard => blizzardLeft > 0;
        /// 눈보라 속에서는 네 틱에 한 번 재장전이 멈춘다 (포탑과 유닛이 25% 느리게 쏜다)
        public bool BlizzardStall => blizzardLeft > 0 && tick % 4 == 0;

        /// 위험 바닥을 깐다: 노이즈로 덩어리를 만들되 핵과 출현 지점 둘레는 비워 둔다. 걷는 데는 지장이 없고, 그 위에는 지을 수 없다
        void MakeHazards()
        {
            blizzardIn = BlizzardEvery;
            if (rules.hazard != Hazard.Lava && rules.hazard != Hazard.Acid) return;
            int ns = (int)(rules.seed % 100000) + 4242;
            float cx = world.core.Cx, cy = world.core.Cy;
            for (int y = 1; y < world.H - 1; y++)
                for (int x = 1; x < world.W - 1; x++)
                {
                    var t = world.tiles[world.Idx(x, y)];
                    if (t.wall || t.water || t.build != null) continue;
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy < HazardCoreClear * HazardCoreClear) continue;
                    bool nearSpawn = false;
                    foreach (int s in world.spawns)
                    {
                        float sx = s % world.W + 0.5f - (x + 0.5f), sy = s / world.W + 0.5f - (y + 0.5f);
                        if (sx * sx + sy * sy < HazardSpawnClear * HazardSpawnClear) nearSpawn = true;
                    }
                    if (nearSpawn) continue;
                    if (Noise.Fractal(x / 4f, y / 4f, ns) > 0.63f) t.hazard = true;
                }
        }

        bool OnHazard(float x, float y)
        {
            int tx = (int)x, ty = (int)y;
            return world.In(tx, ty) && world.tiles[world.Idx(tx, ty)].hazard;
        }

        void UpdateWeather()
        {
            if (rules.hazard != Hazard.Blizzard) return;
            if (blizzardLeft > 0) blizzardLeft--;
            else if (--blizzardIn <= 0) { blizzardLeft = BlizzardTicks; blizzardIn = BlizzardEvery; }
        }

        /// 위험 바닥 위에 선 지상의 적 (0.25초마다)
        void HazardEnemy(Enemy e)
        {
            if (!OnHazard(e.x, e.y)) return;
            if (rules.hazard == Hazard.Lava)
            {
                Damage(e, e.maxHealth * LavaPct * 0.25f * (e.def.boss ? 0.5f : 1f), true);
                fx.Add(new Fx { kind = FxKind.Hit, x = e.x, y = e.y, r = e.def.radius * 0.8f, color = 0xFF922B });
            }
            else
            {
                float mul = e.def.boss ? 1f - (1f - PoolArmor) * 0.5f : PoolArmor;
                if (e.acidTicks > 0 && e.acidMul < mul) return;   // 더 센 산성이 걸려 있다
                e.acidMul = mul; e.acidTicks = Math.Max(e.acidTicks, 20);
            }
        }

        /// 위험 바닥 위에 선 지상의 아군 유닛 (0.25초마다). 용암은 유닛을 죽이지는 못한다 (체력 1 까지)
        void HazardUnit(Unit u)
        {
            if (!OnHazard(u.x, u.y)) return;
            if (rules.hazard == Hazard.Lava)
            {
                if (u.health > 1) u.health = Math.Max(1f, u.health - u.maxHealth * LavaPct * 0.25f);
                u.hitFlash = 4;
            }
            else u.acidTicks = 20;
        }
    }
}
