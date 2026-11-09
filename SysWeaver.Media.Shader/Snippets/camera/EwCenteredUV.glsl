// =============================================================================
//  EwCenteredUV - Camera: aspect correct centered screen coordinates
// =============================================================================
//  Origin at the screen center, y in [-0.5, 0.5], x scaled by the aspect
//  ratio (x in [-0.5 * aspect, 0.5 * aspect]).
//
//  fragCoord  : pixel coordinate (fragCoord / gl_FragCoord.xy)
//  resolution : viewport size in pixels (iResolution.xy)
//  returns    : centered, aspect correct coordinate
//
//  Requires: -
//  Docs    : EwCenteredUV.md
// =============================================================================
vec2 EwCenteredUV(vec2 fragCoord, vec2 resolution)
{
    return (fragCoord - 0.5 * resolution) / resolution.y;
}
