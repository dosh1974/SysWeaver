// =============================================================================
//  EwSdQuadraticBezier - SDF 2D: quadratic Bezier curve
// =============================================================================
//  Exact (unsigned) distance to the quadratic Bezier curve a-b-c (iq).
//  Solves the cubic for the closest curve parameter analytically.
//
//  p       : sample position
//  a       : start point
//  b       : control point
//  c       : end point
//  returns : distance >= 0 (subtract a half width for a stroked curve)
//
//  Requires: -
//  Docs    : EwSdQuadraticBezier.md
// =============================================================================
float EwSdQuadraticBezier(vec2 p, vec2 a, vec2 b, vec2 c)
{
    vec2 linearTerm = b - a;
    vec2 quadraticTerm = a - 2.0 * b + c;   // zero when b is the midpoint of a-c (division by zero)
    vec2 doubledLinearTerm = linearTerm * 2.0;
    vec2 offset = a - p;

    // Cubic coefficients of d/dt |B(t) - p|^2 = 0, normalized.
    float invQuadratic = 1.0 / dot(quadraticTerm, quadraticTerm);
    float kx = invQuadratic * dot(linearTerm, quadraticTerm);
    float ky = invQuadratic * (2.0 * dot(linearTerm, linearTerm) + dot(offset, quadraticTerm)) / 3.0;
    float kz = invQuadratic * dot(offset, linearTerm);

    // Depressed cubic t^3 + 3pt + q.
    float depressedP = ky - kx * kx;
    float depressedP3 = depressedP * depressedP * depressedP;
    float depressedQ = kx * (2.0 * kx * kx - 3.0 * ky) + kz;
    float discriminant = depressedQ * depressedQ + 4.0 * depressedP3;

    float distanceSquared;
    if (discriminant >= 0.0)
    {
        // One real root (Cardano).
        float rootDiscriminant = sqrt(discriminant);
        vec2 x = (vec2(rootDiscriminant, -rootDiscriminant) - depressedQ) * 0.5;
        vec2 cubeRoots = sign(x) * pow(abs(x), vec2(1.0 / 3.0));
        float t = clamp(cubeRoots.x + cubeRoots.y - kx, 0.0, 1.0);
        vec2 delta = offset + (doubledLinearTerm + quadraticTerm * t) * t;
        distanceSquared = dot(delta, delta);
    }
    else
    {
        // Three real roots (trigonometric); the third one can never be the closest.
        float z = sqrt(-depressedP);
        float v = acos(depressedQ / (depressedP * z * 2.0)) / 3.0;
        float m = cos(v);
        float n = sin(v) * 1.732050808;
        vec3 t = clamp(vec3(m + m, -n - m, n - m) * z - kx, 0.0, 1.0);
        vec2 deltaX = offset + (doubledLinearTerm + quadraticTerm * t.x) * t.x;
        vec2 deltaY = offset + (doubledLinearTerm + quadraticTerm * t.y) * t.y;
        distanceSquared = min(dot(deltaX, deltaX), dot(deltaY, deltaY));
    }
    return sqrt(distanceSquared);
}
