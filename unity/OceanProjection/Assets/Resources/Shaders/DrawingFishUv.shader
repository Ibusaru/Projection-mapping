Shader "OceanProjection/Drawing Fish UV"
{
    Properties
    {
        _BaseMap ("Drawing Texture", 2D) = "white" {}
        _DrawingTex ("Drawing Texture Compatibility", 2D) = "white" {}
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _Tint;
                float _AlphaClip;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 drawing = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, saturate(input.uv)) * _Tint;
                float drawingMax = max(drawing.r, max(drawing.g, drawing.b));
                float drawingMin = min(drawing.r, min(drawing.g, drawing.b));
                float saturation = drawingMax - drawingMin;
                float whiteBacking = saturate((drawingMax - 0.965) * 28.0) * saturate((0.04 - saturation) * 25.0);
                float paintAlpha = saturate((drawing.a - _AlphaClip) / max(1.0 - _AlphaClip, 0.0001)) * (1.0 - whiteBacking);

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
