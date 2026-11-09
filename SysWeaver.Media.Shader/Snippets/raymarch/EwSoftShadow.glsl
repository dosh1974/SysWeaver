// =============================================================================
//  EwSoftShadow - Raymarch: soft shadow (iq)
// =============================================================================
//  Sphere traces from a surface point towards the light and tracks the
//  closest approach k * h / t, which gives a penumbra for free (iq).
//  At most 64 steps.
//
//  The effect must define the scene distance BEFORE pasting this snippet:
//      #define EW_SDF(p) mapScene(p)      // float distance
//      #define EW_SDF(p) mapScene(p).x    // if mapScene returns vec2 / vec4
//
//  ro      : ray origin, the surface point (offset along the normal if needed)
//  rd      : normalized direction towards the light
//  tMin    : start distance, > 0 (avoids self shadowing, e.g. 0.01 .. 0.05)
//  tMax    : max distance (distance to the light, or scene size)
//  k       : penumbra sharpness, 2 = very soft .. 32+ = hard (typical 8 .. 16)
//  returns : light visibility, 0 = fully shadowed .. 1 = fully lit
//
//  Requires: EW_SDF (macro defined by the effect)
//  Docs    : EwSoftShadow.md
// =============================================================================
float EwSoftShadow(vec3 ro, vec3 rd, float tMin, float tMax, float k)
{
    float result = 1.0;
    float t = tMin;
    for (int i = 0; i < 64; i++)
    {
        float h = EW_SDF(ro + rd * t);
        // Closest approach relative to the travelled distance = cone occlusion.
        result = min(result, k * h / t);
        // Clamped step: small enough to not skip thin occluders, large enough to progress.
        t += clamp(h, 0.01, 0.2);
        if (result < 0.004 || t > tMax)
        {
            break;
        }
    }
    return clamp(result, 0.0, 1.0);
}
