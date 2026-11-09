// =============================================================================
//  EwFresnelSchlick - Lighting: Schlick Fresnel approximation
// =============================================================================
//  F = f0 + (1 - f0) * (1 - cosTheta)^5 (Schlick 1994).
//
//  cosTheta : cosine between view and normal (or half vector), dot(n, v)
//  f0       : reflectance at normal incidence (0.04 dielectric, albedo metal)
//  returns  : reflectance, f0 at normal incidence -> 1 at grazing angles
//
//  Requires: -
//  Docs    : EwFresnelSchlick.md
// =============================================================================
vec3 EwFresnelSchlick(float cosTheta, vec3 f0)
{
    return f0 + (1.0 - f0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

float EwFresnelSchlick(float cosTheta, float f0)
{
    return f0 + (1.0 - f0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}
