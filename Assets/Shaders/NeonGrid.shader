Shader "Vanguard/NeonGrid"
{
    Properties
    {
        _BaseColor ("Floor Color", Color) = (0.03, 0.02, 0.07, 1)
        [HDR] _LineColor ("Grid Line Color", Color) = (0.15, 0.7, 1.4, 1)
        [HDR] _MajorLineColor ("Major Line Color", Color) = (0.9, 0.2, 1.6, 1)
        _GridSize ("Cell Size (world units)", Float) = 2
        _MajorEvery ("Major Line Every N Cells", Float) = 5
        _LineWidth ("Line Width (pixels)", Range(0.3, 4)) = 1.2
        _LineIntensity ("Line Intensity", Range(0, 3)) = 0.8
        _FadeCenter ("Fade Center (XZ)", Vector) = (0, 0, 0, 0)
        _FadeRadius ("Fade Radius", Float) = 22
        _PulseSpeed ("Pulse Speed", Float) = 1.2
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _LineColor;
            half4 _MajorLineColor;
            half _GridSize;
            half _MajorEvery;
            half _LineWidth;
            half _LineIntensity;
            float4 _FadeCenter;
            half _FadeRadius;
            half _PulseSpeed;
            half _PulseAmount;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            // Trả về độ phủ của đường lưới (0-1) với độ rộng tính theo pixel màn hình
            float GridLine(float2 coord, float widthPx)
            {
                float2 d = fwidth(coord);
                float2 g = abs(frac(coord - 0.5) - 0.5) / max(d, 1e-5);
                float l = min(g.x, g.y);
                return 1.0 - saturate(l / widthPx);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.positionWS.xz / max(_GridSize, 0.01);

                float minor = GridLine(uv, _LineWidth);
                float major = GridLine(uv / max(_MajorEvery, 1.0), _LineWidth * 1.6);

                // Mờ dần về rìa arena và nhấp nháy nhẹ
                float dist = length(IN.positionWS.xz - _FadeCenter.xz);
                float fade = saturate(1.0 - dist / max(_FadeRadius, 0.01));
                fade *= fade;
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed - dist * 0.25) * _PulseAmount;

                half3 emission = (_LineColor.rgb * minor + _MajorLineColor.rgb * major) * _LineIntensity * fade * pulse;

                half3 normalWS = normalize(IN.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half3 lit = _BaseColor.rgb * (SampleSH(normalWS) + mainLight.color * ndl * mainLight.shadowAttenuation);

                half3 color = lit + emission;
                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half frag(Varyings IN) : SV_Target { return IN.positionCS.z; }
            ENDHLSL
        }
    }
    FallBack Off
}
