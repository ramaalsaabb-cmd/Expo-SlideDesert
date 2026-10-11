Shader "Desert/Braided Tow Rope"
{
 Properties
 {
  _BaseColor ("Red strands",Color)=(0.8,0.025,0.02,1)
  _StripeColor ("White strands",Color)=(0.95,0.95,0.92,1)
  _StripeFrequency ("Stripe cycles per meter",Float)=3
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;
   half4 _StripeColor;
   float _StripeFrequency;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; float2 uv:TEXCOORD2; float fog:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
   Varyings vert(Attributes v)
   {
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
    o.positionCS=TransformWorldToHClip(o.positionWS);
    o.normalWS=TransformObjectToWorldNormal(v.normalOS);
    o.uv=v.uv; o.fog=ComputeFogFactor(o.positionCS.z);
    return o;
   }
   half4 frag(Varyings i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half3 n=normalize(i.normalWS);
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    float strand=sin((i.uv.x*3+i.uv.y*16)*6.283185);
    float braid=sin((i.uv.x*6-i.uv.y*12)*6.283185);
    // Fade fine strands with screen footprint to avoid shimmering in the headset.
    float detail=1-saturate(fwidth(i.uv.y)*40);
    half fiber=1+(strand*0.09+braid*0.06)*detail;
    float phase=(i.uv.y*_StripeFrequency+i.uv.x)*6.283185;
    float footprint=max(fwidth(phase),0.01);
    float stripe=smoothstep(-footprint,footprint,sin(phase));
    stripe=lerp(stripe,0.5,saturate(footprint/6.283185));
    half3 ropeColor=lerp(_BaseColor.rgb,_StripeColor.rgb,stripe);
    half3 illumination=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
    return half4(MixFog(ropeColor*fiber*illumination,i.fog),1);
   }
   ENDHLSL
  }
 }
}
