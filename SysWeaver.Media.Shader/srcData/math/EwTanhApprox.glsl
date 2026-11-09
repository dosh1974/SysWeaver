// =============================================================================
//  EwTanhApprox - Math: fast rational tanh approximation
// =============================================================================
//  Pade style x * (27 + x^2) / (27 + 9 x^2), clamped to [-1, 1]. Exact at 0,
//  max abs error ~0.024 (around |x| ~ 1.6), reaches exactly +-1 at |x| = 3.
//
//  x       : input value (any range)
//  returns : approximation of tanh(x) in [-1, 1]
//
//  Requires: -
//  Docs    : EwTanhApprox.md
// =============================================================================
float EwTanhApprox(float x)
{
    float x2 = x * x;
    return clamp(x * (27.0 + x2) / (27.0 + 9.0 * x2), -1.0, 1.0);
}

vec2 EwTanhApprox(vec2 x)
{
    vec2 x2 = x * x;
    return clamp(x * (27.0 + x2) / (27.0 + 9.0 * x2), -1.0, 1.0);
}

vec3 EwTanhApprox(vec3 x)
{
    vec3 x2 = x * x;
    return clamp(x * (27.0 + x2) / (27.0 + 9.0 * x2), -1.0, 1.0);
}

vec4 EwTanhApprox(vec4 x)
{
    vec4 x2 = x * x;
    return clamp(x * (27.0 + x2) / (27.0 + 9.0 * x2), -1.0, 1.0);
}
