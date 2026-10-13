Shader "Bang/Toon" {
 Properties { _Color("Color", Color)=(1,1,1,1) _MainTex("Palette",2D)="white"{} _Outline("Outline",Float)=0.006 }
 SubShader {
  Tags { "RenderType"="Opaque" }
  Pass {
   Cull Front
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float _Outline;
   struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
   float4 vert(appdata v):SV_POSITION { v.vertex.xyz+=v.normal*_Outline;return UnityObjectToClipPos(v.vertex); }
   fixed4 frag():SV_Target { return fixed4(0.035,0.05,0.065,1); }
   ENDCG
  }
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Color;sampler2D _MainTex;float4 _MainTex_ST;
   struct appdata { float4 vertex:POSITION; float3 normal:NORMAL;float2 uv:TEXCOORD0; };
   struct v2f { float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;float2 uv:TEXCOORD1; };
   v2f vert(appdata v) { v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o; }
   fixed4 frag(v2f i):SV_Target { float light=dot(normalize(i.normal),normalize(float3(-.35,.8,-.5)));float shade=light>.45?1:light>-.1?.76:.48;return fixed4(_Color.rgb*tex2D(_MainTex,i.uv).rgb*shade,1); }
   ENDCG
  }
 }
 Fallback "Diffuse"
}
