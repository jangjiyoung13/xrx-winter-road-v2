Shader "UI/Vignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0, 0, 0, 1)
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.5
        _VignetteRadius ("Vignette Radius", Range(0, 1)) = 0.7
        _VignetteSoftness ("Vignette Softness", Range(0.01, 1)) = 0.3
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Overlay"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
            };

            fixed4 _Color;
            float _VignetteIntensity;
            float _VignetteRadius;
            float _VignetteSoftness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // UV 좌표를 중심(0.5, 0.5) 기준으로 변환
                float2 uv = i.texcoord - 0.5;
                
                // 사각형 거리 계산 (가장자리까지의 거리)
                float dist = max(abs(uv.x), abs(uv.y)) * 2.0;
                
                // 비네팅 계산: radius 안쪽은 투명, 바깥쪽은 점점 어두워짐
                float vignette = smoothstep(_VignetteRadius, _VignetteRadius + _VignetteSoftness, dist);
                vignette *= _VignetteIntensity;
                
                // 검은색 + 비네팅 알파
                fixed4 col = i.color;
                col.a *= vignette;
                
                return col;
            }
            ENDCG
        }
    }
}
