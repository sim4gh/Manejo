#ifndef _SHADED_TECH_WINDSHIELD_RAIN_ASSET__RANDOM_HLSL
#define _SHADED_TECH_WINDSHIELD_RAIN_ASSET__RANDOM_HLSL

float rand(float3 value) {
	//make value smaller to avoid artefacts
	float3 smallValue = sin(value);
	//get scalar value from 3d vector
	float random = dot(smallValue, float3(12.9898, 78.233, 37.719));
	//make value more random by making it bigger and then taking teh factional part
	random = frac(sin(random) * 143758.5453);
	return random;
}

float rand3dTo1d(float3 value, float3 dotDir) {// = vec3(12.9898, 78.233, 37.719)){
	//make value smaller to avoid artefacts
	float3 smallValue = sin(value);
	//get scalar value from 3d vector
	float random = dot(smallValue, dotDir);
	//make value more random by making it bigger and then taking teh factional part
	random = frac(sin(random) * 143758.5453);
	return random;
}

float3 rand3dTo3d(float3 value) {
	return float3(
		rand3dTo1d(value, float3(12.989, 78.233, 37.719)),
		rand3dTo1d(value, float3(39.346, 11.135, 83.155)),
		rand3dTo1d(value, float3(73.156, 52.235, 09.151))
		);
}

#endif