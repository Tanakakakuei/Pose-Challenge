Shader "Mocopi/Arena Speed Lines"
{
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
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
            float _Travel, _Activity, _Energy, _Radius;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; UNITY_TRANSFER_FOG(o,o.pos); return o; }
            float4 frag(v2f i):SV_Target
            {
                // Keep the original narrow, independently staggered floor streaks;
                // UV.x now follows the tunnel's inner perimeter instead of the floor.
                // Wider spacing and longer gaps leave roughly half as many visible lights.
                float lane=floor(i.uv.x/0.8);
                float seed=frac(sin(lane*127.1+3.7)*43758.5453);
                float center=(lane+0.5)*0.8+(seed-0.5)*0.18;
                float x=abs(i.uv.x-center), aa=max(fwidth(i.uv.x),0.003);
                float core=1-smoothstep(0.012,0.012+aa,x);
                float glow=exp(-x*24)*0.2;
                float period=24+seed*18;
                // Slow / cruising / overtaking lanes, with stable per-lane variation.
                // Multiply accumulated travel, not clock time, so play/menu transitions stay continuous.
                float speedClass=floor(seed*3);
                float speed=lerp(0.5,lerp(0.85,1.35,step(1.5,speedClass)),step(0.5,speedClass));
                float variation=frac(sin(lane*63.7+8.1)*19417.3);
                speed*=lerp(0.88,1.12,variation);
                // Neighbouring triples enter as a staggered wave. A few lanes carry a soft doublet.
                float group=floor(lane/3);
                float groupSeed=frac(sin(group*91.7+5.2)*31471.7);
                float phase=i.uv.y+_Travel*speed+groupSeed*period+fmod(lane,3)*4;
                float distanceBehind=frac(phase/period)*period;
                float trail=exp(-distanceBehind/(1.2+seed*2.2))*(1-smoothstep(5,9,distanceBehind));
                float secondDistance=frac((phase-6)/period)*period;
                float doublet=exp(-secondDistance/1.2)*(1-smoothstep(3,5,secondDistance));
                trail+=doublet*step(0.82,variation)*0.65;
                float fade=smoothstep(-12,-7,i.uv.y)*(1-smoothstep(50,72,i.uv.y));
                float3 color=lerp(float3(0.05,2.2,3.6),float3(1.7,0.15,3.2),step(0.78,seed));
                float4 result=float4(color*(core+glow)*trail*fade*(0.35+_Activity*0.65+_Energy*0.15),1);
                UNITY_APPLY_FOG_COLOR(i.fogCoord,result,float4(0,0,0,0)); return result;
            }
            ENDCG
        }
    }
}
