// Pixelated swirling-paint space backdrop, in the style of Balatro's background, in the card back's colours.
// The paint swirl is a spin-and-warp pattern mixed between three colours, drawn on a coarse pixel grid.
// Pixel stars twinkle on top and a faint galaxy glow sits in the swirl's centre. Dim darkens everything evenly
// (like a light black cover), so colours stay rich while the board and cards stand out.
// Used by SpaceBackground.cs. All settings are on the material (Resources/VFX/SpaceBackground).
Shader "Stratafield/Space Background"
{
    Properties
    {
        [Header(Colours)]
        _Colour1 ("Swirl Violet", Color) = (0.52, 0.22, 0.66, 1)
        _Colour2 ("Swirl Navy", Color) = (0.07, 0.13, 0.40, 1)
        _Colour3 ("Swirl Deep Space", Color) = (0.02, 0.02, 0.07, 1)
        _GlowColour ("Centre Glow", Color) = (0.95, 0.55, 0.85, 1)

        [Header(Look)]
        _PixelsAcross ("Pixels Across", Range(60, 1200)) = 420
        _ColourSteps ("Colour Steps (0 = smooth)", Range(0, 32)) = 14
        _Contrast ("Swirl Contrast", Range(1, 6)) = 3.2
        _SpinAmount ("Spin Amount", Range(0, 1)) = 0.3
        _Zoom ("Swirl Zoom", Range(5, 60)) = 26
        _GlowStrength ("Centre Glow", Range(0, 1)) = 0.35
        _StarDensity ("Star Density", Range(0, 2)) = 1
        _Dim ("Dim (black cover)", Range(0, 0.9)) = 0.35
        _Vignette ("Edge Darkening", Range(0, 1.5)) = 0.6

        [Header(Motion)]
        _SwirlSpeed ("Swirl Speed", Float) = 0.35
        _SpinSpeed ("Spin Speed", Float) = 0.04
        _TwinkleSpeed ("Twinkle Speed", Float) = 1.5

        [HideInInspector] _Aspect ("Aspect (set by script)", Float) = 1.7778
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SpaceBackground"
            Tags { "LightMode" = "UniversalForwardOnly" }
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Colour1, _Colour2, _Colour3, _GlowColour;
                float _PixelsAcross, _ColourSteps, _Contrast, _SpinAmount, _Zoom, _GlowStrength;
                float _StarDensity, _Dim, _Vignette;
                float _SwirlSpeed, _SpinSpeed, _TwinkleSpeed, _Aspect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // The paint swirl: spin the coordinates around the centre, then fold them through sines a few times.
            // How far the result ends up decides which of the three colours (and how much) shows
            float3 PaintSwirl(float2 p, float time)
            {
                float radius = length(p);
                float spin = time * _SpinSpeed;
                float angle = atan2(p.y, p.x) + spin - 20.0 * (_SpinAmount * radius + (1.0 - _SpinAmount));
                float2 uv = float2(radius * cos(angle), radius * sin(angle)) * _Zoom;

                float speed = time * _SwirlSpeed;
                float2 uv2 = float2(uv.x + uv.y, uv.x + uv.y);
                for (int i = 0; i < 5; i++)
                {
                    uv2 += sin(max(uv.x, uv.y)) + uv;
                    uv += 0.5 * float2(cos(5.1123314 + 0.353 * uv2.y + speed * 0.131121), sin(uv2.x - 0.113 * speed));
                    uv -= 1.0 * cos(uv.x + uv.y) - 1.0 * sin(uv.x * 0.711 - uv.y);
                }

                float contrastMod = 0.25 * _Contrast + 0.5 * _SpinAmount + 1.2;
                float paint = min(2.0, max(0.0, length(uv) * 0.035 * contrastMod));
                float c1 = max(0.0, 1.0 - contrastMod * abs(1.0 - paint));
                float c2 = max(0.0, 1.0 - contrastMod * abs(paint));
                float c3 = 1.0 - min(1.0, c1 + c2);

                float base = 0.3 / _Contrast;
                return base * _Colour1.rgb + (1.0 - base) * (_Colour1.rgb * c1 + _Colour2.rgb * c2 + _Colour3.rgb * c3);
            }

            // Stars that land exactly on the pixel grid. Bright ones get a little plus shape
            float Stars(float2 pixel, float time)
            {
                float seed = Hash(pixel);
                float twinkle = 0.5 + 0.5 * sin(time * _TwinkleSpeed * (0.5 + seed * 3.0) + seed * 60.0);
                twinkle = floor(twinkle * 3.0 + 0.5) / 3.0; // Stepped flicker suits the pixel look

                float light = 0.0;
                if (seed > 1.0 - 0.003 * _StarDensity) light = 0.6 + 0.4 * twinkle;

                // Plus-shaped bright stars: this pixel lights up if a neighbour is a bright star centre
                float2 offsets[4] = { float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1) };
                for (int i = 0; i < 4; i++)
                {
                    float neighbour = Hash(pixel + offsets[i]);
                    if (neighbour > 1.0 - 0.0006 * _StarDensity) light = max(light, 0.35 * (0.5 + 0.5 * twinkle));
                }
                if (seed > 1.0 - 0.0006 * _StarDensity) light = 1.0;
                return light;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y;

                // Snap to a coarse pixel grid (square pixels whatever the screen shape)
                float2 grid = float2(_PixelsAcross, _PixelsAcross / _Aspect);
                float2 pixel = floor(input.uv * grid);
                float2 uv = (pixel + 0.5) / grid;
                float2 p = (uv - 0.5) * float2(_Aspect, 1.0) / _Aspect;

                float3 colour = PaintSwirl(p, time);

                // Faint glow at the swirl's centre, like a distant galaxy core
                float glow = exp(-dot(p, p) * 40.0) * _GlowStrength;
                colour += _GlowColour.rgb * glow;

                // Limit the colour steps for a pixel-art palette feel
                if (_ColourSteps > 0.5) colour = floor(colour * _ColourSteps + 0.5) / _ColourSteps;

                colour += Stars(pixel, time) * float3(0.85, 0.85, 1.0) * 0.8;

                // Darker edges, then the black cover. Scaling all channels the same keeps the colours' saturation
                float edge = length((uv - 0.5) * float2(1.15, 1.35));
                colour *= 1.0 - _Vignette * smoothstep(0.4, 0.95, edge);
                colour *= 1.0 - _Dim;

                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }
}
