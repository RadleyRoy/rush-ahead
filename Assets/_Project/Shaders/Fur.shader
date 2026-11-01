// Rush Ahead's soft fur, for the sculpted animals and creatures (see Sculpture.cs). The mesh's vertex colours say where
// each colour goes (fur r, pale fur g, dark b, skin a), so one sculpted mesh serves every fur colour a player picks.
// Velvet: light wraps a little round the form, a sheen glows along the edges facing away from you (the way fur catches
// the light), fine noise breaks up the surface like tufts, and the dark parts (nose, mask) are glossier. Cheap enough
// for phones: one pass, no fins or shells.
Shader "RushAhead/Fur"
{
    Properties
    {
        _FurColor("Fur", Color) = (0.6, 0.45, 0.3, 1)
        _PaleColor("Pale fur", Color) = (0.9, 0.85, 0.75, 1)
        _DarkColor("Dark (nose, mask)", Color) = (0.08, 0.07, 0.07, 1)
        _SkinColor("Skin", Color) = (1, 0.62, 0.7, 1)
        _Sheen("Sheen", Range(0, 2)) = 0.7
        _Tufts("Tufts per metre", Float) = 90
        _Smoothness("Smoothness", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _FurColor;
            half4 _PaleColor;
            half4 _DarkColor;
            half4 _SkinColor;
            half _Sheen;
            float _Tufts;
            half _Smoothness;
        CBUFFER_END

        float Hash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        // Smooth value noise, 0 to 1.
        float Noise(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 colour : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                half4 regions : TEXCOORD3;
                half fog : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.regions = input.colour;
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 r = input.regions / max(dot(input.regions, half4(1, 1, 1, 1)), 0.001);
                half3 colour = _FurColor.rgb * r.r + _PaleColor.rgb * r.g + _DarkColor.rgb * r.b + _SkinColor.rgb * r.a;

                // Tufts: fine noise in the object's own space (so it moves with the animal) shading the colour and tipping
                // the normal a touch; furry parts only.
                float3 p = input.positionOS * _Tufts;
                float n = Noise(p) * 0.65 + Noise(p * 2.7) * 0.35;
                half furry = r.r + r.g;
                colour *= lerp(1.0, 0.8 + 0.35 * n, furry);
                float3 normalWS = normalize(input.normalWS);
                float3 jitter = float3(Noise(p + 17.1), Noise(p + 41.7), Noise(p + 73.3)) - 0.5;
                normalWS = normalize(normalWS + jitter * 0.35 * furry);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fog;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = colour;
                surface.smoothness = lerp(_Smoothness, 0.75, r.b) + 0.2 * r.a;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = lerp(1.0, 0.75 + 0.25 * n, furry);
                surface.alpha = 1;
                half4 lit = UniversalFragmentPBR(inputData, surface);

                // Velvet sheen: fur glows along the rim, more where the light reaches, a little even in shade.
                Light light = GetMainLight(inputData.shadowCoord);
                half rim = pow(1.0 - saturate(dot(normalWS, inputData.viewDirectionWS)), 3.0);
                half wrap = saturate(dot(normalWS, light.direction) * 0.5 + 0.5) * light.shadowAttenuation;
                lit.rgb += colour * rim * _Sheen * furry * (light.color * wrap + inputData.bakedGI * 0.6);

                lit.rgb = MixFog(lit.rgb, input.fog);
                return half4(lit.rgb, 1);
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
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(ShadowAttributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
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
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target
            {
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
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(DepthAttributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

            struct NormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct NormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            NormalsVaryings NormalsVert(NormalsAttributes input)
            {
                NormalsVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFrag(NormalsVaryings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0);
            }
            ENDHLSL
        }
    }
}
