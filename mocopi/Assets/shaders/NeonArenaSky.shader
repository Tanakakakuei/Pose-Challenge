Shader "Mocopi/Neon Arena Sky"
{
    Properties { _Color ("Deep space", Color)=(0.002,0.008,0.02,1) }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            struct v2f { float4 pos:SV_POSITION; float3 dir:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.dir=v.vertex.xyz; return o; }
            float hash(float3 p) { p=frac(p*0.3183099+float3(0.13,0.27,0.41)); p*=17; return frac(p.x*p.y*p.z*(p.x+p.y+p.z)); }
            float noise(float3 p)
            {
                float3 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);
            }
            float4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.dir);
                float horizon=pow(saturate(1-abs(d.y)*1.3),4);
                float cloud=noise(d*5)+0.5*noise(d*13+7);
                float veil=smoothstep(0.6,1.35,cloud)*horizon;
                float3 c=_Color.rgb+float3(0.005,0.04,0.07)*horizon;
                c+=lerp(float3(0.004,0.045,0.06),float3(0.026,0.01,0.06),saturate(d.x+0.5))*veil;
                float3 cell=d*280;
                float star=step(0.9978,hash(floor(cell)))*pow(saturate(1-length(frac(cell)-0.5)*2),5);
                c+=star*float3(0.45,0.7,0.9)*smoothstep(-0.05,0.2,d.y);
                return float4(c,1);
            }
            ENDCG
        }
    }
}
