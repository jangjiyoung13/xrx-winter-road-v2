Shader "Custom/SphereUVUnwrapSimple"
{
    Properties
    {
        _FrontTex ("Front Camera Texture", 2D) = "white" {}
        _BackTex ("Back Camera Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            sampler2D _FrontTex;
            sampler2D _BackTex;
            
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // 좌측 절반 = 전면, 우측 절반 = 후면 (테스트용)
                if (i.uv.x < 0.5)
                {
                    return tex2D(_FrontTex, float2(i.uv.x * 2.0, i.uv.y));
                }
                else
                {
                    return tex2D(_BackTex, float2((i.uv.x - 0.5) * 2.0, i.uv.y));
                }
            }
            ENDCG
        }
    }
}


