Shader "Vanguard/NeonLit"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (0.1, 0.06, 0.14, 1)
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0.8, 1, 1)
        _PulseSpeed ("Pulse Speed", Float) = 2
        _PulseAmount ("Pulse Amount (0-1)", Range(0, 1)) = 0.3

        [Header(Rim)]
        [HDR] _RimColor ("Rim Color", Color) = (0.6, 0.2, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3

        [Header(Hit Flash)]
        [HDR] _FlashColor ("Flash Color", Color) = (4, 4, 4, 1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0

        [Header(Dissolve)]
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color) = (0, 3, 4, 1)
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0.001, 0.3)) = 0.06
        _NoiseScale ("Noise Scale", Float) = 6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Giữ toàn bộ property trong 1 CBUFFER để tương thích SRP Batcher.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _PulseSpeed;
            half _PulseAmount;
            half4 _RimColor;
            half _RimPower;
            half4 _FlashColor;
            half _FlashAmount;
            half _DissolveAmount;
            half4 _DissolveEdgeColor;
            half _DissolveEdgeWidth;
            half _NoiseScale;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        float Hash31(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        // Value noise 3D trong không gian object, ổn định theo mesh
        float ValueNoise(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(
                lerp(lerp(Hash31(i + float3(0,0,0)), Hash31(i + float3(1,0,0)), f.x),
                     lerp(Hash31(i + float3(0,1,0)), Hash31(i + float3(1,1,0)), f.x), f.y),
                lerp(lerp(Hash31(i + float3(0,0,1)), Hash31(i + float3(1,0,1)), f.x),
                     lerp(Hash31(i + float3(0,1,1)), Hash31(i + float3(1,1,1)), f.x), f.y),
                f.z);
        }
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
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                #ifdef LIGHTMAP_ON
                float2 lightmapUV : TEXCOORD4;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                #ifdef LIGHTMAP_ON
                OUT.lightmapUV = IN.uv1 * unity_LightmapST.xy + unity_LightmapST.zw;
                #endif
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Dissolve: bỏ pixel có noise nhỏ hơn ngưỡng
                float noise = ValueNoise(IN.positionOS * _NoiseScale);
                float dissolveEdge = noise - _DissolveAmount;
                clip(dissolveEdge);

                half3 normalWS = normalize(IN.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction));
                // Vật tĩnh đã bake: lấy lightmap; vật động: Light Probe (SH) nội suy theo vị trí
                #if defined(LIGHTMAP_ON)
                half3 ambient = SampleLightmap(IN.lightmapUV, normalWS);
                #else
                half3 ambient = SampleSH(normalWS);
                #endif
                half3 lit = albedo * (mainLight.color * ndl * mainLight.shadowAttenuation + ambient);

                half pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                half3 emission = _EmissionColor.rgb * pulse;

                half rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                half3 rimCol = _RimColor.rgb * rim;

                half3 color = lit + emission + rimCol;

                // Viền sáng khi tan biến
                half edge = 1.0 - smoothstep(0.0, _DissolveEdgeWidth, dissolveEdge);
                color += _DissolveEdgeColor.rgb * edge * step(0.0001, _DissolveAmount);

                // Hit flash phủ lên cuối cùng
                color = lerp(color, _FlashColor.rgb, _FlashAmount);

                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = positionCS;
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                clip(ValueNoise(IN.positionOS * _NoiseScale) - _DissolveAmount);
                return 0;
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
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half frag(Varyings IN) : SV_Target
            {
                clip(ValueNoise(IN.positionOS * _NoiseScale) - _DissolveAmount);
                return IN.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vertMeta
            #pragma fragment fragMeta
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct AttributesMeta
            {
                float4 positionOS : POSITION;
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                float2 uv2        : TEXCOORD2;
            };

            struct VaryingsMeta
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            VaryingsMeta vertMeta(AttributesMeta IN)
            {
                VaryingsMeta OUT;
                OUT.positionCS = UnityMetaVertexPosition(IN.positionOS.xyz, IN.uv1, IN.uv2);
                OUT.uv = TRANSFORM_TEX(IN.uv0, _BaseMap);
                return OUT;
            }

            half4 fragMeta(VaryingsMeta IN) : SV_Target
            {
                UnityMetaInput meta = (UnityMetaInput)0;
                meta.Albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;
                meta.Emission = _EmissionColor.rgb;
                return UnityMetaFragment(meta);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
