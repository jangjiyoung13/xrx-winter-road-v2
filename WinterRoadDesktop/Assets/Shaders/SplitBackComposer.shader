Shader "Custom/SplitBackComposer"
{
    Properties
    {
        _FrontTex ("Front Texture", 2D) = "white" {}
        _BackTex ("Back Texture", 2D) = "white" {}
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
                float2 uv = i.uv;
                
                // 비율 2:1:1로 분할
                // [0.0 ~ 0.25]: 후면 우측 절반 (1/4)
                // [0.25 ~ 0.75]: 전면 전체 (2/4)
                // [0.75 ~ 1.0]: 후면 좌측 절반 (1/4)
                
                if (uv.x < 0.25) // 좌측 1/4: 후면 우측 절반 (0.5~1.0)
                {
                    // UV를 0~0.25 → 0.5~1.0로 매핑
                    float2 backUV;
                    backUV.x = 0.5 + (uv.x / 0.25) * 0.5;
                    backUV.y = uv.y;
                    return tex2D(_BackTex, backUV);
                }
                else if (uv.x < 0.75) // 중앙 2/4: 전면 전체
                {
                    // UV를 0.25~0.75 → 0~1로 매핑
                    float2 frontUV;
                    frontUV.x = (uv.x - 0.25) / 0.5;
                    frontUV.y = uv.y;
                    return tex2D(_FrontTex, frontUV);
                }
                else // 우측 1/4: 후면 좌측 절반 (0.0~0.5)
                {
                    // UV를 0.75~1.0 → 0~0.5로 매핑
                    float2 backUV;
                    backUV.x = ((uv.x - 0.75) / 0.25) * 0.5;
                    backUV.y = uv.y;
                    return tex2D(_BackTex, backUV);
                }
            }
            ENDCG
        }
    }
}

