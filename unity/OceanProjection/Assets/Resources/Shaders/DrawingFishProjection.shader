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
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
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

                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float edgeLight = saturate(abs(dot(normalize(input.normalWS), viewDirection)));
                half4 color = half4(lerp(_BaseColor.rgb, drawing.rgb, paintAlpha), _BaseColor.a);
                color.rgb *= lerp(0.68, 1.08, edgeLight);
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
