// =============================================================================
//  EwLumaRec601 - Color: luma (Rec.601)
// =============================================================================
//  Classic NTSC / Rec.601 luma weights (0.299, 0.587, 0.114).
//
//  c       : rgb color (traditionally gamma encoded)
//  returns : luma, 1.0 for white
//
//  Requires: -
//  Docs    : EwLumaRec601.md
// =============================================================================
float EwLumaRec601(vec3 c)
{
    return dot(c, vec3(0.299, 0.587, 0.114));
}
