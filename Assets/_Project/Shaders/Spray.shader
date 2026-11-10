// Rush Ahead's see-through bodies of moving water: a waterspout's column, standing waves, a cannon's jet, white water.
// Lit softly by the sun and sky so the shape reads (a cylinder looks round, a wave has a crest), denser at the rim
// the way a column of spray looks from the side, with streaks that run round it (_Spin, in turns a second about the
// object's up axis) and along it (_Rise, metres a second up the object's y, or along z for a jet with _AlongZ 1).
// Needs no texture coordinates, so it works on meshes built in code as well as on Unity's primitives.
Shader "RushAhead/Spray"
{
    Properties
    {
        [MainColor] _BaseColor("Colour (alpha: how solid)", Color) = (1, 1, 1, 0.7)
        _Rim("Denser at the rim", Range(0, 2)) = 0.8
        _Streaks("Streaks", Range(0, 1)) = 0.5
        _Bands("Streaks round", Float) = 7
        _Spacing("Metres between streaks along", Float) = 1.2
        _Twist("Twist (turns a metre)", Float) = 0.08
        _Spin("Spin (turns a second)", Float) = 0
        _Rise("Rise (metres a second)", Float) = 0
        _AlongZ("Streaks along z instead of y", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Rim;
                half _Streaks;
                float _Bands;
                float _Spacing;
                float _Twist;
                float _Spin;
                float _Rise;
                float _AlongZ;
            CBUFFER_END

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
                float3 positionOS : TEXCOORD2;
                float fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.fog = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light sun = GetMainLight();

                // Soft light wrapping round, plus the sky: water lit through, never black on its shaded side.
                half wrap = saturate(dot(n, sun.direction) * 0.5 + 0.5);
                half3 light = sun.color * (0.35 + 0.65 * wrap) * 0.75 + SampleSH(n) * 0.55;

                // Seen edge-on the spray is thicker.
                half facing = abs(dot(n, v));
                half rim = pow(1.0 - facing, 2.0);

                // Streaks: bands round the object's axis, twisting as they go, running round and along with time.
                float3 p = input.positionOS;
                float along = lerp(p.y, p.z, _AlongZ);
                float2 across = lerp(p.xz, p.xy, _AlongZ);
                float turn = atan2(across.y, across.x) / (2.0 * PI);
                float t = _Time.y;
                float ring = sin(2.0 * PI * (_Bands * (turn + _Twist * along - _Spin * t)));
                float lengthways = sin(2.0 * PI * (along - _Rise * t) / max(0.05, _Spacing));
                half streak = saturate(0.5 + 0.5 * ring * 0.7 + 0.5 * lengthways * 0.45);

                half3 colour = _BaseColor.rgb * light;
                colour = lerp(colour, colour * (0.8 + 0.45 * streak), _Streaks);
                colour += rim * 0.12;
                half alpha = _BaseColor.a * saturate(1.0 + _Rim * rim - 0.25 * _Rim);
                alpha *= lerp(1.0, 0.65 + 0.6 * streak, _Streaks);
                colour = MixFog(colour, input.fog);
                return half4(colour, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
