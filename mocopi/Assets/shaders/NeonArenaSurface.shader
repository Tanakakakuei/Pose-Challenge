Shader "Mocopi/Neon Arena Surface"
{
    Properties
    {
        _Color ("Surface tint", Color) = (0.012,0.027,0.048,1)
        [HDR] _NeonColor ("Neon", Color) = (0.1,2,3,1)
        _Mode ("0 metal / 1 floor / 2 wall / 3 light", Float) = 0
        _Glow ("Glow", Range(0,5)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float4 _Color, _NeonColor;
            float _Mode, _Glow;
            struct v2f { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; UNITY_FOG_COORDS(3) };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal); o.uv=v.texcoord;
                UNITY_TRANSFER_FOG(o,o.pos); return o;
            }
            float grid(float2 p, float width)
            {
                float2 d=abs(frac(p-0.5)-0.5);
                float2 aa=max(fwidth(p),0.001);
                float2 gridLine=1-smoothstep(width,width+aa,d);
                return max(gridLine.x,gridLine.y);
            }
            float4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal), view=normalize(_WorldSpaceCameraPos-i.world);
                float rim=pow(1-saturate(dot(n,view)),3);
                float3 c=_Color.rgb*(0.7+0.3*abs(n.y))+_NeonColor.rgb*rim*0.018;
                if(_Mode>2.5)
                    c=_NeonColor.rgb*_Glow*(0.96+0.04*sin(_Time.y*0.7+i.world.z*0.08));
                else if(_Mode>1.5)
                {
                    float edge=1-smoothstep(0.015,0.055,min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y)));
                    float scan=pow(saturate(1-abs(frac(i.world.y*0.18-_Time.y*0.07)-0.5)*2),18);
                    // Neutral white faces, soft side shading and a restrained cell seam keep holes legible.
                    float face=0.78+0.22*abs(dot(n,normalize(UnityObjectToWorldNormal(float3(0,0,1)))));
                    c=_Color.rgb*face*(1-edge*0.10)+_NeonColor.rgb*(edge*0.12+rim*0.12+scan*0.055)*_Glow;
                }
                else if(_Mode>0.5)
                {
                    float distanceFade=1-smoothstep(12,65,distance(i.world,_WorldSpaceCameraPos));
                    float g=grid(i.world.xz*0.5,0.004);
                    float lane=1-smoothstep(0.018,0.055,abs(abs(i.world.x)-4.8));
                    float wash=exp(-abs(abs(i.world.x)-4.8)*2.5)*0.08;
                    float pulse=pow(0.5+0.5*sin(i.world.z*0.7-_Time.y*0.65),16);
                    c+=_NeonColor.rgb*(g*0.045*distanceFade+lane*0.55+wash+pulse*lane*0.12)*_Glow;
                    c+=float3(0.03,0.012,0.055)*pow(saturate(1-abs(i.world.x)/10),2);
                }
                float4 result=float4(c,1); UNITY_APPLY_FOG(i.fogCoord,result); return result;
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    FallBack "Diffuse"
}
