// =============================================================================
//  EwTonemapReinhard - Color: Reinhard tone mapping
// =============================================================================
//  Simple per channel Reinhard operator x / (1 + x).
//
//  x       : linear HDR rgb (>= 0)
//  returns : tone mapped linear rgb in [0, 1)
//
//  Requires: -
//  Docs    : EwTonemapReinhard.md
// =============================================================================
vec3 EwTonemapReinhard(vec3 x)
{
    return x / (1.0 + x);
}
