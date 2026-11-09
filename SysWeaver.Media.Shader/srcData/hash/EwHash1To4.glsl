// =============================================================================
//  EwHash1To4 - Hash: float -> vec4
// =============================================================================
//  Dave Hoskins "Hash without Sine" (MIT). No sin(), stable across GPUs.
//
//  p       : input value, any range (integer ids work well)
//  returns : pseudo random vec4, each component in [0, 1)
//
//  Requires: -
//  Docs    : EwHash1To4.md
// =============================================================================
vec4 EwHash1To4(float p)
{
    vec4 p4 = fract(vec4(p) * vec4(0.1031, 0.1030, 0.0973, 0.1099));
    p4 += dot(p4, p4.wzxy + 33.33);
    return fract((p4.xxyz + p4.yzzw) * p4.zywx);
}
