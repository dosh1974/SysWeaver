// =============================================================================
//  EwHash2To1 - Hash: vec2 -> float
// =============================================================================
//  Dave Hoskins "Hash without Sine" (MIT). No sin(), stable across GPUs.
//
//  p       : input coordinate, any range (integer cell ids work well)
//  returns : pseudo random value in [0, 1)
//
//  Requires: -
//  Docs    : EwHash2To1.md
// =============================================================================
float EwHash2To1(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}
