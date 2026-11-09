// =============================================================================
//  EwFilmGrain - Post: animated multiplicative film grain
// =============================================================================
//  Value noise grain factor around 1.0 (as in the effect template).
//
//  fragCoord : pixel coordinate (fragCoord)
//  time      : iTime (0.0 = static grain)
//  scale     : noise frequency in pixels (1.0 = about one cell per pixel)
//  amount    : grain strength, 0 = none (returns 1.0)
//  returns   : multiplier in [1 - amount / 2, 1 + amount / 2]
//
//  Requires: EwValueNoise2D
//  Docs    : EwFilmGrain.md
// =============================================================================
float EwFilmGrain(vec2 fragCoord, float time, float scale, float amount)
{
    // Large time offsets make each frame sample an unrelated noise region
    float grain = EwValueNoise2D(fragCoord * scale + vec2(121.12, 1445.23) * time);
    return grain * amount + (1.0 - amount * 0.5);
}
