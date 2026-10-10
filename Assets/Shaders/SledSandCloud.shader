Shader "Desert/Sled Sand Cloud"
{
 Properties { _BaseColor ("Sand tint", Color) = (1,1,1,1) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float fog:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
   float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
   float noise(float2 p)
   {
    float2 i=floor(p), f=frac(p); f=f*f*(3.0-2.0*f);
    return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
   }
   Varyings vert(Attributes v)
   {
    Varyings o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
    o.uv=v.uv; o.color=v.color;
    o.fog=ComputeFogFactor(o.positionCS.z);
    return o;
   }
   half4 frag(Varyings i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 p=i.uv*2-1;
    float density=noise(i.uv*5)*0.65+noise(i.uv*11)*0.25+noise(i.uv*23)*0.10;
    float edge=1-smoothstep(0.25,1.0,length(p));
    half alpha=edge*edge*(0.45+density*0.55)*i.color.a*_BaseColor.a;
    Light sun=GetMainLight();
    half3 illumination=clamp(SampleSH(half3(0,1,0))+sun.color*0.55,0.45,1.25);
    half3 rgb=i.color.rgb*_BaseColor.rgb*illumination;
    return half4(MixFog(rgb,i.fog),alpha);
   }
   ENDHLSL
  }
 }
}
