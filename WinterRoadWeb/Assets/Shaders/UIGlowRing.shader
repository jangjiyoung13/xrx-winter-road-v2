Shader "UI/GlowRing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.5, 0.9, 1, 1)
        
        _RingRadius ("Ring Radius", Range(0.1, 0.5)) = 0.38
        _RingWidth ("Ring Width", Range(0.001, 0.1)) = 0.025
        _GlowSize ("Glow Size", Range(0.01, 0.3)) = 0.08
        _GlowIntensity ("Glow Intensity", Range(0.5, 5.0)) = 2.0
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
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
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _RingRadius;
            float _RingWidth;
            float _GlowSize;
            float _GlowIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // UV를 중심 기준으로 변환 (-0.5 ~ 0.5)
                float2 uv = i.texcoord - 0.5;
                float dist = length(uv);
                
                // 링까지의 거리 (링 중심으로부터)
                float ringDist = abs(dist - _RingRadius);
                
                // 코어 링 (선명한 원형 테두리)
                float ring = 1.0 - smoothstep(0.0, _RingWidth, ringDist);
                
                // 글로우 (부드러운 빛 번짐)
                float glow = 1.0 - smoothstep(0.0, _GlowSize, ringDist);
                glow = pow(glow, 1.5) * _GlowIntensity;
                
                // 합성
                float alpha = saturate(ring + glow * 0.5);
                
                // 색상 적용
                fixed4 color;
                color.rgb = i.color.rgb * (ring * _GlowIntensity + glow);
                color.a = alpha * i.color.a;
                
                // Premultiplied alpha
                color.rgb *= color.a;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
