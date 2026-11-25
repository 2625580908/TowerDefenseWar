Shader "MRStrategyRTS/Feedback/DamageRedVignette"
{
    Properties
    {
        _Opacity ("Flash opacity", Range(0,1)) = 0
        _Color ("Damage color", Color) = (0.85,0.015,0.01,1)
    }
    SubShader
    {
        Tags { "Queue"="Overlay+100" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 position : SV_POSITION;
                float2 screen : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            fixed4 _Color;
            half _Opacity;
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = float4(input.vertex.xy, UNITY_NEAR_CLIP_VALUE, 1);
                output.screen = input.vertex.xy;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half edge = smoothstep(0.22h, 1.15h, length(input.screen));
                return fixed4(_Color.rgb, _Opacity * lerp(0.12h, 1.0h, edge));
            }
            ENDCG
        }
    }
}
