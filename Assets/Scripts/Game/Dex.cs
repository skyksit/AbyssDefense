using System.Collections.Generic;
using AbyssDefense.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace AbyssDefense
{
    /// 도감: 유닛·적·건물의 수치를 정의에서 그대로 읽어 보여 준다 (기지에서 연다)
    public sealed partial class GameManager
    {
        static readonly string[] DexTabs = { "유닛", "적", "건물" };
        int dexTab;

        public void ShowDex() { ShowDex(dexTab); }

        public void ShowDex(int tab)
        {
            EndRegion();
            dexTab = Mathf.Clamp(tab, 0, DexTabs.Length - 1);
            var root = NewScreen("Dex");
            Ui.Panel(root, Ui.Bg).Rt().Fill();

            const float topH = 236, rowH = 190, gap = 10;
            var rows = new List<System.Action<Transform, float>>();
            if (dexTab == 0) DexUnits(rows);
            else if (dexTab == 1) DexEnemies(rows);
            else DexBlocks(rows);
            var list = ScrollList(root, "dex" + dexTab, topH, 0, 10 + rows.Count * (rowH + gap) + 30);
            for (int i = 0; i < rows.Count; i++) rows[i](list, 10 + i * (rowH + gap));

            var top = Ui.Panel(root, Ui.PanelCol);
            top.Rt().TopBar(0, topH);
            var title = Ui.Label(top.transform, "도감", 64, null, TextAnchor.UpperLeft, true);
            title.rectTransform.Fill(34, 0, 20, 20);
            var back = Ui.Btn(top.transform, "기지", 34, Violet, ShowBase);
            back.Rt().At(new Vector2(1, 1), new Vector2(1, 1), -30, -24, 200, 84);
            var again = Ui.Btn(top.transform, $"힌트 다시 보기 ({save.hints.Count})", 26, Ui.Card, () => { ResetHints(); ShowDex(dexTab); });
            again.Rt().At(new Vector2(1, 1), new Vector2(1, 1), -244, -24, 300, 84);
            for (int i = 0; i < DexTabs.Length; i++)
            {
                int k = i;
                var tb = Ui.Btn(top.transform, DexTabs[i], 36, i == dexTab ? Primary : Ui.Card, () => ShowDex(k));
                tb.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 30 + i * 345, 16, 330, 88);
            }
        }

        /// 도감 한 줄: 그림, 이름, 수치 두 줄, 설명. locked 가 있으면 이름과 그 말만 보인다
        void DexRow(Transform parent, float top, Sprite icon, string name, Color nameCol, string stats, string desc, string locked)
        {
            var row = Ui.Panel(parent, locked != null ? new Color(0.11f, 0.12f, 0.15f) : Ui.Card, true, "Row");
            row.Rt().TopBar(top, 190, 30, 30);
            var ic = Ui.Icon(row.transform, icon, locked != null ? new Color(1, 1, 1, 0.3f) : Color.white);
            ic.Rt().At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), 20, 0, 120, 120);
            var n = Ui.Label(row.transform, name, 36, locked != null ? Ui.Dim : nameCol, TextAnchor.UpperLeft, true);
            n.rectTransform.Fill(160, 0, 20, 12);
            var s = Ui.Label(row.transform, locked ?? stats, 25, locked != null ? Ui.Dim : Ui.TextCol, TextAnchor.UpperLeft);
            s.supportRichText = true; s.lineSpacing = 1.15f;
            s.rectTransform.Fill(160, 0, 20, 60);
            if (locked == null)
            {
                var d = Ui.Label(row.transform, desc, 23, Ui.Dim, TextAnchor.LowerLeft);
                d.rectTransform.Fill(160, 10, 20, 0);
            }
        }

        static string Cost(ItemStack[] cost)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in cost)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append("<color=#").Append(Content.Items[c.item].color.ToString("X6")).Append('>').Append(c.amount).Append("</color>");
            }
            return sb.ToString();
        }

        static string Targets(bool air, bool ground) => air && ground ? "지상·공중" : air ? "공중만" : "지상만";

        /// 탄의 성질을 한 줄로
        static string BulletText(BulletDef b)
        {
            if (b == null) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append("피해 ").Append(b.damage.ToString("0.#"));
            if (b.splash > 0) sb.Append(" · 범위 ").Append(b.splash.ToString("0.#"));
            if (b.artillery) sb.Append(" · 곡사");
            if (b.pierce > 0) sb.Append(" · 관통 ").Append(b.pierce);
            if (b.pierceArmor) sb.Append(" · 방어력 무시");
            if (b.chain > 0) sb.Append(" · 전격 ").Append(b.chain).Append("번");
            if (b.homing > 0) sb.Append(" · 유도");
            if (b.burn > 0) sb.Append(" · 화상 ").Append(b.burn.ToString("0.#")).Append("/초");
            if (b.slow > 0) sb.Append(" · 둔화 ").Append(Mathf.RoundToInt((1 - b.slow) * 100)).Append('%');
            if (b.acid > 0) sb.Append(" · 산성 ").Append(Mathf.RoundToInt((1 - b.acid) * 100)).Append('%');
            if (b.wetTicks > 0) sb.Append(" · 젖음");
            if (b.knockback > 0) sb.Append(" · 밀어내기");
            if (b.frag != null) sb.Append(" · 파편 ").Append(b.fragCount);
            if (b.heal > 0) sb.Append(" · 건물 수리");
            return sb.ToString();
        }

        void DexUnits(List<System.Action<Transform, float>> rows)
        {
            foreach (var bd in Content.Blocks)
            {
                if (bd.kind != BlockKind.Factory) continue;
                foreach (var u in bd.units) rows.Add(UnitRow(u, bd.name));
            }
            rows.Add(UnitRow(Content.Escort, "모함·기함이 띄운다"));
            rows.Add(UnitRow(Content.Commander, "핵에서 나온다"));
        }

        System.Action<Transform, float> UnitRow(UnitDef u, string from)
        {
            bool open = u.tech < 0 || Research.Level(save.research, u.tech) > 0;
            string move = u.flying ? "비행" : u.naval ? "해상" : u.hover ? "부양" : u.legs ? "다리(벽을 넘는다)" : "지상";
            var sb = new System.Text.StringBuilder();
            sb.Append($"체력 {u.health:0}  방어력 {u.armor:0.#}  속도 {u.speed * 60:0.#}칸/초");
            if (u.barrier > 0) sb.Append($"  보호막 {u.barrier:0}");
            sb.Append('\n');
            if (u.suicide) sb.Append("자폭  ").Append(BulletText(u.bullet));
            else if (u.bullet != null) sb.Append($"사거리 {u.range:0.#}  {u.reloadTicks / 60f:0.##}초마다  {BulletText(u.bullet)}  ({Targets(u.targetAir, u.targetGround)})");
            else if (u.fieldDamage > 0) sb.Append($"사거리 {u.range:0.#}  {u.reloadTicks / 60f:0.##}초마다 적 {u.fieldTargets}기에 {u.fieldDamage:0}");
            else sb.Append("무기 없음");
            string sub = u.cost.Length > 0 ? $"  <size=24><color=#9EA6B8>{u.tier}단계 · {move} · {from} · 한도 {u.supply} · {u.buildTicks / 60}초 · </color>{Cost(u.cost)}</size>" : $"  <size=24><color=#9EA6B8>{move} · {from}</color></size>";
            string locked = open ? null : $"{u.tier}단계 · {from}\n연구 필요: {Research.Defs[u.tech].name}";
            return (p, y) =>
            {
                DexRow(p, y, Art.Get("unit_" + u.key), u.name + (open ? sub : ""), Art.C(u.color), sb.ToString(), u.desc, locked);
                var nameText = p.GetChild(p.childCount - 1).GetChild(1).GetComponent<Text>();
                nameText.supportRichText = true;
            };
        }

        /// 그 적이 처음 나오는 지역 (어느 심연이든 같다)
        static int FirstRegion(EnemyDef e)
        {
            for (int r = 1; r <= Content.RegionCount; r++)
                foreach (var g in WaveGen.Generate(RegionRules.For(0, r)))
                    if (g.enemy == e || g.enemy.splitInto == e) return r;
            return 0;
        }

        void DexEnemies(List<System.Action<Transform, float>> rows)
        {
            int seen = BestCleared + 1;
            foreach (var e in Content.Enemies)
            {
                int first = FirstRegion(e);
                var sb = new System.Text.StringBuilder();
                sb.Append($"체력 {e.health:0}  방어력 {e.armor:0.#}  속도 {e.speed * 60:0.#}칸/초\n공격 {e.damage:0} ({e.attackTicks / 60f:0.##}초마다)  사거리 {e.range:0.#}");
                if (e.barrier > 0) sb.Append($"  보호막 {e.barrier:0}");
                if (e.heal > 0) sb.Append($"  수리 {e.heal:0}/초");
                string kind = (e.boss ? "보스" : e.flying ? "비행" : "지상") + $" · 지역 {first} 부터";
                string locked = first > seen ? $"아직 만나지 못한 적\n지역 {first} 부터 나온다" : null;
                var def = e;
                rows.Add((p, y) =>
                {
                    DexRow(p, y, Art.Get("enemy_" + def.key), locked != null ? "???" : $"{def.name}  <size=24><color=#9EA6B8>{kind} · 수치는 지역 1 기준</color></size>", Art.C(def.color), sb.ToString(), def.desc, locked);
                    p.GetChild(p.childCount - 1).GetChild(1).GetComponent<Text>().supportRichText = true;
                });
            }
        }

        void DexBlocks(List<System.Action<Transform, float>> rows)
        {
            foreach (var b in Content.Buildable)
            {
                bool open = b.tech < 0 || Research.Level(save.research, b.tech) > 0;
                var sb = new System.Text.StringBuilder();
                sb.Append($"체력 {b.health:0}").Append(b.armor > 0 ? $"  방어력 {b.armor:0.#}" : "").Append($"  {b.size}×{b.size}칸");
                if (b.kind != BlockKind.Turret) sb.Append($"  건설 {b.buildTicks / 60f:0.#}초\n");
                if (b.kind == BlockKind.Turret)
                {
                    sb.Append($"  사거리 {b.range:0.#}  {b.reloadTicks / 60f:0.##}초마다  ({Targets(b.targetAir, b.targetGround)})");
                    foreach (var a in b.ammos) sb.Append($"\n<color=#{Content.Items[a.item].color:X6}>{a.name}</color> {a.perItem}발: {BulletText(a.bullet)}");
                }
                else if (b.kind == BlockKind.Support)
                {
                    sb.Append($"범위 {b.supportRange:0.#}칸");
                    if (b.mendPct > 0) sb.Append($"  {b.mendTicks / 60f:0.#}초마다 {b.mendPct * 100:0.#}% 수리");
                    if (b.boost) sb.Append("  포탑 연사 1.33배");
                    if (b.shield > 0) sb.Append($"  역장 {b.shield:0} (초당 {b.shieldRegen:0} 회복)");
                }
                else if (b.kind == BlockKind.Factory) sb.Append($"유닛 {b.units.Length}종").Append(b.needsWater ? "  물가에만 짓는다" : "");
                string locked = open ? null : $"연구 필요: {Research.Defs[b.tech].name}";
                var def = b;
                bool tall = b.kind == BlockKind.Turret && b.ammos.Length > 2;
                rows.Add((p, y) =>
                {
                    DexRow(p, y, Art.Icon(def), def.name + (open ? $"  <size=24>{Cost(def.cost)}</size>" : ""), Art.C(def.color), sb.ToString(), tall ? "" : def.desc, locked);
                    var row = p.GetChild(p.childCount - 1);
                    row.GetChild(1).GetComponent<Text>().supportRichText = true;
                    if (tall) row.GetChild(2).GetComponent<Text>().fontSize = 21;
                });
            }
        }
    }
}
