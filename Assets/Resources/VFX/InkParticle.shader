// Ink toon particles for the Inked Star Chart look. Turns any soft white-on-black (or white-with-alpha) shape into a
// flat cartoon blob with a dark ink edge and a flat shade band, like a hand-drawn effect.
// - The particle's colour tints it.
// - The particle's alpha is how much of the shape is left: 1 = whole, 0 = eaten away. Fading alpha over a particle's
//   life makes it erode from the thin edges inward, in stepped frames like hand-drawn animation (Erosion Steps).
// - Glow > 0 adds a bright core on top (for magic, light and moon effects); use the additive version for pure glow.
// Used by InkFx.cs, which makes the materials in code.
Shader "Stratafield/Ink Particle"
{
    Properties
    {
        _MainTex ("Shape (white = solid)", 2D) = "white" {}
        _InkColor ("Ink", Color) = (0.063, 0.078, 0.094, 1)
        _InkWidth ("Ink Width", Range(0, 0.5)) = 0.1
        _Softness ("Edge Softness", Range(0.001, 0.1)) = 0.015
        _Shade ("Shade Band Darkness", Range(0, 1)) = 0.3
        _ShadeWidth ("Shade Band Width", Range(0, 0.6)) = 0.18
        _Steps ("Erosion Steps (0 = smooth)", Float) = 6
        _Glow ("Glow Core", Range(0, 3)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "InkParticle"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _InkColor;
                half _InkWidth;
                half _Softness;
                half _Shade;
                half _ShadeWidth;
                half _Steps;
                half _Glow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // Works for white-on-black shapes (brightness) and white shapes with alpha alike
                half shape = max(texel.r, max(texel.g, texel.b)) * texel.a;

                // How much of the shape is left, snapped to steps so it erodes in frames
                half left = saturate(input.color.a);
                if (_Steps > 0.5) left = ceil(left * _Steps) / _Steps;
                half threshold = lerp(0.97, 0.03, left);

                half inside = smoothstep(threshold, threshold + _Softness, shape);
                half coreStart = threshold + _InkWidth;
                half core = smoothstep(coreStart, coreStart + _Softness, shape);
                half lit = smoothstep(coreStart + _ShadeWidth, coreStart + _ShadeWidth + _Softness, shape);

                half3 fill = input.color.rgb;
                half3 body = lerp(fill * (1.0 - _Shade), fill, lit);   // Flat shade band, then the flat fill
                half3 colour = lerp(_InkColor.rgb, body, core);       // Ink band at the edge
                colour += fill * _Glow * lit;                          // Optional bright core

                return half4(colour, inside);
            }
            ENDHLSL
        }
    }
}
