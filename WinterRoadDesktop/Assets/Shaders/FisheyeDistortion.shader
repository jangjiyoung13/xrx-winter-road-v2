Shader "Custom/FisheyeDistortion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Fisheye Strength", Range(0, 5)) = 2.0
        _LensRadius ("Lens Radius", Range(0.1, 1.0)) = 0.8
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
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
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Strength;
            float _LensRadius;
            float4 _Center;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 중심점에서의 거리 계산
                float2 coord = i.uv - _Center.xy;
                float distance = length(coord);
                
                // 어안 렌즈 효과 적용
                if (distance < _LensRadius)
                {
                    // 어안 왜곡 공식
                    float normalizedDistance = distance / _LensRadius;
                    float theta = atan2(coord.y, coord.x);
                    
                    // 어안 렌즈 변환
                    float radius = pow(normalizedDistance, _Strength) * _LensRadius;
                    
                    float2 fisheyeCoord = float2(
                        radius * cos(theta),
                        radius * sin(theta)
                    );
                    
                    float2 finalUV = _Center.xy + fisheyeCoord;
                    
                    // 경계 확인
                    if (finalUV.x >= 0.0 && finalUV.x <= 1.0 && 
                        finalUV.y >= 0.0 && finalUV.y <= 1.0)
                    {
                        return tex2D(_MainTex, finalUV);
                    }
                }
                
                return fixed4(0, 0, 0, 1); // 렌즈 외부는 검은색
            }
            ENDCG
        }
    }
}



