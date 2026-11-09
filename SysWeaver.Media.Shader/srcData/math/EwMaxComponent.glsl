// =============================================================================
//  EwMaxComponent - Math: largest component of a vector
// =============================================================================
//  max over all components (vec3 and vec4 overloads).
//
//  v       : input vector
//  returns : largest component
//
//  Requires: -
//  Docs    : EwMaxComponent.md
// =============================================================================
float EwMaxComponent(vec3 v)
{
    return max(max(v.x, v.y), v.z);
}

float EwMaxComponent(vec4 v)
{
    return max(max(v.x, v.y), max(v.z, v.w));
}
