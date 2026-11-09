// =============================================================================
//  EwRaySphere - Raymarch: ray / sphere intersection
// =============================================================================
//  Analytic intersection of a ray with a sphere.
//
//  ro      : ray origin
//  rd      : ray direction, normalized
//  center  : sphere center
//  radius  : sphere radius, > 0
//  returns : vec2(tNear, tFar) along the ray, vec2(-1.0) when the ray misses.
//            tNear < 0 < tFar when ro is inside the sphere; both < 0 when
//            the sphere is behind the ray.
//
//  Requires: -
//  Docs    : EwRaySphere.md
// =============================================================================
vec2 EwRaySphere(vec3 ro, vec3 rd, vec3 center, float radius)
{
    vec3 oc = ro - center;
    float b = dot(oc, rd);
    float c = dot(oc, oc) - radius * radius;
    float h = b * b - c;
    if (h < 0.0)
    {
        return vec2(-1.0);
    }
    h = sqrt(h);
    return vec2(-b - h, -b + h);
}
