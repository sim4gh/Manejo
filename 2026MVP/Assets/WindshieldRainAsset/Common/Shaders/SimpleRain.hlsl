#ifndef _SHADED_TECH_WINDSHIELD_RAIN_ASSET__SIMPLE_RAIN_INCLUDE__
#define _SHADED_TECH_WINDSHIELD_RAIN_ASSET__SIMPLE_RAIN_INCLUDE__

#ifndef SHADERGRAPH_PREVIEW

#define LAYERS_COUNT 4.0

#define S(a, b, t) smoothstep(a, b, t)

float _ModTime;
float3 _RainDirection_0, _RainDirection_1, _RainDirection_2, _RainDirection_3;

float3 N13(float p) {
    //  from DAVE HOSKINS
    float3 p3 = frac(float3(p, p, p) * float3(.1031,.11369,.13787));
   p3 += dot(p3, p3.yzx + 19.19);
   return frac(float3((p3.x + p3.y)*p3.z, (p3.x+p3.z)*p3.y, (p3.y+p3.z)*p3.x));
}

float4 N14(float t) {
    return frac(sin(t*float4(123., 1024., 1456., 264.))*float4(6547., 345., 8799., 1564.));
}
float N(float t) {
    return frac(sin(t*12345.564)*7658.76);
}

float Saw(float b, float t) {
    return S(0., b, t)*S(1., b, t);
}

float2 rotateUV(float2 uv, float2 direction)
{
    float mid = 0.5;
    return float2(
        direction.y * (uv.x - mid) + direction.x * (uv.y - mid) + mid,
        direction.y * (uv.y - mid) - direction.x * (uv.x - mid) + mid
    );
}

float2 rotateVector(float2 vec, float2 direction) {
    direction = normalize(direction);
    float cosTheta = direction.y;
    float sinTheta = direction.x;

    float2 vec_rotated;
    vec_rotated.x = vec.x * cosTheta - vec.y * sinTheta;
    vec_rotated.y = vec.x * sinTheta + vec.y * cosTheta;

    return vec_rotated;
}

float3 DropLayer2(float2 uv, float t, float start_t, float speed, float amount) {
    float2 UV = uv;
    
    float trail_appear = 1.;
    float trail_short = 1.0;
    float trail_len = _RainTrailLength * speed;
    if (trail_len < 1.) {
        trail_short = trail_len;
        trail_len = 1.;
    }
    if (trail_short < 0.01) {
        trail_short = 1.;
        trail_appear = 0.;
    }
    
    uv.y += start_t + t*0.75*speed;
    float2 a = float2(6., 1.0 / trail_len);
    float2 grid = a*2.;
    float2 id = floor(uv*grid);
    
    float colShift = N(id.x); 
    uv.y += colShift;
    
    id = floor(uv*grid);
    float3 n = N13(id.x*35.2+id.y*2376.1);
    float2 st = frac(uv*grid)-float2(.5, 0.);
    
    float x = n.x-.5;
    
    float y = UV.y*30.;
    float wiggle = sin(y+sin(y));
    x += wiggle*(.5-abs(x))*(n.z-.5);
    x *= .7;
    float ti = frac(t*min(1., speed)/trail_len+n.z);
    y = (Saw(.85, ti)-.5)*0.9+.5;
    float2 p = float2(x, y);
    
    float d = length(((st-p)/a.xy) * 6.0);//*a.yx*max(a.y, a.x));
    
    float mainDrop = S(.4, .0, d);
    
    float r = clamp(sqrt(S(1., y, st.y)) * (1.0 / trail_short) - ((1.0 / trail_short) - 1.), 0., 1.);
    float cd = abs(st.x-x);
    float trail = S(.23*r, .15*r*r, cd);
    float trailFront = S(-.02, .02, st.y-y);
    trail *= trailFront*r*r*trail_appear;
    
    float m = mainDrop;

    float2 norm = ((st-p)/a.xy * 6.0);
    float strength =  saturate(1 - S(amount * (1.0 + _RainAppearingSlope) - _RainAppearingSlope, amount * (1.0 + _RainAppearingSlope),  n.z));
    if (m < 0.5) {
      norm = float2(0.0, 0.0);
    } 
    if ((trail * trail_appear) > 0.5) {
        norm = (((st-p)/a.xy) * 6.0);
        float slope = saturate(1. - smoothstep(_RainTrailStrength, _RainTrailStrength + _RainTrailSlope, abs(st.y - p.y)));
        norm = min(length(norm), 0.1 * slope) * normalize(norm);
    }
    return float3(-norm, trail)  * strength;

    //return float2( saturate(m + trail * saturate(1. - smoothstep(_RainTrailStrength, _RainTrailStrength + _RainTrailSlope, st.y))), trail) * saturate(1 - S(amount * (1.0 + _RainAppearingSlope) - _RainAppearingSlope, amount * (1.0 + _RainAppearingSlope),  n.z));
}

#if STATIC_RAIN_ON
float2 StaticDrops(float2 uv, float t) {
    uv *= 40. * _StaticRainScale;
    
    float2 id = floor(uv);
    uv = frac(uv)-.5;
    float3 n = N13(id.x*107.45+id.y*3543.654);
    float2 p = (n.xy-.5)*.7;
    float d = length(uv-p);

    float2 norm = uv-p;
    if (d > 0.15) {
      norm = float2(0.0, 0.0);
    }
    
    float fade = Saw(.025, frac(t * _StaticRainTimeSpeed +n.z));
    norm *= S(.3, 0., d)*frac(n.z*10.)*fade;
    
    return norm;
}
#endif

float3 MyLayer(float2 uv, float t, float start_t, float speed, float2 direction, float amount)
{
    float x1 = fmod(t - start_t, _ModTime);
    float2 uv1 = rotateUV(uv, direction);
    if (speed < 0.001) {
        uv1 = uv;
    }
    float rain_amount = saturate((1. - abs(((_ModTime-x1)/_ModTime) *2. - 1.)) * _RainAppearingSpeed);
    float3 m1 = DropLayer2(uv1, t, start_t + t - x1, speed, rain_amount * amount);

    if (speed >= 0.001) {
        m1.xy = rotateVector(m1.xy, direction);
    }
    return m1;
}

float3 MyDrops(float2 uv, float t, float3 up, float3 right, float amount) {

#if STATIC_RAIN_ON
    float2 s = StaticDrops(uv, t) * amount;
#endif
    
    float2 rain_direction0 = float2(dot(_RainDirection_0, right), dot(_RainDirection_0, up));// float2(dot(_RainDirection_0, up), dot(_RainDirection_0, right));
    float3 m1 = MyLayer(uv + float2(0.1, 0.2), t, 0, length(rain_direction0), normalize(rain_direction0), amount);
    float2 rain_direction1 = float2(dot(_RainDirection_1, right), dot(_RainDirection_1, up));//float2(dot(_RainDirection_1, up), dot(_RainDirection_1, right));
    float offset1 = _ModTime / LAYERS_COUNT;
    float3 m2 = MyLayer(uv + float2(1.2, 2.1), t, offset1, length(rain_direction1), normalize(rain_direction1), amount);
    float2 rain_direction2 = float2(dot(_RainDirection_2, right), dot(_RainDirection_2, up));//float2(dot(_RainDirection_2, up), dot(_RainDirection_2, right));
    float offset2 = (_ModTime / LAYERS_COUNT) * 2;
    float3 m3 = MyLayer(uv + float2(0.2, 0.5), t, offset2, length(rain_direction2), normalize(rain_direction2), amount);
    float2 rain_direction3 = float2(dot(_RainDirection_3, right), dot(_RainDirection_3, up));//float2(dot(_RainDirection_3, up), dot(_RainDirection_3, right));
    float offset3 = (_ModTime / LAYERS_COUNT) * 3;
    float3 m4 = MyLayer(uv + float2(0.9, 0.1), t, offset3, length(rain_direction3), normalize(rain_direction3), amount);
    

#if STATIC_RAIN_ON
    float2 c = s+m1.xy+m2.xy+m3.xy+m4.xy;
#else
    float2 c = m1.xy+m2.xy+m3.xy+m4.xy;
#endif
    //c = S(.3, 1., c);
    
    return float3(c, max((float)(length(c) * _DropSmoothnessMultiplier), (float)(max(max(max(m1.z, m2.z), m3.z), m4.z) * _TrailSmoothnessMultiplier)));
}

//#define CHEAP_NORMALS

float4 sampleRainNormal(float2 rain_uv, float3 up, float3 right, float amount)
{
    float t = _Time.y;
    //float2 c = MyDrops(rain_uv, t, up, right, amount);//Drops(uv, t, staticDrops, layer1, layer2);
    //#ifdef CHEAP_NORMALS
    //    float2 n = float2(ddx(c.x), ddy(c.x));// cheap normals (3x cheaper, but 2 times shittier ;))
    //#else
    //    float2 e = float2(.001, 0.);
    //    float cx = MyDrops(rain_uv+e, t, up, right, amount).x;// Drops(uv+e, t, staticDrops, layer1, layer2).x;
    //    float cy =  MyDrops(rain_uv+e.yx, t, up, right, amount).x;// Drops(uv+e.yx, t, staticDrops, layer1, layer2).x;
    //    float2 n = float2(cx-c.x, cy-c.x);		// expensive normals
    //#endif
    float3 c = MyDrops(rain_uv, t, up, right, amount);
    float2 n = c.xy;
    n *= _RainNormalStrength;
    float4 output = float4(n.x, n.y, 1.0f, c.z);
    output.xyz = normalize(output.xyz);

    return output;
}

#endif // SHADERGRAPH_PREVIEW

#endif // _SHADED_TECH_WINDSHIELD_RAIN_ASSET__SIMPLE_RAIN_INCLUDE__
