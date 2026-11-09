// =============================================================================
//  EwLightAttenuation - Lighting: smooth inverse square falloff
// =============================================================================
//  intensity / (1 + d^2 / r^2)^2. Finite at the light, ~1 / d^4 far away.
//
//  toLight   : vector from the shaded point to the light (not normalized)
//  intensity : light intensity at distance 0
//  radius    : distance at which the intensity has dropped to 1/4
//  returns   : attenuated intensity in (0, intensity]
//
//  Requires: -
//  Docs    : EwLightAttenuation.md
// =============================================================================
float EwLightAttenuation(vec3 toLight, float intensity, float radius)
{
    float d2 = 1.0 + dot(toLight, toLight) / (radius * radius);
    return intensity / (d2 * d2);
}
