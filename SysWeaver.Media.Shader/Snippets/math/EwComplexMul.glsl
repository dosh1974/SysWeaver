// =============================================================================
//  EwComplexMul - Math: complex multiplication
// =============================================================================
//  (a.x + i a.y) * (b.x + i b.y). Multiplying by a unit complex number
//  rotates; by any complex number rotates and scales.
//
//  a, b    : complex numbers (x = real, y = imaginary)
//  returns : product a * b
//
//  Requires: -
//  Docs    : EwComplexMul.md
// =============================================================================
vec2 EwComplexMul(vec2 a, vec2 b)
{
    return vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}
