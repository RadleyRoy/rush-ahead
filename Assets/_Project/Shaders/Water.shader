// Rush Ahead's water: the bright blue of the reference, with light ripples drifting across it and small glints
// where the sun catches it. Two layers of soft noise move in different directions; where they meet, a thin light
// line shows, which reads as ripples from the chase camera's height. It still takes the rafts' shadows and the fog.
Shader "RushAhead/Water"
{
    Properties
    {
        [MainColor] _BaseColor("Colour", Color) = (0.13, 0.66, 0.95, 1)
        _RippleColor("Ripple colour", Color) = (0.62, 0.9, 1, 1)
        _RippleScale("Ripple scale", Float) = 0.32
        _RippleSpeed("Ripple speed", Float) = 0.45
        _Glint("Glints", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _RippleColor;
            float _RippleScale;
            float _RippleSpeed;
            half _Glint;
        CBUFFER_END
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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fog : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.fog = ComputeFogFactor(position.positionCS.z);
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

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionWS.xz * _RippleScale;
                float t = _Time.y * _RippleSpeed;

                // Two drifting layers; a thin line where they meet.
                float a = Noise(uv + float2(t, t * 0.6));
                float b = Noise(uv * 1.7 - float2(t * 0.8, -t * 0.45));
                float lines = smoothstep(0.07, 0.0, abs(a - b));

                // Glints: a finer, faster layer, bright only at its peaks.
                float g = Noise(uv * 8.0 + float2(t * 2.1, -t * 1.4));
                float glint = smoothstep(0.91, 0.985, g) * _Glint;

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = light.shadowAttenuation;

                half3 colour = lerp(_BaseColor.rgb, _RippleColor.rgb, lines * 0.5);
                colour *= lerp(0.7, 1.0, shadow);
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

            float4 DepthVert(DepthAttributes input) : SV_POSITION
            {
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
