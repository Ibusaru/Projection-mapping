Shader "OceanProjection/Stable Water"
{
    Properties
    {
        _BaseColor ("Deep Water", Color) = (0.05, 0.42, 0.62, 0.72)
        _ShallowColor ("Shallow Water", Color) = (0.18, 0.68, 0.76, 0.72)
        _Opacity ("Opacity", Range(0, 1)) = 0.94
        _Smoothness ("Smoothness", Range(0, 1)) = 0.78
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        // The player can cross below the raised surface, so render the same
        // authoritative plane from both above and below.
        Cull Off

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                half4 _BaseColor;
                half4 _ShallowColor;
                half _Opacity;
                half _Smoothness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                float phaseA = dot(p, normalize(float2(0.84, 0.54))) * 0.052 + _Time.y * 0.18;
                float phaseB = dot(p, normalize(float2(-0.42, 0.91))) * 0.081 - _Time.y * 0.12;
                float2 slope = float2(cos(phaseA) * 0.052, sin(phaseA) * 0.052)
                    + float2(cos(phaseB), sin(phaseB)) * 0.035;
                half3 normalWS = normalize(half3(-slope.x, 1.0, -slope.y));

                Light mainLight = GetMainLight();
                half diffuse = saturate(dot(normalWS, mainLight.direction)) * 0.22 + 0.78;
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specular = pow(saturate(dot(normalWS, halfDirection)), lerp(24.0h, 96.0h, _Smoothness));
                half viewBlend = saturate(1.0h - dot(normalWS, viewDirection));
                half3 color = lerp(_ShallowColor.rgb, _BaseColor.rgb, 0.42h + viewBlend * 0.38h);
                color = color * diffuse + specular * mainLight.color * 0.34h;
                return half4(color, saturate(_Opacity));
            }
            ENDHLSL
        }
    }
}
