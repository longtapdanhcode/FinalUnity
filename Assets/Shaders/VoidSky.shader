Shader "Vanguard/VoidSky"
{
    Properties
    {
        [HDR] _ZenithColor ("Zenith Color", Color) = (0.02, 0.01, 0.06, 1)
        [HDR] _HorizonColor ("Horizon Color", Color) = (0.12, 0.04, 0.25, 1)
        [HDR] _GroundColor ("Below Horizon Color", Color) = (0.01, 0.005, 0.03, 1)
        _HorizonSharpness ("Horizon Falloff", Range(0.2, 3)) = 0.7

        [Header(Nebula)]
        [HDR] _NebulaColorA ("Nebula Color A", Color) = (0.55, 0.1, 1.2, 1)
        [HDR] _NebulaColorB ("Nebula Color B", Color) = (0.05, 0.9, 1.2, 1)
        _NebulaScale ("Nebula Scale", Range(0.5, 8)) = 2.2
        _NebulaIntensity ("Nebula Intensity", Range(0, 3)) = 0.9

        [Header(Stars)]
        _StarDensity ("Star Grid Density", Range(20, 300)) = 120
        _StarThreshold ("Star Rarity (higher = fewer)", Range(0.9, 0.9995)) = 0.985
        _StarBrightness ("Star Brightness", Range(0, 8)) = 3

        [Header(Motion)]
        _RotationSpeed ("Rotation Speed (deg/sec)", Float) = 1.5
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "VoidSky"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half _HorizonSharpness;
                half4 _NebulaColorA;
                half4 _NebulaColorB;
                half _NebulaScale;
                half _NebulaIntensity;
                half _StarDensity;
                half _StarThreshold;
                half _StarBrightness;
                half _RotationSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            float VoidHash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float VoidNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(VoidHash(i + float3(0,0,0)), VoidHash(i + float3(1,0,0)), f.x),
                         lerp(VoidHash(i + float3(0,1,0)), VoidHash(i + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(VoidHash(i + float3(0,0,1)), VoidHash(i + float3(1,0,1)), f.x),
                         lerp(VoidHash(i + float3(0,1,1)), VoidHash(i + float3(1,1,1)), f.x), f.y),
                    f.z);
            }

            float VoidFbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += a * VoidNoise(p);
                    p *= 2.03;
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dir);

                // Xoay chậm quanh trục Y để bầu trời có chuyển động nhẹ
                float angle = radians(_RotationSpeed) * _Time.y;
                float s = sin(angle);
                float c = cos(angle);
                float3 rdir = float3(c * dir.x - s * dir.z, dir.y, s * dir.x + c * dir.z);

                // Gradient nền
                float h = rdir.y;
                float up = pow(saturate(h), _HorizonSharpness);
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, up);
                sky = lerp(sky, _GroundColor.rgb, saturate(-h * 4.0));

                // Tinh vân
                float n1 = VoidFbm(rdir * _NebulaScale);
                float n2 = VoidFbm(rdir * _NebulaScale * 1.7 + 11.3);
                float mask = smoothstep(0.42, 0.85, n1);
                half3 nebula = lerp(_NebulaColorA.rgb, _NebulaColorB.rgb, n2) * mask * _NebulaIntensity;
                nebula *= saturate(h * 1.5 + 0.6);

                // Sao
                float3 p = rdir * _StarDensity;
                float3 cell = floor(p);
                float r = VoidHash(cell);
                float3 f = frac(p) - 0.5;
                float starMask = step(_StarThreshold, r) * smoothstep(0.35, 0.0, length(f));
                half3 stars = starMask * _StarBrightness * lerp(half3(0.7, 0.8, 1.0), half3(1.0, 0.8, 0.9), frac(r * 37.0));
                stars *= saturate(h * 3.0 + 0.5);

                return half4(sky + nebula + stars, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
