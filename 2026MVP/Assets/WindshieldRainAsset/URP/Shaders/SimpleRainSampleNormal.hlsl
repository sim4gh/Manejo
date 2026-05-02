#ifndef _SHADED_TECH_WINDSHIELD_RAIN_ASSET__SIMPLE_RAIN_SAMPLE_NORMAL_INCLUDE__
#define _SHADED_TECH_WINDSHIELD_RAIN_ASSET__SIMPLE_RAIN_SAMPLE_NORMAL_INCLUDE__
#include "../../Common/Shaders/SimpleRain.hlsl"

void SampleRainNormal_float(float2 rain_uv, float3 up, float3 right, float amount, out float4 rain_out_normal)
{
#ifndef SHADERGRAPH_PREVIEW
	rain_out_normal = sampleRainNormal(rain_uv, up, right, amount);
#else
	rain_out_normal = float4(0, 0, 1, 0);
#endif
}

#endif