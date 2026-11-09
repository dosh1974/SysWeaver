// =============================================================================
//  EwHash1To2 - Hash: float -> vec2
// =============================================================================
//  Dave Hoskins "Hash without Sine" (MIT). No sin(), stable across GPUs.
//
//  p       : input value, any range (integer ids work well)
//  returns : pseudo random vec2, each component in [0, 1)
//
//  Requires: -
//  Docs    : EwHash1To2.md
// =============================================================================
vec2 EwHash1To2(float p)
{
    vec3 p3 = fract(vec3(p) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xx + p3.yz) * p3.zy);
}
