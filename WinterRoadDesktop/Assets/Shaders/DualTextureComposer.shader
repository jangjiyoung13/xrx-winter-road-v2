Shader "Custom/DualTextureComposer"
{
    Properties
    {
        _FrontTex ("Front Texture", 2D) = "white" {}
        _BackTex ("Back Texture", 2D) = "white" {}
        _FrontRect ("Front Rect (x,y,w,h)", Vector) = (0, 0.5, 1, 0.5)
        _BackRect ("Back Rect (x,y,w,h)", Vector) = (0, 0, 1, 0.5)
        _BlendMode ("Blend Mode", Range(0, 2)) = 0
        _Opacity ("Opacity", Range(0, 1)) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
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
            
            sampler2D _FrontTex;
            sampler2D _BackTex;
            float4 _FrontTex_ST;
            float4 _BackTex_ST;
            float4 _FrontRect;  // x, y, width, height (0~1)
            float4 _BackRect;   // x, y, width, height (0~1)
            float _BlendMode;
            float _Opacity;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                fixed4 finalColor = fixed4(0, 0, 0, 1);
                
                // 전면 텍스처 영역 확인
                bool inFrontRect = (uv.x >= _FrontRect.x && uv.x <= _FrontRect.x + _FrontRect.z &&
                                   uv.y >= _FrontRect.y && uv.y <= _FrontRect.y + _FrontRect.w);
                
                // 후면 텍스처 영역 확인  
                bool inBackRect = (uv.x >= _BackRect.x && uv.x <= _BackRect.x + _BackRect.z &&
                                  uv.y >= _BackRect.y && uv.y <= _BackRect.y + _BackRect.w);
                
                if (inFrontRect)
                {
                    // 전면 텍스처 영역의 로컬 UV 계산
                    float2 frontLocalUV = float2(
                        (uv.x - _FrontRect.x) / _FrontRect.z,
                        (uv.y - _FrontRect.y) / _FrontRect.w
                    );
                    
                    fixed4 frontColor = tex2D(_FrontTex, frontLocalUV);
                    
                    if (inBackRect)
                    {
                        // 겹치는 영역: 블렌딩 처리
                        float2 backLocalUV = float2(
                            (uv.x - _BackRect.x) / _BackRect.z,
                            (uv.y - _BackRect.y) / _BackRect.w
                        );
                        
                        fixed4 backColor = tex2D(_BackTex, backLocalUV);
                        
                        // 블렌드 모드에 따른 합성
                        if (_BlendMode < 0.5) // Normal blend
                        {
                            finalColor = lerp(backColor, frontColor, frontColor.a * _Opacity);
                        }
                        else if (_BlendMode < 1.5) // Additive blend
                        {
                            finalColor = backColor + frontColor * _Opacity;
                        }
                        else // Screen blend
                        {
                            finalColor = 1 - (1 - backColor) * (1 - frontColor * _Opacity);
                        }
                    }
                    else
                    {
                        // 전면만 있는 영역
                        finalColor = frontColor * _Opacity;
                    }
                }
                else if (inBackRect)
                {
                    // 후면만 있는 영역
                    float2 backLocalUV = float2(
                        (uv.x - _BackRect.x) / _BackRect.z,
                        (uv.y - _BackRect.y) / _BackRect.w
                    );
                    
                    finalColor = tex2D(_BackTex, backLocalUV);
                }
                
                return finalColor;
            }
            ENDCG
        }
    }
}



