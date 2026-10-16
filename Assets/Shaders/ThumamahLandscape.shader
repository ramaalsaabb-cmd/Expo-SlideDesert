Shader "Desert/Thumamah Landscape"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _HazeColor ("Distant desert haze", Color) = (0.74,0.70,0.62,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ThumamahForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _HazeColor;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float fog : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = normalize(input.normalWS);
                Light sun = GetMainLight();
                half3 lighting = max(SampleSH(normal), half3(0.06,0.055,0.05))
                    + sun.color * saturate(dot(normal, sun.direction)) * 0.78;
                // Subtle horizontal limestone strata; no expensive textures or transparency.
                float band = sin(input.positionWS.y * 1.25 + sin(input.positionWS.x * 0.025) * 0.8);
                half strata = lerp(1.0h, 0.88h + 0.09h * band, input.color.a);
                half3 rgb = input.color.rgb * _BaseColor.rgb * lighting * strata;
                float haze = smoothstep(350.0, 1100.0, distance(GetCameraPositionWS(), input.positionWS)) * 0.38;
                half3 hazeLighting = max(SampleSH(half3(0,1,0)), half3(0.06,0.055,0.05)) + sun.color * 0.12;
                rgb = lerp(rgb, _HazeColor.rgb * hazeLighting, haze);
                return half4(MixFog(rgb, input.fog), 1);
            }
            ENDHLSL
        }
    }
}

