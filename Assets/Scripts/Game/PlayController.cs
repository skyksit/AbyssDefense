using System.Collections.Generic;
using AbyssDefense.Sim;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AbyssDefense
{
    /// 카메라 이동·확대와 건설 조작. 터치와 마우스를 같은 흐름으로 다룬다.
    ///   도구 없음: 한 손가락 끌기 = 맵 이동, 건물 탭 = 정보
    ///   블록 선택: 탭 = 설치, 끌기 = 한 줄 설치
    ///   공장 탭 = 공장 창 (만들 유닛, 집결 지점)
    ///   두 손가락 / 오른쪽 버튼 / 휠: 언제나 이동·확대
    public sealed class PlayController : MonoBehaviour
    {
        public BlockDef Selected { get; private set; }
        public bool RemoveMode { get; private set; }
        public FactoryBuilding Factory { get; private set; }   // 탭해서 고른 공장
        public bool RallyMode { get; private set; }            // 다음에 탭하는 곳이 그 공장의 집결 지점이 된다
        public bool CommandMode { get; private set; }          // 다음에 탭하는 곳으로 지휘기를 보낸다 (핵을 탭하면 켜진다)

        GameManager gm;
        Sim.Sim sim;
        Camera cam;
        QuadBatch ghost;
        SpriteRenderer rangeRing;

        float viewW = 13f;              // 화면 가로에 보이는 칸 수
        const float MinViewW = 7f;
        bool dragging, dragIsPan, pinching;
        Vector2 downScreen, lastScreen;
        Vector2Int downTile, curTile;
        float pinchDist;
        Vector2 pinchMid;
        readonly List<Vector2Int> cells = new List<Vector2Int>();
        readonly List<RaycastResult> hits = new List<RaycastResult>();

        public void Init(GameManager gm, Sim.Sim sim, Camera cam)
        {
            this.gm = gm; this.sim = sim; this.cam = cam;
            ghost = new QuadBatch(transform, "Ghost", 25);
            var go = new GameObject("Range");
            go.transform.SetParent(transform, false);
            rangeRing = go.AddComponent<SpriteRenderer>();
            rangeRing.sprite = Art.Get("fx_ring");
            rangeRing.sortingOrder = 26;
            rangeRing.color = new Color(1, 1, 1, 0.55f);
            rangeRing.enabled = false;

            viewW = Mathf.Min(13f, sim.world.W + 1);
            cam.orthographic = true;
            ApplyZoom();
            var core = sim.Core;
            cam.transform.position = new Vector3(core.Cx, core.Cy + cam.orthographicSize * 0.35f, -10);
            ClampCamera();
        }

        public void Select(BlockDef def) { Selected = Selected == def ? null : def; RemoveMode = false; dragging = false; Factory = null; RallyMode = false; CommandMode = false; }
        public void ToggleRemove() { RemoveMode = !RemoveMode; Selected = null; dragging = false; Factory = null; RallyMode = false; CommandMode = false; }
        public void Cancel() { Selected = null; RemoveMode = false; dragging = false; Factory = null; RallyMode = false; CommandMode = false; }
        public void BeginCommand() { Cancel(); CommandMode = true; }
        public void SelectFactory(FactoryBuilding f) { Cancel(); Factory = f; }
        public void BeginRally() { if (Factory != null) { RallyMode = !RallyMode; dragging = false; } }
        bool HasTool => Selected != null || RemoveMode || RallyMode || CommandMode;

        void ApplyZoom()
        {
            viewW = Mathf.Clamp(viewW, MinViewW, sim.world.W + 3);
            cam.orthographicSize = viewW / cam.aspect * 0.5f;
        }

        /// 맵 밖으로 너무 나가지 않게. 위아래는 HUD 에 가리는 만큼 더 밀 수 있다
        void ClampCamera()
        {
            float hh = cam.orthographicSize, hw = hh * cam.aspect;
            float top = hh * 2 * GameManager.HudTopFrac, bottom = hh * 2 * GameManager.HudBottomFrac;
            var p = cam.transform.position;
            float minX = hw - 1, maxX = sim.world.W - hw + 1;
            float minY = hh - bottom - 1, maxY = sim.world.H - hh + top + 1;
            p.x = minX > maxX ? sim.world.W * 0.5f : Mathf.Clamp(p.x, minX, maxX);
            p.y = minY > maxY ? sim.world.H * 0.5f : Mathf.Clamp(p.y, minY, maxY);
            cam.transform.position = p;
        }

        bool OverUi(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var ped = new PointerEventData(es) { position = screen };
            hits.Clear();
            es.RaycastAll(ped, hits);
            return hits.Count > 0;
        }

        Vector2 ToWorld(Vector2 screen) => cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
        Vector2Int Tile(Vector2 screen) { var w = ToWorld(screen); return new Vector2Int(Mathf.FloorToInt(w.x), Mathf.FloorToInt(w.y)); }

        void Update()
        {
            if (sim == null) return;
            bool playing = sim.state == SimState.Playing && !gm.ResultShown;
            if (Factory != null && Factory.dead) { Factory = null; RallyMode = false; }
            if (!playing) { dragging = false; DrawGhost(); return; }

            Keyboard();
            if (Input.touchCount >= 2) Pinch();
            else
            {
                pinching = false;
                if (Input.touchCount == 1)
                {
                    var t = Input.GetTouch(0);
                    Pointer(t.position, t.phase == TouchPhase.Began, t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled);
                }
                else if (Input.touchCount == 0) Mouse();
            }
            ClampCamera();
            DrawGhost();
        }

        void Keyboard()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Cancel();
            if (Input.GetKeyDown(KeyCode.X)) ToggleRemove();
            for (int i = 0; i < Content.Buildable.Length && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && sim.IsUnlocked(Content.Buildable[i])) Select(Content.Buildable[i]);
        }

        void Mouse()
        {
            Vector2 pos = Input.mousePosition;
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0 && !OverUi(pos))
            {
                var before = ToWorld(pos);
                viewW *= 1 - scroll * 0.1f;
                ApplyZoom();
                cam.transform.position += (Vector3)(before - ToWorld(pos));
            }
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) lastScreen = pos;
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                cam.transform.position += (Vector3)(ToWorld(lastScreen) - ToWorld(pos));
                lastScreen = pos;
                return;
            }
            if (Input.GetMouseButtonDown(0)) Pointer(pos, true, false);
            else if (Input.GetMouseButtonUp(0)) Pointer(pos, false, true);
            else if (Input.GetMouseButton(0)) Pointer(pos, false, false);
        }

        void Pinch()
        {
            dragging = false;
            Vector2 a = Input.GetTouch(0).position, b = Input.GetTouch(1).position;
            float dist = Vector2.Distance(a, b);
            Vector2 mid = (a + b) * 0.5f;
            if (!pinching) { pinching = true; pinchDist = dist; pinchMid = mid; return; }
            if (dist > 1 && pinchDist > 1)
            {
                var before = ToWorld(mid);
                viewW *= pinchDist / dist;
                ApplyZoom();
                cam.transform.position += (Vector3)(before - ToWorld(mid));
            }
            cam.transform.position += (Vector3)(ToWorld(pinchMid) - ToWorld(mid));
            pinchDist = dist; pinchMid = mid;
        }

        void Pointer(Vector2 pos, bool down, bool up)
        {
            if (down)
            {
                if (OverUi(pos)) { dragging = false; return; }
                dragging = true;
                dragIsPan = !HasTool;
                downScreen = lastScreen = pos;
                downTile = curTile = Tile(pos);
            }
            if (!dragging) return;

            if (dragIsPan)
            {
                cam.transform.position += (Vector3)(ToWorld(lastScreen) - ToWorld(pos));
                lastScreen = pos;
                if (up)
                {
                    dragging = false;
                    if ((pos - downScreen).magnitude < Screen.width * 0.02f)
                    {
                        var b = sim.world.BuildAt(downTile.x, downTile.y);
                        if (b is FactoryBuilding f && f.Built) Factory = f;
                        else if (b is CoreBuilding && sim.commanderEnabled) { BeginCommand(); gm.Toast("지휘기를 보낼 곳을 탭하세요"); }
                        else { Factory = null; gm.ShowInfo(b); }
                    }
                }
                return;
            }

            if (CommandMode)
            {
                if (!up) return;
                dragging = false;
                var cp = ToWorld(pos);
                if (sim.SetCommand(cp.x, cp.y)) CommandMode = false;
                return;
            }

            if (RallyMode)
            {
                if (!up) return;
                dragging = false;
                var wp = ToWorld(pos);
                if (sim.SetRally(Factory, wp.x, wp.y)) RallyMode = false;
                else gm.Toast("그곳에는 집결 지점을 둘 수 없습니다");
                return;
            }

            curTile = Tile(pos);
            if (up) { dragging = false; Commit(); }
        }

        /// 점검용: 실제 입력 대신 화면 좌표로 누르기·끌기·떼기를 흉내 낸다 (같은 처리 경로)
        public void DebugPointer(Vector2 screen, bool down, bool up) { Pointer(screen, down, up); DrawGhost(); }

        /// 누른 칸에서 지금 칸까지, 더 많이 움직인 축을 따라 한 줄
        void BuildLine()
        {
            cells.Clear();
            int size = RemoveMode ? 1 : Selected.size;
            int dx = curTile.x - downTile.x, dy = curTile.y - downTile.y;
            bool horiz = Mathf.Abs(dx) >= Mathf.Abs(dy);
            int len = horiz ? dx : dy;
            int n = Mathf.Abs(len) / size;
            int sgn = len >= 0 ? 1 : -1;
            for (int i = 0; i <= n; i++)
                cells.Add(horiz ? new Vector2Int(downTile.x + i * size * sgn, downTile.y) : new Vector2Int(downTile.x, downTile.y + i * size * sgn));
        }

        void Commit()
        {
            BuildLine();
            if (RemoveMode)
            {
                foreach (var c in cells) sim.Remove(c.x, c.y);
                return;
            }
            PlaceResult fail = PlaceResult.Ok;
            int placed = 0;
            foreach (var c in cells)
            {
                var r = sim.Place(Selected, c.x, c.y);
                if (r == PlaceResult.Ok) placed++;
                else if (fail == PlaceResult.Ok) fail = r;
            }
            if (fail != PlaceResult.Ok && (placed == 0 || fail == PlaceResult.NoResources)) gm.Toast(Reason(fail));
        }

        string Reason(PlaceResult r)
        {
            switch (r)
            {
                case PlaceResult.NoResources: return "자원이 부족합니다";
                case PlaceResult.NearSpawn: return sim.rules.assault ? $"적 핵에서 {Sim.Sim.EnemyCoreSafeRadius}칸 안에는 지을 수 없습니다. 유닛을 보내 부수세요" : "적 출현 지점 가까이에는 지을 수 없습니다";
                case PlaceResult.Locked: return "기지에서 연구해야 지을 수 있습니다";
                case PlaceResult.NeedsWater: return "조선소는 호수 바로 옆에만 지을 수 있습니다";
                default: return "그 자리에는 지을 수 없습니다";
            }
        }

        void DrawGhost()
        {
            ghost.Begin();
            rangeRing.enabled = false;
            if (sim.commanderEnabled)
            {
                // 지휘기가 지키는 곳
                var cc = Art.C(Content.Commander.color, CommandMode ? 1f : 0.6f);
                ghost.Quad(sim.commandX, sim.commandY, 0.5f, 0.07f, cc, 45); ghost.Quad(sim.commandX, sim.commandY, 0.5f, 0.07f, cc, -45);
            }
            if (Factory != null)
            {
                // 고른 공장의 테두리와 집결 지점
                var f = Factory;
                float fs = f.def.size;
                var gold = new Color(1f, 0.82f, 0.3f, 0.9f);
                ghost.Quad(f.Cx, f.Cy + fs * 0.5f, fs + 0.1f, 0.08f, gold); ghost.Quad(f.Cx, f.Cy - fs * 0.5f, fs + 0.1f, 0.08f, gold);
                ghost.Quad(f.Cx + fs * 0.5f, f.Cy, 0.08f, fs + 0.1f, gold); ghost.Quad(f.Cx - fs * 0.5f, f.Cy, 0.08f, fs + 0.1f, gold);
                float lx = f.rallyX - f.Cx, ly = f.rallyY - f.Cy, len = Mathf.Sqrt(lx * lx + ly * ly);
                if (len > 0.01f) ghost.Quad((f.Cx + f.rallyX) * 0.5f, (f.Cy + f.rallyY) * 0.5f, len, 0.06f, new Color(1f, 0.82f, 0.3f, 0.45f), Mathf.Atan2(ly, lx) * Mathf.Rad2Deg);
                ghost.Quad(f.rallyX, f.rallyY, 0.42f, 0.42f, gold, 45);
                ghost.Quad(f.rallyX, f.rallyY, 0.22f, 0.22f, new Color(0.1f, 0.1f, 0.12f, 0.9f), 45);
            }
            if (dragging && !dragIsPan && HasTool && !RallyMode && !CommandMode)
            {
                BuildLine();
                if (RemoveMode)
                {
                    foreach (var c in cells)
                    {
                        var b = sim.world.BuildAt(c.x, c.y);
                        if (b != null && !(b is CoreBuilding)) ghost.Quad(b.Cx, b.Cy, b.def.size, b.def.size, new Color(1f, 0.25f, 0.25f, 0.55f));
                        else ghost.Quad(c.x + 0.5f, c.y + 0.5f, 0.9f, 0.9f, new Color(1f, 0.25f, 0.25f, 0.18f));
                    }
                }
                else
                {
                    var def = Selected;
                    var left = (int[])sim.Core.items.Clone();
                    var committed = sim.Committed();
                    for (int i = 0; i < left.Length; i++) left[i] -= committed[i];
                    float half = def.size * 0.5f;
                    foreach (var c in cells)
                    {
                        bool ok = sim.CanPlace(def, c.x, c.y, false) == PlaceResult.Ok;
                        if (ok)
                        {
                            foreach (var s in def.cost) if (left[s.item] < s.amount) ok = false;
                            if (ok) foreach (var s in def.cost) left[s.item] -= s.amount;
                        }
                        var col = ok ? new Color(0.4f, 1f, 0.55f, 0.45f) : new Color(1f, 0.3f, 0.3f, 0.5f);
                        ghost.Quad(c.x + half, c.y + half, def.size - 0.08f, def.size - 0.08f, col);
                    }
                    if (def.kind == BlockKind.Turret && cells.Count > 0)
                    {
                        var last = cells[cells.Count - 1];
                        rangeRing.enabled = true;
                        rangeRing.transform.position = new Vector3(last.x + half, last.y + half);
                        rangeRing.transform.localScale = Vector3.one * (def.range * sim.world.mods.rangeMul * 2 / rangeRing.sprite.bounds.size.x);
                    }
                }
            }
            ghost.End();
        }

    }
}
