Shader "Bang/Particle" {
 Properties { _MainTex("Texture",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _SoftRound("Soft default particles",Float)=0 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Blend SrcAlpha OneMinusSrcAlpha Cull Off Lighting Off ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _MainTex_ST;fixed4 _Color;float _SoftRound;
   struct appdata { float4 vertex:POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0; };
   struct v2f { float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0; };
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o;}
   fixed4 frag(v2f i):SV_Target { fixed4 color=tex2D(_MainTex,i.uv)*i.color;color.a*=lerp(1,1-smoothstep(.15,.5,length(i.uv-.5)),_SoftRound);return color; }
   ENDCG
  }
 }
}
