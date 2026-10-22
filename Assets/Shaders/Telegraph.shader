Shader "Vanguard/Telegraph"
{
    // Vùng cảnh báo phẳng cho TelegraphWarning (Sprint 8 - 8.4).
    // Mesh dùng toạ độ đã chuẩn hoá (đĩa/quạt bán kính 1 hướng +Z, vệt rộng 1 từ x=-0.5..0.5 và dài 1 từ z=0..1),
    // nên mọi hiệu ứng tính theo vị trí object-space, không cần UV.
    Properties
    {
        [HDR] _BaseColor ("Color", Color) = (1, 0.2, 0.05, 0.6)
        _Layer ("Layer (0 = nền, 1 = nạp đầy)", Float) = 0
        _Shape ("Shape (0 = tròn, 1 = quạt, 2 = vệt)", Float) = 0
        _HalfAngle ("Half Angle (rad, quạt)", Float) = 3.14159
        _Progress ("Progress", Range(0, 1)) = 0
        _EdgeWidth ("Edge Width", Range(0.005, 0.3)) = 0.06
        _EdgeBoost ("Edge Brightness", Float) = 2.5
        _StripeScale ("Stripe Scale", Float) = 10
        _StripeSpeed ("Stripe Speed", Float) = 0.6
        _StripeStrength ("Stripe Strength", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Telegraph"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Layer;
                half _Shape;
                half _HalfAngle;
                half _Progress;
                half _EdgeWidth;
                half _EdgeBoost;
                half _StripeScale;
                half _StripeSpeed;
                half _StripeStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 p : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.p = IN.positionOS.xz;
                return OUT;
            }

            // Độ gần mép (0 = xa mép, 1 = sát mép) theo từng hình dạng
            half EdgeFactor(float2 p)
            {
                half w = _EdgeWidth;
                if (_Shape > 1.5)
                {
                    half ex = smoothstep(0.5 - w * 0.5, 0.5, abs(p.x));
                    half ez = smoothstep(1.0 - w, 1.0, p.y);
                    return max(ex, ez);
                }

                half r = length(p);
                half edge = smoothstep(1.0 - w, 1.0, r);
                if (_Shape > 0.5 && _HalfAngle < 3.0)
                {
                    half angle = abs(atan2(p.x, p.y));
                    half side = 1.0 - smoothstep(0.0, w, r * (_HalfAngle - angle));
                    edge = max(edge, side * step(angle, _HalfAngle + 0.05));
                }
                return saturate(edge);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half edge = EdgeFactor(IN.p);

                // Sọc cảnh báo chéo chạy chậm
                half stripes = step(0.5, frac((IN.p.x + IN.p.y) * _StripeScale - _Time.y * _StripeSpeed));

                half intensity;
                half alpha;
                if (_Layer < 0.5)
                {
                    // Khung nền: mờ, viền sáng rõ
                    intensity = 0.35 + stripes * 0.12 + edge * _EdgeBoost;
                    alpha = (0.35 + edge * 0.65) * _BaseColor.a;
                }
                else
                {
                    // Lớp nạp đầy: đặc hơn, nhấp nháy nhanh dần khi sắp nổ
                    half flicker = 1.0 + 0.4 * _Progress * sin(_Time.y * (8.0 + 28.0 * _Progress));
                    intensity = (0.8 + stripes * _StripeStrength + edge * _EdgeBoost) * flicker;
                    alpha = (0.7 + edge * 0.3) * _BaseColor.a;
                }

                return half4(_BaseColor.rgb * intensity, saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
