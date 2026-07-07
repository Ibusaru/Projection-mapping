Shader "OceanProjection/Drawing Fish Projection"
{
    Properties
    {
        _DrawingTex ("Drawing Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _AlphaClip ("Alpha Clip", Range(0, 1)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_DrawingTex);
            SAMPLER(sampler_DrawingTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _Tint;
                float _AlphaClip;
                float4x4 _DrawingWorldToProjector;
                float4 _DrawingProjectorOrigin;
                float4 _DrawingProjectorU;
                float4 _DrawingProjectorV;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.shadowCoord = GetShadowCoord(positionInputs);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 projectorPosition = mul(_DrawingWorldToProjector, float4(input.positionWS, 1.0)).xyz;
                float3 relative = projectorPosition - _DrawingProjectorOrigin.xyz;
                float uLengthSq = max(dot(_DrawingProjectorU.xyz, _DrawingProjectorU.xyz), 0.000001);
                float vLengthSq = max(dot(_DrawingProjectorV.xyz, _DrawingProjectorV.xyz), 0.000001);
                float2 rawUv = float2(
                    dot(relative, _DrawingProjectorU.xyz) / uLengthSq,
                    dot(relative, _DrawingProjectorV.xyz) / vLengthSq
                );

                float insideProjection =
                    step(0.0, rawUv.x) * step(rawUv.x, 1.0) *
                    step(0.0, rawUv.y) * step(rawUv.y, 1.0);
                float2 uv = saturate(rawUv);

                half4 drawing = SAMPLE_TEXTURE2D(_DrawingTex, sampler_DrawingTex, uv) * _Tint;
                float paintAlpha = saturate((drawing.a - _AlphaClip) / max(1.0 - _AlphaClip, 0.0001)) * insideProjection;

                half4 color = half4(lerp(_BaseColor.rgb, drawing.rgb, paintAlpha), _BaseColor.a);
                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                half mainLightAmount = saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation;
                half3 ambient = SampleSH(normalWS) * 0.55;
                half3 directional = mainLight.color * (0.24 + mainLightAmount * 0.76);
                color.rgb = saturate(color.rgb * (ambient + directional + 0.08));
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 lightDirectionWS = _LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    lightDirectionWS = normalize(_LightPosition - positionWS);
                #endif

                output.positionHCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    output.positionHCS.z = min(output.positionHCS.z, output.positionHCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionHCS.z = max(output.positionHCS.z, output.positionHCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
