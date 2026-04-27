Shader "Custom/IceBreakEffect"
{
    Properties
    {
        _MainTex ("Video Texture", 2D) = "white" {}
        _CrackTex ("Crack Texture", 2D) = "white" {}
        _TouchPos ("Touch Position", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Crack Radius", Float) = 0.0
        _Intensity ("Intensity", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            sampler2D _CrackTex;
            float4 _TouchPos;
            float _Radius;
            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                fixed4 col = tex2D(_MainTex, uv);

                // 터치 중심과의 거리
                float dist = distance(uv, _TouchPos.xy);

                // Crack 텍스처 샘플링 (중심에 가까울수록 강하게)
                float crack = tex2D(_CrackTex, uv * 5 - _TouchPos.xy * 4).r;
                crack *= smoothstep(_Radius, _Radius * 0.5, dist);
                crack *= _Intensity;

                // 얼음 균열 섞기
                col.rgb = lerp(col.rgb, col.rgb * (1 - crack) + crack * float3(0.7, 0.9, 1.0), crack);
                return col;
            }
            ENDCG
        }
    }
}
