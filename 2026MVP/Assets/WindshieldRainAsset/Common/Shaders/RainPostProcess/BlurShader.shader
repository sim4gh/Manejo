Shader "WindshieldRainAsset/RainPostProcess/BlurShader" {
    Properties{
        [HideInInspector]_MainTex("Texture", 2D) = "white" {}
        _Resolution("Resolution", vector) = (100, 100, 0, 0)
        _TexelSizeMultiplier("Texel Size Multiplier", Range(0, 1)) = 1 
        _BlurRadius("Blur Radius", Range(1, 10)) = 3
        _HeightThreshold("Height Threshold", Range(0, 1)) = 0
    }

        SubShader{
            Pass {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                sampler2D _MainTex;
                float _BlurRadius;
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

                float _HeightThreshold;

                float getHeight(float2 uv) {
                    return clamp((tex2D(_MainTex, uv).r - _HeightThreshold) / (1 - _HeightThreshold), 0, 1);
                }

                float2 _Resolution;
                float4 frag(v2f i) : SV_Target {
                    float2 texelSize = (1.0 / _Resolution.xy) * _TexelSizeMultiplier;

                    float blurColor = 0;
                    float blurWeight = 0.0;

                    for (int x = -3; x <= 3; x++) {
                        for (int y = -3; y <= 3; y++) {
                            float2 offset = float2(x, y) * texelSize;
                            float weight = exp(-(x * x + y * y) / (_BlurRadius * _BlurRadius));
                            blurColor += getHeight(i.uv + offset) * weight;
                            blurWeight += weight;
                        }
                    }

                    blurColor = blurColor / blurWeight;
                    return float4(blurColor, blurColor, blurColor, tex2D(_MainTex, i.uv).a);
                }
                ENDCG
            }
        }
            FallBack "Diffuse"
}