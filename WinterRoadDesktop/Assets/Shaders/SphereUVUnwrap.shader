Shader "Custom/SphereUVUnwrap"
{
    Properties
    {
        _FrontTex ("Front Camera Texture", 2D) = "white" {}
        _BackTex ("Back Camera Texture", 2D) = "white" {}
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
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // UV (0~1) → 구체 표면 좌표
                float2 uv = i.uv;
                
                // Lat-Long (위도-경도) 매핑
                float theta = uv.x * UNITY_TWO_PI;     // 0 ~ 2π (경도)
                float phi = (1.0 - uv.y) * UNITY_PI;   // 0 ~ π (위도)
                
                // 구면 좌표 → 3D 방향 벡터
                float3 dir;
                dir.x = sin(phi) * cos(theta);
                dir.y = cos(phi);
                dir.z = sin(phi) * sin(theta);
                
                // 전면/후면 판단 (Z 축 기준)
                float isFront = step(0.0, dir.z);
                
                // 구형 왜곡 투영 (Fish-eye)
                float3 camDir = isFront > 0.5 ? dir : float3(dir.x, dir.y, -dir.z);
                
                // 각도 계산
                float dist = length(camDir.xy);
                float viewAngle = atan2(dist, camDir.z);
                float azimuth = atan2(camDir.y, camDir.x);
                
                // Fish-eye UV 계산
                float radius = viewAngle / (UNITY_PI * 0.5);
                float2 fisheyeUV;
                fisheyeUV.x = 0.5 + radius * cos(azimuth) * 0.5;
                fisheyeUV.y = 0.5 + radius * sin(azimuth) * 0.5;
                
                // 텍스처 샘플링
                fixed4 frontColor = tex2D(_FrontTex, fisheyeUV);
                fixed4 backColor = tex2D(_BackTex, fisheyeUV);
                
                // 블렌딩
                float blend = smoothstep(-0.2, 0.2, dir.z);
                fixed4 finalColor = lerp(backColor, frontColor, blend);
                
                return finalColor;
            }
            ENDCG
        }
    }
    
    Fallback "Unlit/Texture"
}