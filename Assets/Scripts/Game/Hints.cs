using System;
using System.Collections.Generic;
using AbyssDefense.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace AbyssDefense
{
    /// 상황별 힌트: 조건이 맞을 때 한 번 뜨고, 그 일을 해내거나(done) 시간이 지나거나(auto) '확인'을 누르면 다시 뜨지 않는다.
    /// 본 힌트는 세이브에 적어 둔다. 도감의 '힌트 다시 보기'로 지운다
    public sealed partial class GameManager
    {
        sealed class Hint
        {
            public string key, text;
            public Func<bool> show;      // 이 조건이 맞으면 띄운다
            public Func<bool> done;      // 이 조건이 맞으면 해낸 것으로 보고 내린다 (없으면 auto 초 뒤에 내린다)
            public float auto;
        }

        readonly List<Hint> hintList = new List<Hint>();
        Hint hintNow;
        GameObject hintPanel;
        Text hintText;
        float hintCheck, hintShownAt;

        void BuildHints(RectTransform root, float top, float width)
        {
            var hp = Ui.Panel(root, new Color(0.16f, 0.13f, 0.05f, 0.94f), true, "Hint");
            hp.Rt().At(new Vector2(0, 1), new Vector2(0, 1), 20, top, width, 104);
            hintPanel = hp.gameObject;
            hintText = Ui.Label(hp.transform, "", 25, new Color(1f, 0.9f, 0.62f), TextAnchor.MiddleLeft);
            hintText.rectTransform.Fill(20, 6, 132, 6);
            var ok = Ui.Btn(hp.transform, "확인", 26, new Color(0.45f, 0.34f, 0.12f), () => { if (hintNow != null) FinishHint(); });
            ok.Rt().At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), -12, 0, 108, 76);
            hintPanel.SetActive(false);
            hintNow = null; hintCheck = 0;
            MakeHints();
        }

        int Count(Func<Building, bool> f) { int n = 0; foreach (var b in sim.world.buildings) if (!b.def.enemy && f(b)) n++; return n; }
        bool Alive(EnemyDef d) { foreach (var e in sim.enemies) if (e.def == d && !e.dead) return true; return false; }
        bool Coming(Func<EnemyDef, bool> f) { foreach (var p in preview) if (f(p.def)) return true; return false; }

        void MakeHints()
        {
            hintList.Clear();
            void Add(string key, string text, Func<bool> show, Func<bool> done = null, float auto = 10f) =>
                hintList.Add(new Hint { key = key, text = text, show = show, done = done, auto = done == null ? auto : 0 });

            // 처음 해 보는 조작
            Add("build", "아래 줄에서 속사포를 고르고 적이 오는 길 옆을 탭하세요. 끌면 한 줄로 짓습니다.",
                () => sim.wave == 0 && Count(b => b is TurretBuilding) == 0, () => Count(b => b is TurretBuilding) > 0);
            Add("callwave", "준비가 끝났으면 '다음 웨이브 부르기'를 눌러 기다리지 않고 시작할 수 있습니다.",
                () => sim.wave == 0 && sim.waveCountdown > 20 * 60 && Count(b => b is TurretBuilding && b.Built) >= 3, () => sim.wave >= 1);
            Add("preview", "위의 띠는 다음 회차에 나올 적입니다. 미리 보고 맞는 포탑을 세우세요. 적의 능력은 기지의 도감에 있습니다.",
                () => sim.wave >= 1 && sim.wave <= 2 && !sim.AllWavesSent);
            Add("ammo", "포탑을 탭하면 넣을 탄과 겨눌 적을 고를 수 있습니다. 강철·화약·회로 같은 가공 자원을 넣으면 탄의 성질이 달라집니다.",
                () => Count(b => b is TurretBuilding t && b.Built && t.def.ammos.Length > 1 && HasAltAmmo(t)) > 0, () => play.Turret != null);
            Add("starved", "붉은 점이 깜빡이는 포탑은 탄약 자원이 떨어졌습니다. 포탑을 탭해 다른 탄으로 바꾸거나, 그 자원을 쓰는 건설을 줄이세요.",
                () => Count(b => b is TurretBuilding t && b.Built && t.Starved) > 0, () => Count(b => b is TurretBuilding t && b.Built && t.Starved) == 0);
            Add("factory", "공장을 탭해 만들 유닛을 고르고 '집결 지점 옮기기'로 유닛이 모일 곳을 정하세요.",
                () => Count(b => b is FactoryBuilding && b.Built) > 0, () => play.Factory != null);
            Add("stance", "유닛이 모였으면 위의 '방어 중' 단추로 공격 태세로 바꿔 적 출현 지점 앞에서 싸우게 할 수 있습니다.",
                () => sim.units.Count >= 3 && sim.stance == UnitStance.Defend, () => sim.stance == UnitStance.Attack);
            Add("replace", "방벽 위에 강철 방벽을 놓으면 철거하지 않고 바뀝니다. 포탑도 같은 크기끼리 덮어 지을 수 있습니다.",
                () => sim.IsUnlocked(Content.SteelWall) && sim.Core.items[Content.Steel] >= 10 && Count(b => b.def == Content.Wall && b.Built) > 0, () => Count(b => b.def == Content.SteelWall) > 0);
            Add("rebuild", "부서진 건물은 10초 뒤 같은 자리에 다시 지어집니다. 철광이 탄약에 모자라면 오른쪽 아래의 '자동 재건'을 끄세요.",
                () => sim.autoRebuild && sim.ruins.Count > 0);
            Add("minimap", "오른쪽 위의 미니맵을 누르거나 끌면 카메라가 그 자리로 갑니다. 붉은 점이 적입니다.",
                () => sim.wave >= 2 && sim.world.H > 36);
            Add("support", "수리탑·증폭탑·역장 발생기는 고리 안의 건물을 돕습니다. 포탑이 모인 곳 가운데에 지으세요.",
                () => sim.IsUnlocked(Content.MendTower) && Count(b => b is TurretBuilding && b.Built) >= 5 && Count(b => b is SupportBuilding) == 0, () => Count(b => b is SupportBuilding) > 0);

            // 처음 만나는 적과 규칙
            Add("air", "다음 회차에 비행체가 옵니다. 박격포는 공중을 못 쏩니다. 대공포는 공중만 쏘지만 멀리서 범위로 때립니다.",
                () => Coming(d => d.flying) && Count(b => b.def == Content.FlakTurret) == 0, () => Count(b => b.def == Content.FlakTurret) > 0 || sim.wave >= 8);
            Add("boss", "곧 보스가 옵니다. 보스는 둔화·산성·밀어내기가 절반만 듣습니다. 화력을 핵 앞에 모아 두세요.",
                () => sim.BossIn(2) > 0);
            Add("healer", "수리병(초록 십자)이 둘레의 적을 계속 고칩니다. 범위 피해로 함께 잡거나 먼저 노리세요.", () => Alive(Content.Healer));
            Add("bombard", "포격병은 속사포의 사거리 밖에서 포탑을 칩니다. 박격포·결정포나 유닛으로 잡으세요.", () => Alive(Content.Bombard));
            Add("splitter", "분열체는 죽으면 빠른 조각 셋으로 갈라집니다. 박격포·전격탑 같은 범위 화력이 잘 듣습니다.", () => Alive(Content.Splitter));
            Add("guard", "방패병의 보호막은 3초 동안 맞지 않으면 다시 찹니다. 화상(소이탄·화염탑)이나 끊기지 않는 화력으로 잡으세요.", () => Alive(Content.Guard));
            Add("dropship", "수송체는 건물 곁에 보행병 넷을 내려놓습니다. 내려놓기 전에 대공포로 떨어뜨리면 실린 적도 사라집니다.", () => Alive(Content.Dropship));
            Add("raider", "습격기는 핵이 아니라 공장으로 날아갑니다. 공장 곁에도 공중을 쏘는 포탑을 두세요.", () => Alive(Content.Raider));
            Add("armor", "철갑병은 약한 탄을 거의 튕겨 냅니다. 속사포에 철갑탄(강철)을 넣거나 결정포·화상·산성으로 잡으세요.", () => Alive(Content.Shell));
            Add("assault", "적 거점 지역입니다. 웨이브는 끝없이 오고, 유닛을 모아 공격 태세로 보내 적 핵을 모두 부숴야 점령합니다.",
                () => sim.rules.assault && sim.tick > 60 * 8);
        }

        /// 그 포탑에 넣을 수 있는 다른 탄의 자원이 핵에 있는가
        bool HasAltAmmo(TurretBuilding t)
        {
            for (int i = 1; i < t.def.ammos.Length; i++) if (sim.Core.items[t.def.ammos[i].item] > 0) return true;
            return false;
        }

        void FinishHint()
        {
            if (!save.hints.Contains(hintNow.key)) save.hints.Add(hintNow.key);
            hintNow = null;
            hintPanel.SetActive(false);
        }

        void UpdateHints()
        {
            if (hintPanel == null || resuming) return;
            if (ResultShown || sim.state != SimState.Playing) { if (hintPanel.activeSelf) hintPanel.SetActive(false); hintNow = null; return; }
            hintCheck -= Time.unscaledDeltaTime;
            if (hintCheck > 0) return;
            hintCheck = 0.4f;
            if (hintNow != null)
            {
                // 띄운 뒤 적어도 2초는 보여 준다
                float shown = Time.unscaledTime - hintShownAt;
                if (shown > 2f && (hintNow.done != null ? hintNow.done() : shown > hintNow.auto)) FinishHint();
                return;
            }
            foreach (var h in hintList)
            {
                if (save.hints.Contains(h.key) || !h.show()) continue;
                if (h.done != null && h.done()) { save.hints.Add(h.key); continue; }   // 이미 할 줄 아는 일
                hintNow = h;
                hintShownAt = Time.unscaledTime;
                hintText.text = h.text;
                hintPanel.SetActive(true);
                break;
            }
        }

        /// 본 힌트를 지워 처음부터 다시 뜨게 한다
        public void ResetHints()
        {
            save.hints.Clear();
            Save();
            Toast("힌트를 처음부터 다시 보여 줍니다");
        }
    }
}
