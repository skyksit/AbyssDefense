using System.Collections.Generic;
using AbyssDefense.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace AbyssDefense
{
    /// 미니맵을 누르거나 끌면 그 자리(0~1)를 알려 준다
    public sealed class MiniClick : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IDragHandler
    {
        public System.Action<Vector2> on;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { Report(e); }
        public void OnDrag(UnityEngine.EventSystems.PointerEventData e) { Report(e); }
        void Report(UnityEngine.EventSystems.PointerEventData e)
        {
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var lp)) return;
            var r = rt.rect;
            on?.Invoke(new Vector2(Mathf.Clamp01((lp.x - r.xMin) / r.width), Mathf.Clamp01((lp.y - r.yMin) / r.height)));
        }
    }

    /// 지역 플레이 화면의 HUD: 위 상태 줄, 아래 건설 바, 공장 창, 결과 창
    public sealed partial class GameManager
    {
        sealed class BuildSlot { public BlockDef def; public Image bg; public Text cost, name; public Image icon; }

        readonly List<BuildSlot> slots = new List<BuildSlot>();
        Text[] itemTexts;
        Text waveText, timerText, infoText, speedText, pauseText, hpText, stanceText;
        RectTransform hpFill;
        Image removeBg, stanceBg, rebuildBg;
        Text rebuildText;
        Button nextWaveBtn;
        string infoOverride;
        float infoUntil;

        // 공장을 탭하면 뜨는 창: 만들 유닛 고르기, 생산 정지, 집결 지점
        const int ProductButtons = 12;
        const float ProductRowPx = 1002, ProductGapPx = 8;   // 생산 버튼 한 줄의 너비. 6종까지는 한 줄(6종이면 버튼을 좁힌다), 7종부터는 두 줄로 놓고 창을 그만큼 키운다
        const float FactoryPanelPx = 284, ProductRowStepPx = 124;
        GameObject factoryPanel;
        Text factoryTitle;
        readonly Text[] prodText = new Text[ProductButtons];
        readonly Image[] prodBg = new Image[ProductButtons];
        Image stopBg, rallyBg, facStanceBg;
        Text facStanceText;
        static readonly string[] FactoryStanceNames = { "태세: 전체를 따름", "태세: 늘 방어", "태세: 늘 공격" };

        // 다음 회차에 나올 적 (위 단추 줄 아래의 띠), 보스 체력 막대, 미니맵
        const int PreviewSlots = 9;
        GameObject previewStrip, bossBar;
        Text previewLabel, bossNote, bossText;
        RectTransform bossFill;
        readonly Image[] previewIcon = new Image[PreviewSlots];
        readonly Image[] previewDot = new Image[PreviewSlots];
        readonly Text[] previewCount = new Text[PreviewSlots];
        readonly List<WavePreview> preview = new List<WavePreview>();
        Texture2D miniTex;
        Color32[] miniBase, miniPx;
        Image miniFrame;
        float miniTimer, coreAlertUntil, lastCoreHp;
        Image blizzardTint;
        int lastBlizzardIn, lastBlizzardLeft;

        // 포탑을 탭하면 뜨는 창: 넣을 탄, 겨눌 적
        const int AmmoButtons = 4;
        const float TurretPanelPx = 284;
        static readonly string[] ModeNames = { "가까운 적", "튼튼한 적", "핵에 가까운 적", "비행체 먼저" };
        GameObject turretPanel;
        Text turretTitle;
        readonly Text[] ammoText = new Text[AmmoButtons];
        readonly Image[] ammoBg = new Image[AmmoButtons];
        readonly Image[] modeBg = new Image[4];
        Image overBg;
        Text overText;

        void BuildHud()
        {
            var root = NewScreen("Hud", null, HudBottomPx + 24);
            slots.Clear();

            // 눈보라가 부는 동안 화면 전체에 엷게 깔리는 흰 막
            blizzardTint = Ui.Panel(root, new Color(0.82f, 0.92f, 1f, 0.09f), false, "Blizzard");
            blizzardTint.raycastTarget = false;
            blizzardTint.Rt().Fill();
            blizzardTint.gameObject.SetActive(false);
            lastBlizzardIn = sim.blizzardIn; lastBlizzardLeft = sim.blizzardLeft;

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
            nextWaveBtn = Ui.Btn(root, "다음 웨이브 부르기", 27, new Color(0.55f, 0.22f, 0.25f, 0.95f), () => sim.CmdCallWave());
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

            // ── 그 아래: 다음 회차의 적 · 보스 체력 · 미니맵 ──
            float sy = by - 78 - 8;
            int mw = 200, mh = Mathf.Min(300, Mathf.RoundToInt(200f * sim.world.H / sim.world.W));
            var strip = Ui.Panel(root, new Color(0, 0, 0, 0.5f), true, "NextWave");
            strip.raycastTarget = false;
            strip.Rt().At(new Vector2(0, 1), new Vector2(0, 1), 20, sy, 1040 - mw - 14, 62);
            previewStrip = strip.gameObject;
            previewLabel = Ui.Label(strip.transform, "다음", 25, Ui.Dim, TextAnchor.MiddleCenter, true);
            previewLabel.rectTransform.At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), 6, 0, 74, 56);
            for (int i = 0; i < PreviewSlots; i++)
            {
                var slot = Ui.Node("Slot", strip.transform).At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), 84 + i * 72, 0, 70, 56);
                previewIcon[i] = Ui.Icon(slot, null);
                previewIcon[i].Rt().At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), 0, 0, 36, 36);
                previewDot[i] = Ui.Icon(previewIcon[i].transform, Art.Get("fx_disc"));
                previewDot[i].Rt().At(new Vector2(1, 1), new Vector2(0.5f, 0.5f), 0, 0, 13, 13);
                previewCount[i] = Ui.Label(slot, "", 23, null, TextAnchor.MiddleLeft, true);
                previewCount[i].rectTransform.Fill(38, 0, 0, 0);
            }
            bossNote = Ui.Label(strip.transform, "", 24, Ui.Gold, TextAnchor.MiddleRight, true);
            bossNote.rectTransform.Fill(0, 0, 14, 0);

            var bb = Ui.Panel(root, new Color(0, 0, 0, 0.7f), true, "Boss");
            bb.raycastTarget = false;
            bb.Rt().At(new Vector2(0, 1), new Vector2(0, 1), 20, sy - 62 - 8, 1040 - mw - 14, 46);
            bossBar = bb.gameObject;
            var bf = Ui.Panel(bb.transform, Art.C(Content.Boss.color), true, "Fill");
            bf.raycastTarget = false;
            bossFill = bf.Rt();
            bossFill.anchorMin = Vector2.zero; bossFill.anchorMax = Vector2.one;
            bossFill.offsetMin = new Vector2(3, 3); bossFill.offsetMax = new Vector2(-3, -3);
            bossText = Ui.Label(bb.transform, "", 25, Color.white, TextAnchor.MiddleCenter, true);
            bossBar.SetActive(false);

            BuildHints(root, sy - 62 - 8 - 46 - 8, 1040 - mw - 14);

            miniFrame = Ui.Panel(root, new Color(0, 0, 0, 0.8f), false, "Minimap");
            miniFrame.Rt().At(new Vector2(1, 1), new Vector2(1, 1), -20, sy, mw + 8, mh + 8);
            miniTex = new Texture2D(sim.world.W, sim.world.H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var raw = Ui.Node("Map", miniFrame.transform).gameObject.AddComponent<RawImage>();
            raw.texture = miniTex;
            raw.rectTransform.Fill(4, 4, 4, 4);
            raw.gameObject.AddComponent<MiniClick>().on = uv => play.LookAt(uv.x * sim.world.W, uv.y * sim.world.H);
            miniBase = new Color32[sim.world.W * sim.world.H];
            miniPx = new Color32[miniBase.Length];
            Color32 mFloor = Art.C(sim.abyss.floorColor), mWall = Art.C(sim.abyss.wallColor), mWater = new Color32(40, 90, 150, 255);
            Color32 mHazard = sim.rules.hazard == Hazard.Lava ? new Color32(190, 70, 25, 255) : new Color32(110, 150, 40, 255);
            for (int i = 0; i < miniBase.Length; i++) { var mt = sim.world.tiles[i]; miniBase[i] = mt.water ? mWater : mt.wall ? mWall : mt.hazard ? mHazard : mFloor; }
            miniTimer = 0; lastCoreHp = sim.Core.health; coreAlertUntil = 0;

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
            bRem.Rt().At(new Vector2(1, 0), new Vector2(1, 0), -20, 14 + 106, remW, slotH - 106);
            removeBg = bRem.GetComponent<Image>();
            var bReb = Ui.Btn(bottom.transform, "자동 재건", 24, Ui.Card, ToggleAutoRebuild);
            bReb.Rt().At(new Vector2(1, 0), new Vector2(1, 0), -20, 14, remW, 96);
            rebuildBg = bReb.GetComponent<Image>();
            rebuildText = bReb.GetComponentInChildren<Text>();

            // ── 공장 창 (공장을 탭했을 때만 보인다) ──
            var fp = Ui.Panel(root, Ui.PanelCol, true, "Factory");
            fp.Rt().BottomBar(HudBottomPx + 10, FactoryPanelPx, 16, 16);
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
            sb.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 20, 12, 300, 84);
            // 이 공장에서 나온 유닛만 따로 태세를 정한다 (누를 때마다 전체를 따름 → 늘 방어 → 늘 공격)
            var fsb = Ui.Btn(fp.transform, FactoryStanceNames[0], 25, Ui.Card, CycleFactoryStance);
            fsb.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 330, 12, 340, 84);
            facStanceBg = fsb.GetComponent<Image>();
            facStanceText = fsb.GetComponentInChildren<Text>();
            stopBg = sb.GetComponent<Image>();
            var rb = Ui.Btn(fp.transform, "집결 지점 옮기기", 27, Ui.Card, () => play.BeginRally());
            rb.Rt().At(new Vector2(1, 0), new Vector2(1, 0), -20, 12, 350, 84);
            rallyBg = rb.GetComponent<Image>();
            factoryPanel.SetActive(false);

            // ── 포탑 창 (포탑을 탭했을 때만 보인다) ──
            var tp = Ui.Panel(root, Ui.PanelCol, true, "Turret");
            tp.Rt().BottomBar(HudBottomPx + 10, TurretPanelPx, 16, 16);
            turretPanel = tp.gameObject;
            turretTitle = Ui.Label(tp.transform, "", 28, null, TextAnchor.MiddleLeft, true);
            turretTitle.rectTransform.TopBar(8, 46, 22, 22);
            float aw = (ProductRowPx - ProductGapPx * (AmmoButtons - 1)) / AmmoButtons;
            for (int i = 0; i < AmmoButtons; i++)
            {
                int k = i;
                var ab = Ui.Btn(tp.transform, " ", 22, Ui.Card, () => SetAmmo(k));
                ab.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 20 + i * (aw + ProductGapPx), 106, aw, 116);
                ammoBg[i] = ab.GetComponent<Image>();
                ammoText[i] = ab.GetComponentInChildren<Text>();
                ammoText[i].supportRichText = true;
            }
            float mwid = (ProductRowPx - ProductGapPx * 4) / 5;
            for (int i = 0; i < modeBg.Length; i++)
            {
                int k = i;
                var mb = Ui.Btn(tp.transform, ModeNames[i], 22, Ui.Card, () => SetTargetMode((TargetMode)k));
                mb.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 20 + i * (mwid + ProductGapPx), 12, mwid, 84);
                modeBg[i] = mb.GetComponent<Image>();
            }
            // 과부하: 회로를 태워 1.5배 빨리 쏜다
            var ob = Ui.Btn(tp.transform, "과부하", 22, Ui.Card, ToggleOvercharge);
            ob.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 20 + 4 * (mwid + ProductGapPx), 12, mwid, 84);
            overBg = ob.GetComponent<Image>();
            overText = ob.GetComponentInChildren<Text>();
            overText.supportRichText = true;
            turretPanel.SetActive(false);

            UpdateHud();
        }

        /// 방어 ↔ 공격. 모든 유닛에 함께 적용된다
        public void ToggleStance()
        {
            if (sim == null) return;
            sim.CmdStance(sim.stance == UnitStance.Defend ? UnitStance.Attack : UnitStance.Defend);
            Toast(sim.stance == UnitStance.Defend ? "방어 태세: 유닛이 핵 옆 집결 지점으로 돌아와 핵을 지킵니다" : "공격 태세: 유닛이 적 출현 지점 앞으로 나가 싸웁니다");
        }

        /// 자동 재건 켜기·끄기. 끄면 적어 둔 자리도 지운다
        public void ToggleAutoRebuild()
        {
            if (sim == null) return;
            sim.CmdAutoRebuild(!sim.autoRebuild);
            Toast(sim.autoRebuild ? "자동 재건 켬: 적에게 부서진 건물을 10초 뒤 같은 자리에 다시 짓습니다 (비용은 그대로)" : "자동 재건 끔");
        }

        /// 고른 공장의 유닛 태세를 다음 것으로 바꾼다
        public void CycleFactoryStance()
        {
            var f = play != null ? play.Factory : null;
            if (f == null) return;
            var next = (FactoryStance)(((int)f.stance + 1) % 3);
            sim.CmdFactoryStance(f, next);
            Toast(next == FactoryStance.Follow ? $"{f.def.name}의 유닛은 위의 전체 태세를 따릅니다" : next == FactoryStance.Defend ? $"{f.def.name}의 유닛은 전체 태세와 상관없이 집결 지점을 지킵니다" : $"{f.def.name}의 유닛은 전체 태세와 상관없이 적 출현 지점 앞으로 나갑니다");
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
            sim.CmdProduct(f, k);
        }

        /// 고른 포탑에 넣을 탄을 바꾼다. 장전해 둔 탄은 버린다
        public void SetAmmo(int k)
        {
            var t = play != null ? play.Turret : null;
            if (t == null || k >= t.def.ammos.Length) return;
            if (sim.CmdAmmo(t, k) && sim.Core.items[t.Ammo.item] <= 0) Toast($"{t.Ammo.name}: 핵에 {Content.Items[t.Ammo.item].name}이(가) 없습니다");
        }

        /// 고른 포탑의 과부하를 켜고 끈다
        public void ToggleOvercharge()
        {
            var t = play != null ? play.Turret : null;
            if (t == null) return;
            sim.CmdOvercharge(t, !t.overcharge);
            Toast(t.overcharge ? "과부하 켬: 쏠 적이 있는 동안 회로 하나로 10초씩 1.5배 빨리 쏩니다" : "과부하 끔");
        }

        /// 고른 포탑이 겨눌 적의 기준을 바꾼다
        public void SetTargetMode(TargetMode m)
        {
            var t = play != null ? play.Turret : null;
            if (t != null) sim.CmdMode(t, m);
        }

        /// 다음 회차의 적, 보스 예고, 보스 체력 막대
        void UpdatePreview()
        {
            bool show = !sim.AllWavesSent && !ResultShown;
            if (previewStrip.activeSelf != show) previewStrip.SetActive(show);
            if (show)
            {
                sim.Preview(sim.wave + 1, preview);
                bool bossNext = false;
                for (int i = 0; i < PreviewSlots; i++)
                {
                    bool has = i < preview.Count;
                    previewIcon[i].transform.parent.gameObject.SetActive(has);
                    if (!has) continue;
                    var pv = preview[i];
                    bool more = i == PreviewSlots - 1 && preview.Count > PreviewSlots;   // 자리가 모자라면 마지막 칸에 남은 종류 수
                    previewIcon[i].sprite = Art.Get("enemy_" + pv.def.key);
                    previewIcon[i].enabled = !more;
                    previewDot[i].enabled = !more && (pv.elite || pv.shield);
                    previewDot[i].color = pv.elite ? Ui.Gold : new Color(0.45f, 0.85f, 1f);
                    previewCount[i].text = more ? $"+{preview.Count - PreviewSlots + 1}종" : pv.count.ToString();
                    previewCount[i].color = pv.def.boss ? Ui.Bad : Ui.TextCol;
                    if (pv.def.boss) bossNext = true;
                }
                foreach (var pv in preview) if (pv.def.boss) bossNext = true;
                int bossIn = bossNext ? 1 : sim.BossIn(3);
                previewLabel.text = bossNext ? "보스!" : "다음";
                previewLabel.color = bossNext ? Ui.Bad : Ui.Dim;
                bossNote.text = bossIn >= 2 ? $"보스 {bossIn}회 뒤" : "";
            }

            float hp = 0, max = 0; string name = null; int n = 0;
            foreach (var e in sim.enemies)
                if (e.def.boss && !e.dead) { hp += e.health; max += e.maxHealth; n++; if (name == null) name = e.def.name; }
            bool boss = n > 0 && !ResultShown;
            if (bossBar.activeSelf != boss) bossBar.SetActive(boss);
            if (boss)
            {
                bossFill.anchorMax = new Vector2(Mathf.Max(0.01f, Mathf.Clamp01(hp / max)), 1);
                bossText.text = (n > 1 ? $"{name} ×{n}" : name) + $"  {Mathf.CeilToInt(hp):N0}";
            }
        }

        /// 서리 심연의 눈보라: 10초 전에 알리고, 부는 동안 화면에 흰 막을 깐다
        void UpdateWeather()
        {
            if (sim.rules.hazard != Hazard.Blizzard) return;
            if (blizzardTint.gameObject.activeSelf != sim.Blizzard) blizzardTint.gameObject.SetActive(sim.Blizzard);
            if (sim.blizzardIn <= Sim.Sim.BlizzardWarn && lastBlizzardIn > Sim.Sim.BlizzardWarn && !sim.Blizzard) Toast("10초 뒤 눈보라가 붑니다");
            if (sim.blizzardLeft > 0 && lastBlizzardLeft == 0) Toast("눈보라: 포탑과 유닛이 느리게 쏘고 비행체가 느려집니다 (20초)");
            if (sim.blizzardLeft == 0 && lastBlizzardLeft > 0) Toast("눈보라가 그쳤습니다");
            lastBlizzardIn = sim.blizzardIn; lastBlizzardLeft = sim.blizzardLeft;
        }

        /// 미니맵: 지형 위에 건물·적·유닛을 점으로. 초당 다섯 번 다시 그린다. 핵이 맞으면 테두리가 붉어진다
        void UpdateMinimap()
        {
            var core = sim.Core;
            if (core.health < lastCoreHp - 0.01f) coreAlertUntil = Time.unscaledTime + 0.8f;
            lastCoreHp = core.health;
            miniFrame.color = Time.unscaledTime < coreAlertUntil && Mathf.Repeat(Time.unscaledTime, 0.3f) < 0.15f ? new Color(1f, 0.2f, 0.2f, 0.95f) : new Color(0, 0, 0, 0.8f);
            miniTimer -= Time.unscaledDeltaTime;
            if (miniTimer > 0) return;
            miniTimer = 0.2f;
            var w = sim.world;
            System.Array.Copy(miniBase, miniPx, miniBase.Length);
            foreach (var b in w.buildings)
            {
                Color32 c = b is CoreBuilding ? new Color32(255, 255, 255, 255) : b.def.enemy ? new Color32(230, 60, 60, 255) : (Color32)Art.C(b.def.color);
                for (int dy = 0; dy < b.def.size; dy++)
                    for (int dx = 0; dx < b.def.size; dx++) miniPx[w.Idx(b.x + dx, b.y + dy)] = c;
            }
            foreach (int sp in w.spawns) miniPx[sp] = new Color32(255, 80, 90, 255);
            foreach (var u in sim.units) Dot((int)u.x, (int)u.y, new Color32(90, 220, 255, 255));
            foreach (var e in sim.enemies) Dot((int)e.x, (int)e.y, e.def.boss ? new Color32(255, 200, 60, 255) : new Color32(255, 70, 70, 255));
            // 화면에 보이는 범위
            float hh = cam.orthographicSize, hw = hh * cam.aspect;
            int x0 = Mathf.FloorToInt(cam.transform.position.x - hw), x1 = Mathf.CeilToInt(cam.transform.position.x + hw) - 1;
            int y0 = Mathf.FloorToInt(cam.transform.position.y - hh), y1 = Mathf.CeilToInt(cam.transform.position.y + hh) - 1;
            var frame = new Color32(255, 255, 255, 255);
            for (int x = x0; x <= x1; x++) { Dot(x, y0, frame); Dot(x, y1, frame); }
            for (int y = y0; y <= y1; y++) { Dot(x0, y, frame); Dot(x1, y, frame); }
            miniTex.SetPixels32(miniPx);
            miniTex.Apply(false);

            void Dot(int x, int y, Color32 c) { if (w.In(x, y)) miniPx[w.Idx(x, y)] = c; }
        }

        void UpdateTurretPanel()
        {
            var t = play.Turret;
            bool show = t != null && !ResultShown;
            if (turretPanel.activeSelf != show) turretPanel.SetActive(show);
            if (!show) return;
            string only = !t.def.targetGround ? "  ·  공중만" : !t.def.targetAir ? "  ·  지상만" : "";
            turretTitle.text = $"{t.def.name}  ·  체력 {Mathf.CeilToInt(t.health)}/{(int)t.maxHealth}  ·  사거리 {t.Range:0.#}{only}" + (t.Starved ? "  ·  탄약 없음" : "");
            turretTitle.color = t.Starved ? Ui.Bad : Ui.TextCol;
            for (int i = 0; i < AmmoButtons; i++)
            {
                bool has = i < t.def.ammos.Length;
                ammoBg[i].gameObject.SetActive(has);
                if (!has) continue;
                var a = t.def.ammos[i];
                long stock = sim.Core.items[a.item];
                int per = Mathf.RoundToInt(a.perItem * sim.world.mods.ammoMul);
                ammoBg[i].color = t.ammoIdx == i ? new Color(0.25f, 0.45f, 0.42f) : Ui.Card;
                ammoText[i].text = $"{a.name}\n<color=#{Content.Items[a.item].color:X6}>{Content.Items[a.item].name} {Ui.Num(stock)}</color> · {per}발\n<color=#9EA6B8>{a.desc}</color>";
                ammoText[i].fontSize = 20;
                ammoText[i].color = stock > 0 || (t.ammoIdx == i && t.ammo > 0) ? Ui.TextCol : Ui.Dim;
            }
            for (int i = 0; i < modeBg.Length; i++) modeBg[i].color = (int)t.mode == i ? new Color(0.25f, 0.45f, 0.42f) : Ui.Card;
            long oc = sim.Core.items[Sim.Sim.OverchargeItem];
            overBg.color = t.overcharge ? (oc > 0 || t.overchargeLeft > 0 ? new Color(0.62f, 0.44f, 0.16f) : new Color(0.5f, 0.24f, 0.26f)) : Ui.Card;
            overText.text = (t.overcharge ? "과부하 켬" : "과부하 끔") + $"\n<color=#{Content.Items[Sim.Sim.OverchargeItem].color:X6}>회로 {Ui.Num(oc)}</color>";
        }

        /// 건물을 탭했을 때 설명 줄에 잠깐 보여 준다
        public void ShowInfo(Building b)
        {
            if (b == null) return;
            int g = sim.BuildingGrade(b), gap = sim.rules.grade - g;
            string s = $"{b.def.name}  G{g}{(gap >= 2 ? " (이 지역보다 등급이 낮습니다)" : "")}  체력 {Mathf.CeilToInt(b.health)}/{(int)b.maxHealth}";
            if (!b.Built) s = $"{b.def.name}  건설 중 {Mathf.FloorToInt(b.progress * 100)}%" + (b.constructing ? "" : "  (순서를 기다리거나 자원이 부족합니다)");
            else if (b is TurretBuilding t)
                s += t.Starved ? $"  탄약 없음 ({Content.Items[t.Ammo.item].name} 부족)" : $"  {t.Ammo.name}: {Content.Items[t.Ammo.item].name} 하나에 {t.AmmoPerItem}발";
            else if (b is SupportBuilding sb)
            {
                if (b.def.shield > 0) s += $"  역장 {Mathf.CeilToInt(sb.shieldHp)}/{(int)sb.MaxShield}" + (sb.shieldHp <= 0 ? " (다 닳았습니다)" : "");
                s += $"  범위 {b.def.supportRange:0.#}칸";
            }
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
                sb.Append("<color=#").Append(Content.Items[c.item].color.ToString("X6")).Append('>').Append(Ui.Num(c.amount)).Append("</color>");
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
            int count = Mathf.Min(ProductButtons, f.def.units.Length);
            int rows = count > 6 ? 2 : 1;
            int perRow = (count + rows - 1) / rows;
            int cols = Mathf.Max(5, perRow);
            float pw = (ProductRowPx - ProductGapPx * (cols - 1)) / cols;
            factoryPanel.GetComponent<RectTransform>().BottomBar(HudBottomPx + 10, FactoryPanelPx + (rows - 1) * ProductRowStepPx, 16, 16);
            for (int i = 0; i < ProductButtons; i++)
            {
                bool has = i < count;
                prodBg[i].gameObject.SetActive(has);
                if (!has) continue;
                int row = i / perRow, col = i % perRow;   // 앞 단계가 윗줄
                prodBg[i].rectTransform.At(new Vector2(0, 0), new Vector2(0, 0), 20 + col * (pw + ProductGapPx), 106 + (rows - 1 - row) * ProductRowStepPx, pw, 116);
                prodText[i].fontSize = cols > 5 ? 19 : 22;
                var u = f.def.units[i];
                bool unlocked = sim.IsUnlocked(u);
                prodBg[i].color = f.product == i ? new Color(0.25f, 0.45f, 0.42f) : Ui.Card;
                prodText[i].text = unlocked ? $"{u.name}\n{CostText(u.cost)}\n<color=#9EA6B8>{u.buildTicks / 60}초 · 한도 {u.supply}</color>" : $"{u.name}\n<color=#9EA6B8>연구 필요</color>";
                prodText[i].color = unlocked ? Ui.TextCol : Ui.Dim;
            }
            stopBg.color = f.product < 0 ? new Color(0.5f, 0.24f, 0.26f) : Ui.Card;
            rallyBg.color = play.RallyMode ? new Color(0.55f, 0.45f, 0.18f) : Ui.Card;
            facStanceText.text = FactoryStanceNames[(int)f.stance];
            facStanceBg.color = f.stance == FactoryStance.Attack ? new Color(0.6f, 0.34f, 0.16f) : f.stance == FactoryStance.Defend ? new Color(0.16f, 0.36f, 0.42f) : Ui.Card;
        }

        void UpdateHud()
        {
            if (sim == null || itemTexts == null || waveText == null) return;
            var core = sim.Core;
            for (int i = 0; i < itemTexts.Length; i++)
            {
                itemTexts[i].text = Ui.Num(core.items[i]);
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
            rebuildBg.color = sim.autoRebuild ? new Color(0.2f, 0.42f, 0.3f) : Ui.Card;
            rebuildText.text = sim.autoRebuild ? (sim.ruins.Count > 0 ? $"자동 재건\n대기 {sim.ruins.Count}" : "자동 재건\n켬") : "자동 재건\n끔";
            bool attack = sim.stance == UnitStance.Attack;
            stanceText.text = (attack ? "공격 중 (출현 지점)" : "방어 중 (핵 수비)") + $"\n유닛 {sim.SupplyUsed}/{sim.UnitCap}  ·  눌러서 전환";
            stanceBg.color = attack ? new Color(0.6f, 0.34f, 0.16f, 0.95f) : new Color(0.16f, 0.36f, 0.42f, 0.95f);

            foreach (var s in slots)
            {
                bool unlocked = sim.IsUnlocked(s.def);
                bool afford = sim.CanAfford(s.def);
                s.bg.color = play.Selected == s.def ? new Color(0.25f, 0.45f, 0.42f) : Ui.Card;
                s.icon.color = unlocked ? Color.white : new Color(1, 1, 1, 0.25f);
                int g = System.Math.Max(s.def.grade, s.def.kind == BlockKind.Turret ? sim.world.mods.turretGrade : sim.world.mods.buildGrade);
                s.cost.text = unlocked ? $"<color=#{(sim.rules.grade - g >= 2 ? "F05A5A" : "9EA6B8")}>G{g}</color> " + CostText(s.def.cost) : "연구 필요";
                s.name.color = !unlocked ? Ui.Dim : afford ? Ui.TextCol : Ui.Bad;   // 자원이 모자라면 이름이 붉다
            }

            UpdateFactoryPanel();
            UpdateTurretPanel();
            UpdatePreview();
            UpdateMinimap();
            UpdateWeather();
            UpdateHints();

            if (infoOverride != null && Time.unscaledTime < infoUntil && play.Selected == null && !play.RemoveMode)
            {
                infoText.text = infoOverride; infoText.color = Ui.TextCol;
            }
            else if (play.RemoveMode) { infoText.text = "철거: 건물을 탭하거나 끌어서 지웁니다. 비용의 75%를 돌려받습니다."; infoText.color = Ui.Bad; }
            else if (play.Selected != null) { infoText.text = $"{play.Selected.name} — {play.Selected.desc}  건설 {play.Selected.buildTicks / 60f / sim.world.mods.buildSpeedMul:0.#}초"; infoText.color = Ui.TextCol; }
            else if (play.CommandMode) { infoText.text = "지휘기를 보낼 곳을 맵에서 탭하세요. 지휘기 곁의 건설은 1.5배 빠릅니다."; infoText.color = Ui.Gold; }
            else if (play.Turret != null) { infoText.text = "넣을 탄과 겨눌 적을 고르세요. 탄을 바꾸면 장전해 둔 탄은 버립니다."; infoText.color = Ui.Dim; }
            else if (play.Factory != null) { infoText.text = "만들 유닛을 고르세요. 방어 태세의 유닛은 집결 지점을 지킵니다. 가운데 단추로 이 공장만 태세를 따로 정합니다."; infoText.color = Ui.Dim; }
            else if (assault) { infoText.text = "적 거점: 유닛을 모아 공격 태세로 보내 적 핵을 모두 부수세요. 웨이브는 끝없이 옵니다."; infoText.color = Ui.Gold; }
            else { infoText.text = "건물을 골라 탭하거나 끌어서 짓습니다. 공장 탭 = 유닛, 포탑 탭 = 탄종, 핵 탭 = 지휘기."; infoText.color = Ui.Dim; }
        }

        // ───────── 결과 창 ─────────

        void ShowResult(bool won, long[] returned, string bonus, List<string> unlocked)
        {
            play.Cancel();
            factoryPanel.SetActive(false);
            turretPanel.SetActive(false);
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
                sb.Append($"{Content.Items[i].name}   가져감 {Ui.Num(Taken[i])}");
                if (sim.looted[i] > 0) sb.Append($"  ·  전리품 {Ui.Num(sim.looted[i])}");
                if (won) sb.Append($"  →  <color=#{Content.Items[i].color:X6}>{Ui.Num(returned[i])}</color>");
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

            // 다음 지역: 방금 점령한 지역과 이어진 곳 중 아직 점령하지 않은, 더 깊은 쪽의 가장 앞 번호 (없으면 이어진 아무 곳)
            int a = AbyssId, r = Region, next = 0;
            if (won)
            {
                foreach (int n in RegionGraph.Neighbors(r)) if (!IsDone(a, n) && n > r && (next == 0 || n < next)) next = n;
                if (next == 0) foreach (int n in RegionGraph.Neighbors(r)) if (!IsDone(a, n) && (next == 0 || n < next)) next = n;
            }
            bool nextAbyss = won && r == Content.RegionCount && a + 1 < Content.Abysses.Length;
            var main = Ui.Btn(card.transform, nextAbyss ? $"다음 심연: {Content.Abysses[a + 1].name}" : next > 0 ? $"다음 지역 {next}" : won ? "지역 지도" : "다시 도전", 44,
                won ? Primary : new Color(0.55f, 0.22f, 0.25f),
                () =>
                {
                    EndRegion();
                    if (nextAbyss) { ShowRegionMap(a + 1); ShowRegionSheet(a + 1, 1); }
                    else { ShowRegionMap(a); if (next > 0 || !won) ShowRegionSheet(a, next > 0 ? next : r); }
                });
            main.Rt().At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 150, 780, 110);
            float half = (780 - 20) / 2f;
            var back = Ui.Btn(card.transform, "기지", 38, Violet, ShowBase);
            back.Rt().At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -(half + 20) / 2, 40, half, 96);
            var map = Ui.Btn(card.transform, "지역 지도", 38, Ui.Card, () => { EndRegion(); ShowRegionMap(a); });
            map.Rt().At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), (half + 20) / 2, 40, half, 96);
        }
    }
}
