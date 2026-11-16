// 물·용암·산성 바닥. 정점 색이 바탕이고, 그 위에 흐르는 밝은 줄과 얼룩을 얹는다.
// uv.x = 종류 (0 물, 1 용암, 2 산성, 3 움직이지 않는 테두리)
Shader "AbyssDefense/Liquid"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float3 data : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.data = float3(mul(unity_ObjectToWorld, v.vertex).xy, v.uv.x);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.data.xy;
                float k = i.data.z;
                float isWater = step(k, 0.5);
                float isLava = step(0.5, k) * step(k, 1.5);
                float isAcid = step(1.5, k) * step(k, 2.5);

                float t = _Time.y * (isWater * 1.0 + isLava * 0.45 + isAcid * 0.7);
                float a = sin(p.x * 3.1 + p.y * 1.7 + t * 1.3 + sin(p.y * 2.3 + t * 0.7) * 1.2);
                float b = sin(-p.x * 2.3 + p.y * 3.7 + t * 0.9 + sin(p.x * 1.9 - t * 0.5) * 1.4);
                float lines = 1.0 - smoothstep(0.0, 0.22, abs(a + b));
                float blobs = smoothstep(0.35, 0.95, a * b * 0.5 + 0.5);

                fixed3 hi = fixed3(0.45, 0.72, 0.90) * isWater + fixed3(1.0, 0.66, 0.20) * isLava + fixed3(0.78, 0.97, 0.38) * isAcid;
                float amt = isWater * (lines * 0.45 + blobs * 0.08)
                          + isLava * (blobs * 0.75 + lines * 0.35)
                          + isAcid * (blobs * 0.50 + lines * 0.20);

                fixed4 c = i.color;
                c.rgb = lerp(c.rgb, hi, saturate(amt));
                return c;
            }
            ENDCG
        }
    }
}
