// =============================================================================
//  EwOklabToLinear - Color: Oklab -> linear sRGB
// =============================================================================
//  The inverse of EwLinearToOklab (Björn Ottosson's Oklab). Colors mixed in
//  Oklab can fall outside the sRGB gamut: clamp the result (after
//  EwLinearToSrgb) when it is shown.
//
//  lab     : x = L (lightness), y = a (green .. red), z = b (blue .. yellow)
//  returns : linear rgb (EwLinearToSrgb for the output color)
//
//  Requires: -
//  Docs    : EwOklabToLinear.md
// =============================================================================
vec3 EwOklabToLinear(vec3 lab)
{
    vec3 lms = vec3(
        dot(lab, vec3(1.0, 0.3963377774, 0.2158037573)),
        dot(lab, vec3(1.0, -0.1055613458, -0.0638541728)),
        dot(lab, vec3(1.0, -0.0894841775, -1.2914855480)));
    lms = lms * lms * lms;
    // Cone responses (LMS) -> linear sRGB
    return vec3(
        dot(lms, vec3(4.0767416621, -3.3077115913, 0.2309699292)),
        dot(lms, vec3(-1.2684380046, 2.6097574011, -0.3413193965)),
        dot(lms, vec3(-0.0041960863, -0.7034186147, 1.7076147010)));
}
