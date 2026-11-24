// The Play -> Draft black hole (BlackHoleTransition.cs drives every value from code).
// Draws a picture of the screen (_MainTex: a snapshot of the menu, or the game's camera) spiralled toward a black
// hole in the middle, with a glowing gold accretion ring on the hole's edge, a faint glow round the screen edge while
// it's fully covered, a gold-white flash, and a spark at the very end. In Resources so it's always in builds
Shader "Stratafield/Black Hole"
{
    Properties
    {
        [PerRendererData] _MainTex ("Screen", 2D) = "black" {}
        _Hole ("Hole size (1 = reaches the corners)", Float) = 0
        _Pull ("Pull inward", Float) = 0
        _Twist ("Twist", Float) = 0
        _Darken ("Darken near the hole", Float) = 0
        _RingAlpha ("Ring", Float) = 0
        _Spin ("Ring spin", Float) = 0
        _EdgeGlow ("Screen edge glow", Float) = 0
        _Flash ("Flash", Float) = 0
        _Spark ("Spark", Float) = 0
        _Aspect ("Screen aspect (set by script)", Float) = 1.7778
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Opaque" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Hole, _Pull, _Twist, _Darken, _RingAlpha, _Spin, _EdgeGlow, _Flash, _Spark, _Aspect;

            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            static const float3 Gold = float3(1.0, 0.89, 0.6);
            static const float3 Sage = float3(0.73, 0.81, 0.78);
            static const float3 FlashColour = float3(1.0, 0.965, 0.87);
            static const float TwoPi = 6.2831853;

            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 aspect = float2(_Aspect, 1.0);
                float2 p = (input.uv - 0.5) * aspect;        // Screen point, centre = 0, height = 1
                float r = length(p);
                float corner = length(aspect * 0.5);         // Distance to a corner
                float hole = _Hole * corner;
                float angle = atan2(p.y, p.x);

                // The picture, spiralled inward toward the hole
                float3 colour = 0;
                if (r >= hole)
                {
                    float near = hole > 0.0001 ? saturate(1.0 - (r - hole) / (corner * 0.6)) : 0;
                    float sourceRadius = r * (1.0 + _Pull * 2.2 * (0.3 + near));
                    float sourceAngle = angle + _Twist * (near * near * 2.5 + _Pull * 0.5);
                    float2 sourceUV = float2(cos(sourceAngle), sin(sourceAngle)) * sourceRadius / aspect + 0.5;
                    bool inside = all(sourceUV >= 0.0) && all(sourceUV <= 1.0);
                    colour = inside ? tex2D(_MainTex, sourceUV).rgb : 0;
                    colour *= 1.0 - _Darken * near;
                }

                // Accretion ring: soft gold glow just outside the hole, with bright arcs spinning round it
                if (hole > 0.0001 && _RingAlpha > 0)
                {
                    float outside = r - hole;
                    float glow = saturate(1.0 - outside / (hole * 0.45 + 0.03));
                    float3 ring = lerp(Sage * 0.35, Gold, glow) * glow * glow * step(0, outside);
                    float band = 1.0 - saturate(abs(r - hole * 1.06) / (hole * 0.012 + 0.003));
                    float arcs = step(frac((angle - _Spin) / TwoPi * 6.0), 0.3);
                    colour += (ring + Gold * band * arcs * 1.2) * _RingAlpha;
                }

                // While the screen is fully covered: the ring's glow showing at the screen edge, slowly turning
                float edge = smoothstep(corner * 0.78, corner * 1.02, r);
                float edgeArcs = 0.7 + 0.3 * step(frac((angle - _Spin * 0.3) / TwoPi * 4.0), 0.25);
                colour += Gold * edge * edgeArcs * _EdgeGlow * 0.25;

                // Spark as the hole pinches shut
                colour += FlashColour * exp(-r * r / (0.0004 + 0.0025 * _Spark)) * _Spark;

                colour = lerp(colour, FlashColour, saturate(_Flash));
                return fixed4(colour, 1);
            }
            ENDCG
        }
    }
}
