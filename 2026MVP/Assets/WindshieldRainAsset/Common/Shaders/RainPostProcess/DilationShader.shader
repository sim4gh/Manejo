Shader "WindshieldRainAsset/RainPostProcess/DilationShader" {
    Properties{
        [HideInInspector] _MainTex("Texture", 2D) = "white" {}
        _Resolution("Resolution", vector) = (100, 100, 0, 0)
        _TexelSizeMultiplier("Texel Size Multiplier", Range(0, 1)) = 1 
        _BlurRadius("Blur Radius", Range(1, 10)) = 3
        _Strength("Strength", Range(0, 1)) = 0.1
        _HeightThreshold("Height Threshold", Range(0, 1)) = 0
    }

        SubShader{
            Pass {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                sampler2D _MainTex;
                float _BlurRadius, _Strength;
                float _TexelSizeMultiplier;

                struct appdata {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2f {
                    float2 uv : TEXCOORD0;
                    float4 vertex : SV_POSITION;
                };

                v2f vert(appdata v) {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    return o;
                }

                float2 _Resolution;
                float _HeightThreshold;
                float4 frag(v2f i) : SV_Target {
                    float2 texelSize = (1.0 / _Resolution.xy) * _TexelSizeMultiplier;

                    float blurWeight = 0.0;
                    float blurColor = 0;

                    float reduce_amount = _HeightThreshold;
                    for (int x = -3; x <= 3; x++) {
                        for (int y = -3; y <= 3; y++) {
                            float2 offset = float2(x, y) * texelSize;
                            float weight = exp(-(x * x + y * y) / (_BlurRadius * _BlurRadius));
                            float col = tex2D(_MainTex, i.uv + offset).x * weight;
                            col = clamp((col - reduce_amount) / (1 - reduce_amount), 0, 1);
                            if (col >= 1) return float4(1, 1, 1, 1);
                            blurColor += col;
                            blurWeight += weight;
                        }
                    }

                    blurColor = min(1, blurColor / (blurWeight * (1.0 - _Strength)));
                    return float4(blurColor, blurColor, blurColor, tex2D(_MainTex, i.uv).r);
                }
                ENDCG
            }
        }
            FallBack "Diffuse"
}