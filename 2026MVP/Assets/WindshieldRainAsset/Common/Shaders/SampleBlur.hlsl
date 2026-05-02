// BLUR_ITER 3
// This file is generated don't modify it
#ifndef _SHADED_TECH_WINDSHIELD_RAIN_ASSET__SAMPLE_BLUR_INCLUDE__
#define _SHADED_TECH_WINDSHIELD_RAIN_ASSET__SAMPLE_BLUR_INCLUDE__
#define BLUR_ITER 3.0f

#if defined(IS_HDRP)
#define BLUR_UV (uv * _RTHandleScale.xy)
#else
#define BLUR_UV uv
#endif

#if !defined(IS_HDRP) && !defined(IS_URP)

#define PRESAMPLE_BLUR_TEXTURES(uv) \
float step = (1.0f / BLUR_ITER); \
float offset = 0; \
float4 tempGrab = tex2D(_WindshieldGrabTexture, uv); \
float4 tempBlurColor0 = tex2D(_GrabBlurTexture_0, uv); \
float4 tempBlurColor1 = tex2D(_GrabBlurTexture_1, uv); \
float4 tempBlurColor2 = tex2D(_GrabBlurTexture_2, uv);

sampler2D _WindshieldGrabTexture;
sampler2D _GrabBlurTexture_0;
sampler2D _GrabBlurTexture_1;
sampler2D _GrabBlurTexture_2;

#else

#define PRESAMPLE_BLUR_TEXTURES(uv) \
float step = (1.0f / BLUR_ITER); \
float offset = 0; \
float4 tempGrab = SAMPLE_TEXTURE2D_X(_WindshieldGrabTexture, sampler_WindshieldGrabTexture, BLUR_UV); \
float4 tempBlurColor0 = SAMPLE_TEXTURE2D_X(_GrabBlurTexture_0, sampler_GrabBlurTexture_0, BLUR_UV); \
float4 tempBlurColor1 = SAMPLE_TEXTURE2D_X(_GrabBlurTexture_1, sampler_GrabBlurTexture_1, BLUR_UV); \
float4 tempBlurColor2 = SAMPLE_TEXTURE2D_X(_GrabBlurTexture_2, sampler_GrabBlurTexture_2, BLUR_UV);

TEXTURE2D_X(_WindshieldGrabTexture);
SAMPLER(sampler_WindshieldGrabTexture);
TEXTURE2D_X(_GrabBlurTexture_0);
SAMPLER(sampler_GrabBlurTexture_0);
TEXTURE2D_X(_GrabBlurTexture_1);
SAMPLER(sampler_GrabBlurTexture_1);
TEXTURE2D_X(_GrabBlurTexture_2);
SAMPLER(sampler_GrabBlurTexture_2);

#endif

#define SET_PRESAMPLE_GRAB_COLOR(grab_color) grab_color = tempGrab;

#define GET_BLUR_COLOR_FROM_PRESAMPLE(out_color, strength) \
offset = 0; \
if (strength <= (offset + step)) { \
out_color = lerp(tempGrab, tempBlurColor0, (strength - offset) / step); \
} else { \
 offset += step; \
if (strength <= (offset + step)) { \
out_color = lerp(tempBlurColor0, tempBlurColor1, (strength - offset) / step); \
} else { \
 offset += step; \
out_color = lerp(tempBlurColor1, tempBlurColor2, (strength - offset) / step); \
} } 

#endif
