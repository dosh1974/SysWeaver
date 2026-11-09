// =============================================================================
//  EwCalcNormalCentral - Raymarch: SDF normal (central differences, 6 samples)
// =============================================================================
//  Surface normal as the normalized gradient of the scene SDF, estimated with
//  central differences along x, y and z (6 samples).
//
//  The effect must define the scene distance BEFORE pasting this snippet:
//      #define EW_SDF(p) mapScene(p)      // float distance
//      #define EW_SDF(p) mapScene(p).x    // if mapScene returns vec2 / vec4
//
//  p       : surface position (ray hit point)
//  eps     : sample offset along each axis, e.g. 0.001
//  returns : normalized surface normal
//
//  Requires: EW_SDF (macro defined by the effect)
//  Docs    : EwCalcNormalCentral.md
// =============================================================================
vec3 EwCalcNormalCentral(vec3 p, float eps)
{
    vec2 e = vec2(eps, 0.0);
    return normalize(vec3(EW_SDF(p + e.xyy) - EW_SDF(p - e.xyy),
                          EW_SDF(p + e.yxy) - EW_SDF(p - e.yxy),
                          EW_SDF(p + e.yyx) - EW_SDF(p - e.yyx)));
}
