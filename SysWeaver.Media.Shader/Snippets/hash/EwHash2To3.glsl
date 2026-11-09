// =============================================================================
//  EwHash2To3 - Hash: vec2 -> vec3
// =============================================================================
//  Dave Hoskins "Hash without Sine" (MIT). No sin(), stable across GPUs.
//
//  p       : input coordinate, any range (integer cell ids work well)
//  returns : pseudo random vec3, each component in [0, 1)
//
//  Requires: -
//  Docs    : EwHash2To3.md
// =============================================================================
vec3 EwHash2To3(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return fract((p3.xxy + p3.yzz) * p3.zyx);
}
