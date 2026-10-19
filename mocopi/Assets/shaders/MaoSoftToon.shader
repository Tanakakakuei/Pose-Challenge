Shader "Mocopi/Mao Soft Toon"
{
    Properties
    {
        [MainTexture] _MainTex ("Base texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)
        [Header(Soft cel shading)]
        _ShadeColor ("Lavender shadow tint", Color) = (0.78,0.74,0.91,1)
        _ShadeThreshold ("Shadow threshold", Range(0,1)) = 0.52
        _ShadeSoftness ("Shadow softness", Range(0.01,0.4)) = 0.075
        _ShadowStrength ("Cast shadow strength", Range(0,1)) = 0.65
        _FaceLight ("Face light fill", Range(0,1)) = 0
        _LightInfluence ("Scene light color", Range(0,1)) = 0.2
        [Header(Silky highlight)]
        _SpecColor ("Highlight tint", Color) = (0.86,0.94,1,1)
        _SpecStrength ("Highlight strength", Range(0,0.5)) = 0.1
        _SpecPower ("Highlight focus", Range(8,128)) = 36
        [Header(Soft rim)]
        _RimColor ("Rim tint", Color) = (1,0.84,0.92,1)
        _RimStrength ("Rim strength", Range(0,0.5)) = 0.12
        _RimPower ("Rim focus", Range(1,8)) = 3.5
        [Header(Fine colored outline)]
        _OutlineColor ("Outline tint", Color) = (0.29,0.24,0.4,1)
        _OutlineWidth ("Outline pixels (0 disables)", Range(0,3)) = 0.85
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 normalVS = mul((float3x3)UNITY_MATRIX_V, UnityObjectToWorldNormal(v.normal));
                float2 direction = mul((float2x2)UNITY_MATRIX_P, normalVS.xy);
                direction /= max(length(direction), 0.0001);
                // Pixel width stays delicate at both portrait and full-body distances.
                o.pos.xy += direction * (2.0 * _OutlineWidth / _ScreenParams.xy) * o.pos.w;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                clip(_OutlineWidth - 0.001);
                fixed3 textureTint = tex2D(_MainTex, i.uv).rgb;
                fixed4 color = fixed4(_OutlineColor.rgb * lerp(0.7, 1.0, textureTint), 1);
                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            Cull Back
            ZWrite On
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color, _ShadeColor, _RimColor;
            float _ShadeThreshold, _ShadeSoftness, _ShadowStrength, _FaceLight, _LightInfluence;
            float _SpecStrength, _SpecPower, _RimStrength, _RimPower;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 normal : TEXCOORD2;
                SHADOW_COORDS(3)
                UNITY_FOG_COORDS(4)
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 l = normalize(UnityWorldSpaceLightDir(i.worldPos));
                float3 v = normalize(UnityWorldSpaceViewDir(i.worldPos));
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);
                float halfLambert = dot(n,l) * 0.5 + 0.5;
                float band = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, halfLambert);
                band *= lerp(1.0, attenuation, _ShadowStrength);
                band = lerp(band, 1.0, _FaceLight);
                fixed3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                fixed3 color = albedo * lerp(_ShadeColor.rgb, fixed3(1,1,1), band);
                // A filled illustration look keeps the face readable even in backlight.
                color *= lerp(fixed3(1,1,1), max(_LightColor0.rgb, fixed3(0.65,0.65,0.65)), _LightInfluence);
                float specular = pow(saturate(dot(n, normalize(l + v))), _SpecPower);
                specular = smoothstep(0.35, 0.65, specular) * _SpecStrength * band;
                color += _SpecColor.rgb * specular;
                float rim = pow(1.0 - saturate(dot(n,v)), _RimPower) * _RimStrength;
                color = lerp(color, max(color, _RimColor.rgb), rim);
                fixed4 result = fixed4(color, 1);
                UNITY_APPLY_FOG(i.fogCoord, result);
                return result;
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    FallBack "Diffuse"
}
