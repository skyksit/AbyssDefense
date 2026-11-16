using UnityEngine;

namespace AbyssDefense
{
    /// 빛 번짐. 빛나는 것(탄·불꽃·섬광·광원)만 작은 화면에 따로 그려 흐린 뒤 원래 화면에 더한다.
    /// 메인 카메라에 붙고, 전투 화면(GameView)이 있는 동안만 켜진다. 셰이더를 못 쓰는 기기에서는 꺼진 채로 남는다.
    public sealed class Glow : MonoBehaviour
    {
        public const int Layer = 31;       // 화면에도 그리고 빛 번짐에도 넣는 것
        public const int OnlyLayer = 30;   // 빛 번짐에만 넣는 것 (용암의 은은한 빛)
        const float Intensity = 0.85f;

        Camera main, cam;
        RenderTexture rt;
        Material blur, comp;
        bool ok;

        /// 전투 화면을 열고 닫을 때 부른다
        public static void Set(Camera c, bool on)
        {
            if (c == null) return;
            c.cullingMask &= ~(1 << OnlyLayer);
            var g = c.GetComponent<Glow>();
            if (g == null) { if (!on) return; g = c.gameObject.AddComponent<Glow>(); g.Setup(c); }
            g.enabled = on && g.ok;
            if (g.cam != null) g.cam.enabled = g.enabled;
            if (!g.enabled) g.Release();
        }

        void Setup(Camera c)
        {
            main = c;
            var sb = Resources.Load<Shader>("Shaders/GlowBlur");
            var sc = Resources.Load<Shader>("Shaders/GlowComposite");
            if (sb == null || sc == null || !sb.isSupported || !sc.isSupported) return;
            blur = new Material(sb);
            comp = new Material(sc);

            var go = new GameObject("GlowCam");
            go.transform.SetParent(c.transform, false);
            cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = (1 << Layer) | (1 << OnlyLayer);
            cam.depth = c.depth - 1;   // 메인 카메라보다 먼저 그려 둔다
            cam.allowMSAA = false;
            cam.allowHDR = false;
            cam.useOcclusionCulling = false;
            cam.nearClipPlane = c.nearClipPlane;
            cam.farClipPlane = c.farClipPlane;
            go.AddComponent<GlowSync>().Init(c, cam);
            ok = true;
        }

        void Release()
        {
            if (cam != null) cam.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
        }

        void Update()
        {
            // 화면 크기가 바뀌면 다시 만든다 (가로세로 절반 크기)
            int w = Mathf.Max(16, Screen.width / 2), h = Mathf.Max(16, Screen.height / 2);
            if (rt != null && rt.width == w && rt.height == h) return;
            Release();
            rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            cam.targetTexture = rt;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (rt == null || !rt.IsCreated()) { Graphics.Blit(src, dst); return; }
            int w = rt.width / 2, h = rt.height / 2;
            var a = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var b = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            // 가로·세로로 두 번씩, 두 번째는 더 넓게
            blur.SetVector("_Dir", new Vector2(1, 0)); Graphics.Blit(rt, a, blur);
            blur.SetVector("_Dir", new Vector2(0, 1)); Graphics.Blit(a, b, blur);
            blur.SetVector("_Dir", new Vector2(2, 0)); Graphics.Blit(b, a, blur);
            blur.SetVector("_Dir", new Vector2(0, 2)); Graphics.Blit(a, b, blur);
            comp.SetTexture("_GlowTex", b);
            comp.SetFloat("_Intensity", Intensity);
            Graphics.Blit(src, dst, comp);
            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
        }

        void OnDestroy() => Release();
    }

    /// 빛 번짐 카메라가 그리기 직전에 메인 카메라의 줌을 따라가게 한다 (위치는 자식이라 저절로 따라간다)
    public sealed class GlowSync : MonoBehaviour
    {
        Camera main, cam;
        public void Init(Camera main, Camera cam) { this.main = main; this.cam = cam; }
        void OnPreCull()
        {
            cam.orthographicSize = main.orthographicSize;
            cam.aspect = main.aspect;
        }
    }
}
