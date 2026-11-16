using System;
using System.Collections.Generic;
using System.IO;
using AbyssDefense.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbyssDefense
{
    [Serializable]
    public class AbyssSave
    {
        public int cleared;                 // (예전 세이브) 지역이 한 줄로 이어져 있던 때의 가장 깊은 지역. 불러올 때 done 으로 옮기고 더는 쓰지 않는다
        public bool[] done = new bool[0];   // done[지역] = 그 지역을 점령했는가 (지역은 거미줄처럼 이어져 있어 건너뛴 지역이 있을 수 있다)
    }

    [Serializable]
    public class SaveData
    {
        public int version = 4;
        public List<AbyssSave> abysses = new List<AbyssSave>();
        public int[] research = new int[0];             // 연구 단계 (모든 심연 공통)
        public int[] researchPaid = new int[0];         // 다음 단계 비용 중 미리 넣어 둔 양 [연구 × 자원]
        // 기지
        public double[] stock = { 500, 150, 0, 0, 0, 0, 0 };   // 보유 자원 (Content.Items 순서)
        public int[] mineLv = { 1, 1, 0 };              // 채굴장 단계 [철광, 황석, 심연 결정]
        public int[] refLv = new int[0];                // 가공 건물 단계 (0 = 아직 안 지음)
        public bool[] refOn = new bool[0];              // 가공 건물 가동 여부
        public int storageLv = 1;                       // 창고 단계
        public int[] loadout = { 400, 100, 0, 0, 0, 0, 0 };   // 출격할 때 가져갈 양 (마지막 설정)
        public double lastTime;                         // 생산을 마지막으로 정산한 시각 (초)
        public List<string> hints = new List<string>(); // 이미 본 힌트의 key
        public RunSave run = new RunSave();             // 진행 중인 지역 (없으면 active = false)
    }

    /// 진행 중인 지역 한 판. 상태를 통째로 적지 않고 플레이어가 내린 명령만 적어 둔다. 이어할 때 같은 명령을 같은 틱에 다시 넣는다
    [Serializable]
    public sealed class RunSave
    {
        public bool active;
        public int rules;                               // 그 판을 시작할 때의 규칙 지문. 달라졌으면 이어할 수 없다
        public int abyss, region;
        public int[] taken = new int[0];                // 가져간 자원
        public int[] research = new int[0];             // 출격할 때의 연구 단계
        public int tick;                                // 마지막으로 저장한 틱
        public List<SimCommand> cmds = new List<SimCommand>();
    }

    /// 씬에 아무것도 없어도 실행하면 게임이 뜨게 한다
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (UnityEngine.Object.FindAnyObjectByType<GameManager>() != null) return;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            var go = new GameObject("Game");
            go.AddComponent<GameManager>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-autoshot");
            if (i >= 0 && i + 1 < args.Length) go.AddComponent<AutoShot>().dir = args[i + 1];
#endif
        }
    }

    /// 화면 전환, 세이브, 기지 경제, 지역 한 판의 진행을 맡는다. 화면별 코드는 Screens.cs · Hud.cs
    public sealed partial class GameManager : MonoBehaviour
    {
        const float HudTopPx = 172, HudBottomPx = 340;
        public const float RetreatReturn = 0.5f;   // 후퇴하면 남은 자원의 절반만 가져온다

        public static bool TestSave;        // 점검 실행은 진짜 세이브를 건드리지 않는다
        public static float CanvasHeight => 1080f * Screen.height / Mathf.Max(1, Screen.width);
        public static float HudTopFrac => HudTopPx / CanvasHeight;
        public static float HudBottomFrac => HudBottomPx / CanvasHeight;

        public SaveData save;
        public Sim.Sim sim;
        public AutoBot bot;                 // 점검용 자동 플레이
        public bool ResultShown { get; private set; }
        public int Speed { get; private set; } = 1;
        public bool Paused { get; private set; }
        public int AbyssId { get; private set; }
        public int Region { get; private set; }
        public int[] Taken { get; private set; }     // 이번 출격에 가져간 자원

        Camera cam;
        RectTransform safe, screen;
        GameObject playRoot;
        PlayController play;
        float acc;
        Text toastText;
        float toastUntil, saveTimer, refreshTimer;
        Action refresh;                     // 지금 화면을 다시 그리는 방법 (기지·연구소처럼 숫자가 흐르는 화면)
        string offlineNote;

        string SavePath => Path.Combine(Application.persistentDataPath, TestSave ? "save_test.json" : "save.json");
        static double Now => DateTime.UtcNow.Ticks / 1e7;

        void Awake()
        {
            TestSave = Array.IndexOf(Environment.GetCommandLineArgs(), "-autoshot") >= 0;
            Load();

            cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cgo.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Ui.Bg;
            cam.transform.position = new Vector3(0, 0, -10);

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // 노치·둥근 모서리를 피한 안전 영역
            safe = Ui.Node("Safe", canvasGo.transform);
            var sa = Screen.safeArea;
            safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;

            ShowTitle();
        }

        // ───────── 세이브 ─────────

        void Load()
        {
            save = null;
            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    // 기지가 없던 예전 세이브는 구조가 달라 새로 시작한다
                    if (json.Contains("\"version\"")) save = JsonUtility.FromJson<SaveData>(json);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Save] 읽기 실패: " + e.Message); }
            bool fresh = save == null;
            if (fresh) save = new SaveData();
            Normalize();
            if (fresh) save.lastTime = Now;
            else
            {
                // 꺼 둔 동안의 생산
                var before = (double[])save.stock.Clone();
                Accrue();
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < before.Length; i++)
                {
                    int g = (int)(save.stock[i] - before[i]);
                    if (g > 0) sb.Append(sb.Length > 0 ? "   " : "").Append(Content.Items[i].name).Append(" +").Append(g);
                }
                if (sb.Length > 0) offlineNote = "자리를 비운 동안  " + sb;
            }
        }

        void Normalize()
        {
            int n = Content.Items.Length;
            while (save.abysses.Count < Content.Abysses.Length) save.abysses.Add(new AbyssSave());
            foreach (var ab in save.abysses)
            {
                // 예전 세이브(가장 깊은 지역 하나만 적던 때)는 지역 1 부터 거기까지를 모두 점령한 것으로 옮긴다
                var done = new bool[Content.RegionCount + 1];
                if (ab.done != null) Array.Copy(ab.done, done, Math.Min(done.Length, ab.done.Length));
                for (int r = 1; r <= ab.cleared && r <= Content.RegionCount; r++) done[r] = true;
                ab.done = done;
                ab.cleared = 0;
            }
            save.research = Fit(save.research, Research.Defs.Length, new int[0]);
            if (save.researchPaid == null || save.researchPaid.Length != Research.Defs.Length * n)
            {
                // 연구가 뒤에 늘어난 예전 세이브는 미리 넣어 둔 양을 그대로 옮긴다 (자원 종류가 달라졌으면 비운다)
                var paid = new int[Research.Defs.Length * n];
                if (save.researchPaid != null && save.researchPaid.Length % n == 0) Array.Copy(save.researchPaid, paid, Math.Min(paid.Length, save.researchPaid.Length));
                save.researchPaid = paid;
            }
            // 자원 종류가 늘어난 예전 세이브는 있던 값을 그대로 옮긴다
            save.stock = Fit(save.stock, n, new double[] { 500, 150 });
            save.loadout = Fit(save.loadout, n, new[] { 400, 100 });
            if (save.mineLv == null || save.mineLv.Length != Content.RawCount) save.mineLv = new[] { 1, 1, 0 };
            int rn = BaseRules.Refineries.Length;
            save.refLv = Fit(save.refLv, rn, new int[0]);
            if (save.refOn == null || save.refOn.Length != rn) { save.refOn = new bool[rn]; for (int i = 0; i < rn; i++) save.refOn[i] = true; }
            save.storageLv = Mathf.Clamp(save.storageLv, 1, BaseRules.MaxStorageLevel);
            save.version = 4;
            if (save.hints == null) save.hints = new List<string>();
            if (save.run == null) save.run = new RunSave();
            var run = save.run;
            if (run.active && (run.rules != Sim.Sim.RulesFingerprint() || run.cmds == null || run.taken == null || run.taken.Length != n
                || run.abyss < 0 || run.abyss >= Content.Abysses.Length || run.region < 1 || run.region > Content.RegionCount))
            {
                // 규칙이 바뀌어 이어할 수 없는 판: 가져갔던 자원을 돌려주고 버린다
                if (run.taken != null) for (int i = 0; i < Math.Min(n, run.taken.Length); i++) save.stock[i] += run.taken[i];
                save.run = new RunSave();
                offlineNote = "게임이 바뀌어 진행 중이던 지역은 이어할 수 없습니다. 가져간 자원은 돌려받았습니다";
            }
        }

        static T[] Fit<T>(T[] a, int n, T[] fallback)
        {
            if (a != null && a.Length == n) return a;
            var src = a != null && a.Length > 0 ? a : fallback;
            var r = new T[n];
            Array.Copy(src, r, Math.Min(n, src.Length));
            return r;
        }

        public void Save()
        {
            StoreRun();
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(save)); }
            catch (Exception e) { Debug.LogWarning("[Save] 쓰기 실패: " + e.Message); }
        }

        /// 진행 중인 판을 세이브에 옮겨 적는다 (지금 틱과 지금까지의 명령)
        void StoreRun()
        {
            if (sim == null || resuming || !save.run.active) return;
            if (sim.state != SimState.Playing || ResultShown || bot != null) { save.run.active = false; return; }   // 봇이 둔 판은 명령 기록이 없다
            save.run.tick = sim.tick;
            save.run.cmds = new List<SimCommand>(sim.log);
        }

        /// 이어할 수 있는 판이 있는가
        public bool CanResume => save.run != null && save.run.active && sim == null;

        /// 점검용: 세이브 파일을 다시 읽는다 (앱을 껐다 켠 것처럼)
        public void ReloadSave() { EndRegion(); Load(); }

        public void ResetSave()
        {
            save = new SaveData();
            Normalize();
            save.lastTime = Now;
            offlineNote = null;
            Save();
        }

        void OnApplicationPause(bool paused) { if (paused && save != null) { Accrue(); Save(); } }
        void OnApplicationQuit() { if (save != null) { Accrue(); Save(); } }

        // ───────── 기지 경제 ─────────

        // ───────── 진행: 점령한 지역 ─────────

        /// 심연별 점령 여부 (기지 생산 계산용)
        public bool[][] Done
        {
            get
            {
                var d = new bool[save.abysses.Count][];
                for (int i = 0; i < d.Length; i++) d[i] = save.abysses[i].done;
                return d;
            }
        }

        public bool IsDone(int abyss, int region) => abyss >= 0 && abyss < save.abysses.Count && region >= 1 && region <= Content.RegionCount && save.abysses[abyss].done[region];

        /// 그 심연에서 점령한 지역의 수
        public int DoneCount(int abyss) { int n = 0; foreach (bool b in save.abysses[abyss].done) if (b) n++; return n; }

        /// 그 심연에서 점령한 가장 깊은 지역 (없으면 0)
        public int Deepest(int abyss)
        {
            var d = save.abysses[abyss].done;
            for (int r = Content.RegionCount; r >= 1; r--) if (d[r]) return r;
            return 0;
        }

        /// 가장 깊이 점령한 전체 단계 (잿불 1~100, 서리 101~ …). 기지 강화와 도감이 이것을 본다
        public int BestStage
        {
            get
            {
                int m = 0;
                for (int a = 0; a < save.abysses.Count; a++) { int d = Deepest(a); if (d > 0) m = Mathf.Max(m, Content.Stage(a, d)); }
                return m;
            }
        }

        /// 그 단계의 지역을 직접 점령했는가 (0 = 조건 없음). 연구와 기지 건물의 해금은 이것을 본다
        public bool StageDone(int stage) => stage <= 0 || IsDone(Content.StageAbyss(stage), Content.StageRegion(stage));

        /// 서리 심연부터 점령한 군주·거점 지역의 수: 그만큼 연구 상한이 늘어난다
        public int Expansions
        {
            get
            {
                int n = 0;
                for (int a = 1; a < save.abysses.Count; a++)
                    for (int r = 1; r <= Content.RegionCount; r++)
                        if (save.abysses[a].done[r] && RegionRules.IsKey(Content.Stage(a, r))) n++;
                return n;
            }
        }

        /// 심연은 앞 심연의 마지막 지역(군주)을 점령해야 열린다
        public bool IsAbyssUnlocked(int abyss) => abyss <= 0 || IsDone(abyss - 1, Content.RegionCount);

        /// 지역은 점령한 지역과 이어져 있어야 고를 수 있다 (첫 구간의 위 줄은 처음부터 열려 있다)
        public bool IsUnlocked(int abyss, int region) => IsAbyssUnlocked(abyss) && RegionGraph.Reachable(save.abysses[abyss].done, region);

        /// 점검용: 그 심연의 지역 1~n 을 점령한 상태로 만든다 (그 밖의 지역은 점령하지 않은 것으로)
        public void SetClearedUpTo(int abyss, int n)
        {
            var d = save.abysses[abyss].done;
            for (int r = 1; r <= Content.RegionCount; r++) d[r] = r <= n;
        }

        /// 채굴로 들어오는 분당 생산량 (가공 건물이 쓰는 양은 빼지 않은 값)
        public float Rate(int item) => item < Content.RawCount ? BaseRules.Rate(item, save.mineLv[item], save.research, Done) : 0;
        /// 가공 건물까지 넣은 자원별 분당 증감
        public float[] NetRates() => BaseRules.NetRates(save.mineLv, save.refLv, save.refOn, save.research, Done);
        public int StorageCap => BaseRules.StorageCap(save.storageLv);
        public int LoadoutCap => BaseRules.LoadoutCap(save.research);

        /// 지난 시간만큼 기지를 돌린다 (채굴 + 가공). 창고 한도까지만 쌓이고, 오래 비워도 8시간치까지만
        public void Accrue()
        {
            double now = Now;
            double dt = Math.Min(Math.Max(0, now - save.lastTime), BaseRules.OfflineCapSeconds);
            save.lastTime = now;
            if (dt <= 0) return;
            BaseRules.Advance(save.stock, save.mineLv, save.refLv, save.refOn, save.research, Done, save.storageLv, dt);
        }

        public bool CanPay(int[] cost)
        {
            for (int i = 0; i < cost.Length; i++) if (save.stock[i] < cost[i]) return false;
            return true;
        }

        bool Pay(int[] cost)
        {
            Accrue();
            if (!CanPay(cost)) { Toast("자원이 부족합니다"); return false; }
            for (int i = 0; i < cost.Length; i++) save.stock[i] -= cost[i];
            return true;
        }

        public bool CanBuildMine(int item) => item != Content.Crystal || save.mineLv[item] > 0 || StageDone(BaseRules.CrystalMineRegion);

        /// 기지 건물의 강화는 그 단계만큼 깊이 점령한 뒤에야 할 수 있다 (이미 올려 둔 단계는 그대로 남는다)
        public bool CanUpgrade(int level) => BestStage >= BaseRules.UpgradeRegion(level);

        public bool UpgradeMine(int item)
        {
            if (save.mineLv[item] >= BaseRules.MaxMineLevel || !CanBuildMine(item) || !CanUpgrade(save.mineLv[item])) return false;
            if (!Pay(BaseRules.MineCost(item, save.mineLv[item]))) return false;
            save.mineLv[item]++;
            Save();
            return true;
        }

        public bool CanBuildRefinery(int k) => save.refLv[k] > 0 || StageDone(BaseRules.Refineries[k].reqRegion);

        public bool UpgradeRefinery(int k)
        {
            if (save.refLv[k] >= BaseRules.MaxRefineryLevel || !CanBuildRefinery(k) || !CanUpgrade(save.refLv[k])) return false;
            if (!Pay(BaseRules.RefineryCost(BaseRules.Refineries[k], save.refLv[k]))) return false;
            save.refLv[k]++;
            Save();
            return true;
        }

        /// 가공 건물을 멈추거나 다시 돌린다 (원료를 아끼고 싶을 때)
        public void ToggleRefinery(int k)
        {
            Accrue();
            save.refOn[k] = !save.refOn[k];
            Save();
        }

        public bool UpgradeStorage()
        {
            if (save.storageLv >= BaseRules.MaxStorageLevel || !CanUpgrade(save.storageLv)) return false;
            if (!Pay(BaseRules.StorageCost(save.storageLv))) return false;
            save.storageLv++;
            Save();
            return true;
        }

        /// 연구는 그것이 묶인 지역을 직접 점령해야 열린다
        public bool ResearchOpen(ResearchDef d) => StageDone(d.reqRegion);
        /// 지금 올릴 수 있는 최대 단계 (서리 심연부터의 군주·거점 점령으로 늘어난다)
        public int ResearchMax(ResearchDef d) => Research.MaxLevel(d, Expansions);

        /// 연구에 자원을 넣는다. 모자라면 가진 만큼만 넣어 두고, 비용을 다 채우면 단계가 오른다 (그때 true)
        public bool BuyResearch(ResearchDef d)
        {
            if (!ResearchAvailable(d)) return false;
            Accrue();
            var cost = Research.Cost(d, save.research[d.id]);
            bool complete = true, any = false;
            for (int i = 0; i < cost.Length; i++)
            {
                int k = d.id * Content.Items.Length + i;
                int use = Mathf.Clamp(cost[i] - save.researchPaid[k], 0, (int)save.stock[i]);
                save.stock[i] -= use;
                save.researchPaid[k] += use;
                if (use > 0) any = true;
                if (save.researchPaid[k] < cost[i]) complete = false;
            }
            if (complete)
            {
                save.research[d.id]++;
                for (int i = 0; i < cost.Length; i++) save.researchPaid[d.id * Content.Items.Length + i] = 0;
            }
            else if (!any) Toast("자원이 부족합니다");
            Save();
            return complete;
        }

        /// 지금 설정대로 출격하면 실제로 가져가게 되는 양
        public int LoadoutAmount(int item) => Mathf.Clamp(save.loadout[item], 0, Mathf.Min(LoadoutCap, (int)save.stock[item]));

        // ───────── 화면 ─────────

        /// 지금 화면을 지우고 새 화면의 뿌리를 만든다
        RectTransform NewScreen(string name, Action refresh = null, float toastY = 60)
        {
            // 화면을 다시 그려도 떠 있던 알림은 이어서 보여 준다
            string pending = toastText != null && toastText.transform.parent.gameObject.activeSelf && Time.unscaledTime < toastUntil ? toastText.text : null;
            if (screen != null) Destroy(screen.gameObject);
            screen = Ui.Node(name, safe);
            screen.Fill();
            this.refresh = refresh;
            refreshTimer = 0;

            var toast = Ui.Panel(screen, new Color(0, 0, 0, 0.85f), true, "Toast");
            toast.Rt().At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), 0, toastY, 900, 76);
            toast.raycastTarget = false;
            toastText = Ui.Label(toast.transform, "", 30);
            toast.gameObject.SetActive(pending != null);
            if (pending != null) toastText.text = pending;
            return screen;
        }

        /// 화면에 처음 들어왔을 때 한 번만 뜨는 안내 (본 것은 힌트와 함께 세이브에 적는다). 띄웠으면 true
        bool Tip(string key, string msg)
        {
            if (save.hints.Contains(key)) return false;
            save.hints.Add(key);
            Toast(msg, 8f);
            return true;
        }

        public void Toast(string msg, float seconds = 2.4f)
        {
            if (toastText == null) return;
            toastText.text = msg;
            var go = toastText.transform.parent.gameObject;
            // 긴 알림은 두 줄로: 판을 키우고 글씨를 줄인다
            bool tall = msg.Length > 34;
            ((RectTransform)go.transform).sizeDelta = new Vector2(tall ? 1000 : 900, tall ? 124 : 76);
            toastText.fontSize = tall ? 26 : 30;
            go.SetActive(true);
            go.transform.SetAsLastSibling();
            toastUntil = Time.unscaledTime + seconds;
        }

        // ───────── 지역 한 판 ─────────

        /// 설정한 만큼의 자원을 기지 재고에서 꺼내 지역으로 출격한다
        public void StartRegion(int abyss, int region)
        {
            EndRegion();
            Accrue();
            AbyssId = abyss; Region = region;
            ResultShown = false; Paused = false; Speed = 1; acc = 0;
            Taken = new int[Content.Items.Length];
            for (int i = 0; i < Taken.Length; i++)
            {
                Taken[i] = LoadoutAmount(i);
                save.stock[i] -= Taken[i];
            }
            sim = new Sim.Sim(abyss, region, Taken, Research.Mods(save.research));
            save.run = new RunSave { active = true, rules = Sim.Sim.RulesFingerprint(), abyss = abyss, region = region, taken = (int[])Taken.Clone(), research = (int[])save.research.Clone() };
            Save();
            EnterPlay();
            if (sim.rules.hazard != Hazard.None) Toast($"{sim.abyss.hazardName}: {sim.abyss.hazardDesc}", 6f);
        }

        GameView view;

        void EnterPlay()
        {
            playRoot = new GameObject("Play");
            view = playRoot.AddComponent<GameView>();
            view.Init(sim);
            play = playRoot.AddComponent<PlayController>();
            play.Init(this, sim, cam);
            cam.backgroundColor = Art.C(sim.abyss.wallColor);
            BuildHud();
        }

        bool resuming;

        /// 진행 중이던 지역을 이어한다: 같은 지역을 새로 만들고, 적어 둔 명령을 틱에 맞춰 넣으며 저장한 틱까지 빨리 돌린다
        public void ResumeRun()
        {
            if (!CanResume || resuming) return;
            StartCoroutine(ResumeRoutine());
        }

        System.Collections.IEnumerator ResumeRoutine()
        {
            var run = save.run;
            resuming = true;
            EndRegion();
            AbyssId = run.abyss; Region = run.region;
            ResultShown = false; Paused = false; Speed = 1; acc = 0;
            Taken = (int[])run.taken.Clone();
            var root = NewScreen("Resume");
            Ui.Panel(root, Ui.Bg).Rt().Fill();
            var label = Ui.Label(root, "", 44, null, TextAnchor.MiddleCenter, true);
            label.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 40, 1000, 160);
            var back = Ui.Panel(root, new Color(0, 0, 0, 0.6f), true, "Bar");
            back.Rt().At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, -80, 760, 40);
            var fill = Ui.Panel(back.transform, Primary, true, "Fill").Rt();
            fill.anchorMin = Vector2.zero; fill.offsetMin = new Vector2(4, 4); fill.offsetMax = new Vector2(-4, -4);

            var s = new Sim.Sim(run.abyss, run.region, Taken, Research.Mods(run.research));
            var cmds = new List<SimCommand>(run.cmds);
            int next = 0;
            var watch = new System.Diagnostics.Stopwatch();
            while (s.tick < run.tick && s.state == SimState.Playing)
            {
                watch.Restart();
                while (watch.ElapsedMilliseconds < 14 && s.tick < run.tick && s.state == SimState.Playing) next = s.Replay(cmds, next, run.tick, 120);
                float k = run.tick > 0 ? (float)s.tick / run.tick : 1;
                fill.anchorMax = new Vector2(Mathf.Max(0.02f, k), 1);
                label.text = $"{Content.Abysses[run.abyss].name} · 지역 {run.region}\n이어하는 중 {Mathf.FloorToInt(k * 100)}%";
                yield return null;
            }
            if (s.tick >= run.tick) s.Replay(cmds, next, run.tick, 0);
            sim = s;
            resuming = false;
            EnterPlay();
            Paused = true;   // 이어한 판은 멈춘 채로 연다
            Toast("이어하기: 멈춘 상태입니다. '계속'을 눌러 다시 시작하세요");
        }

        void EndRegion()
        {
            if (playRoot != null) { Destroy(playRoot); toastUntil = 0; }   // 지역이 끝나면 그 지역에서 뜬 알림은 다음 화면으로 넘기지 않는다
            playRoot = null; sim = null; play = null; bot = null;
            cam.backgroundColor = Ui.Bg;
        }

        /// 싸움을 포기하고 돌아간다. 핵에 남은 자원의 절반만 기지로 돌아온다
        public void Retreat()
        {
            if (sim != null && sim.state == SimState.Playing)
            {
                for (int i = 0; i < save.stock.Length; i++) save.stock[i] += (int)(sim.Core.items[i] * RetreatReturn);
            }
            save.run.active = false;
            Save();
            int a = AbyssId;
            EndRegion();
            ShowRegionMap(a);
        }

        public void SetSpeed(int s) { Speed = s; }
        public void TogglePause() { Paused = !Paused; }

        void Update()
        {
            if (toastText != null && toastText.transform.parent.gameObject.activeSelf && Time.unscaledTime > toastUntil)
                toastText.transform.parent.gameObject.SetActive(false);

            // 기지는 어느 화면에 있든 계속 생산한다
            Accrue();
            saveTimer += Time.unscaledDeltaTime;
            if (saveTimer > 20) { saveTimer = 0; Save(); }
            if (refresh != null)
            {
                refreshTimer += Time.unscaledDeltaTime;
                if (refreshTimer > 1f && !Input.GetMouseButton(0) && Input.touchCount == 0) { refreshTimer = 0; refresh(); }
            }
            if (sim == null || resuming) return;

            if (!Paused && sim.state == SimState.Playing)
            {
                acc += Mathf.Min(Time.deltaTime, 0.1f) * Sim.Sim.TicksPerSecond * Speed;
                int n = Mathf.Min((int)acc, 40);
                acc -= (int)acc;
                for (int i = 0; i < n && sim.state == SimState.Playing; i++)
                {
                    if (i == n - 1 && view != null) view.Capture();   // 마지막 틱 직전의 자리를 기억해 두고, 화면은 거기서 지금 자리까지를 잇는다
                    bot?.Step();
                    sim.Tick();
                }
                if (view != null) view.Alpha = acc;
            }
            UpdateHud();
            if (sim.state != SimState.Playing && !ResultShown) Finish();
        }

        /// 그 단계의 지역을 처음 점령하면 열리는 것들의 이름. 가장 깊은 단계가 before → after 로 바뀔 때 열리는 기지 강화도 함께 적는다
        public List<string> StageUnlocks(int stage, int before, int after)
        {
            var list = new List<string>();
            foreach (var d in Research.Defs) if (d.reqRegion == stage) list.Add(d.name);
            if (stage == BaseRules.CrystalMineRegion) list.Add(BaseRules.MineNames[Content.Crystal]);
            foreach (var rf in BaseRules.Refineries) if (rf.reqRegion == stage) list.Add(rf.name);
            for (int lv = 1; lv < BaseRules.MaxMineLevel; lv++)
            {
                int need = BaseRules.UpgradeRegion(lv);
                if (need > before && need <= after) list.Add($"기지 강화 Lv {lv + 1}");
            }
            if (stage > Content.RegionCount && RegionRules.IsKey(stage)) list.Add("연구 상한 +1 (포탑 화력·강화 장갑·핵 보호막·기체 강화)");
            int ab = Content.StageAbyss(stage);
            if (Content.StageRegion(stage) == Content.RegionCount && ab + 1 < Content.Abysses.Length) list.Add(Content.Abysses[ab + 1].name);
            return list;
        }

        void Finish()
        {
            ResultShown = true;
            bool won = sim.state == SimState.Won;
            var returned = new int[Content.Items.Length];
            string bonus = null;
            var unlocked = new List<string>();
            if (won)
            {
                // 남은 자원은 모두 기지로 돌아온다
                for (int i = 0; i < returned.Length; i++)
                {
                    returned[i] = sim.Core.items[i];
                    save.stock[i] += returned[i];
                }
                if (!IsDone(AbyssId, Region))
                {
                    // 처음 점령: 기지 생산량이 늘고, 이 지역에 묶인 기술이 열린다
                    int stage = Content.Stage(AbyssId, Region), bestBefore = BestStage;
                    save.abysses[AbyssId].done[Region] = true;
                    BaseRules.RegionBonus(AbyssId, Region, out int item, out float perMin);
                    bonus = $"{Content.Items[item].name} +{perMin:0.##}/분";
                    unlocked.AddRange(StageUnlocks(stage, bestBefore, BestStage));
                }
            }
            save.run.active = false;
            Save();
            ShowResult(won, returned, bonus, unlocked);
        }
    }
}
