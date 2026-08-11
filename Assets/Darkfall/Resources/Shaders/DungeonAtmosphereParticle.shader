Shader "Darkfall/Dungeon Atmosphere Particle"
{
    Properties
    {
        _MainTex ("Soft smoke", 2D) = "white" {}
        _Tint ("Atmosphere tint", Color) = (1,1,1,1)
        _PlayerPosition ("Player world position", Vector) = (0,0,0,0)
        _ClearInner ("Clear pocket inner radius", Float) = 0.32
        _ClearOuter ("Clear pocket outer radius", Float) = 1.55
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "DungeonAtmosphere"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float2 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _Tint;
            float4 _PlayerPosition;
            float _ClearInner;
            float _ClearOuter;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color * _Tint;
                output.positionWS = positionWS.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 smoke = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // A softly evacuated pocket follows the actor, while the world-space particles
                // keep their own positions. This is dispersal, not a fog texture parented to him.
                float clear = smoothstep(_ClearInner, _ClearOuter,
                    distance(input.positionWS, _PlayerPosition.xy));
                // Slow opacity advection breaks the last screen-overlay impression without
                // translating the field. Particle noise still drives the physical cloud motion.
                float time = _Time.y;
                float flowA = sin(input.positionWS.x * 1.37 + time * .21 +
                    sin(input.positionWS.y * 1.81 - time * .13));
                float flowB = sin(input.positionWS.y * 2.23 - time * .16 +
                    sin(input.positionWS.x * .91 + time * .09));
                float volume = lerp(.62, 1.18, saturate(flowA * .27 + flowB * .23 + .5));
                half4 result = smoke * input.color;
                result.a *= clear * volume;
                return result;
            }
            ENDHLSL
        }
    }
}
