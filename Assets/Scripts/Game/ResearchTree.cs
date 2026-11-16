using System;
using System.Collections.Generic;
using AbyssDefense.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace AbyssDefense
{
    /// 두 손가락 벌리기·휠로 내용물을 확대한다 (연구 트리). 끌어서 옮기는 것은 ScrollRect 가 맡는다
    public sealed class PinchZoom : MonoBehaviour
    {
        public RectTransform content;
        public ScrollRect scroll;
        public float min = 0.55f, max = 1.5f;
        public Action<float> changed;
        float pinch;

        void Update()
        {
            float z = content.localScale.x, nz = z;
            if (Input.touchCount >= 2)
            {
                scroll.enabled = false;
                float d = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
                if (pinch > 1 && d > 1) nz = z * d / pinch;
                pinch = d;
            }
            else
            {
                pinch = 0;
                if (!scroll.enabled) scroll.enabled = true;
                float w = Input.mouseScrollDelta.y;
                if (w != 0) nz = z * (1 + w * 0.1f);
            }
            nz = Mathf.Clamp(nz, min, max);
            if (Mathf.Approximately(nz, z)) return;
            content.localScale = Vector3.one * nz;
            content.anchoredPosition *= nz / z;
            changed?.Invoke(nz);
        }
    }

    /// 연구소: 기지를 뿌리로 위아래로 뻗는 연구 트리.
    ///   앞 연구를 마쳐야 다음 연구가 드러난다 · 지역 점령 조건이 안 맞으면 자물쇠
    ///   노드를 탭하면 아래 창에 남은 비용이 나오고, 가진 만큼만 먼저 넣어 둘 수 있다
    public sealed partial class GameManager
    {
        const float NodeW = 150, NodeH = 176, TreeStepX = 186, TreeStepY = 236, TreePad = 520, InfoH = 440;
        static readonly Color TreeAccent = new Color(1f, 0.83f, 0.37f);
        static readonly Color TreeDone = new Color(0.62f, 0.52f, 1f);

        int treeSel = -1;
        bool treeMoved, treeFocus;
        Vector2 treePos;
        float treeZoom = 1f;
        Vector2[] treeXY;
        float treeMinY, treeMaxY, treeMinX, treeMaxX;

        public void SelectResearch(int id) { treeSel = id; treeFocus = id >= 0; }

        // ───────── 연구 진행 (가진 만큼 먼저 넣기) ─────────

        int PaidIdx(ResearchDef d, int item) => d.id * Content.Items.Length + item;

        /// 다음 단계까지 아직 내야 할 비용
        public int[] ResearchRemaining(ResearchDef d)
        {
            var c = Research.Cost(d, save.research[d.id]);
            for (int i = 0; i < c.Length; i++) c[i] = Mathf.Max(0, c[i] - save.researchPaid[PaidIdx(d, i)]);
            return c;
        }

        /// 다음 단계 비용 중 이미 넣은 비율 (0~1)
        public float ResearchProgress(ResearchDef d)
        {
            var c = Research.Cost(d, save.research[d.id]);
            float sum = 0, paid = 0;
            for (int i = 0; i < c.Length; i++) { sum += c[i]; paid += Mathf.Min(c[i], save.researchPaid[PaidIdx(d, i)]); }
            return sum <= 0 ? 0 : paid / sum;
        }

        /// 앞 연구를 마쳤고 지역 조건도 맞아 지금 연구할 수 있다
        public bool ResearchAvailable(ResearchDef d) => save.research[d.id] < ResearchMax(d) && ResearchOpen(d) && Research.ParentDone(save.research, d);

        // ───────── 배치: 뿌리 가지의 절반은 위로, 절반은 아래로 ─────────

        void LayoutTree()
        {
            if (treeXY != null) return;
            int n = Research.Defs.Length;
            treeXY = new Vector2[n];
            var kids = new List<int>[n];
            var roots = new List<int>();
            for (int i = 0; i < n; i++) kids[i] = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int p = Research.Defs[i].parent;
                if (p < 0) roots.Add(i); else kids[p].Add(i);
            }
            int half = Mathf.CeilToInt(roots.Count / 2f);
            for (int side = 0; side < 2; side++)
            {
                int from = side == 0 ? 0 : half, to = side == 0 ? half : roots.Count;
                if (from >= to) continue;
                float cursor = 0;
                var placed = new List<int>();
                for (int k = from; k < to; k++) Place(roots[k], 1, side == 0 ? 1 : -1, kids, ref cursor, placed);
                float mid = (treeXY[roots[from]].x + treeXY[roots[to - 1]].x) * 0.5f;
                foreach (int id in placed) treeXY[id].x -= mid;
            }
            treeMinX = treeMaxX = treeMinY = treeMaxY = 0;
            foreach (var p in treeXY)
            {
                treeMinX = Mathf.Min(treeMinX, p.x); treeMaxX = Mathf.Max(treeMaxX, p.x);
                treeMinY = Mathf.Min(treeMinY, p.y); treeMaxY = Mathf.Max(treeMaxY, p.y);
            }
        }

        void Place(int id, int depth, int dir, List<int>[] kids, ref float cursor, List<int> placed)
        {
            placed.Add(id);
            float x;
            if (kids[id].Count == 0) { x = cursor; cursor += TreeStepX; }
            else
            {
                foreach (int k in kids[id]) Place(k, depth + 1, dir, kids, ref cursor, placed);
                x = (treeXY[kids[id][0]].x + treeXY[kids[id][kids[id].Count - 1]].x) * 0.5f;
            }
            treeXY[id] = new Vector2(x, dir * depth * TreeStepY);
        }

        // ───────── 화면 ─────────

        public void ShowResearch()
        {
            EndRegion();
            Accrue();
            LayoutTree();
            var root = NewScreen("Research", ShowResearch, 40);
            Ui.Panel(root, Ui.Bg).Rt().Fill();
            Tip("tip_research", "연구: 노드를 탭하면 효과와 비용이 나옵니다. 자원이 모자라도 가진 만큼 먼저 넣어 둘 수 있고, 지역을 점령하면 새 연구가 열립니다.");

            // 끌어서 옮기고 벌려서 확대하는 판
            var view = Ui.Panel(root, new Color(0, 0, 0, 0.01f), false, "View");
            view.Rt().Fill(0, 0, 0, TopH);
            view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            var content = Ui.Node("Content", view.transform);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            float cx = (treeMinX + treeMaxX) * 0.5f, cy = (treeMinY + treeMaxY) * 0.5f;
            content.sizeDelta = new Vector2(treeMaxX - treeMinX + NodeW + TreePad * 2, treeMaxY - treeMinY + NodeH + TreePad * 2);
            scroll.content = content; scroll.viewport = view.Rt();
            scroll.horizontal = true; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 0;
            scroll.inertia = true;
            if (!treeMoved) treePos = new Vector2(cx, cy) * treeZoom;   // 처음에는 기지 노드가 가운데
            if (treeFocus && treeSel >= 0)
            {
                // 방금 고른 노드가 아래 창이나 화면 밖에 가리지 않게 판을 민다
                treeFocus = false;
                Vector2 p = (treeXY[treeSel] - new Vector2(cx, cy)) * treeZoom + treePos;
                float hv = (CanvasHeight - TopH) * 0.5f, hw = 540, nh = NodeH * 0.5f * treeZoom + 24, nw = NodeW * 0.5f * treeZoom + 24;
                float lo = -hv + InfoH + nh, hi = hv - nh;
                if (p.y < lo) treePos.y += lo - p.y; else if (p.y > hi) treePos.y += hi - p.y;
                if (p.x < -hw + nw) treePos.x += -hw + nw - p.x; else if (p.x > hw - nw) treePos.x += hw - nw - p.x;
                treeMoved = true;
            }
            content.localScale = Vector3.one * treeZoom;
            content.anchoredPosition = treePos;
            scroll.onValueChanged.AddListener(_ => { treePos = content.anchoredPosition; treeMoved = true; });
            var zoom = view.gameObject.AddComponent<PinchZoom>();
            zoom.content = content; zoom.scroll = scroll;
            zoom.changed = z => { treeZoom = z; treePos = content.anchoredPosition; treeMoved = true; };

            Vector2 At(Vector2 p) => new Vector2(p.x - cx, p.y - cy);
            var lv = save.research;

            // 이음선: 부모 높이에서 옆으로 간 뒤 자식까지 세로로. 양쪽 다 연구했으면 밝은 색
            for (int i = 0; i < Research.Defs.Length; i++)
            {
                var d = Research.Defs[i];
                bool parentDone = Research.ParentDone(lv, d);
                bool lit = parentDone && lv[i] > 0;
                var col = lit ? TreeAccent : parentDone ? new Color(0.36f, 0.38f, 0.45f) : new Color(0.2f, 0.21f, 0.26f);
                Vector2 a = At(d.parent < 0 ? Vector2.zero : treeXY[d.parent]), b = At(treeXY[i]);
                float th = lit ? 8 : 6;
                if (Mathf.Abs(a.x - b.x) > 1) Line(content, new Vector2((a.x + b.x) * 0.5f, a.y), Mathf.Abs(a.x - b.x) + th, th, col, lit);
                Line(content, new Vector2(b.x, (a.y + b.y) * 0.5f), th, Mathf.Abs(a.y - b.y) + th, col, lit);
            }

            // 뿌리: 기지
            {
                var rim = Ui.Panel(content, TreeAccent, true, "Root");
                rim.Rt().At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), At(Vector2.zero).x, At(Vector2.zero).y, NodeW + 20, NodeH + 4);
                var inner = Ui.Panel(rim.transform, new Color(0.10f, 0.20f, 0.20f), true);
                inner.Rt().Fill(5, 5, 5, 5);
                Ui.Icon(inner.transform, Art.Get("icon_core")).Rt().At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -12, 96, 96);
                var t = Ui.Label(inner.transform, "기지", 30, TreeAccent, TextAnchor.LowerCenter, true);
                t.rectTransform.Fill(0, 14, 0, 0);
            }

            for (int i = 0; i < Research.Defs.Length; i++) Node(content, Research.Defs[i], At(treeXY[i]));

            // 머리줄
            var top = Ui.Panel(root, Ui.PanelCol);
            top.Rt().TopBar(0, TopH);
            var back = Ui.Btn(top.transform, "기지", 34, Ui.Card, ShowBase);
            back.Rt().At(new Vector2(0, 1), new Vector2(0, 1), 24, -24, 170, 96);
            var title = Ui.Label(top.transform, "연구소", 56, null, TextAnchor.UpperLeft, true);
            title.rectTransform.Fill(220, 0, 20, 22);
            var hint = Ui.Label(top.transform, "앞 연구를 마치면 다음 연구가 드러납니다. 끌어서 옮기고 벌려서 확대합니다.", 26, Ui.Dim, TextAnchor.UpperLeft);
            hint.rectTransform.Fill(220, 0, 20, 92);
            StockBar(top.transform, -164);

            if (treeSel >= 0 && Research.ParentDone(lv, Research.Defs[treeSel])) ResearchInfo(root, Research.Defs[treeSel]);
            else treeSel = -1;
        }

        static void Line(Transform parent, Vector2 center, float w, float h, Color c, bool front)
        {
            var img = Ui.Panel(parent, c, false, "Line");
            img.raycastTarget = false;
            img.Rt().At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center.x, center.y, w, h);
            if (!front) img.transform.SetAsFirstSibling();   // 밝은 선이 어두운 선 위에 오게
        }

        void Node(Transform parent, ResearchDef d, Vector2 pos)
        {
            int lv = save.research[d.id];
            bool revealed = Research.ParentDone(save.research, d);
            if (!revealed)
            {
                // 앞 연구를 마치기 전에는 무엇인지 알 수 없다
                var ghost = Ui.Panel(parent, new Color(0.12f, 0.13f, 0.16f), true, "Hidden");
                ghost.raycastTarget = false;
                ghost.Rt().At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos.x, pos.y, NodeW * 0.6f, NodeW * 0.6f);
                Ui.Label(ghost.transform, "?", 44, new Color(0.3f, 0.32f, 0.38f), TextAnchor.MiddleCenter, true);
                return;
            }

            bool open = ResearchOpen(d), max = lv >= ResearchMax(d), done = lv > 0;
            bool afford = !max && open && CanPay(ResearchRemaining(d));
            Color rim = max ? TreeAccent : done ? TreeDone : !open ? new Color(0.22f, 0.23f, 0.28f) : afford ? new Color(0.85f, 0.87f, 0.92f) : new Color(0.62f, 0.27f, 0.29f);
            Color fill = max ? new Color(0.22f, 0.19f, 0.10f) : done ? new Color(0.17f, 0.15f, 0.27f) : !open ? new Color(0.10f, 0.11f, 0.14f) : afford ? Ui.Card : new Color(0.20f, 0.13f, 0.15f);
            if (treeSel == d.id) rim = Color.white;

            var b = Ui.Btn(parent, null, 0, rim, () => { SelectResearch(treeSel == d.id ? -1 : d.id); ShowResearch(); });
            b.Rt().At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos.x, pos.y, NodeW + (treeSel == d.id ? 12 : 0), NodeH + (treeSel == d.id ? 12 : 0));
            var inner = Ui.Panel(b.transform, fill, true);
            inner.raycastTarget = false;
            inner.Rt().Fill(5, 5, 5, 5);

            var icon = Ui.Icon(inner.transform, Art.Get(open ? d.icon : "icon_lock"), open ? (done || afford ? Color.white : new Color(1, 1, 1, 0.55f)) : new Color(0.45f, 0.47f, 0.55f));
            icon.Rt().At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), 0, -10, 78, 78);
            var name = Ui.Label(inner.transform, open ? d.name : $"지역 {d.reqRegion}", 23, open ? Ui.TextCol : Ui.Dim, TextAnchor.MiddleCenter, true);
            name.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 38, NodeW - 12, 34);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;

            if (!open) return;
            // 단계 칸 (여러 단계 연구) 또는 진행 막대
            float prog = max ? 0 : ResearchProgress(d);
            if (d.maxLevel > 1)
            {
                // 상한이 늘어난 연구는 칸이 너무 잘아지지 않게 12칸까지만 그리고 비율로 채운다
                int cap = ResearchMax(d), pips = Mathf.Min(cap, 12), lit = cap <= 12 ? lv : Mathf.CeilToInt(lv * pips / (float)cap);
                float pw = (NodeW - 30 - (pips - 1) * 3) / pips;
                for (int p = 0; p < pips; p++)
                {
                    var pip = Ui.Panel(inner.transform, p < lit ? (max ? TreeAccent : TreeDone) : new Color(0, 0, 0, 0.5f), false, "Pip");
                    pip.raycastTarget = false;
                    pip.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 10 + p * (pw + 3), 20, pw, 10);
                    if (p == lv && prog > 0)
                    {
                        var part = Ui.Panel(pip.transform, Ui.Good, false, "Part");
                        part.raycastTarget = false;
                        part.Rt().anchorMin = Vector2.zero; part.Rt().anchorMax = new Vector2(prog, 1);
                        part.Rt().offsetMin = part.Rt().offsetMax = Vector2.zero;
                    }
                }
            }
            else
            {
                var bar = Ui.Panel(inner.transform, done ? TreeAccent : new Color(0, 0, 0, 0.5f), false, "Bar");
                bar.raycastTarget = false;
                bar.Rt().At(new Vector2(0, 0), new Vector2(0, 0), 10, 20, NodeW - 30, 10);
                if (!done && prog > 0)
                {
                    var part = Ui.Panel(bar.transform, Ui.Good, false, "Part");
                    part.raycastTarget = false;
                    part.Rt().anchorMin = Vector2.zero; part.Rt().anchorMax = new Vector2(prog, 1);
                    part.Rt().offsetMin = part.Rt().offsetMax = Vector2.zero;
                }
            }
        }

        static string EffectText(ResearchDef d, int lv) =>
            d.maxLevel == 1 ? (lv > 0 ? "해금됨" : "잠김") : d.id == Research.Loadout ? $"+{BaseRules.LoadoutBonus(lv):N0}" : d.percent ? $"+{Mathf.RoundToInt(lv * d.perLevel * 100)}%" : $"+{(int)(lv * d.perLevel)}";

        /// 고른 연구의 창: 이름·설명·효과, 남은 비용(가진 양 / 남은 양), 연구 버튼
        void ResearchInfo(Transform root, ResearchDef d)
        {
            int lv = save.research[d.id];
            bool open = ResearchOpen(d), max = lv >= ResearchMax(d);
            var panel = Ui.Panel(root, Ui.PanelCol, false, "Info");
            panel.Rt().BottomBar(0, InfoH);
            var line = Ui.Panel(panel.transform, max ? TreeAccent : TreeDone, false, "Line");
            line.Rt().TopBar(0, 6);

            Ui.Icon(panel.transform, Art.Get(open ? d.icon : "icon_lock")).Rt().At(new Vector2(0, 1), new Vector2(0, 1), 36, -30, 96, 96);
            var title = Ui.Label(panel.transform, open ? d.name : "???", 48, null, TextAnchor.UpperLeft, true);
            title.rectTransform.Fill(156, 0, 190, 26);
            string lvText = d.maxLevel > 1 ? $"단계 {lv} / {ResearchMax(d)}" : lv > 0 ? "연구 완료" : "미연구";
            var sub = Ui.Label(panel.transform, open ? $"{lvText}   ·   {d.desc}" : "아직 알 수 없는 연구입니다", 27, Ui.Dim, TextAnchor.UpperLeft);
            sub.rectTransform.Fill(156, 0, 40, 90);
            var close = Ui.Btn(panel.transform, "닫기", 30, Ui.Card, () => { treeSel = -1; ShowResearch(); });
            close.Rt().At(new Vector2(1, 1), new Vector2(1, 1), -30, -24, 140, 70);

            if (!open)
            {
                var need = Ui.Label(panel.transform, $"목표   {Content.StageName(d.reqRegion)} 점령   (그 지역을 직접 점령해야 열립니다)", 30, Ui.Bad, TextAnchor.UpperLeft, true);
                need.rectTransform.Fill(40, 0, 40, 170);
                return;
            }

            string eff = d.maxLevel == 1 ? "" : max ? $"효과 {EffectText(d, lv)}  (최대)" : $"효과 {EffectText(d, lv)}  →  {EffectText(d, lv + 1)}";
            var ef = Ui.Label(panel.transform, eff, 30, Ui.Gold, TextAnchor.UpperLeft, true);
            ef.rectTransform.Fill(40, 0, 40, 150);
            if (max)
            {
                var doneText = Ui.Label(panel.transform, d.expands && Expansions < Research.MaxExpansions ? "지금 상한까지 마쳤습니다. 서리 심연부터 군주·거점 지역을 점령할 때마다 상한이 한 단계씩 늘어납니다." : "연구를 모두 마쳤습니다.", 32, TreeAccent, TextAnchor.UpperLeft, true);
                doneText.rectTransform.Fill(40, 0, 40, 210);
                return;
            }

            // 남은 비용: 자원마다 '넣을 수 있는 양 / 남은 양'
            var remain = ResearchRemaining(d);
            float prog = ResearchProgress(d);
            var head = Ui.Label(panel.transform, prog > 0 ? $"남은 비용   (연구 진행 {Mathf.Min(99, Mathf.FloorToInt(prog * 100))}%)" : "비용", 27, prog > 0 ? Ui.Good : Ui.TextCol, TextAnchor.UpperLeft);
            head.rectTransform.Fill(40, 0, 40, 200);
            int col = 0;
            bool any = false, all = true;
            for (int i = 0; i < remain.Length; i++)
            {
                if (remain[i] <= 0) continue;
                int have = (int)save.stock[i];
                if (have > 0) any = true;
                if (have < remain[i]) all = false;
                var chip = Ui.Panel(panel.transform, new Color(0, 0, 0, 0.35f), true, "Cost");
                chip.Rt().At(new Vector2(0, 1), new Vector2(0, 1), 30 + col * 257, -244, 250, 76);
                Ui.Icon(chip.transform, Art.Get("item_" + Content.Items[i].key)).Rt().At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), 10, 0, 44, 44);
                var t = Ui.Label(chip.transform, $"{Mathf.Min(have, remain[i]):N0} / {remain[i]:N0}", 29, have >= remain[i] ? Ui.TextCol : Ui.Bad, TextAnchor.MiddleLeft, true);
                t.rectTransform.Fill(62, 0, 4, 0);
                col++;
            }
            var buy = Ui.Btn(panel.transform, all ? "연구" : any ? "가진 만큼 넣기" : "자원이 없습니다", all ? 46 : 36, all ? Violet : any ? Primary : Off, () => { BuyResearch(d); ShowResearch(); }, any ? Ui.TextCol : Ui.Dim);
            buy.Rt().At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, 26, 1020, 82);
            buy.interactable = any;
        }
    }
}
