Shader "Hidden/UVSpaceShaderBaker"
{
    Properties {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata
            {
                float2 uv    : TEXCOORD0;
                float4 color : COLOR; // pre-computed lightMult in .r
            };
            struct v2f
            {
                float4 pos       : SV_POSITION;
                float  lightMult : TEXCOORD0;
            };
            float4 _BaseColor;
            v2f vert(appdata v)
            {
                v2f o;
                // Map UV [0,1] to clip space [-1,1]
                float2 clipXY = v.uv * 2.0 - 1.0;
                #if !UNITY_UV_STARTS_AT_TOP
                    clipXY.y = -clipXY.y;
                #endif
                o.pos       = float4(clipXY, 0.0, 1.0);
                o.lightMult = v.color.r;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                return _BaseColor * i.lightMult;
            }
            ENDHLSL
        }
    }
}
