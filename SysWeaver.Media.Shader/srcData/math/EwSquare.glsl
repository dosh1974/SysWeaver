// =============================================================================
//  EwSquare - Math: x * x
// =============================================================================
//  Square of a value (component wise for vectors). Cheaper and safer than
//  pow(x, 2.0), which is undefined for negative x.
//
//  x       : input value
//  returns : x * x
//
//  Requires: -
//  Docs    : EwSquare.md
// =============================================================================
float EwSquare(float x)
{
    return x * x;
}

vec2 EwSquare(vec2 x)
{
    return x * x;
}

vec3 EwSquare(vec3 x)
{
    return x * x;
}

vec4 EwSquare(vec4 x)
{
    return x * x;
}
