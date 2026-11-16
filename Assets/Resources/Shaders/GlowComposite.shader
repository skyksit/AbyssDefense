// 흐린 빛을 원래 화면에 더한다
Shader "AbyssDefense/GlowComposite"
{
    Properties { _MainTex ("", 2D) = "white" {} }
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
            sampler2D _GlowTex;
            float _Intensity;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 guv = i.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) guv.y = 1 - guv.y;
                #endif
                fixed4 c = tex2D(_MainTex, i.uv);
                c.rgb += tex2D(_GlowTex, guv).rgb * _Intensity;
                return c;
            }
            ENDCG
        }
    }
}
