// =============================================================================
//  EwSmoothAbs - Math: smooth absolute value
// =============================================================================
//  Rounded V: equals abs(x) for |x| >= k / 2, rounded with a parabola
//  inside. Identical to -EwSmoothMin(x, -x, k). Note: EwSmoothAbs(0) = k / 4.
//
//  x       : input value
//  k       : blend width (> 0), the rounding spans |x| < k / 2
//  returns : smooth abs(x), >= abs(x)
//
//  Requires: -
//  Docs    : EwSmoothAbs.md
// =============================================================================
float EwSmoothAbs(float x, float k)
{
    float h = max(k - abs(2.0 * x), 0.0) / k;
    return abs(x) + h * h * k * 0.25;
}
