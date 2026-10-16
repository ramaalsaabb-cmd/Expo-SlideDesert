Shader "Desert/Soft Cloud Sunset"
{
 Properties
 {
  _Tex ("Original sunset", Cube) = "grey" {}
  _Tint ("Sky tint", Color) = (.43,.49,.50,.5)
  _Exposure ("Exposure", Range(0,8)) = .48
  _Rotation ("Rotation", Range(0,360)) = 0
  _SunDirection ("Sunset direction", Vector) = (0,.12,1,0)
  _CloudTint ("Sunset orange cloud tint", Color) = (1,.78,.57,1)
  _CloudWhitening ("Cloud whitening", Range(0,1)) = .85
  _CloudBrightness ("Cloud softness", Range(0,1)) = .23
 }
 SubShader
 {
  Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
  Cull Off ZWrite Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   samplerCUBE _Tex; half4 _SunDirection; half4 _Tex_HDR;half4 _Tint,_CloudTint;float _Exposure,_Rotation,_CloudWhitening,_CloudBrightness;
   struct appdata { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 vertex:SV_POSITION;float3 direction:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
   v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);float angle=radians(_Rotation);float s=sin(angle),c=cos(angle);o.direction=float3(c*v.vertex.x-s*v.vertex.z,v.vertex.y,s*v.vertex.x+c*v.vertex.z);return o;}
   half4 frag(v2f i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 direction=normalize(i.direction);half3 original=DecodeHDR(texCUBE(_Tex,direction),_Tex_HDR);
    // One restrained dusk palette across the full dome, with no baked halo or ground band.
    float height=smoothstep(-.15,.85,direction.y);
    half3 sky=lerp(half3(.10,.16,.26),half3(.055,.10,.20),height);
    float maximum=max(max(original.r,original.g),original.b);
    float warmth=(original.r-max(original.g,original.b))/max(.001,maximum);
    float detail=smoothstep(.035,.18,warmth);
    float sourceLight=dot(original,half3(.2126,.7152,.0722));
    // Keep only faint cloud texture; suppress the panorama's sun glow and horizon strip.
    float cloudMask=detail*smoothstep(.10,.30,direction.y)*(1-smoothstep(.45,.75,sourceLight));
    sky=lerp(sky,half3(.19,.23,.30),cloudMask*.24);
    // Low amber sun, with a compact soft edge rather than a broad sky halo.
    float sunDistance=length(direction-normalize(_SunDirection.xyz));
    float sunDisk=1-smoothstep(.019,.023,sunDistance);
    float sunGlow=exp(-sunDistance*sunDistance/0.0018)*.055;
    sky+=half3(1,.36,.065)*sunGlow;
    sky=lerp(sky,half3(1,.46,.12),sunDisk);
    sky*=(_Exposure/.48);
    return half4(sky,1);
   }
   ENDCG
  }
 }
 Fallback Off
}










