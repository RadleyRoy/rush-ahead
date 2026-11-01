// Rush Ahead's photo-textured surfaces for things built in code, which have no texture coordinates worth using:
// cliffs, the gorge floor, rocks, timber, metal. The texture is projected from three sides and blended by which way
// the surface faces (triplanar), at a fixed number of metres per tile, with a normal map for the bumps.
// _BaseColor tints it as an overlay: mid grey (0.5) leaves the photo as it is, warmer or darker shifts it.
// In world space by default (still things); _ObjectSpace 1 projects in the object's own frame so the texture moves
// with it (the ferry, the sweeper's log), in metres whatever the object's scale.
Shader "RushAhead/Triplanar"
{
    Properties
    {
        [MainTexture] _BaseMap("Colour", 2D) = "white" {}
        [MainColor] _BaseColor("Tint (0.5 = as photographed)", Color) = (0.5, 0.5, 0.5, 1)
        [Normal] _BumpMap("Normal map", 2D) = "bump" {}
        _BumpScale("Bumps", Float) = 1
        _Smoothness("Smoothness", Range(0, 1)) = 0.15
        _Tile("Metres per tile", Float) = 3
        _ObjectSpace("Move with the object", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float4 _BumpMap_ST;
            half _BumpScale;
            half _Smoothness;
            float _Tile;
            float _ObjectSpace;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        // Where to project from: world position, or the position in the object's own (unscaled) frame.
        float3 ProjectionPoint(float3 positionWS)
        {
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            float3x3 m = (float3x3)GetObjectToWorldMatrix();
            float3 x = normalize(m._m00_m10_m20), y = normalize(m._m01_m11_m21), z = normalize(m._m02_m12_m22);
            float3 d = positionWS - origin;
            float3 local = float3(dot(d, x), dot(d, y), dot(d, z));
            return lerp(positionWS, local, _ObjectSpace);
        }

        float3 ProjectionNormal(float3 normalWS)
        {
            float3x3 m = (float3x3)GetObjectToWorldMatrix();
            float3 x = normalize(m._m00_m10_m20), y = normalize(m._m01_m11_m21), z = normalize(m._m02_m12_m22);
            float3 local = float3(dot(normalWS, x), dot(normalWS, y), dot(normalWS, z));
            return normalize(lerp(normalWS, local, _ObjectSpace));
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
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
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = normalize(input.normalWS);
                float3 p = ProjectionPoint(input.positionWS) / max(_Tile, 0.01);
                float3 n = ProjectionNormal(normalWS);

                // How much each of the three projections counts: sharply by which way the surface faces.
                float3 w = pow(abs(n), 4);
                w /= (w.x + w.y + w.z);

                float2 uvX = p.zy, uvY = p.xz, uvZ = p.xy;
                half3 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX).rgb * w.x
                             + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY).rgb * w.y
                             + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ).rgb * w.z;
                colour *= _BaseColor.rgb * 2.0;

                // Normal maps per projection, swizzled into the projection's axes and blended (whiteout blend).
                half3 tX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX), _BumpScale);
                half3 tY = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY), _BumpScale);
                half3 tZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ), _BumpScale);
                tX = half3(tX.xy + n.zy, abs(tX.z) * n.x);
                tY = half3(tY.xy + n.xz, abs(tY.z) * n.y);
                tZ = half3(tZ.xy + n.xy, abs(tZ.z) * n.z);
                float3 bumped = normalize(tX.zyx * w.x + tY.xzy * w.y + tZ.xyz * w.z);
                // Back from the projection's frame to the world when projecting in the object's frame.
                float3x3 m = (float3x3)GetObjectToWorldMatrix();
                float3 ox = normalize(m._m00_m10_m20), oy = normalize(m._m01_m11_m21), oz = normalize(m._m02_m12_m22);
                float3 bumpedWS = normalize(lerp(bumped, bumped.x * ox + bumped.y * oy + bumped.z * oz, _ObjectSpace));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = bumpedWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fog;
                inputData.bakedGI = SampleSH(bumpedWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = colour;
                surface.metallic = 0;
                surface.specular = half3(0, 0, 0);
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 lit = UniversalFragmentPBR(inputData, surface);
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
