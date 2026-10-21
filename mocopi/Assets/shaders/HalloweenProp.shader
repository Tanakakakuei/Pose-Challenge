Shader "Mocopi/Halloween Prop"
{
    Properties
    {
        _Color ("Color", Color) = (0.8,0.22,0.03,1)
        [HDR] _Emission ("Warm glow", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float4 _Color, _Emission;
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; UNITY_FOG_COORDS(2) };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz; UNITY_TRANSFER_FOG(o,o.pos); return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal), view=normalize(_WorldSpaceCameraPos-i.world);
                float light=0.32+0.68*saturate(dot(n,normalize(float3(-0.4,0.8,-0.6))));
                float rim=pow(1-abs(dot(n,view)),3);
                float3 color=_Color.rgb*light+_Emission.rgb+rim*float3(0.1,0.035,0.16);
                float4 c=float4(color,1); UNITY_APPLY_FOG(i.fogCoord,c); return c;
            }
            ENDCG
        }
    }
}
