Shader "Custom/SplitBackComposerWithBorder"
{
    Properties
    {
        _FrontTex ("Front Texture", 2D) = "white" {}
        _BackTex ("Back Texture", 2D) = "white" {}
        _BorderTex ("Border Sprite", 2D) = "white" {}
        _BorderOpacity ("Border Opacity", Range(0, 1)) = 1.0
        _FrontBorderScale ("Front Border Scale", Range(0.5, 2.0)) = 1.0
        _BackBorderScale ("Back Border Scale", Range(0.5, 2.0)) = 1.0
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
            sampler2D _BorderTex;
            float _BorderOpacity;
            float _FrontBorderScale;
            float _BackBorderScale;
            
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }
            
            fixed4 ApplyBorder(fixed4 baseColor, float2 regionUV, float borderScale)
            {
                // 테두리 UV 계산 (중앙 기준으로 스케일링)
                float2 borderUV = (regionUV - 0.5) / borderScale + 0.5;
                
                // 테두리 텍스처 샘플링
                fixed4 borderColor = tex2D(_BorderTex, borderUV);
                
                // 알파 블렌딩으로 테두리 합성
                float alpha = borderColor.a * _BorderOpacity;
                return lerp(baseColor, borderColor, alpha);
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                fixed4 finalColor;
                
                // 비율 2:1:1로 분할
                // [0.0 ~ 0.25]: 후면 우측 절반 (1/4)
                // [0.25 ~ 0.75]: 전면 전체 (2/4)
                // [0.75 ~ 1.0]: 후면 좌측 절반 (1/4)
                
                if (uv.x < 0.25) // 좌측 1/4: 후면 우측 절반
                {
                    // 후면 전체 텍스처에서 우측 절반 샘플링 (0.5~1.0)
                    float2 backUV;
                    backUV.x = 0.5 + (uv.x / 0.25) * 0.5;
                    backUV.y = uv.y;
                    
                    // 후면 텍스처 샘플링
                    fixed4 backColor = tex2D(_BackTex, backUV);
                    
                    // 후면 전체 테두리 UV (우측 절반 부분)
                    float2 backBorderUV;
                    backBorderUV.x = 0.5 + (uv.x / 0.25) * 0.5;
                    backBorderUV.y = uv.y;
                    
                    // 후면 전체에 적용된 테두리 적용
                    finalColor = ApplyBorder(backColor, backBorderUV, _BackBorderScale);
                }
                else if (uv.x < 0.75) // 중앙 2/4: 전면 전체
                {
                    // UV를 0.25~0.75 → 0~1로 매핑
                    float2 frontUV;
                    frontUV.x = (uv.x - 0.25) / 0.5;
                    frontUV.y = uv.y;
                    
                    // 전면 텍스처 샘플링
                    fixed4 frontColor = tex2D(_FrontTex, frontUV);
                    
                    // 전면 테두리 적용
                    finalColor = ApplyBorder(frontColor, frontUV, _FrontBorderScale);
                }
                else // 우측 1/4: 후면 좌측 절반
                {
                    // 후면 전체 텍스처에서 좌측 절반 샘플링 (0.0~0.5)
                    float2 backUV;
                    backUV.x = ((uv.x - 0.75) / 0.25) * 0.5;
                    backUV.y = uv.y;
                    
                    // 후면 텍스처 샘플링
                    fixed4 backColor = tex2D(_BackTex, backUV);
                    
                    // 후면 전체 테두리 UV (좌측 절반 부분)
                    float2 backBorderUV;
                    backBorderUV.x = ((uv.x - 0.75) / 0.25) * 0.5;
                    backBorderUV.y = uv.y;
                    
                    // 후면 전체에 적용된 테두리 적용
                    finalColor = ApplyBorder(backColor, backBorderUV, _BackBorderScale);
                }
                
                return finalColor;
            }
            ENDCG
        }
    }
}
