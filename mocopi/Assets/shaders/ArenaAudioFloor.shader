Shader "Mocopi/Arena Audio Floor"
{
    Properties { _SpectrumHistory("Spectrum", 2D)="black" {} }
    SubShader
    {
        Tags { "Queue"="Transparent+12" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _SpectrumHistory;
            float _Activity, _MaxHeight, _TileOpacity;
            float4 _Cyan, _Lavender;
            struct input { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 data:TEXCOORD1; float up:TEXCOORD2; UNITY_FOG_COORDS(3) };
            v2f vert(input v)
            {
                v2f o;
                float level=tex2Dlod(_SpectrumHistory,float4(v.color.rg,0,0)).r*_Activity;
                float height=0.012+pow(saturate(level),0.85)*_MaxHeight;
                v.vertex.y=0.01+v.vertex.y*height;
                o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
                o.data=float3(level,v.color.b,v.vertex.y/height); o.up=saturate(v.normal.y);
                UNITY_TRANSFER_FOG(o,o.pos); return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float edge=1-smoothstep(0.025,0.07,min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y)));
                float3 pastel=0.72+0.28*cos(6.283185*(i.data.y+float3(0,0.3333,0.6667)));
                float3 light=pastel*1.5;
                float glow=0.4+i.data.x*0.32+edge*0.45+i.up*0.1;
                float opacity=_TileOpacity*(0.5+edge*0.8+i.up*0.3);
                float4 result=float4(light*glow,opacity);
                UNITY_APPLY_FOG(i.fogCoord,result); return result;
            }
            ENDCG
        }
    }
}
