Shader "Vanguard/UICardGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _GlowColor ("Glow Color", Color) = (1,0.6,0,1)
        _FillColor ("Fill Color", Color) = (0.05,0.06,0.1,0.94)
        _Intensity ("Glow Intensity", Float) = 1
        _GlowRadius ("Glow Radius (px)", Float) = 18
        _FrameWidth ("Frame Width (px)", Float) = 3
        _Corner ("Corner Radius (px)", Float) = 10
        _PulseSpeed ("Pulse Speed", Float) = 2
        _PulseAmount ("Pulse Amount", Float) = 0.25
        _ShimmerSpeed ("Shimmer Speed", Float) = 0.5
        _ShimmerStrength ("Shimmer Strength", Float) = 0.3
        _Sparkle ("Sparkle", Float) = 0
        _Hover ("Hover (0-1)", Range(0,1)) = 0
        _Size ("Rect Size (px)", Vector) = (260,380,0,0)
        _Inset ("Glow Inset (px)", Float) = 20
        _UTime ("Unscaled Time", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            fixed4 _Color, _GlowColor, _FillColor;
            float _Intensity, _GlowRadius, _FrameWidth, _Corner, _PulseSpeed, _PulseAmount;
            float _ShimmerSpeed, _ShimmerStrength, _Sparkle, _Hover, _Inset, _UTime;
            float4 _Size;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float hash21 (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 size = _Size.xy;
                float2 p = (i.uv - 0.5) * size;
                float2 halfExt = size * 0.5 - _Inset;

                // SDF hộp bo góc của thân thẻ: d > 0 là bên ngoài (vùng glow), d < 0 là bên trong
                float2 q = abs(p) - halfExt + _Corner;
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _Corner;

                float pulse = 1.0 + sin(_UTime * _PulseSpeed) * _PulseAmount;
                float boost = 1.0 + _Hover * 0.9;

                // Outer glow
                float outer = pow(exp(-max(d, 0.0) / max(_GlowRadius, 0.001)), 1.4);
                outer *= smoothstep(-0.5, 0.5, d) * (1.0 - smoothstep(_Inset * 0.55, _Inset, d));
                float glowA = saturate(outer * _Intensity * pulse * boost);

                // Shimmer: dải sáng chéo chạy qua thẻ
                float s = frac((p.x + p.y) / (size.x + size.y) - _UTime * _ShimmerSpeed);
                float band = exp(-pow((s - 0.5) * 9.0, 2.0)) * _ShimmerStrength * (1.0 + _Hover);

                float fd = -d;
                float frameMask = step(0.0, fd) * (1.0 - smoothstep(_FrameWidth - 1.0, _FrameWidth, fd));

                float2 cell = floor(p / 5.0) + floor(_UTime * 7.0);
                float sparkle = step(0.9925, hash21(cell)) * _Sparkle * step(0.0, fd);

                float rim = exp(-max(fd, 0.0) / 16.0) * 0.28 * _Intensity * pulse * boost;

                float3 glowRGB = _GlowColor.rgb;
                float3 frameCol = glowRGB * (1.0 + _Hover * 0.2) + band * 0.9 + sparkle * 0.8;
                float3 fillCol = _FillColor.rgb + glowRGB * (rim + band * 0.12) + sparkle * glowRGB;
                float3 outerCol = glowRGB;

                float inside = step(0.0, fd);
                float3 rgb = lerp(outerCol, lerp(fillCol, frameCol, frameMask), inside);
                float a = lerp(glowA, lerp(_FillColor.a, 1.0, frameMask), inside);
                return fixed4(saturate(rgb), a * i.color.a);
            }
            ENDCG
        }
    }
}
