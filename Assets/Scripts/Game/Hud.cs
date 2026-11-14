using System.Collections.Generic;
using AbyssDefense.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace AbyssDefense
{
    /// 지역 플레이 화면의 HUD: 위 상태 줄, 아래 건설 바, 공장 창, 결과 창
    public sealed partial class GameManager
    {
        sealed class BuildSlot { public BlockDef def; public Image bg; public Text cost, name; public Image icon; }

        readonly List<BuildSlot> slots = new List<BuildSlot>();
        Text[] itemTexts;
        Text waveText, timerText, infoText, speedText, pauseText, hpText, stanceText;
        RectTransform hpFill;
        Image removeBg, stanceBg;
        Button nextWaveBtn;
        string infoOverride;
        float infoUntil;

        // 공장을 탭하면 뜨는 창: 만들 유닛 고르기, 생산 정지, 집결 지점
        const int ProductButtons = 6;
        const float ProductRowPx = 1002, ProductGapPx = 8;   // 생산 버튼 한 줄의 너비. 유닛이 5종을 넘는 공장은 버튼을 좁혀 한 줄에 넣는다
        GameObject factoryPanel;
        Text factoryTitle;
        readonly Text[] prodText = new Text[ProductButtons];
        readonly Image[] prodBg = new Image[ProductButtons];
        Image stopBg, rallyBg;

        void BuildHud()
        {
            var root = NewScreen("Hud", null, HudBottomPx + 24);
            slots.Clear();

            // ── 위: 핵에 있는 자원 · 핵 체력 · 웨이브 ──
            var top = Ui.Panel(root, Ui.PanelCol);
            top.Rt().TopBar(0, HudTopPx);
            itemTexts = new Text[Content.Items.Length];
            for (int i = 0; i < Content.Items.Length; i++)
            {
                var chip = Ui.Node("Item", top.transform).At(new Vector2(0, 1), new Vector2(0, 1), 16 + i * 150, -12, 148, 60);
                Ui.Icon(chip, Art.Get("item_" + Content.Items[i].key)).Rt().At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 40, 40);
                itemTexts[i] = Ui.Label(chip, "0", 31, null, TextAnchor.MiddleLeft, true);
                itemTexts[i].rectTransform.Fill(46);
            }

            var hpBack = Ui.Panel(top.transform, new Color(0, 0, 0, 0.55f), true, "Hp");
            hpBack.Rt().At(new Vector2(0, 1), new Vector2(0, 1), 20, -88, 470, 66);
            var fill = Ui.Panel(hpBack.transform, Art.C(Content.Core.color), true, "Fill");
            hpFill = fill.Rt();
            hpFill.anchorMin = Vector2.zero; hpFill.anchorMax = Vector2.one;
            hpFill.offsetMin = new Vector2(4, 4); hpFill.offsetMax = new Vector2(-4, -4);
            hpText = Ui.Label(hpBack.transform, "", 30, Color.white, TextAnchor.MiddleCenter, true);
            timerText = Ui.Label(top.transform, "", 30, null, TextAnchor.MiddleCenter);
            timerText.rectTransform.At(new Vector2(0, 1), new Vector2(0, 1), 500, -88, 300, 66);
            waveText = Ui.Label(top.transform, "", 36, Ui.Gold, TextAnchor.MiddleRight, true);
            waveText.rectTransform.At(new Vector2(1, 1), new Vector2(1, 1), -20, -88, 250, 66);

            // ── 위 줄 아래: 다음 웨이브 · 유닛 태세 · 일시정지 · 배속 · 후퇴 ──
            float by = -HudTopPx - 14;
            var dark = new Color(0.2f, 0.22f, 0.28f, 0.92f);
            nextWaveBtn = Ui.Btn(root, "다음 웨이브 부르기", 27, new Color(0.55f, 0.22f, 0.25f, 0.95f), () => sim.CallNextWave());
            nextWaveBtn.Rt().At(new Vector2(0, 1), new Vector2(0, 1), 20, by, 290, 78);
            var bStance = Ui.Btn(root, "방어 태세", 23, dark, ToggleStance);
            bStance.Rt().At(new Vector2(0, 1), new Vector2(0, 1), 320, by, 300, 78);
            stanceBg = bStance.GetComponent<Image>();
            stanceText = bStance.GetComponentInChildren<Text>();
            bStance.gameObject.SetActive(sim.IsUnlocked(Content.RobotFactory) || sim.IsUnlocked(Content.Airfield));
            var bRetreat = Ui.Btn(root, "후퇴", 30, dark, Retreat);
            bRetreat.Rt().At(new Vector2(1, 1), new Vector2(1, 1), -20, by, 130, 78);
            var bSpeed = Ui.Btn(root, "x1", 32, dark, () => SetSpeed(Speed % 3 + 1));
            bSpeed.Rt().At(new Vector2(1, 1), new Vector2(1, 1), -160, by, 110, 78);
            speedText = bSpeed.GetComponentInChildren<Text>();
            var bPause = Ui.Btn(root, "멈춤", 30, dark, TogglePause);
            bPause.Rt().At(new Vector2(1, 1), new Vector2(1, 1), -280, by, 130, 78);
            pauseText = bPause.GetComponentInChildren<Text>();

            // ── 아래: 설명 줄 · 옆으로 넘기는 건설 줄 + 철거 ──
            var bottom = Ui.Panel(root, Ui.PanelCol);
            bottom.Rt().BottomBar(0, HudBottomPx);
            infoText = Ui.Label(bottom.transform, "", 28, Ui.Dim, TextAnchor.MiddleLeft);
            infoText.rectTransform.TopBar(8, 70, 24, 24);

            int n = Content.Buildable.Length;
            const float gap = 10, remW = 170, sw = 190, slotH = 236;
            var view = Ui.Panel(bottom.transform, new Color(0, 0, 0, 0.01f), false, "BuildBar");
            view.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 20, 14, 1080 - 40 - remW - gap, slotH);
            view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            var content = Ui.Node("Content", view.transform);
            content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 0.5f);
            content.sizeDelta = new Vector2(n * (sw + gap) - gap, 0);
            content.anchoredPosition = Vector2.zero;
            scroll.content = content; scroll.viewport = view.Rt();
            scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            for (int i = 0; i < n; i++)
            {
                var def = Content.Buildable[i];
                var b = Ui.Btn(content, null, 0, Ui.Card, () => { if (sim.IsUnlocked(def)) play.Select(def); else Toast($"{def.name}: 기지 연구소에서 '{Research.Defs[def.tech].name}'을 연구해야 합니다"); });
                b.Rt().At(new Vector2(0, 0), new Vector2(0, 0), i * (sw + gap), 0, sw, slotH);
                var icon = Ui.Icon(b.transform, Art.Icon(def));
                icon.Rt().At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -10, 116, 116);
                var name = Ui.Label(b.transform, def.name, 30, null, TextAnchor.MiddleCenter, true);
                name.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 60, sw, 40);
                var cost = Ui.Label(b.transform, "", 27, Ui.Dim, TextAnchor.MiddleCenter, true);
                cost.supportRichText = true;
                cost.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 14, sw, 40);
                slots.Add(new BuildSlot { def = def, bg = b.GetComponent<Image>(), cost = cost, name = name, icon = icon });
            }
            var bRem = Ui.Btn(bottom.transform, "철거", 34, Ui.Card, () => play.ToggleRemove());
            bRem.Rt().At(new Vector2(1, 0), new Vector2(1, 0), -20, 14, remW, slotH);
            removeBg = bRem.GetComponent<Image>();

            // ── 공장 창 (공장을 탭했을 때만 보인다) ──
            var fp = Ui.Panel(root, Ui.PanelCol, true, "Factory");
            fp.Rt().BottomBar(HudBottomPx + 10, 284, 16, 16);
            factoryPanel = fp.gameObject;
            factoryTitle = Ui.Label(fp.transform, "", 28, null, TextAnchor.MiddleLeft, true);
            factoryTitle.rectTransform.TopBar(8, 46, 22, 22);
            const float bw = ProductRowPx / 5 - ProductGapPx * 4 / 5, bgap = ProductGapPx;
            for (int i = 0; i < ProductButtons; i++)
            {
                int k = i;
                var pb = Ui.Btn(fp.transform, " ", 22, Ui.Card, () => SetProduct(k));
                pb.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 20 + i * (bw + bgap), 106, bw, 116);
                prodBg[i] = pb.GetComponent<Image>();
                prodText[i] = pb.GetComponentInChildren<Text>();
                prodText[i].supportRichText = true;
            }
            var sb = Ui.Btn(fp.transform, "생산 정지", 27, Ui.Card, () => SetProduct(-1));
            sb.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 20, 12, 500, 84);
            stopBg = sb.GetComponent<Image>();
            var rb = Ui.Btn(fp.transform, "집결 지점 옮기기", 27, Ui.Card, () => play.BeginRally());
            rb.Rt().At(new Vector2(1, 0), new Vector2(1, 0), -20, 12, 500, 84);
            rallyBg = rb.GetComponent<Image>();
            factoryPanel.SetActive(false);

            UpdateHud();
        }

        /// 방어 ↔ 공격. 모든 유닛에 함께 적용된다
        public void ToggleStance()
        {
            if (sim == null) return;
            sim.stance = sim.stance == UnitStance.Defend ? UnitStance.Attack : UnitStance.Defend;
            Toast(sim.stance == UnitStance.Defend ? "방어 태세: 유닛이 핵 옆 집결 지점으로 돌아와 핵을 지킵니다" : "공격 태세: 유닛이 적 출현 지점 앞으로 나가 싸웁니다");
        }

        /// 고른 공장이 만들 유닛을 바꾼다 (-1 = 정지). 만들던 유닛은 끝까지 만든다
        public void SetProduct(int k)
        {
            var f = play != null ? play.Factory : null;
            if (f == null) return;
            if (k >= 0)
            {
                if (k >= f.def.units.Length) return;
                var u = f.def.units[k];
                if (!sim.IsUnlocked(u)) { Toast($"{u.name}: 기지 연구소에서 '{Research.Defs[u.tech].name}'을 연구해야 합니다"); return; }
            }
            f.product = k;
        }

        /// 건물을 탭했을 때 설명 줄에 잠깐 보여 준다
        public void ShowInfo(Building b)
        {
            if (b == null) return;
            string s = $"{b.def.name}  체력 {Mathf.CeilToInt(b.health)}/{(int)b.maxHealth}";
            if (!b.Built) s = $"{b.def.name}  건설 중 {Mathf.FloorToInt(b.progress * 100)}%" + (b.constructing ? "" : "  (순서를 기다리거나 자원이 부족합니다)");
            else if (b is TurretBuilding t)
                s += t.Starved ? $"  탄약 없음 ({Content.Items[t.def.ammoItem].name} 부족)" : $"  탄약 {Content.Items[t.def.ammoItem].name} (하나에 {t.AmmoPerItem}발)";
            else if (b.def.armor > 0) s += $"  방어력 {b.def.armor:0}";
            infoOverride = s; infoUntil = Time.unscaledTime + 3f;
        }

        static string CostText(ItemStack[] cost)
        {
            // 숫자만, 위 자원 표시와 같은 색으로
            var sb = new System.Text.StringBuilder();
            foreach (var c in cost)
            {
                if (sb.Length > 0) sb.Append("  ");
                sb.Append("<color=#").Append(Content.Items[c.item].color.ToString("X6")).Append('>').Append(c.amount).Append("</color>");
            }
            return sb.ToString();
        }

        string FactoryStatusText(FactoryBuilding f)
        {
            switch (f.status)
            {
                case FactoryStatus.Producing: return f.making != null ? $"{f.making.name} 생산 중 {Mathf.FloorToInt(f.work * 100)}%" : "생산 중";
                case FactoryStatus.NoResources: return $"{f.def.units[f.product].name}: 핵에 자원이 부족합니다";
                case FactoryStatus.Cap: return $"유닛 한도가 찼습니다 ({sim.SupplyUsed}/{sim.UnitCap})";
                case FactoryStatus.Locked: return "연구가 필요한 유닛입니다";
                default: return "생산 정지";
            }
        }

        void UpdateFactoryPanel()
        {
            var f = play.Factory;
            bool show = f != null && !ResultShown;
            if (factoryPanel.activeSelf != show) factoryPanel.SetActive(show);
            if (!show) return;
            factoryTitle.text = play.RallyMode ? $"{f.def.name}  ·  유닛이 모일 곳을 맵에서 탭하세요" : $"{f.def.name}  ·  {FactoryStatusText(f)}";
            factoryTitle.color = play.RallyMode ? Ui.Gold : Ui.TextCol;
            for (int i = 0; i < ProductButtons; i++)
            {
                bool has = i < f.def.units.Length;
                prodBg[i].gameObject.SetActive(has);
                if (!has) continue;
                int cols = Mathf.Max(5, f.def.units.Length);
                float pw = (ProductRowPx - ProductGapPx * (cols - 1)) / cols;
                prodBg[i].rectTransform.At(new Vector2(0, 0), new Vector2(0, 0), 20 + i * (pw + ProductGapPx), 106, pw, 116);
                prodText[i].fontSize = cols > 5 ? 19 : 22;
                var u = f.def.units[i];
                bool unlocked = sim.IsUnlocked(u);
                prodBg[i].color = f.product == i ? new Color(0.25f, 0.45f, 0.42f) : Ui.Card;
                prodText[i].text = unlocked ? $"{u.name}\n{CostText(u.cost)}\n<color=#9EA6B8>{u.buildTicks / 60}초 · 한도 {u.supply}</color>" : $"{u.name}\n<color=#9EA6B8>연구 필요</color>";
                prodText[i].color = unlocked ? Ui.TextCol : Ui.Dim;
            }
            stopBg.color = f.product < 0 ? new Color(0.5f, 0.24f, 0.26f) : Ui.Card;
            rallyBg.color = play.RallyMode ? new Color(0.55f, 0.45f, 0.18f) : Ui.Card;
        }

        void UpdateHud()
        {
            if (sim == null || itemTexts == null || waveText == null) return;
            var core = sim.Core;
            for (int i = 0; i < itemTexts.Length; i++)
            {
                itemTexts[i].text = core.items[i].ToString();
                itemTexts[i].color = core.items[i] > 0 ? Ui.TextCol : new Color(1, 1, 1, 0.28f);
            }

            bool assault = sim.rules.assault;
            waveText.text = assault ? $"적 핵 {sim.EnemyCores}" : $"웨이브 {sim.wave}/{sim.rules.winWave}";
            waveText.color = assault ? Ui.Bad : Ui.Gold;
            if (!sim.AllWavesSent)
            {
                int sec = Mathf.CeilToInt(sim.waveCountdown / 60f);
                timerText.text = assault ? $"웨이브 {sim.wave + 1}  {sec / 60}:{sec % 60:00}" : $"다음 웨이브 {sec / 60}:{sec % 60:00}";
                timerText.color = sec <= 10 ? Ui.Bad : Ui.TextCol;
            }
            else
            {
                timerText.text = $"남은 적 {sim.enemies.Count + sim.PendingSpawns}";
                timerText.color = Ui.Gold;
            }
            nextWaveBtn.gameObject.SetActive(!sim.AllWavesSent && !ResultShown);

            float hp = Mathf.Clamp01(core.health / core.maxHealth);
            hpFill.anchorMax = new Vector2(Mathf.Max(0.02f, hp), 1);
            hpFill.GetComponent<Image>().color = Color.Lerp(Ui.Bad, Art.C(Content.Core.color), hp);
            hpText.text = $"심연 핵 {Mathf.CeilToInt(Mathf.Max(0, core.health))}";

            speedText.text = "x" + Speed;
            pauseText.text = Paused ? "계속" : "멈춤";
            removeBg.color = play.RemoveMode ? Ui.Bad : Ui.Card;
            bool attack = sim.stance == UnitStance.Attack;
            stanceText.text = (attack ? "공격 중 (출현 지점)" : "방어 중 (핵 수비)") + $"\n유닛 {sim.SupplyUsed}/{sim.UnitCap}  ·  눌러서 전환";
            stanceBg.color = attack ? new Color(0.6f, 0.34f, 0.16f, 0.95f) : new Color(0.16f, 0.36f, 0.42f, 0.95f);

            foreach (var s in slots)
            {
                bool unlocked = sim.IsUnlocked(s.def);
                bool afford = sim.CanAfford(s.def);
                s.bg.color = play.Selected == s.def ? new Color(0.25f, 0.45f, 0.42f) : Ui.Card;
                s.icon.color = unlocked ? Color.white : new Color(1, 1, 1, 0.25f);
                s.cost.text = unlocked ? CostText(s.def.cost) : "연구 필요";
                s.name.color = !unlocked ? Ui.Dim : afford ? Ui.TextCol : Ui.Bad;   // 자원이 모자라면 이름이 붉다
            }

            UpdateFactoryPanel();

            if (infoOverride != null && Time.unscaledTime < infoUntil && play.Selected == null && !play.RemoveMode)
            {
                infoText.text = infoOverride; infoText.color = Ui.TextCol;
            }
            else if (play.RemoveMode) { infoText.text = "철거: 건물을 탭하거나 끌어서 지웁니다. 비용의 75%를 돌려받습니다."; infoText.color = Ui.Bad; }
            else if (play.Selected != null) { infoText.text = $"{play.Selected.name} — {play.Selected.desc}  건설 {play.Selected.buildTicks / 60f / sim.world.mods.buildSpeedMul:0.#}초"; infoText.color = Ui.TextCol; }
            else if (play.CommandMode) { infoText.text = "지휘기를 보낼 곳을 맵에서 탭하세요. 지휘기 곁의 건설은 1.5배 빠릅니다."; infoText.color = Ui.Gold; }
            else if (play.Factory != null) { infoText.text = "만들 유닛을 고르세요. 방어 태세의 유닛은 집결 지점(기본: 핵 옆)을 지킵니다."; infoText.color = Ui.Dim; }
            else if (assault) { infoText.text = "적 거점: 유닛을 모아 공격 태세로 보내 적 핵을 모두 부수세요. 웨이브는 끝없이 옵니다."; infoText.color = Ui.Gold; }
            else { infoText.text = "건물을 골라 탭하거나 끌어서 짓습니다. 공장 탭 = 유닛 고르기, 핵 탭 = 지휘기 보내기."; infoText.color = Ui.Dim; }
        }

        // ───────── 결과 창 ─────────

        void ShowResult(bool won, int[] returned, string bonus, List<string> unlocked)
        {
            play.Cancel();
            factoryPanel.SetActive(false);
            var dim = Ui.Panel(screen, new Color(0, 0, 0, 0.7f), false, "Result");
            dim.Rt().Fill();
            var card = Ui.Panel(dim.transform, Ui.PanelCol, true);
            card.Rt().At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 30, 900, 1180);
            var accent = Art.C(sim.abyss.accent);

            var title = Ui.Label(card.transform, won ? (sim.rules.assault ? "적 거점 파괴" : "지역 점령") : "심연 핵 파괴", 76, won ? accent : Ui.Bad, TextAnchor.MiddleCenter, true);
            title.rectTransform.TopBar(46, 110);
            var sub = Ui.Label(card.transform, $"{sim.abyss.name} · 지역 {Region}", 36, Ui.Dim);
            sub.rectTransform.TopBar(160, 50);

            int sec = sim.tick / 60;
            string body = (sim.rules.assault ? $"남은 적 핵  {sim.EnemyCores}      버틴 웨이브  {sim.wave}" : $"웨이브  {sim.wave} / {sim.rules.winWave}") + $"      처치한 적  {sim.kills}\n걸린 시간  {sec / 60}분 {sec % 60:00}초      남은 핵 체력  {Mathf.CeilToInt(Mathf.Max(0, sim.Core.health))}";
            if (sim.unitsBuilt > 0) body += $"\n만든 유닛  {sim.unitsBuilt}      잃은 유닛  {sim.unitsLost}";
            var stats = Ui.Label(card.transform, body, 33, null, TextAnchor.UpperCenter);
            stats.lineSpacing = 1.3f;
            stats.rectTransform.TopBar(232, 140, 30, 30);

            // 자원 정산: 가져간 양 → 돌아온 양
            var sb = new System.Text.StringBuilder();
            sb.Append(won ? "기지로 돌아온 자원\n" : "가져간 자원을 모두 잃었습니다\n");
            for (int i = 0; i < returned.Length; i++)
            {
                if (Taken[i] == 0 && returned[i] == 0 && sim.looted[i] == 0) continue;
                sb.Append($"{Content.Items[i].name}   가져감 {Taken[i]:N0}");
                if (sim.looted[i] > 0) sb.Append($"  ·  전리품 {sim.looted[i]:N0}");
                if (won) sb.Append($"  →  <color=#{Content.Items[i].color:X6}>{returned[i]:N0}</color>");
                sb.Append('\n');
            }
            var res = Ui.Label(card.transform, sb.ToString(), 29, Ui.TextCol, TextAnchor.UpperCenter);
            res.supportRichText = true; res.lineSpacing = 1.25f;
            res.rectTransform.TopBar(384, 300, 30, 30);

            string reward;
            if (!won) reward = sim.rules.assault ? "적 핵은 유닛으로만 부술 수 있습니다.\n유닛을 한도까지 모아 공격 태세로 한꺼번에 보내 보세요." : "기지에서 자원이 더 쌓이기를 기다리거나,\n연구로 포탑과 유닛을 강화한 뒤 다시 도전해 보세요.";
            else if (bonus == null) reward = "이미 점령한 지역이라 새 보상은 없습니다.";
            else
            {
                reward = "첫 점령 보상\n기지 생산  " + bonus;
                if (unlocked.Count > 0) reward += "\n해금  " + string.Join(", ", unlocked);
            }
            var rw = Ui.Label(card.transform, reward, 32, won && bonus != null ? Ui.Gold : Ui.Dim, TextAnchor.UpperCenter, true);
            rw.lineSpacing = 1.25f;
            rw.rectTransform.TopBar(700, 190, 30, 30);

            bool hasNext = won && Region < Content.RegionCount;
            int a = AbyssId, r = Region;
            var main = Ui.Btn(card.transform, hasNext ? $"다음 지역 {r + 1}" : won ? "심연 점령 완료" : "다시 도전", 44,
                won ? Primary : new Color(0.55f, 0.22f, 0.25f),
                () => { EndRegion(); ShowRegionMap(a); ShowRegionSheet(a, hasNext ? r + 1 : r); });
            main.Rt().At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 150, 780, 110);
            float half = (780 - 20) / 2f;
            var back = Ui.Btn(card.transform, "기지", 38, Violet, ShowBase);
            back.Rt().At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -(half + 20) / 2, 40, half, 96);
            var map = Ui.Btn(card.transform, "지역 지도", 38, Ui.Card, () => { EndRegion(); ShowRegionMap(a); });
            map.Rt().At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), (half + 20) / 2, 40, half, 96);
        }
    }
}
