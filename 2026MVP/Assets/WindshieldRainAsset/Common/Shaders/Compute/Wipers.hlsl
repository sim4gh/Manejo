#ifndef _SHADED_TECH_WINDSHIELD_RAIN_ASSET__WIPERS_HLSL
#define _SHADED_TECH_WINDSHIELD_RAIN_ASSET__WIPERS_HLSL

#define EPSILON 0.0001f

struct Wiper {
	float2 origin;
	float4 pos;
	float4 prevPos;
};

float2 PosToUV(float2 pos)
{
	return float2((pos.x / _WindshieldPlaneSize.x) * _Resolution.x, (pos.y / _WindshieldPlaneSize.y) * _Resolution.y);
}

float2 UVToPos(float2 uv)
{
	return float2((uv.x / _Resolution.x) * _WindshieldPlaneSize.x, (uv.y / _Resolution.y) * _WindshieldPlaneSize.y);
}

RWStructuredBuffer< Wiper > _Wipers;
int _WipersCount;


float cross2d(float2 vec1, float2 vec2) {
	return (vec1.x * vec2.y - vec1.y * vec2.x);
}

bool isBetweenVectors(float2 vec0, float2 vec1, float2 vec2) {
	if (length(vec1 - vec2) <= EPSILON) {
		return (length(vec0 - vec1) <= EPSILON);
	}
	bool isBetween = (cross2d(vec0, vec1) * cross2d(vec2, vec1)) > 0 && (cross2d(vec0, vec2) * cross2d(vec1, vec2)) > 0;
	isBetween = isBetween || (length(vec0 - vec1) <= EPSILON) || (length(vec0 - vec2) <= EPSILON);
	return isBetween;
}

bool isPointBetweenWiper(float2 p, float4 wiperPos, float4 wiperPrevPos, float2 wiperOrigin) {
	float2 startPos = wiperPos.xy;
	float2 endPos = wiperPos.zw;
	float2 prevStartPos = wiperPrevPos.xy;
	float2 prevEndPos = wiperPrevPos.zw;
	float distToOrigin = length(p - wiperOrigin);
	float distToStart = length(startPos - wiperOrigin);
	float distToEnd = length(endPos - wiperOrigin);
	if (distToOrigin < distToStart || distToOrigin > distToEnd) {
		return false;
	}

	float2 toOriginVec = normalize(p - wiperOrigin);

	float2 startVec = normalize(startPos - wiperOrigin);
	float2 endVec = normalize(endPos - wiperOrigin);
	float2 prevStartVec = normalize(prevStartPos - wiperOrigin);
	float2 prevEndVec = normalize(prevEndPos - wiperOrigin);

	if (distToStart <= EPSILON || length(startVec - endVec) <= EPSILON) {
		return isBetweenVectors(toOriginVec, endVec, prevEndVec);
	}

	bool direction = dot(startVec, prevEndVec) > dot(prevStartVec, endVec);
	bool isBetweenInner = direction ? isBetweenVectors(toOriginVec, startVec, prevEndVec) : isBetweenVectors(toOriginVec, prevStartVec, endVec);
	bool isBetweenNow = isBetweenVectors(toOriginVec, startVec, endVec);
	bool isBetweenPrev = isBetweenVectors(toOriginVec, prevStartVec, prevEndVec);

	if (isBetweenInner && !isBetweenNow && !isBetweenPrev) {
		return true;
	}

	float2 toOrigin = (p - wiperOrigin);

	bool isInNow = false;
	if (isBetweenNow) {
		float2 wiperVec = endPos - startPos;
		float distToWiper = length((cross2d((wiperOrigin - startPos), wiperVec) / cross2d(wiperVec, toOrigin)) * toOrigin);
		bool dir = cross2d(normalize(wiperVec), startVec) * cross2d(prevStartVec, startVec) > 0;
		if ((distToWiper <= distToOrigin && !dir) || (distToWiper >= distToOrigin && dir)) {
			isInNow = true;
		}
	}

	bool isInPrev = false;
	if (isBetweenPrev) {
		float2 wiperVec = prevEndPos - prevStartPos;
		float distToWiper = length((cross2d((wiperOrigin - prevStartPos), wiperVec) / cross2d(wiperVec, toOrigin)) * toOrigin);
		bool dir = cross2d(normalize(wiperVec), prevStartVec) * cross2d(prevStartVec, startVec) > 0;
		if ((distToWiper <= distToOrigin && dir) || (distToWiper >= distToOrigin && !dir)) {
			isInPrev = true;
		}
	}

	if (isBetweenNow && isBetweenPrev) {
		return (isInNow && isInPrev);
	}

	if (isBetweenNow) {
		return isInNow;
	}

	if (isBetweenPrev) {
		return isInPrev;
	}

	return false;
	/*float2 toOriginVec = normalize(p - wiperOrigin);
	float2 wiperVecNorm = normalize(wiperVec);
	float2 wiperPrevVecNorm = normalize(wiperPrevVec);
	bool isBetween = isBetweenVectors(toOriginVec, wiperVecNorm, wiperPrevVecNorm);
	float distToOrigin = length(p - wiperOrigin);

	float wiperLen = length(wiperVec);
	return isBetween && distToOrigin < wiperLen;*/
}

#endif