Shader "Custom/ReadyHandOrbGeometry"
{
    Properties
    {
        _Radius ("Radius", Float) = 0.11
        _Glow ("Glow", Float) = 2.2
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Radius;
            float _Glow;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
            };

            struct v2g
            {
                float3 worldPos : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2g vert(appdata v)
            {
                v2g o;
                o.worldPos = v.vertex.xyz;
                o.color = v.color;
                return o;
            }

            void AddVertex(inout TriangleStream<g2f> stream, float3 center, float3 offset, float2 uv, fixed4 color)
            {
                g2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(center + offset, 1.0));
                o.uv = uv;
                o.color = color;
                stream.Append(o);
            }

            [maxvertexcount(4)]
            void geom(point v2g input[1], inout TriangleStream<g2f> stream)
            {
                float3 cameraRight = UNITY_MATRIX_I_V._m00_m01_m02;
                float3 cameraUp = UNITY_MATRIX_I_V._m10_m11_m12;
                float3 right = normalize(cameraRight) * _Radius;
                float3 up = normalize(cameraUp) * _Radius;
                float3 center = input[0].worldPos;
                fixed4 color = input[0].color;

                AddVertex(stream, center, -right - up, float2(-1, -1), color);
                AddVertex(stream, center, -right + up, float2(-1, 1), color);
                AddVertex(stream, center, right - up, float2(1, -1), color);
                AddVertex(stream, center, right + up, float2(1, 1), color);
                stream.RestartStrip();
            }

            fixed4 frag(g2f i) : SV_Target
            {
                float r2 = dot(i.uv, i.uv);
                clip(1.0 - r2);

                float z = sqrt(saturate(1.0 - r2));
                float3 normal = normalize(float3(i.uv.x, i.uv.y, z));
                float lighting = saturate(normal.z * 0.7 + 0.3);
                float rim = pow(saturate(1.0 - normal.z), 2.0);
                float alpha = saturate((1.0 - r2) * 1.5) * i.color.a;
                float glow = 1.0 + rim * _Glow;

                fixed3 rgb = i.color.rgb * lighting * glow;
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
