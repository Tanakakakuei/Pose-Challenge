Shader "Mocopi/Halloween Wisp"
{
    Properties { _Color ("Color", Color) = (0.55,0.24,0.8,0.12) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            float4 frag(v2f i):SV_Target { float r=length(i.uv*2-1); return float4(i.color.rgb,i.color.a*pow(saturate(1-r*r),3)); }
            ENDCG
        }
    }
}
