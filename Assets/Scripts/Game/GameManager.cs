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
        public int cleared;                 // 점령한 가장 깊은 지역
    }

    [Serializable]
    public class SaveData
    {
        public int version = 3;
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
        public int difficulty = 1;                      // 난이도 (0 쉬움, 1 보통, 2 어려움). 지역 정보 창에서 바꾼다
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
        public int difficulty = 1;
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
            save.version = 3;
            save.difficulty = Mathf.Clamp(save.difficulty, 0, 2);
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

        public int[] Cleared
        {
            get
            {
                var c = new int[save.abysses.Count];
                for (int i = 0; i < c.Length; i++) c[i] = save.abysses[i].cleared;
                return c;
            }
        }

        /// 어느 심연에서든 가장 깊이 점령한 지역 (연구 해금 기준)
        public int BestCleared
        {
            get { int m = 0; foreach (var a in save.abysses) m = Mathf.Max(m, a.cleared); return m; }
        }

        /// 채굴로 들어오는 분당 생산량 (가공 건물이 쓰는 양은 빼지 않은 값)
        public float Rate(int item) => item < Content.RawCount ? BaseRules.Rate(item, save.mineLv[item], save.research, Cleared) : 0;
        /// 가공 건물까지 넣은 자원별 분당 증감
        public float[] NetRates() => BaseRules.NetRates(save.mineLv, save.refLv, save.refOn, save.research, Cleared);
        public int StorageCap => BaseRules.StorageCap(save.storageLv);
        public int LoadoutCap => BaseRules.LoadoutCap(save.research);
        public bool IsUnlocked(int abyss, int region) => region <= save.abysses[abyss].cleared + 1;

        /// 지난 시간만큼 기지를 돌린다 (채굴 + 가공). 창고 한도까지만 쌓이고, 오래 비워도 8시간치까지만
        public void Accrue()
        {
            double now = Now;
            double dt = Math.Min(Math.Max(0, now - save.lastTime), BaseRules.OfflineCapSeconds);
            save.lastTime = now;
            if (dt <= 0) return;
            BaseRules.Advance(save.stock, save.mineLv, save.refLv, save.refOn, save.research, Cleared, save.storageLv, dt);
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

        public bool CanBuildMine(int item) => item != Content.Crystal || save.mineLv[item] > 0 || BestCleared >= BaseRules.CrystalMineRegion;

        public bool UpgradeMine(int item)
        {
            if (save.mineLv[item] >= BaseRules.MaxMineLevel || !CanBuildMine(item)) return false;
            if (!Pay(BaseRules.MineCost(item, save.mineLv[item]))) return false;
            save.mineLv[item]++;
            Save();
            return true;
        }

        public bool CanBuildRefinery(int k) => save.refLv[k] > 0 || BestCleared >= BaseRules.Refineries[k].reqRegion;

        public bool UpgradeRefinery(int k)
        {
            if (save.refLv[k] >= BaseRules.MaxRefineryLevel || !CanBuildRefinery(k)) return false;
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
            if (save.storageLv >= BaseRules.MaxStorageLevel) return false;
            if (!Pay(BaseRules.StorageCost(save.storageLv))) return false;
            save.storageLv++;
            Save();
            return true;
        }

        public bool ResearchOpen(ResearchDef d) => BestCleared >= d.reqRegion;

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
            sim = new Sim.Sim(abyss, region, Taken, Research.Mods(save.research), save.difficulty);
            save.run = new RunSave { active = true, rules = Sim.Sim.RulesFingerprint(), abyss = abyss, region = region, difficulty = save.difficulty, taken = (int[])Taken.Clone(), research = (int[])save.research.Clone() };
            Save();
            EnterPlay();
            if (sim.rules.hazard != Hazard.None) Toast($"{sim.abyss.hazardName}: {sim.abyss.hazardDesc}", 6f);
        }

        void EnterPlay()
        {
            playRoot = new GameObject("Play");
            playRoot.AddComponent<GameView>().Init(sim);
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

            var s = new Sim.Sim(run.abyss, run.region, Taken, Research.Mods(run.research), run.difficulty);
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
            if (playRoot != null) Destroy(playRoot);
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
                    bot?.Step();
                    sim.Tick();
                }
            }
            UpdateHud();
            if (sim.state != SimState.Playing && !ResultShown) Finish();
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
                var a = save.abysses[AbyssId];
                if (Region > a.cleared)
                {
                    // 처음 점령: 기지 생산량이 늘고, 조건이 맞으면 새 연구가 열린다
                    int bestBefore = BestCleared;
                    a.cleared = Region;
                    BaseRules.RegionBonus(Region, out int item, out float perMin);
                    bonus = $"{Content.Items[item].name} +{perMin:0.##}/분";
                    foreach (var d in Research.Defs)
                        if (d.reqRegion > bestBefore && d.reqRegion <= BestCleared) unlocked.Add(d.name);
                    if (bestBefore < BaseRules.CrystalMineRegion && BestCleared >= BaseRules.CrystalMineRegion) unlocked.Add(BaseRules.MineNames[Content.Crystal]);
                    foreach (var rf in BaseRules.Refineries)
                        if (rf.reqRegion > bestBefore && rf.reqRegion <= BestCleared) unlocked.Add(rf.name);
                }
            }
            save.run.active = false;
            Save();
            ShowResult(won, returned, bonus, unlocked);
        }
    }
}
