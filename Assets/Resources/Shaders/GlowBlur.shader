// 빛 번짐용 흐림. _Dir 방향으로 한 줄을 흐린다 (가로 한 번, 세로 한 번)
Shader "AbyssDefense/GlowBlur"
{
    Properties { _MainTex ("", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float2 _Dir;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 o = _MainTex_TexelSize.xy * _Dir;
                fixed4 c = tex2D(_MainTex, i.uv) * 0.2270270;
                c += (tex2D(_MainTex, i.uv + o * 1.3846154) + tex2D(_MainTex, i.uv - o * 1.3846154)) * 0.3162162;
                c += (tex2D(_MainTex, i.uv + o * 3.2307692) + tex2D(_MainTex, i.uv - o * 3.2307692)) * 0.0702703;
                return c;
            }
            ENDCG
        }
    }
}
