Shader "UI/KatheebDesertButton"
{
 Properties
 {
  [PerRendererData] _MainTex("Texture",2D)="white"{}
  _Color("Tint",Color)=(1,1,1,1)
  _StencilComp("Stencil Comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil Operation",Float)=0
  _StencilWriteMask("Stencil Write Mask",Float)=255
  _StencilReadMask("Stencil Read Mask",Float)=255
  _ColorMask("Color Mask",Float)=15
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True"}
  Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
  Cull Off Lighting Off ZWrite Off ZTest Always
  Blend SrcAlpha OneMinusSrcAlpha
  ColorMask [_ColorMask]
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct v2f {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 world:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   float4 _Color,_ClipRect;
   v2f vert(appdata v)
   {
    v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;
   }
   fixed4 frag(v2f i):SV_Target
   {
    float2 p=(i.uv-.5)*float2(4.80769,1);
    float2 q=abs(p)-float2(1.953845,.05);
    float d=length(max(q,0))+min(max(q.x,q.y),0)-.43;
    float aa=max(fwidth(d),.002);
    float alpha=1-smoothstep(-aa,aa,d);
    float inner=smoothstep(-.055,-.035,d);
    float3 bronze=lerp(float3(.13,.045,.012),float3(.32,.15,.045),i.uv.y);
    bronze+=float3(.13,.065,.015)*pow(saturate(1-abs(i.uv.y-.68)*2),5);
    // Layered sand silhouettes echo the dunes inside the logo.
    float duneA=.07+max(0,.28-abs(i.uv.x-.14)*2.2);
    float duneB=.035+max(0,.22-abs(i.uv.x-.25)*1.5);
    float dunes=(1-smoothstep(duneA-.01,duneA+.01,i.uv.y))*step(i.uv.x,.34);
    bronze=lerp(bronze,float3(.64,.30,.065),dunes*.75);
    float ridge=(1-smoothstep(duneB-.008,duneB+.008,i.uv.y))*step(i.uv.x,.38);
    bronze=lerp(bronze,float3(.87,.48,.12),ridge*.65);
    float3 gold=lerp(float3(.57,.27,.045),float3(1,.86,.48),i.uv.y);
    gold=lerp(gold,float3(1,.93,.69),pow(saturate(1-abs(i.uv.y-.86)*7),5)*.65);
    float3 color=lerp(bronze,gold,inner);
    float bevel=(1-smoothstep(.008,.024,abs(d+.065)))*.4;
    color+=bevel*float3(.5,.27,.06);
    fixed4 result=fixed4(color,alpha)*i.color;
    #ifdef UNITY_UI_CLIP_RECT
    result.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
    #endif
    return result;
   }
   ENDCG
  }
 }
}
