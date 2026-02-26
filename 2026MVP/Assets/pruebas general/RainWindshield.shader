Shader "Custom/HeartfeltRain"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TimeScale ("Time Scale", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _TimeScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float N(float t)
            {
                return frac(sin(t*12345.564)*7658.76);
            }

            float S(float a, float b, float t)
            {
                return smoothstep(a,b,t);
            }

            float rain(float2 uv, float time)
            {
                uv.y += time*0.5;
                float2 gv = frac(uv*10)-0.5;
                float2 id = floor(uv*10);
                float n = N(id.x + id.y*57.0);
                float drop = smoothstep(0.2,0.0,length(gv));
                return drop*n;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float time = _Time.y * _TimeScale;

                float r = rain(uv,time);

                float2 offset = float2(ddx(r), ddy(r))*0.1;

                float3 col = tex2D(_MainTex, uv+offset).rgb;

                col *= 1.0 - r*0.5;

                return float4(col,1);
            }
            ENDCG
        }
    }
}