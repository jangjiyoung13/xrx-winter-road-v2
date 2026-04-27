Shader "Custom/SphericalDistortion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DistortionStrength ("Distortion Strength", Range(0, 2)) = 1.0
        _SphereRadius ("Sphere Radius", Range(0.1, 1.0)) = 0.5
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
            float _DistortionStrength;
            float _SphereRadius;
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
                // UV를 중심점 기준으로 변환
                float2 centeredUV = i.uv - _Center.xy;
                
                // 거리 계산
                float distance = length(centeredUV);
                
                // 구형 왜곡 계산
                float2 distortedUV;
                
                if (distance < _SphereRadius)
                {
                    // 구의 내부: 볼록 렌즈 효과
                    float normalizedDistance = distance / _SphereRadius;
                    float factor = pow(normalizedDistance, _DistortionStrength);
                    distortedUV = _Center.xy + normalize(centeredUV) * factor * _SphereRadius;
                }
                else
                {
                    // 구의 외부: 원본 유지 또는 약간의 확대
                    distortedUV = i.uv;
                }
                
                // 경계 체크
                if (distortedUV.x < 0.0 || distortedUV.x > 1.0 || 
                    distortedUV.y < 0.0 || distortedUV.y > 1.0)
                {
                    return fixed4(0, 0, 0, 1); // 검은색으로 처리
                }
                
                return tex2D(_MainTex, distortedUV);
            }
            ENDCG
        }
    }
}


