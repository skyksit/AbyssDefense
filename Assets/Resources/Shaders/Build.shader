// 짓는 중인 건물. 그림의 아래에서 _Progress 높이까지는 제 색으로, 그 위는 흐린 설계도로 그린다
Shader "AbyssDefense/Build"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _Plan ("Blueprint Color", Color) = (0.45, 0.8, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "False" }
        Cull Off ZWrite Off Lighting Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float _Progress;
            fixed4 _Plan;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv) * i.color;
                float built = step(i.uv.y, _Progress);
                // 아직 안 지어진 곳: 밝기만 남기고 설계도 색을 입힌다
                float lum = dot(t.rgb, fixed3(0.3, 0.59, 0.11));
                fixed3 plan = lerp(fixed3(lum, lum, lum), _Plan.rgb, 0.55);
                fixed3 rgb = lerp(plan, t.rgb, built);
                float a = t.a * lerp(0.32, 1.0, built);
                // 지어지는 높이의 밝은 줄
                float band = saturate(1.0 - abs(i.uv.y - _Progress) / 0.04) * step(0.001, _Progress);
                rgb = lerp(rgb, fixed3(1, 1, 1), band * 0.7);
                a = max(a, t.a * band);
                return fixed4(rgb * a, a);
            }
            ENDCG
        }
    }
}
