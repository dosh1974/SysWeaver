// =============================================================================
//  EwCosinePalette - Color: iq cosine palette
// =============================================================================
//  Inigo Quilez procedural palette: a + b * cos(2pi * (c * t + d)).
//
//  t       : palette position, any range (period 1 / c per channel)
//  a       : bias (center color)
//  b       : amplitude (contrast)
//  c       : frequency per channel
//  d       : phase per channel
//  returns : rgb color (can leave [0, 1] if a +- b does)
//
//  Requires: -
//  Docs    : EwCosinePalette.md
// =============================================================================
vec3 EwCosinePalette(float t, vec3 a, vec3 b, vec3 c, vec3 d)
{
    return a + b * cos(6.28318530718 * (c * t + d));
}
