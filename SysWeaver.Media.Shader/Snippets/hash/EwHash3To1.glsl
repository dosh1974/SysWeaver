// =============================================================================
//  EwHash3To1 - Hash: vec3 -> float
// =============================================================================
//  Dave Hoskins "Hash without Sine" (MIT). No sin(), stable across GPUs.
//
//  p       : input coordinate, any range (integer cell ids work well)
//  returns : pseudo random value in [0, 1)
//
//  Requires: -
//  Docs    : EwHash3To1.md
// =============================================================================
float EwHash3To1(vec3 p)
{
    vec3 p3 = fract(p * 0.1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return fract((p3.x + p3.y) * p3.z);
}
