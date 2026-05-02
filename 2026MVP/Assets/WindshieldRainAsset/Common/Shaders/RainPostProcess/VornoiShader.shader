Shader "WindshieldRainAsset/RainPostProcess/MaskShader" {
    Properties{
        [HideInInspector] _MainTex("Texture", 2D) = "white" {}
        _MaskTex("Mask Texture", 2D) = "white" {}
        _Offset("Offset", Range(-1, 1)) = 0.1
        _Scale("Scale", Range(1, 100)) = 1
        _Strength("Strength", Range(0, 1)) = 0.5
        _HeightThreshold("Height Threshold", Range(0, 1)) = 0
        [Toggle(INVERT)]_Invert("Invert", int) = 0
    }

        SubShader{
            Pass {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                #pragma shader_feature INVERT

                sampler2D _MainTex;
                sampler2D _MaskTex;
                float4 _MaskTex_ST;
                float _Offset, _Strength, _Scale, _HeightThreshold;
                int _Invert;

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

                float4 frag(v2f i) : SV_Target {
                    float col = saturate((tex2D(_MainTex, i.uv).r - _HeightThreshold) / (1 - _HeightThreshold));
                    float mask = tex2D(_MaskTex, i.uv * _MaskTex_ST.xy + _MaskTex_ST.zw).r;

                    if (_Invert == 0) {
                        col = lerp(col, col * saturate(max(0.01, mask + _Offset) * _Scale), _Strength);
                    }
                    else {
                        col = lerp(col, col * saturate(max(0.01, 1 - (mask + _Offset)) * _Scale), _Strength);
                    }

                    col = clamp(col, 0, 1);

                    return float4(col, col, col, tex2D(_MainTex, i.uv).a);
                }
                ENDCG
            }
        }
            FallBack "Diffuse"
}