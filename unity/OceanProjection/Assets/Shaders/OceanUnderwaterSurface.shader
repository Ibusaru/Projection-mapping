Shader "OceanProjection/Underwater Surface Cue"
{
    Properties
    {
        _Tint ("Tropical Water Tint", Color) = (0.025, 0.48, 0.68, 0.86)
        _WaterLevel ("Water Level", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "UnderwaterSurfaceCue"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _WaterLevel;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Above-water cameras must not see this underside-only cue.
                clip(_WaterLevel - GetCameraPositionWS().y - 0.05);

                float time = _Time.y;
                float rippleA = 0.5 + 0.5 * sin(input.positionWS.x * 0.56 + input.positionWS.z * 0.23 + time * 2.15);
                float rippleB = 0.5 + 0.5 * cos(input.positionWS.x * 0.19 - input.positionWS.z * 0.48 - time * 1.54);
                float rippleC = 0.5 + 0.5 * sin((input.positionWS.x + input.positionWS.z) * 0.31 + time * 1.08);
                // Intersecting animated bands read as moving sunlight through
                // a shallow tropical water surface rather than a flat tint.
                float caustic = saturate(0.16 + pow(rippleA * rippleB, 2.25) * 0.94 + rippleC * 0.14);
                float viewDepth = saturate((_WaterLevel - GetCameraPositionWS().y) * 0.16);
                half3 deepColor = lerp(half3(0.015h, 0.16h, 0.28h), _Tint.rgb * 0.72h, viewDepth);
                half3 color = lerp(deepColor, half3(0.16h, 0.82h, 0.94h), caustic);
                color += pow(caustic, 5.0) * half3(0.28h, 0.42h, 0.44h);
                half alpha = saturate(_Tint.a * (0.44h + caustic * 0.46h));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
