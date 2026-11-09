// =============================================================================
//  EwHash1To1 - Hash: float -> float (+ vec2/vec3/vec4 per component)
// =============================================================================
//  Dave Hoskins "Hash without Sine" (MIT). No sin(), stable across GPUs.
//  The vector overloads hash each component independently (same body).
//
//  p       : input value(s), any range (integer ids work well)
//  returns : pseudo random value(s) in [0, 1), per component
//
//  Requires: -
//  Docs    : EwHash1To1.md
// =============================================================================
float EwHash1To1(float p)
{
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

vec2 EwHash1To1(vec2 p)
{
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

vec3 EwHash1To1(vec3 p)
{
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

vec4 EwHash1To1(vec4 p)
{
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}
