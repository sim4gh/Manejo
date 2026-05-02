#ifndef _SHADED_TECH_WINDSHIELD_RAIN_ASSET__SAMPLE_BLUR_URP_FUNC_INCLUDE__
#define _SHADED_TECH_WINDSHIELD_RAIN_ASSET__SAMPLE_BLUR_URP_FUNC_INCLUDE__

#define IS_URP
#include "../../Common/Shaders/SampleBlur.hlsl"

void SampleGrabTexture_float(float2 uv, out float4 out_color)
{
	out_color = SAMPLE_TEXTURE2D_X(_WindshieldGrabTexture, sampler_WindshieldGrabTexture, uv);
}

void SampleBlur_float(float2 uv, float strength, out float4 grab_color, out float4 out_color)
{
	PRESAMPLE_BLUR_TEXTURES(uv)
	SET_PRESAMPLE_GRAB_COLOR(grab_color)
	GET_BLUR_COLOR_FROM_PRESAMPLE(out_color, strength)
}

void SampleBlur2_float(float2 uv, float strength1, float strength2, out float4 grab_color, out float4 out_color1, out float4 out_color2)
{
	PRESAMPLE_BLUR_TEXTURES(uv)
	SET_PRESAMPLE_GRAB_COLOR(grab_color)
	GET_BLUR_COLOR_FROM_PRESAMPLE(out_color1, strength1)
	GET_BLUR_COLOR_FROM_PRESAMPLE(out_color2, strength2)
}


#endif