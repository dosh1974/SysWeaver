// =============================================================================
//  EwHueRotate - Color: hue rotation around the grey axis
// =============================================================================
//  Rotates an rgb color around the (1, 1, 1) / sqrt(3) axis (Rodrigues).
//  Keeps the grey component (r + g + b) unchanged, no HSV round trip.
//
//  color   : rgb color
//  angle   : rotation in radians (2pi = full turn, 2pi / 3 maps r -> g -> b)
//  returns : rotated rgb (may leave [0, 1] slightly for saturated input)
//
//  Requires: -
//  Docs    : EwHueRotate.md
// =============================================================================
vec3 EwHueRotate(vec3 color, float angle)
{
    const vec3 greyAxis = vec3(0.57735026919); // 1 / sqrt(3)
    float cosAngle = cos(angle);
    float sinAngle = sin(angle);
    return color * cosAngle
         + cross(greyAxis, color) * sinAngle
         + greyAxis * dot(greyAxis, color) * (1.0 - cosAngle);
}
