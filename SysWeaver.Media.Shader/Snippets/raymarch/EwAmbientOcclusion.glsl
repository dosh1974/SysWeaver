// =============================================================================
//  EwAmbientOcclusion - Raymarch: SDF ambient occlusion (5 samples)
// =============================================================================
//  Samples the scene SDF at 5 increasing distances along the normal and
//  compares them with the expected (unoccluded) distance (iq).
//
//  The effect must define the scene distance BEFORE pasting this snippet:
//      #define EW_SDF(p) mapScene(p)      // float distance
//      #define EW_SDF(p) mapScene(p).x    // if mapScene returns vec2 / vec4
//
//  p       : surface position
//  n       : normalized surface normal at p
//  returns : ambient visibility, 0 = fully occluded .. 1 = open
//
//  Requires: EW_SDF (macro defined by the effect)
//  Docs    : EwAmbientOcclusion.md
// =============================================================================
float EwAmbientOcclusion(vec3 p, vec3 n)
{
    float occlusion = 0.0;
    float weight = 1.0;
    for (int i = 0; i < 5; i++)
    {
        // Sample heights 0.01 .. 0.13 along the normal.
        float h = 0.01 + 0.12 * float(i) / 4.0;
        float d = EW_SDF(p + h * n);
        // Unoccluded the SDF equals h; any shortfall is occlusion.
        occlusion += (h - d) * weight;
        weight *= 0.95;
    }
    return clamp(1.0 - 3.0 * occlusion, 0.0, 1.0);
}
