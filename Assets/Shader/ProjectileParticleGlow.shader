Shader "MRStrategyRTS/Particles/ProjectileGlow"
{
    Properties
    {
        _TintColor ("Tint", Color) = (1,1,1,1)
        _Ring ("Ring Shape", Range(0,1)) = 0
        _Arc ("Electric Fork Shape", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _TintColor;
            half _Ring;
            half _Arc;
            struct appdata
            {
                float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 position : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _TintColor;
                output.uv = input.uv;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half radius = length(input.uv * 2 - 1);
                half glow = pow(saturate(1 - radius), 1.6h);
                half ring = saturate(1 - abs(radius - 0.66h) / 0.12h);
                half2 p=input.uv;
                half boltX=.5h+.13h*sin(p.y*18)+.055h*sin(p.y*43);
                half bolt=saturate(1-abs(p.x-boltX)/.038h);
                half fork=saturate(1-abs(p.x-(.5h+(p.y-.35h)*.6h))/.025h)
                    *step(.35h,p.y)*step(p.y,.82h);
                half arc=max(bolt,fork)*saturate(min(p.y,1-p.y)*12);
                input.color.a *= lerp(lerp(glow, ring, _Ring),arc,_Arc);
                return input.color;
            }
            ENDCG
        }
    }
}
