// Rush Ahead's water. Two layers of soft noise drift in different directions; where they meet a thin light line
// shows, which reads as ripples from the chase camera's height, and their slopes tilt the surface so it reflects the
// sky (the reflection cubemap) more at a glancing angle than looking straight down. Small glints where the sun
// catches it. On a river (_RiverSpace 1, with UVs across and down the river, and the half width in the second UV)
// the whole pattern rushes downstream at _FlowSpeed, with long foam streaks along the current, and the middle runs
// deeper and darker than the edges. It takes the rafts' shadows and the fog.
// A river's water (_Holes 1) is cut away over its whirlpools (the global _RushWhirlpools: world x, z and radius), so
// the funnel meshes below show through; a funnel's water (_Swirl 1) turns about _SwirlCentre with three foam arms
// spiralling into a dark eye, lit by the funnel's own slope.
Shader "RushAhead/Water"
{
    Properties
    {
        [MainColor] _BaseColor("Colour", Color) = (0.08, 0.42, 0.55, 1)
        _DeepColor("Colour mid-river", Color) = (0.04, 0.24, 0.33, 1)
        _RippleColor("Ripple colour", Color) = (0.6, 0.84, 0.9, 1)
        _RippleScale("Ripple scale", Float) = 0.32
        _RippleSpeed("Ripple speed", Float) = 0.45
        _Glint("Glints", Range(0, 1)) = 0.55
        _Reflect("Sky reflection", Range(0, 1)) = 0.6
        _RiverSpace("River UVs", Float) = 0
        _FlowSpeed("Flow speed (m/s)", Float) = 0
        _Holes("Cut away whirlpools", Float) = 0
        _Swirl("Whirlpool funnel", Float) = 0
        _SwirlCentre("Funnel centre (world x, z)", Vector) = (0, 0, 0, 0)
        _SwirlRadius("Funnel radius", Float) = 5
        _SwirlSpin("Funnel turn (radians a second)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _DeepColor;
            half4 _RippleColor;
            float _RippleScale;
            float _RippleSpeed;
            half _Glint;
            half _Reflect;
            float _RiverSpace;
            float _FlowSpeed;
            float _Holes;
            float _Swirl;
            float4 _SwirlCentre;
            float _SwirlRadius;
            float _SwirlSpin;
        CBUFFER_END

        // The course's whirlpools (world x, z, radius), set by the course view: a river cuts itself away over them.
        float4 _RushWhirlpools[6];
        float _RushWhirlpoolCount;

        void CutWhirlpools(float3 positionWS)
        {
            if (_Holes < 0.5) return;
            for (int i = 0; i < 6; i++)
            {
                if (i >= (int)_RushWhirlpoolCount) break;
                float2 d = positionWS.xz - _RushWhirlpools[i].xy;
                clip(dot(d, d) - _RushWhirlpools[i].z * _RushWhirlpools[i].z);
            }
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fog : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float halfWidth : TEXCOORD3;
                float3 normalWS : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.fog = ComputeFogFactor(position.positionCS.z);
                output.uv = input.uv;
                output.halfWidth = input.uv2.x;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            // The two drifting ripple layers, summed: a height field for the surface.
            float Ripples(float2 uv, float t, out float a, out float b)
            {
                a = Noise(uv + float2(t, t * 0.6));
                b = Noise(uv * 1.7 - float2(t * 0.8, -t * 0.45));
                return a + b;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                CutWhirlpools(input.positionWS);

                // On a river, sample in river space and slide the whole pattern downstream at the water's speed.
                float2 place = lerp(input.positionWS.xz, input.uv - float2(0, _FlowSpeed * _Time.y), _RiverSpace);

                // In a funnel, sample a pattern turning about its middle and wound into a spiral; three foam arms
                // spiral into the dark eye and a white ring marks the rim.
                float eye = 0, arms = 0;
                float3 slope = float3(0, 1, 0);
                if (_Swirl > 0.5)
                {
                    float2 d = input.positionWS.xz - _SwirlCentre.xy;
                    float r = length(d);
                    float k = saturate(1.0 - r / _SwirlRadius);
                    float theta = _Time.y * _SwirlSpin - 1.8 * sign(_SwirlSpin) * log(max(r, 0.15) / _SwirlRadius); // arms trail the way it turns
                    float s, c;
                    sincos(theta, s, c);
                    place = float2(c * d.x - s * d.y, s * d.x + c * d.y);
                    float spiral = frac((atan2(d.y, d.x) + theta) * 3.0 / 6.2831853);
                    arms = smoothstep(0.16, 0.05, abs(spiral - 0.5)) * smoothstep(0.05, 0.3, k) * (1.0 - smoothstep(0.8, 0.97, k));
                    arms = max(arms, smoothstep(0.07, 0.0, abs(k - 0.04))); // the rim
                    eye = k * k;
                    slope = normalize(input.normalWS);
                }
                float2 uv = place * _RippleScale;
                float t = _Time.y * _RippleSpeed;

                float a, b, ignoredA, ignoredB;
                float h = Ripples(uv, t, a, b);
                float lines = smoothstep(0.07, 0.0, abs(a - b));

                // Long foam streaks down the current, more of them the faster it runs.
                float streak = Noise(float2(place.x * 1.1, place.y * 0.12));
                lines = max(lines, smoothstep(0.78, 0.9, streak) * saturate(_FlowSpeed * 0.25) * _RiverSpace);

                // The ripples' slopes tilt the surface.
                float hx = Ripples(uv + float2(0.06, 0), t, ignoredA, ignoredB);
                float hz = Ripples(uv + float2(0, 0.06), t, ignoredA, ignoredB);
                float3 normalWS = normalize(slope + float3((h - hx) * 1.6, 0, (h - hz) * 1.6));

                // Deeper and darker in the middle of a river.
                float middle = _RiverSpace * saturate(1.0 - abs(input.uv.x) / max(input.halfWidth, 0.5));
                half3 water = lerp(_BaseColor.rgb, _DeepColor.rgb, middle * middle);
                water = lerp(water, _DeepColor.rgb * 0.35, eye);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = light.shadowAttenuation;

                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 sky = GlossyEnvironmentReflection(reflect(-viewWS, normalWS), 0.08, 1.0);
                half fresnel = 0.04 + 0.96 * pow(1.0 - saturate(dot(normalWS, viewWS)), 5.0);

                // Glints: a finer, faster layer, bright only at its peaks.
                float g = Noise(uv * 8.0 + float2(t * 2.1, -t * 1.4));
                float glint = smoothstep(0.91, 0.985, g) * _Glint;

                half3 colour = water * lerp(0.7, 1.0, shadow) * (0.55 + 0.45 * saturate(dot(normalWS, light.direction)));
                colour = lerp(colour, sky, saturate(fresnel * _Reflect));
                colour = lerp(colour, _RippleColor.rgb, lines * 0.5);
                colour = lerp(colour, half3(0.93, 0.97, 1.0), arms * 0.8);
                colour += glint * light.color * shadow * 0.75;
                colour = MixFog(colour, input.fog);
                return half4(colour, 1);
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
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                CutWhirlpools(input.positionWS);
                return 0;
            }
            ENDHLSL
        }
    }
}
