Shader "Custom/BorderOverlay"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _BorderTex ("Border Sprite", 2D) = "white" {}
        _BorderOpacity ("Border Opacity", Range(0, 1)) = 1.0
        _BorderScale ("Border Scale", Range(0.5, 2.0)) = 1.0
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
            
            sampler2D _MainTex;
            sampler2D _BorderTex;
            float _BorderOpacity;
            float _BorderScale;
            
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // 메인 텍스처 샘플링
                fixed4 mainColor = tex2D(_MainTex, i.uv);
                
                // 테두리 UV 계산 (중앙 기준으로 스케일링)
                float2 borderUV = (i.uv - 0.5) / _BorderScale + 0.5;
                
                // 테두리 텍스처 샘플링
                fixed4 borderColor = tex2D(_BorderTex, borderUV);
                
                // 알파 블렌딩으로 테두리 합성
                float alpha = borderColor.a * _BorderOpacity;
                fixed4 finalColor = lerp(mainColor, borderColor, alpha);
                
                return finalColor;
            }
            ENDCG
        }
    }
}

