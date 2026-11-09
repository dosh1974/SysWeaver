// =============================================================================
//  EwCalcNormal - Raymarch: SDF normal (tetrahedral, 4 samples)
// =============================================================================
//  Surface normal as the normalized gradient of the scene SDF, estimated from
//  4 samples at the corners of a tetrahedron (iq). Cheaper than 6 tap central
//  differences with comparable quality.
//
//  The effect must define the scene distance BEFORE pasting this snippet:
//      #define EW_SDF(p) mapScene(p)      // float distance
//      #define EW_SDF(p) mapScene(p).x    // if mapScene returns vec2 / vec4
//
//  p       : surface position (ray hit point)
//  eps     : sample offset per axis (offset length eps * sqrt(3)), e.g. 0.001
//  returns : normalized surface normal
//
//  Requires: EW_SDF (macro defined by the effect)
//  Docs    : EwCalcNormal.md
// =============================================================================
vec3 EwCalcNormal(vec3 p, float eps)
{
    const vec2 k = vec2(1.0, -1.0);
    return normalize(k.xyy * EW_SDF(p + k.xyy * eps) +
                     k.yyx * EW_SDF(p + k.yyx * eps) +
                     k.yxy * EW_SDF(p + k.yxy * eps) +
                     k.xxx * EW_SDF(p + k.xxx * eps));
}
