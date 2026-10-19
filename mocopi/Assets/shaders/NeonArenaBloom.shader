Shader "Hidden/Mocopi/Neon Arena Bloom"
{
    Properties { _MainTex("Source",2D)="white"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _BloomTex;
        float4 _MainTex_TexelSize;
        float2 _BlurDirection;
        float _Threshold, _Intensity;
        float4 extract(v2f_img i):SV_Target
        {
            float3 c=tex2D(_MainTex,i.uv).rgb;
            float brightness=max(c.r,max(c.g,c.b));
            return float4(c*max(brightness-_Threshold,0)/max(brightness,0.0001),1);
        }
        float4 blur(v2f_img i):SV_Target
        {
            float2 d=_MainTex_TexelSize.xy*_BlurDirection;
            return tex2D(_MainTex,i.uv)*0.227027+
                (tex2D(_MainTex,i.uv+d*1.384615)+tex2D(_MainTex,i.uv-d*1.384615))*0.316216+
                (tex2D(_MainTex,i.uv+d*3.230769)+tex2D(_MainTex,i.uv-d*3.230769))*0.070270;
        }
        float4 composite(v2f_img i):SV_Target
        {
            float4 c=tex2D(_MainTex,i.uv);
            c.rgb+=tex2D(_BloomTex,i.uv).rgb*_Intensity;
            return c;
        }
        ENDCG
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment extract
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blur
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment composite
            ENDCG }
    }
    Fallback Off
}
