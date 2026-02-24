Shader "Custom/RainGlass"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _Size ("Size", float) = 1
        _T("Tiempo", float) = 1
        _Distortion("Distorsion", range(-5, 5)) = 1
        _Blur("Blur", range(0,1)) = 1
    }

    SubShader
    {
        Tags 
        {    
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #define S(a, b, t) smoothstep(a, b, t)

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 grabUv : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            float _Size, _T, _Distortion, _Blur;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                // Equivalente moderno a ComputeGrabScreenPos
                OUT.grabUv = ComputeScreenPos(OUT.positionHCS);

                return OUT;
            }

            float N21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float3 Layer(float2 UV, float t)
            {
                float2 aspect = float2(2,1);

                float2 uv = UV * _Size * aspect;
                uv.y += t * .25;

                float2 gv = frac(uv) - .5;
                float2 id = floor(uv);

                float n = N21(id);

                t += n * 6.2831;

                float w = UV.y * 10;

                float x = (n - .5) * .8;
                x += (.4 - abs(x)) * sin(3*w) * pow(sin(w), 6) * .45;

                float y = -sin(t + sin(t + sin(t)*.5)) * .45;
                y -= gv.x * gv.x;

                float2 dropPos = (gv - float2(x, y)) / aspect;
                float drop = S(.05, .03, length(dropPos));

                float2 trailPos = (gv - float2(x, t * .25)) / aspect;
                trailPos.y = (frac(trailPos.y * 8) - .5) / 8;

                float trail = S(.03, .01, length(trailPos));

                float fogtrail = S(-.05, .05, dropPos.y);
                fogtrail *= S(.5, y, gv.y);
                trail *= fogtrail;
                fogtrail *= S(.05, .04, abs(dropPos.x));

                float2 offs = drop * dropPos + trail * trailPos;

                return float3(offs, fogtrail);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                   float t = fmod(_Time.y + _T, 7200);

                   float3 drops = Layer(IN.uv, t); 
                  drops += Layer(IN.uv, t * 1.35 + 7.12);
                  drops += Layer(IN.uv, t * 1.65 + 1.12);
                   drops += Layer(IN.uv, t * 1.55 + 7.12);

                   float blur = _Blur * 7 * (1 - drops.z);

                   float2 screenUV = IN.grabUv.xy / IN.grabUv.w;

                  
                   const int numSamples = 16;
                    blur *= .9;

                   half4 color = 0;   // 🔴 ESTA LÍNEA ES LA QUE FALTABA
                   float a = 0;

                   for (int i = 0; i < numSamples; i++)
                   {
                           float2 offs = float2(sin(a), cos(a))*blur;

                           color += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture,screenUV + offs);

                           a++;
                   }
                     color /= numSamples; 
                    float alpha = saturate(0.15 + drops.z * 0.4);

                return half4(color.rgb, alpha);
            }
             
            ENDHLSL
        }
    }
}