Shader "Mocopi/Arena Electric Halo"
{
    SubShader
    {
        Tags { "Queue"="Transparent+11" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float _Activity, _Energy;
            float4 _Cyan, _Lavender;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; UNITY_TRANSFER_FOG(o,o.pos); return o; }
            float4 frag(v2f i):SV_Target
            {
                float r=length(i.uv), a=atan2(i.uv.y,i.uv.x);
                float aa=max(fwidth(r),0.002);
                float rim=1-smoothstep(0.003,0.003+aa,abs(r-0.97));
                float inner=1-smoothstep(0.002,0.002+aa,abs(r-0.715));
                float circuitRadius=0.935+sin(a*24)*0.006+sin(a*41+_Time.y*0.6)*0.003;
                float circuit=1-smoothstep(0.0015,0.0015+aa,abs(r-circuitRadius));
                float pulse=pow(0.5+0.5*cos(a*3-_Time.y*1.4),6);
                float spokes=(1-smoothstep(0.03,0.08,abs(sin(a*24))))*smoothstep(0.68,0.72,r)*(1-smoothstep(0.75,0.79,r));
                float wash=exp(-pow((r-0.86)/0.16,2))*0.045+exp(-r*r*4)*0.013;
                float outerGlow=exp(-abs(r-0.97)*85)*0.10;
                float3 tint=(0.72+0.28*cos(a+6.283185*float3(0,0.3333,0.6667)))*1.35;
                float light=wash+outerGlow+rim*0.40+inner*0.14+spokes*0.15+circuit*(0.12+pulse*0.38*_Activity);
                float4 result=float4(tint*light*(0.8+_Energy*0.25)*(1-smoothstep(0.985,1,r)),1);
                UNITY_APPLY_FOG_COLOR(i.fogCoord,result,float4(0,0,0,0)); return result;
            }
            ENDCG
        }
    }
}
