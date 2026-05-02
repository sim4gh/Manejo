#ifndef _SHADED_TECH_WINDSHIELD_RAIN_ASSET__WIPERS_HANDLER_HLSL
#define _SHADED_TECH_WINDSHIELD_RAIN_ASSET__WIPERS_HANDLER_HLSL

#include "Wipers.hlsl"

#if WIPERS_ENABLED
// Check if a point is within the bounds of a line segment
bool IsOnSegment(float2 start, float2 end, float2 p)
{
	// Check if the point is within the bounds of the line segment
	float minX = min(start.x, end.x);
	float maxX = max(start.x, end.x);
	float minY = min(start.y, end.y);
	float maxY = max(start.y, end.y);
	return p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY;
}

// Compute the cross point of two line segments
bool ComputeLineSegmentCrossPoint(float2 start1, float2 end1, float2 start2, float2 end2, out float2 crossPoint)
{
	// Compute slopes and y-intercepts of the two line segments
	float m1 = (end1.y - start1.y) / (end1.x - start1.x);
	float b1 = start1.y - m1 * start1.x;
	float m2 = (end2.y - start2.y) / (end2.x - start2.x);
	float b2 = start2.y - m2 * start2.x;

	// Check if the slopes are equal (and the lines are either parallel or coincident)
	if (abs(m1 - m2) < 0.001 && abs(b1 - b2) < 0.001)
	{
		// The lines are coincident (and intersecting at every point)
		crossPoint = start1;
		return true;
	}
	else if (abs(m1 - m2) < 0.001)
	{
		// The lines are parallel (and non-intersecting)
		crossPoint = float2(0, 0);
		return false;
	}

	// Compute the intersection point of the two lines
	float x = (b2 - b1) / (m1 - m2);
	float y = m1 * x + b1;
	float2 intersection = float2(x, y);

	// Check if the intersection point lies on both line segments
	bool onSegment1 = IsOnSegment(start1, end1, intersection);
	bool onSegment2 = IsOnSegment(start2, end2, intersection);

	// Return the cross point if the line segments are crossing
	if (onSegment1 && onSegment2)
	{
		crossPoint = intersection;
		return true;
	}
	else
	{
		crossPoint = float2(0, 0);
		return false;
	}
}

Drop handleWiper(Drop drop, int wiper_index) {
	float2 currentPos = drop.position;
	float2 mov = drop.velocity;
	float2 wiperVec = normalize(_Wipers[wiper_index].pos.zw - _Wipers[wiper_index].pos.xy);
	float wiperLen = length(_Wipers[wiper_index].pos.zw - _Wipers[wiper_index].pos.xy);
	float2 toStartVec = normalize(currentPos - _Wipers[wiper_index].pos.xy);
	float distToStart = length(currentPos - _Wipers[wiper_index].pos.xy);
	float distToOrigin = length(currentPos - _Wipers[wiper_index].origin);
	float maxRange = length(_Wipers[wiper_index].pos.zw - _Wipers[wiper_index].origin);
	float minRange = length(_Wipers[wiper_index].pos.xy - _Wipers[wiper_index].origin);

	bool isInRange = dot(wiperVec, toStartVec) > 0.9995 && distToOrigin < maxRange && distToOrigin > minRange;
	float2 crossPoint = float2(0, 0);
	bool isCrossing = ComputeLineSegmentCrossPoint(currentPos, currentPos + mov, _Wipers[wiper_index].pos.xy, _Wipers[wiper_index].pos.zw, crossPoint);

	float2 toWiperVec = (wiperVec * distToStart + _Wipers[wiper_index].pos.xy) - currentPos;
	bool isNotPushedByWipers = WipersTexture[PosToUV(currentPos)].z < 0.1f
		&& WipersTexture[PosToUV(currentPos - mov)].z < 0.1f
		&& WipersTexture[PosToUV(currentPos - 2*mov)].z < 0.1f;
	if (isInRange && isNotPushedByWipers) {
		float speed = dot(mov, wiperVec);
		mov = wiperVec * speed;
		drop.position += toWiperVec;
	}
	else if (isCrossing && isNotPushedByWipers) {
		float2 movVecBeforeWiper = crossPoint - currentPos;
		float2 movVecAfterWiper = currentPos + mov - crossPoint;
		float2 adjustedMov = wiperVec * dot(movVecAfterWiper, wiperVec);
		mov = movVecBeforeWiper + adjustedMov;
	}
	drop.velocity = mov;

	float2 wipingVec = WipersTexture[PosToUV(currentPos)].xy;
	float wiperStren = WipersTexture[PosToUV(currentPos)].z;
	if (wiperStren > _WipersThreshold) {// && length(wipingVec) > 0.001f) {
		float speed = _WipersSpeed * length(wipingVec) * wiperStren;// * max(0.01f, _DeltaTime);// * length(wipingVec) * wiperStren;// length(wipingVec) * wiperStren * _WipersSpeed;
		//speed = max(0.5, speed);
		float2 perp1 = float2(-wiperVec.y, wiperVec.x);
		float2 perp2 = float2(wiperVec.y, -wiperVec.x);
		float2 direction = dot(perp1, normalize(wipingVec)) > 0 ? perp1 : perp2;
		speed += dot(mov, direction);// length(drop.velocity);
		drop.velocity =  normalize(direction) * speed;//* WipersTexture[PosToUV(currentPos)].z * _WipersSpeed;
		drop.position += wipingVec;// * 0.1f;// * min(1.0, _DeltaTime);// * 0.99f;
		// if (wiperStren < _WipersThreshold + 0.01f) {
		// 	drop.size = -1;
		// }
	}
	return drop;
}
#endif

#endif