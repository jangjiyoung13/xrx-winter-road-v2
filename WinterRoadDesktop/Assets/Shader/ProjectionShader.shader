Shader "Hidden/CubemapToEquirectangular"
{
    Properties { _Cube ("Cubemap", CUBE) = "" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _Cube;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata_img v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                float2 uv = i.uv; // 0~1
                float phi   = (uv.x - 0.5) * UNITY_PI * 2;   // -π ~ π
                float theta = (0.5 - uv.y) * UNITY_PI;       // -π/2 ~ π/2

                float3 dir;
                dir.x = cos(theta) * cos(phi);
                dir.y = sin(theta);
                dir.z = cos(theta) * sin(phi);

                return texCUBE(_Cube, normalize(dir));
            }
            ENDCG
        }
    }
}
