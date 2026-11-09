// =============================================================================
//  EwHash1To3 - Hash: float -> vec3
// =============================================================================
//  Dave Hoskins "Hash without Sine" (MIT). No sin(), stable across GPUs.
//
//  p       : input value, any range (integer ids work well)
//  returns : pseudo random vec3, each component in [0, 1)
//
//  Requires: -
//  Docs    : EwHash1To3.md
// =============================================================================
vec3 EwHash1To3(float p)
{
    vec3 p3 = fract(vec3(p) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xxy + p3.yzz) * p3.zyx);
}
