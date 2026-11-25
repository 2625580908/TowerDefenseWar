Shader "MRStrategyRTS/Particles/SoftSmoke"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata
            {
                float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex=UnityObjectToClipPos(input.vertex);
                output.color=input.color;output.uv=input.uv;
                return output;
            }
            fixed4 frag(v2f input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half2 p=input.uv*2-1;
                half radius=length(p);
                half billow=.85h+.15h*sin(p.x*10+p.y*8);
                input.color.a*=pow(saturate(1-radius),1.35h)*billow;
                return input.color;
            }
            ENDCG
        }
    }
}
