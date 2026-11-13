using System;
using AbyssDefense.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace AbyssDefense
{
    /// 메뉴 화면: 타이틀 → 기지(채굴장·창고·연구소) → 심연 선택 → 지역 지도 → 출격
    public sealed partial class GameManager
    {
        ScrollRect mapScroll;
        RectTransform sheet;
        readonly System.Collections.Generic.Dictionary<string, float> scrollPos = new System.Collections.Generic.Dictionary<string, float>();
        const float StockH = 200, TopH = 378;   // 보유 자원 두 줄, 기지·연구소 머리줄 높이
        static readonly Color Primary = new Color(0.18f, 0.48f, 0.44f);
        static readonly Color Violet = new Color(0.30f, 0.26f, 0.46f);
        static readonly Color Off = new Color(0.13f, 0.14f, 0.18f);

        public void ShowTitle()
        {
            EndRegion();
            var root = NewScreen("Title");
            Ui.Panel(root, Ui.Bg).Rt().Fill();

            // 아래로 꺼지는 구덩이 느낌의 겹친 원
            for (int i = 0; i < 6; i++)
            {
                var ring = Ui.Icon(root, Art.Get("fx_disc"), Color.Lerp(new Color(0.16f, 0.13f, 0.22f), new Color(0.02f, 0.02f, 0.03f), i / 5f));
                float s = 1000 - i * 150;
                ring.Rt().At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, -60 - i * 26, s, s * 0.62f);
                ring.preserveAspect = false;
            }

            var title = Ui.Label(root, "ABYSS\nDEFENSE", 150, Ui.TextCol, TextAnchor.MiddleCenter, true);
            title.lineSpacing = 0.9f;
            title.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -230, 1000, 360);
            var sub = Ui.Label(root, "기지를 키우고, 심연의 바닥까지 100개의 지역을 점령하라", 36, Ui.Dim);
            sub.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -610, 1040, 60);

            var start = Ui.Btn(root, "시작", 60, Primary, ShowBase);
            start.Rt().At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 330, 620, 150);
            var ver = Ui.Label(root, "v" + Application.version, 26, Ui.Dim);
            ver.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 40, 600, 40);
        }

        // ───────── 공통 조각 ─────────

        /// 보유 자원 두 줄: 자원마다 '보유량 / 분당 증감(채굴 + 가공)'
        void StockBar(Transform parent, float y)
        {
            int cap = StorageCap;
            var net = NetRates();
            for (int i = 0; i < Content.Items.Length; i++)
            {
                var chip = Ui.Panel(parent, new Color(0, 0, 0, 0.35f), true, "Stock");
                chip.Rt().At(new Vector2(0, 1), new Vector2(0, 1), 30 + (i % 4) * 257, y - (i / 4) * 104, 250, 96);
                Ui.Icon(chip.transform, Art.Get("item_" + Content.Items[i].key)).Rt().At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), 10, 0, 46, 46);
                bool full = save.stock[i] >= cap;
                var amount = Ui.Label(chip.transform, ((int)save.stock[i]).ToString("N0"), 34, full ? Ui.Gold : Ui.TextCol, TextAnchor.UpperLeft, true);
                amount.rectTransform.Fill(64, 0, 4, 8);
                float rate = net[i];
                bool none = Mathf.Abs(rate) < 0.05f;
                string sub = full && rate >= 0 ? "창고 가득" : none ? "생산 없음" : rate > 0 ? $"+{rate:0.#}/분" : $"−{-rate:0.#}/분";
                var r = Ui.Label(chip.transform, sub, 23, full && rate >= 0 ? Ui.Gold : none ? Ui.Dim : rate > 0 ? Ui.Good : Ui.Bad, TextAnchor.LowerLeft);
                r.rectTransform.Fill(64, 9, 4, 0);
            }
        }

        static string CostRich(int[] cost, double[] stock)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cost.Length; i++)
            {
                if (cost[i] <= 0) continue;
                if (sb.Length > 0) sb.Append("  ");
                sb.Append("<color=#").Append((stock[i] >= cost[i] ? Content.Items[i].color : 0xF05A5Au).ToString("X6")).Append('>').Append(cost[i].ToString("N0")).Append("</color>");
            }
            return sb.ToString();
        }

        /// 보유 자원을 자원 색 숫자로만 한 줄에 (0 인 자원은 뺀다)
        string StockRich()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Content.Items.Length; i++)
            {
                if (i >= 2 && save.stock[i] < 1) continue;
                if (sb.Length > 0) sb.Append("   ");
                sb.Append("<color=#").Append(Content.Items[i].color.ToString("X6")).Append('>').Append(((int)save.stock[i]).ToString("N0")).Append("</color>");
            }
            return sb.ToString();
        }

        public void SetScroll(string key, float y) { scrollPos[key] = y; }

        /// 위아래로 끌어 넘기는 목록. 화면을 다시 그려도 넘겨 둔 위치를 기억한다
        RectTransform ScrollList(Transform root, string key, float top, float bottom, float height)
        {
            var view = Ui.Panel(root, new Color(0, 0, 0, 0.01f), false, "View");
            view.Rt().Fill(0, bottom, 0, top);
            view.gameObject.AddComponent<RectMask2D>();
            var sr = view.gameObject.AddComponent<ScrollRect>();
            var content = Ui.Node("Content", view.transform);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, height);
            sr.content = content; sr.viewport = view.Rt();
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 60;
            scrollPos.TryGetValue(key, out float y);
            content.anchoredPosition = new Vector2(0, y);
            sr.onValueChanged.AddListener(_ => scrollPos[key] = content.anchoredPosition.y);
            return content;
        }

        /// 이름·설명·단계 칸·버튼이 있는 한 줄 (기지 건물, 연구가 같이 쓴다). toggle 을 주면 가동/정지 버튼이 붙는다
        void UpgradeRow(Transform parent, float top, float h, string name, string desc, string effect, int level, int maxLevel,
            Color accent, string locked, int[] cost, string verb, Action buy, string toggle = null, bool toggleOn = false, Action onToggle = null)
        {
            bool max = level >= maxLevel, isLocked = locked != null;
            bool afford = !max && !isLocked && CanPay(cost);
            float inset = toggle != null ? 450 : 300;
            var row = Ui.Panel(parent, isLocked ? new Color(0.11f, 0.12f, 0.15f) : Ui.Card, true, "Row");
            row.Rt().TopBar(top, h, 30, 30);
            var n = Ui.Label(row.transform, name, 40, isLocked ? Ui.Dim : max ? accent : Ui.TextCol, TextAnchor.UpperLeft, true);
            n.rectTransform.Fill(28, 0, inset, 12);
            var d = Ui.Label(row.transform, desc, 26, Ui.Dim, TextAnchor.UpperLeft);
            d.rectTransform.Fill(28, 0, 300, 64);

            float pw = Mathf.Min(54, 560f / maxLevel - 6);
            for (int p = 0; p < maxLevel; p++)
            {
                var pip = Ui.Panel(row.transform, p < level ? accent : new Color(0, 0, 0, 0.45f), true, "Pip");
                pip.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 28 + p * (pw + 6), 16, pw, 22);
            }
            var eff = Ui.Label(row.transform, effect, 27, Ui.Gold, TextAnchor.LowerLeft, true);
            eff.rectTransform.Fill(40 + maxLevel * (pw + 6), 10, 290, 0);

            if (toggle != null)
            {
                var tb = Ui.Btn(row.transform, toggle, 26, toggleOn ? Primary : Off, () => { if (onToggle != null) onToggle(); }, toggleOn ? Ui.TextCol : Ui.Dim);
                tb.Rt().At(new Vector2(1, 1), new Vector2(1, 1), -296, -12, 136, 58);
            }

            int kinds = 0;
            if (cost != null) foreach (int c in cost) if (c > 0) kinds++;
            string label = isLocked ? locked : max ? "완료" : verb + "\n" + CostRich(cost, save.stock);
            var btn = Ui.Btn(row.transform, label, isLocked ? 26 : kinds >= 3 ? 27 : 32, afford ? Violet : Off, () => { if (buy != null) buy(); }, afford ? Ui.TextCol : Ui.Dim);
            btn.Rt().At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), -20, 0, 260, h - 28);
            btn.interactable = !max && !isLocked;
            btn.GetComponentInChildren<Text>().supportRichText = true;
        }

        // ───────── 기지 ─────────

        public void ShowBase()
        {
            EndRegion();
            Accrue();
            var root = NewScreen("Base", ShowBase, 230);
            Ui.Panel(root, Ui.Bg).Rt().Fill();

            var accent = Art.C(Content.Core.color);
            const float rowH = 172, gap = 12, headH = 56;
            int rn = BaseRules.Refineries.Length;
            var list = ScrollList(root, "base", TopH, 226, 10 + headH * 2 + (Content.RawCount + 1 + rn) * (rowH + gap) + 20);
            float y = 10;
            var cleared = Cleared;

            var h1 = Ui.Label(list, "채굴  ·  점령한 지역이 많을수록 생산량이 늘어납니다", 27, Ui.Dim, TextAnchor.MiddleLeft);
            h1.rectTransform.TopBar(y, headH, 40, 30);
            y += headH;
            for (int i = 0; i < Content.RawCount; i++)
            {
                int item = i, lv = save.mineLv[i];
                bool max = lv >= BaseRules.MaxMineLevel;
                float mul = 1 + Research.Level(save.research, Research.Mining) * Research.Defs[Research.Mining].perLevel;
                float now = BaseRules.MineRate(i, lv) * mul, next = BaseRules.MineRate(i, lv + 1) * mul;
                float conquest = BaseRules.ConquestRate(i, cleared);
                string desc = $"분당 {Content.Items[i].name} {now:0.#}" + (conquest > 0 ? $"  +  점령 지역 {conquest:0.##}" : "");
                string effect = max ? "최대" : $"→ {next:0.#}/분";
                string locked = CanBuildMine(i) ? null : $"지역 {BaseRules.CrystalMineRegion}\n점령 시 해금";
                UpgradeRow(list, y, rowH, $"{BaseRules.MineNames[i]}  Lv {lv}", desc, effect, lv, BaseRules.MaxMineLevel,
                    Art.C(Content.Items[i].color), locked, max ? null : BaseRules.MineCost(i, lv), lv == 0 ? "건설" : "강화", () => { if (UpgradeMine(item)) ShowBase(); });
                y += rowH + gap;
            }
            {
                int lv = save.storageLv;
                bool max = lv >= BaseRules.MaxStorageLevel;
                UpgradeRow(list, y, rowH, $"창고  Lv {lv}", $"자원마다 {StorageCap:N0} 까지 쌓인다", max ? "최대" : $"→ {BaseRules.StorageCap(lv + 1):N0}",
                    lv, BaseRules.MaxStorageLevel, accent, null, max ? null : BaseRules.StorageCost(lv), "강화", () => { if (UpgradeStorage()) ShowBase(); });
                y += rowH + gap;
            }

            var h2 = Ui.Label(list, "가공  ·  원광을 가공 자원으로 바꿉니다. 멈춰 두면 원료를 쓰지 않습니다", 27, Ui.Dim, TextAnchor.MiddleLeft);
            h2.rectTransform.TopBar(y, headH, 40, 30);
            y += headH;
            for (int k = 0; k < rn; k++)
            {
                int id = k, lv = save.refLv[k];
                var d = BaseRules.Refineries[k];
                bool max = lv >= BaseRules.MaxRefineryLevel, on = save.refOn[k];
                float now = BaseRules.RefineryRate(d, Mathf.Max(1, lv)), next = BaseRules.RefineryRate(d, lv + 1);
                var sb = new System.Text.StringBuilder(lv > 0 && !on ? "정지 중  ·  분당 " : "분당 ");
                for (int j = 0; j < d.inputs.Length; j++)
                    sb.Append(j > 0 ? " + " : "").Append(Content.Items[d.inputs[j].item].name).Append(' ').Append((now * d.inputs[j].amount).ToString("0.#"));
                sb.Append(" → ").Append(Content.Items[d.output].name).Append(' ').Append(now.ToString("0.#"));
                string effect = max ? "최대" : lv == 0 ? "" : $"→ {next:0.#}/분";
                string locked = CanBuildRefinery(k) ? null : $"지역 {d.reqRegion}\n점령 시 해금";
                UpgradeRow(list, y, rowH, lv > 0 ? $"{d.name}  Lv {lv}" : d.name, sb.ToString(), effect, lv, BaseRules.MaxRefineryLevel,
                    Art.C(Content.Items[d.output].color), locked, max ? null : BaseRules.RefineryCost(d, lv), lv == 0 ? "건설" : "강화", () => { if (UpgradeRefinery(id)) ShowBase(); },
                    lv > 0 ? (on ? "가동 중" : "정지") : null, on, () => { ToggleRefinery(id); ShowBase(); });
                y += rowH + gap;
            }

            var top = Ui.Panel(root, Ui.PanelCol);
            top.Rt().TopBar(0, TopH);
            var title = Ui.Label(top.transform, "기지", 64, null, TextAnchor.UpperLeft, true);
            title.rectTransform.Fill(34, 0, 20, 20);
            var hint = Ui.Label(top.transform, $"창고 한도 {StorageCap:N0}  ·  게임을 꺼 두어도 8시간까지 생산합니다", 27, Ui.Dim, TextAnchor.UpperLeft);
            hint.rectTransform.Fill(34, 0, 20, 104);
            StockBar(top.transform, -164);

            int buyable = 0;
            foreach (var d in Research.Defs)
                if (ResearchAvailable(d) && CanPay(ResearchRemaining(d))) buyable++;
            var bottom = Ui.Panel(root, Ui.Bg);
            bottom.Rt().BottomBar(0, 226);
            var lab = Ui.Btn(bottom.transform, buyable > 0 ? $"연구소\n연구 가능 {buyable}" : "연구소", buyable > 0 ? 38 : 46, Violet, ShowResearch);
            lab.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 30, 56, 380, 150);
            var go = Ui.Btn(bottom.transform, "심연으로 출격", 56, Primary, ShowAbyssSelect);
            go.Rt().At(new Vector2(1, 0), new Vector2(1, 0), -30, 56, 620, 150);

            if (offlineNote != null) { Toast(offlineNote); offlineNote = null; }
        }

        // ───────── 심연 선택 ─────────

        public void ShowAbyssSelect()
        {
            EndRegion();
            var root = NewScreen("AbyssSelect");
            Ui.Panel(root, Ui.Bg).Rt().Fill();
            var head = Ui.Label(root, "심연 선택", 72, null, TextAnchor.MiddleCenter, true);
            head.rectTransform.TopBar(70, 110);
            var hint = Ui.Label(root, "내려갈 심연을 고르세요. 심연마다 100개의 지역이 있습니다.", 32, Ui.Dim);
            hint.rectTransform.TopBar(180, 50);

            for (int i = 0; i < Content.Abysses.Length; i++)
            {
                var a = Content.Abysses[i];
                int id = i;
                var accent = Art.C(a.accent);
                var card = Ui.Btn(root, null, 0, Ui.Card, () => ShowRegionMap(id));
                card.Rt().TopBar(280 + i * 430, 400, 50, 50);

                var swatch = Ui.Panel(card.transform, Art.C(a.floorColor), true, "Swatch");
                swatch.raycastTarget = false;
                swatch.Rt().At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), 30, 0, 250, 340);
                var pit = Ui.Icon(swatch.transform, Art.Get("fx_disc"), Art.C(a.wallColor));
                pit.Rt().At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, 190, 190);
                var glow = Ui.Icon(swatch.transform, Art.Get("fx_circle"), accent);
                glow.Rt().At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, 150, 150);

                var name = Ui.Label(card.transform, a.name, 58, accent, TextAnchor.UpperLeft, true);
                name.rectTransform.Fill(310, 0, 30, 34);
                var desc = Ui.Label(card.transform, a.desc, 31, Ui.TextCol, TextAnchor.UpperLeft);
                desc.rectTransform.Fill(310, 0, 30, 120);
                string traits = $"적 체력 ×{a.enemyHealthMul:0.##}   속도 ×{a.enemySpeedMul:0.##}\n적 수 ×{a.enemyCountMul:0.##}   전리품 ×{a.bountyMul:0.##}";
                var tr = Ui.Label(card.transform, traits, 28, Ui.Dim, TextAnchor.UpperLeft);
                tr.rectTransform.Fill(310, 0, 30, 215);

                int cleared = save.abysses[i].cleared;
                var prog = Ui.Label(card.transform, cleared >= Content.RegionCount ? "점령 완료" : $"지역 {cleared} / {Content.RegionCount} 점령", 34, Ui.Gold, TextAnchor.LowerLeft, true);
                prog.rectTransform.Fill(310, 34, 30, 0);
                var barBack = Ui.Panel(card.transform, new Color(0, 0, 0, 0.5f), true, "Bar");
                barBack.raycastTarget = false;
                barBack.Rt().At(new Vector2(1, 0), new Vector2(1, 0), -30, 40, 300, 22);
                var bar = Ui.Panel(barBack.transform, accent, true, "Fill");
                bar.raycastTarget = false;
                bar.Rt().anchorMin = Vector2.zero; bar.Rt().anchorMax = new Vector2(Mathf.Max(0.04f, cleared / (float)Content.RegionCount), 1);
                bar.Rt().offsetMin = bar.Rt().offsetMax = Vector2.zero;
            }

            var back = Ui.Btn(root, "기지", 36, Ui.Card, ShowBase);
            back.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 50, 50, 240, 96);
        }

        // ───────── 지역 지도: 100개 지역이 아래로 이어진다 ─────────

        const float NodeGap = 190, NodeTop = 160;

        static Vector2 NodePos(int region) => new Vector2(Mathf.Sin(region * 0.85f) * 300, -(NodeTop + (region - 1) * NodeGap));

        public void ShowRegionMap(int abyss)
        {
            EndRegion();
            AbyssId = abyss;
            var a = Content.Abysses[abyss];
            var sv = save.abysses[abyss];
            var accent = Art.C(a.accent);
            var root = NewScreen("RegionMap");
            Ui.Panel(root, Color.Lerp(Art.C(a.wallColor), Color.black, 0.35f)).Rt().Fill();

            // 스크롤 영역
            var view = Ui.Panel(root, new Color(0, 0, 0, 0.01f), false, "View");
            view.Rt().Fill(0, 0, 0, 170);
            view.gameObject.AddComponent<RectMask2D>();
            mapScroll = view.gameObject.AddComponent<ScrollRect>();
            var content = Ui.Node("Content", view.transform);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            float height = NodeTop + Content.RegionCount * NodeGap + 900;
            content.sizeDelta = new Vector2(0, height);
            mapScroll.content = content; mapScroll.viewport = view.Rt();
            mapScroll.horizontal = false; mapScroll.vertical = true;
            mapScroll.movementType = ScrollRect.MovementType.Clamped;
            mapScroll.scrollSensitivity = 60;

            // 깊어질수록 어두워지는 배경 띠
            for (int i = 0; i < 10; i++)
            {
                var band = Ui.Panel(content, Color.Lerp(Art.C(a.floorColor) * 0.75f, new Color(0.02f, 0.02f, 0.03f), i / 9f), false, "Band");
                band.raycastTarget = false;
                band.Rt().TopBar(i * height / 10f, height / 10f + 2);
            }

            // 이음선
            for (int r = 1; r < Content.RegionCount; r++)
            {
                Vector2 p = NodePos(r), q = NodePos(r + 1);
                var line = Ui.Panel(content, r <= sv.cleared ? accent : new Color(1, 1, 1, 0.12f), false, "Line");
                line.raycastTarget = false;
                var rt = line.Rt();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = (p + q) * 0.5f;
                rt.sizeDelta = new Vector2((q - p).magnitude, r <= sv.cleared ? 10 : 6);
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(q.y - p.y, q.x - p.x) * Mathf.Rad2Deg);
            }

            // 지역 노드
            for (int r = 1; r <= Content.RegionCount; r++)
            {
                int region = r;
                bool cleared = r <= sv.cleared, current = r == sv.cleared + 1, boss = r % 10 == 0;
                float size = boss ? 150 : 118;
                Color col = cleared ? accent * 0.75f : current ? accent : new Color(0.2f, 0.21f, 0.26f);
                col.a = 1;
                if (current)
                {
                    var halo = Ui.Icon(content, Art.Get("fx_circle"), accent);
                    halo.Rt().At(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), NodePos(r).x, NodePos(r).y, size * 2.1f, size * 2.1f);
                }
                if (boss)
                {
                    var rim = Ui.Icon(content, Art.Get("fx_disc"), cleared || current ? Ui.Gold : new Color(0.45f, 0.36f, 0.2f));
                    rim.Rt().At(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), NodePos(r).x, NodePos(r).y, size + 16, size + 16);
                }
                var img = Ui.Icon(content, Art.Get("fx_disc"), col);
                img.raycastTarget = true;
                img.Rt().At(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), NodePos(r).x, NodePos(r).y, size, size);
                var btn = img.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => ShowRegionSheet(abyss, region));
                Ui.Label(img.transform, r.ToString(), boss ? 52 : 44, cleared || current ? new Color(0.06f, 0.07f, 0.09f) : Ui.Dim, TextAnchor.MiddleCenter, true);
                bool assault = RegionRules.IsAssault(r);
                string tag = cleared ? "점령" : boss ? "군주" : assault ? "적 거점" : null;
                if (tag != null)
                {
                    var t = Ui.Label(content, tag, 26, cleared ? accent : assault ? Ui.Bad : Ui.Gold, TextAnchor.MiddleCenter, true);
                    t.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), NodePos(r).x, NodePos(r).y - size * 0.5f - 22, 160, 34);
                }
            }

            // 머리줄
            var top = Ui.Panel(root, Ui.PanelCol);
            top.Rt().TopBar(0, 170);
            var back = Ui.Btn(top.transform, "뒤로", 34, Ui.Card, ShowAbyssSelect);
            back.Rt().At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), 24, 0, 170, 96);
            var name = Ui.Label(top.transform, a.name, 52, accent, TextAnchor.UpperLeft, true);
            name.rectTransform.Fill(220, 0, 230, 22);
            var baseBtn = Ui.Btn(top.transform, "기지", 34, Violet, ShowBase);
            baseBtn.Rt().At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), -24, 0, 190, 96);
            var sub = Ui.Label(top.transform, $"지역 {sv.cleared} / {Content.RegionCount} 점령\n{StockRich()}", 27, Ui.Dim, TextAnchor.LowerLeft);
            sub.supportRichText = true;
            sub.rectTransform.Fill(220, 16, 230, 0);

            ScrollToRegion(Mathf.Min(sv.cleared + 1, Content.RegionCount));
            sheet = null;
        }

        public void ScrollToRegion(int region)
        {
            if (mapScroll == null) return;
            Canvas.ForceUpdateCanvases();
            float viewH = mapScroll.viewport.rect.height, contentH = mapScroll.content.rect.height;
            float y = Mathf.Clamp(-NodePos(region).y - viewH * 0.25f, 0, Mathf.Max(0, contentH - viewH));
            mapScroll.content.anchoredPosition = new Vector2(0, y);
            mapScroll.velocity = Vector2.zero;
        }

        /// 지역을 고르면 아래에서 올라오는 판: 지역 정보, 첫 점령 보상, 가져갈 자원 설정, 출격
        public void ShowRegionSheet(int abyss, int region)
        {
            if (sheet != null) Destroy(sheet.gameObject);
            Accrue();
            var a = Content.Abysses[abyss];
            var r = RegionRules.For(abyss, region);
            bool unlocked = IsUnlocked(abyss, region);
            bool first = region > save.abysses[abyss].cleared;
            var accent = Art.C(a.accent);

            // 가진 자원만 줄로 보여 준다 (철광·황석은 언제나)
            var shown = new System.Collections.Generic.List<int>();
            for (int i = 0; i < Content.Items.Length; i++) if (i < 2 || save.stock[i] >= 1) shown.Add(i);
            const float rowStep = 108;

            var panel = Ui.Panel(screen, Ui.PanelCol, false, "Sheet");
            sheet = panel.Rt();
            sheet.BottomBar(0, 404 + shown.Count * rowStep + 186);
            var line = Ui.Panel(sheet, accent, false, "Line");
            line.Rt().TopBar(0, 6);

            var title = Ui.Label(sheet, $"지역 {region}" + (r.hasBoss ? "  ·  심연 군주" : r.assault ? "  ·  적 거점" : ""), 58, r.hasBoss ? Ui.Gold : r.assault ? Ui.Bad : Ui.TextCol, TextAnchor.UpperLeft, true);
            title.rectTransform.Fill(40, 0, 180, 30);
            var close = Ui.Btn(sheet, "닫기", 30, Ui.Card, () => { if (sheet != null) Destroy(sheet.gameObject); sheet = null; });
            close.Rt().At(new Vector2(1, 1), new Vector2(1, 1), -30, -28, 140, 76);

            var names = new System.Text.StringBuilder();
            foreach (var g in WaveGen.Generate(r))
            {
                if (names.Length > 0) names.Append(", ");
                names.Append(g.enemy.name);
            }
            string body =
                (r.assault ? $"맵 {r.mapW}×{r.mapH}칸  ·  적 핵 {r.spawnCount}개를 유닛으로 모두 부수면 점령  ·  웨이브는 끝없이 옵니다\n"
                           : $"맵 {r.mapW}×{r.mapH}칸  ·  웨이브 {r.winWave}회  ·  출현 지점 {r.spawnCount}곳  ·  적 체력 ×{r.enemyHealthMul:0.0}\n") +
                $"적: {names}";
            var info = Ui.Label(sheet, body, 29, Ui.Dim, TextAnchor.UpperLeft);
            info.lineSpacing = 1.3f;
            info.rectTransform.Fill(40, 0, 40, 118);

            // 첫 점령 보상
            string reward;
            if (first)
            {
                BaseRules.RegionBonus(region, out int item, out float perMin);
                reward = $"첫 점령 보상   기지 생산 {Content.Items[item].name} +{perMin:0.##}/분" + (r.assault ? "  (거점 보상 2배)" : "");
                int best = BestCleared;
                if (best < region)
                {
                    string res = "", bld = "";
                    foreach (var d in Research.Defs)
                        if (d.reqRegion == region) res += (res.Length > 0 ? ", " : "") + d.name;
                    if (region == BaseRules.CrystalMineRegion) bld = BaseRules.MineNames[Content.Crystal];
                    foreach (var rf in BaseRules.Refineries)
                        if (rf.reqRegion == region) bld += (bld.Length > 0 ? ", " : "") + rf.name;
                    if (res.Length > 0) reward += $"\n새 연구 해금   {res}";
                    if (bld.Length > 0) reward += $"\n새 기지 건물 해금   {bld}";
                }
            }
            else reward = "이미 점령한 지역입니다. 전리품만 얻습니다.";
            var rw = Ui.Label(sheet, reward, 30, first ? Ui.Gold : Ui.Dim, TextAnchor.UpperLeft, true);
            rw.lineSpacing = 1.25f;
            rw.rectTransform.Fill(40, 0, 40, 216);

            // 가져갈 자원
            int cap = LoadoutCap;
            var lt = Ui.Label(sheet, $"가져갈 자원  (종류마다 최대 {cap:N0}, 기본 보급 철광 {r.startItems[Content.Iron]} 은 따로 받습니다)", 27, Ui.TextCol, TextAnchor.UpperLeft);
            lt.rectTransform.Fill(40, 0, 40, 356);
            for (int k = 0; k < shown.Count; k++)
            {
                int item = shown[k];
                int have = (int)save.stock[item], limit = Mathf.Min(cap, have);
                var row = Ui.Panel(sheet, Ui.Card, true, "Loadout");
                row.Rt().TopBar(404 + k * rowStep, rowStep - 10, 30, 30);
                Ui.Icon(row.transform, Art.Get("item_" + Content.Items[item].key)).Rt().At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), 18, 0, 52, 52);
                var nm = Ui.Label(row.transform, Content.Items[item].name, 31, null, TextAnchor.UpperLeft, true);
                nm.rectTransform.Fill(86, 0, 600, 12);
                var hv = Ui.Label(row.transform, $"보유 {have:N0}", 24, Ui.Dim, TextAnchor.LowerLeft);
                hv.rectTransform.Fill(86, 11, 600, 0);
                var val = Ui.Label(row.transform, "", 42, Ui.Gold, TextAnchor.MiddleCenter, true);
                val.rectTransform.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), -380, 0, 190, 76);
                Action show = () => val.text = LoadoutAmount(item).ToString("N0");
                Action<int> set = v => { save.loadout[item] = Mathf.Clamp(v, 0, limit); show(); };
                var minus = Ui.Btn(row.transform, "-50", 30, Off, () => set(LoadoutAmount(item) - 50));
                minus.Rt().At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), -580, 0, 120, 76);
                var plus = Ui.Btn(row.transform, "+50", 30, Off, () => set(LoadoutAmount(item) + 50));
                plus.Rt().At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), -250, 0, 120, 76);
                var maxB = Ui.Btn(row.transform, "최대", 30, Off, () => set(limit));
                maxB.Rt().At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), -20, 0, 110, 76);
                var zero = Ui.Btn(row.transform, "0", 30, Off, () => set(0));
                zero.Rt().At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), -135, 0, 110, 76);
                show();
            }

            var go = Ui.Btn(sheet, unlocked ? "출격" : "이전 지역을 먼저 점령하세요", unlocked ? 54 : 34,
                unlocked ? Primary : Ui.Card, () => { if (unlocked) StartRegion(abyss, region); }, unlocked ? Ui.TextCol : Ui.Dim);
            go.Rt().At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 40, 1000, 130);
            go.interactable = unlocked;
        }
    }
}
