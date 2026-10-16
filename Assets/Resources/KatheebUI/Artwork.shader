Shader "UI/KatheebArtwork"
{
 Properties
 {
  [PerRendererData] _MainTex("Artwork",2D)="white"{}
  _Color("Tint",Color)=(1,1,1,1)
  _UVRect("Artwork region",Vector)=(0,0,1,1)
  _ChromaKey("Remove neutral checkerboard at render time",Float)=0
  _Roundness("Corner radius",Float)=0
  _Aspect("Region aspect",Float)=1
  _StencilComp("Stencil Comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil Operation",Float)=0
  _StencilWriteMask("Stencil Write Mask",Float)=255
  _StencilReadMask("Stencil Read Mask",Float)=255
  _ColorMask("Color Mask",Float)=15
  [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True"}
  Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
  Cull Off Lighting Off ZWrite Off ZTest Always
  Blend SrcAlpha OneMinusSrcAlpha
  ColorMask [_ColorMask]
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct v2f {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 world:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   sampler2D _MainTex;float4 _Color,_UVRect,_ClipRect;float _ChromaKey,_Roundness,_Aspect;
   v2f vert(appdata v)
   {
    v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;
   }
   fixed4 frag(v2f i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    fixed4 c=tex2D(_MainTex,i.uv)*i.color;
    if(_ChromaKey>0.5)
    {
     float hi=max(c.r,max(c.g,c.b)),lo=min(c.r,min(c.g,c.b));
     // Keep fine brown fronds and dark outlines; reject the neutral grey backdrop.
     float key=smoothstep(0.065,0.16,hi-lo)*smoothstep(0.025,0.10,c.r-c.b);
     key=max(key,1-smoothstep(0.08,0.16,hi));
     c.a*=smoothstep(0.18,0.65,key);
    }
    if(_Roundness>0)
    {
     float2 p=(i.uv-_UVRect.xy)/_UVRect.zw-0.5;p.x*=_Aspect;
     float2 q=abs(p)-float2(_Aspect*0.5-_Roundness,0.5-_Roundness);
     float d=length(max(q,0))+min(max(q.x,q.y),0)-_Roundness;
     c.a*=1-smoothstep(-0.004,0.004,d);
    }
    #ifdef UNITY_UI_CLIP_RECT
    c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(c.a-0.001);
    #endif
    return c;
   }
   ENDCG
  }
 }
}
