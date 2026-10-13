Shader "Bang/WesternSky" {
 SubShader {
  Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
  Cull Off ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct v2f { float4 vertex:SV_POSITION; float3 direction:TEXCOORD0; };
   v2f vert(float4 vertex:POSITION) { v2f o;o.vertex=UnityObjectToClipPos(vertex);o.direction=vertex.xyz;return o; }
   fixed4 frag(v2f i):SV_Target {
    float3 d=normalize(i.direction);float3 sky=lerp(float3(.89,.66,.44),float3(.28,.47,.61),smoothstep(-.03,.55,d.y));
    float sun=smoothstep(.997,.999,dot(d,normalize(float3(-.65,.23,.7))));
    return fixed4(lerp(sky,float3(1,.88,.58),sun),1);
   }
   ENDCG
  }
 }
}
