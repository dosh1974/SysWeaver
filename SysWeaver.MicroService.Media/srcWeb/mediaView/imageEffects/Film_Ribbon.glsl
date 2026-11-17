//  media: collage
//  Film Ribbon: glossy 3D film strips wind across a transparent background and scroll endlessly,
//  one collage picture per film frame; a smaller, muted ribbon runs behind for depth. The image
//  always fades to full transparency at the screen borders (seamless on web pages).
//  Live controls:
//  iMouse.x = Wave | Height of the ribbon's curves | 0.5 | 0.0 .. 0.3
//  iMouse.y = Size | Height of the film strip | 0.5 | 0.2 .. 0.6
//  iMouse.z = Sepia | Old film tint of the pictures | 0.25 | 0.0 .. 1.0
//  iMouse.w = Edge Fade | Width of the fade to full transparency at the screen borders | 0.7 | 0.0 .. 1.0
#define Wave mix(0.0, 0.3, iMouse.x)
#define FilmSize mix(0.2, 0.6, iMouse.y)
#define Sepia mix(0.0, 1.0, iMouse.z)
#define EdgeFade mix(0.0, 1.0, iMouse.w)

// --- Collage atlas layout (set by the editor from the chosen pictures) ---
const float TileCountX = 4.0; // var: { "name": "Tile Count X", "desc": "Atlas columns" }
const float TileCountY = 3.0; // var: { "name": "Tile Count Y", "desc": "Atlas rows" }
const float TileCount = 12.0; // var: { "name": "Tile Count", "desc": "Tiles in the atlas" }
const float TileWidth = 384.0; // var: { "name": "Tile Width", "desc": "Tile width in pixels" }
const float TileHeight = 216.0; // var: { "name": "Tile Height", "desc": "Tile height in pixels" }
const float PageWidth = 1536.0; // var: { "name": "Page Width", "desc": "Atlas width in pixels" }
const float PageHeight = 648.0; // var: { "name": "Page Height", "desc": "Atlas height in pixels" }
const float ImageWidth = 380.0; // var: { "name": "Image Width", "desc": "Picture width in pixels" }
const float ImageHeight = 212.0; // var: { "name": "Image Height", "desc": "Picture height in pixels" }
const float UniqueCount = 11.0; // var: { "name": "Unique Count", "desc": "Different pictures" }
const float Border = 2.0; // var: { "name": "Border", "desc": "Transparent tile border in pixels" }

// --- Motion ---
const float ScrollSpeed = 0.35; // var: { "min": -2.0, "max": 2.0, "step": 0.01, "name": "Scroll Speed", "desc": "How fast the film runs (negative = backwards)", "unit": "x" }
const float WaveFreq = 2.2; // var: { "min": 0.5, "max": 6.0, "step": 0.05, "name": "Wave Frequency", "desc": "Number of curves across the screen" }
const float WaveSpeed = 0.5; // var: { "min": 0.0, "max": 2.0, "step": 0.01, "name": "Wave Speed", "desc": "How fast the curves travel" }
const float Tilt = 0.08; // var: { "min": -0.5, "max": 0.5, "step": 0.01, "name": "Tilt", "desc": "Diagonal slant of the ribbon" }
const float Twist = 0.15; // var: { "min": 0.0, "max": 0.4, "step": 0.01, "name": "Depth Swing", "desc": "How far the ribbon swings toward / away from you (perspective, keeps the pictures' shape)" }
const float FrontOffset = -0.04; // var: { "min": -0.3, "max": 0.3, "step": 0.01, "name": "Front Offset", "desc": "Vertical position of the front ribbon" }

// --- Film ---
const vec3 FilmColor = vec3(0.07, 0.05, 0.04); // var: { "type": "col", "name": "Film Color", "desc": "Color of the film base" }
const float FilmOpacity = 0.93; // var: { "min": 0.3, "max": 1.0, "step": 0.01, "name": "Film Opacity", "desc": "Opacity of the film base around the pictures", "scale": 100, "unit": "%" }
const vec3 FrameGlow = vec3(0.06, 0.036, 0.012); // var: { "type": "col", "name": "Frame Glow", "desc": "Warm glow on the film around each picture (black = off)" }
const float PictureHeight = 0.64; // var: { "min": 0.5, "max": 0.7, "step": 0.01, "name": "Picture Height", "desc": "Picture height as a share of the strip (bigger = narrower film margins)", "scale": 100, "unit": "%" }
const float FrameGap = 0.09; // var: { "min": 0.02, "max": 0.4, "step": 0.01, "name": "Frame Gap", "desc": "Film space between neighbouring pictures" }
const float HolesPerFrame = 6.0; // var: { "min": 3.0, "max": 10.0, "step": 1.0, "name": "Holes Per Frame", "desc": "Sprocket holes along each picture" }

// --- Pictures ---
const vec3 SepiaTone = vec3(1.08, 0.86, 0.62); // var: { "type": "col", "name": "Sepia Tone", "desc": "Tint color used by the Sepia control" }
const float Grain = 0.06; // var: { "min": 0.0, "max": 0.3, "step": 0.01, "name": "Grain", "desc": "Film grain on the pictures" }

// --- Light & gloss ---
const float Sheen = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Gloss", "desc": "Strength of the glossy highlights" }
const float Shading = 0.7; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shading", "desc": "3D light and shade on the curved ribbon", "scale": 100, "unit": "%" }
const vec3 LightDir = vec3(-0.35, 0.55, 0.75); // var: { "min": -1.0, "max": 1.0, "step": 0.05, "name": "Light Direction", "desc": "Direction the light comes from (x right, y up, z toward you)" }
const vec3 HighlightColor = vec3(1.0, 0.97, 0.92); // var: { "type": "col", "name": "Highlight Color", "desc": "Color of the glossy highlights" }
const vec3 BandColor = vec3(0.95, 0.97, 1.0); // var: { "type": "col", "name": "Reflection Color", "desc": "Color of the soft reflection band" }
const float BandStrength = 0.22; // var: { "min": 0.0, "max": 0.6, "step": 0.01, "name": "Reflection", "desc": "Brightness of the soft reflection band sliding along the curves" }
const float RimLight = 0.25; // var: { "min": 0.0, "max": 0.6, "step": 0.01, "name": "Rim Light", "desc": "Light catching the top edge of the strip" }
const float Curl = 0.9; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Curl", "desc": "How rounded the strip looks across its width" }
const float EdgeDarken = 0.35; // var: { "min": 0.0, "max": 0.8, "step": 0.01, "name": "Edge Darken", "desc": "Darkening of the film edges", "scale": 100, "unit": "%" }

// --- Shadow & edges ---
const float ShadowAmount = 0.45; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shadow", "desc": "Soft drop shadow under the ribbons", "scale": 100, "unit": "%" }
const float ShadowDistance = 0.12; // var: { "min": 0.0, "max": 0.4, "step": 0.01, "name": "Shadow Distance", "desc": "How far below the strip the shadow falls (apparent height)" }
const float ShadowSoftness = 0.9; // var: { "min": 0.4, "max": 1.5, "step": 0.05, "name": "Shadow Softness", "desc": "Width of the shadow's soft falloff" }
const float SideFade = 1.6; // var: { "min": 0.5, "max": 3.0, "step": 0.1, "name": "Side Fade", "desc": "Width of the left / right border fade relative to top / bottom", "unit": "x" }

// --- Back ribbon ---
const bool BackRibbon = true; // var: { "name": "Back Ribbon", "desc": "Show a second, smaller ribbon behind" }
const float BackSize = 0.55; // var: { "min": 0.3, "max": 1.0, "step": 0.01, "name": "Back Size", "desc": "Size of the back ribbon relative to the front one", "unit": "x" }
const float BackOffset = 0.2; // var: { "min": -0.5, "max": 0.5, "step": 0.01, "name": "Back Offset", "desc": "Vertical position of the back ribbon" }
const float BackDim = 0.45; // var: { "min": 0.0, "max": 0.9, "step": 0.01, "name": "Back Dim", "desc": "Darkens / mutes the back ribbon for depth", "scale": 100, "unit": "%" }
const float BackBlur = 1.2; // var: { "min": 0.0, "max": 3.0, "step": 0.1, "name": "Back Blur", "desc": "Depth of field softness of the back pictures" }
const float BackScroll = 0.6; // var: { "min": -1.0, "max": 2.0, "step": 0.05, "name": "Back Scroll", "desc": "Scroll speed of the back ribbon relative to the front (negative = opposite way)", "unit": "x" }
const float BackFreq = 1.3; // var: { "min": 0.5, "max": 2.5, "step": 0.05, "name": "Back Wave Frequency", "desc": "Curve frequency of the back ribbon relative to the front", "unit": "x" }
const float BackTilt = -0.6; // var: { "min": -2.0, "max": 2.0, "step": 0.05, "name": "Back Tilt", "desc": "Slant of the back ribbon relative to the front (negative = crossing)", "unit": "x" }
const float BackPhase = 2.1; // var: { "min": 0.0, "max": 6.28, "step": 0.05, "name": "Back Phase", "desc": "Shifts the back ribbon's wave against the front one" }
const float BackShadow = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Back Shadow", "desc": "Shadow strength of the back ribbon relative to the front", "scale": 100, "unit": "%" }
const float BackPictureShift = 5.0; // var: { "min": 0.0, "max": 20.0, "step": 1.0, "name": "Back Picture Shift", "desc": "Which picture the back ribbon starts with (so the ribbons show different pictures)" }

// Film layout in units of the strip height (derived)
const float ImgH = PictureHeight;
const float ImgW = ImgH * 16.0 / 9.0;
const float Pitch = ImgW + FrameGap;

// Width of the transparent fade at the screen borders (screen heights)
float fadeWidth() { return mix(0.04, 0.3, EdgeFade); }

// Ribbon center line at x: (y, dy/dx, depth z, dz/dx); rp = (phase, yOffset, tilt, frequency), k = path squeeze to fit the screen
vec4 ribbonAt(float x, float t, vec4 rp, float k)
{
    float f = rp.w;
    float a = x * f + t + rp.x;
    float b = x * f * 0.57 - t * 0.7 + 1.3 + rp.x * 1.7;
    float c = x * f * 0.8 - t * 0.9 + rp.x + 1.0;
    float y = k * (rp.y + Wave * (0.75 * sin(a) + 0.4 * sin(b)) + rp.z * x);
    float dy = k * (Wave * f * (0.75 * cos(a) + 0.228 * cos(b)) + rp.z);
    return vec4(y, dy, Twist * sin(c), Twist * f * 0.8 * cos(c));
}

// Samples picture idx of the atlas at local uv (0..1), clamped inside its tile (no bleeding)
vec4 samplePicture(float idx, vec2 luv, float lod)
{
    float c = mod(idx, TileCountX);
    float r = floor(idx / TileCountX);
    vec2 inset = 1.0 / vec2(ImageWidth, ImageHeight);
    luv = clamp(luv, inset, 1.0 - inset);
    vec2 s = vec2(ImageWidth / TileWidth, ImageHeight / TileHeight);
    vec2 tl = 0.5 + (luv - 0.5) * s;
    vec2 uv = vec2((c + tl.x) / TileCountX, 1.0 - (r + 1.0 - tl.y) / TileCountY);
    return textureLod(iChannel0, uv, lod);
}

// One film ribbon (strip, sprocket holes, pictures, 3D light): returns premultiplied color;
// shadow = alpha of the soft shadow it casts on what lies behind.
// rp = (wave phase, y offset, tilt, frequency), spd = scroll factor, picOff = first picture, lodBias = blur
vec4 filmLayer(vec2 p, vec2 fc, float H, vec4 rp, float spd, float picOff, float lodBias, out float shadow)
{
    float t = mod(iTime * WaveSpeed, 10.0 * EW_TAU);
    // keep the strip inside the screen: limit its size, then squeeze its path into the free room
    float fw = fadeWidth();
    float swell = 0.55 * (1.0 + Twist);
    H = min(H, (0.47 - 0.5 * fw) / swell);
    float room = max(0.5 - 0.5 * fw - swell * H, 0.0);
    float maxDev = abs(rp.y) + Wave * 1.15 + abs(rp.z) * 0.5 * iResolution.x / iResolution.y;
    float k = min(1.0, room / max(maxDev, 1e-4));
    vec4 r = ribbonAt(p.x, t, rp, k);
    float h = H * max(1.0 + r.z, 0.2);
    float nrm = inversesqrt(1.0 + r.y * r.y);
    float v = (p.y - r.x) * nrm / h;

    // soft shadow, further away when the ribbon swings toward the viewer
    float sOff = H * (ShadowDistance + 0.25 * r.z);
    float sv = (p.y - r.x + sOff) * nrm / h;
    shadow = ShadowAmount * (1.0 - smoothstep(0.2, ShadowSoftness, abs(sv)));
    if (abs(v) > 0.6) return vec4(0.0);

    // arc length in local strip heights: pictures keep their aspect along the curve
    float s = 0.0;
    float dx = p.x / 10.0;
    for (int i = 0; i < 10; i++)
    {
        vec4 q = ribbonAt((float(i) + 0.5) * dx, t, rp, k);
        s += sqrt(1.0 + q.y * q.y) / max(1.0 + q.z, 0.2);
    }
    float count = max(min(UniqueCount, TileCount), 1.0);
    float u = s * dx / H + mod(iTime * ScrollSpeed * spd, Pitch * count);
    float pxs = 1.0 / (iResolution.y * h);

    // film body + sprocket holes
    float body = EwFillAA(abs(v) - 0.5, pxs);
    float hp = Pitch / HolesPerFrame;
    vec2 hq = vec2(mod(u, hp) - 0.5 * hp, abs(v) - 0.415);
    float holes = EwFillAA(EwSdRoundedBox2D(hq, vec2(0.045, 0.034), 0.014), pxs);

    // picture frame
    float fi = floor(u / Pitch);
    float fx = u - fi * Pitch - 0.5 * Pitch;
    float imgD = EwSdRoundedBox2D(vec2(fx, v), 0.5 * vec2(ImgW, ImgH), 0.02);
    float imgMask = EwFillAA(imgD, pxs);
    float idx = mod(fi + picOff, count);
    vec2 luv = vec2(fx / ImgW + 0.5, v / ImgH + 0.5);
    // mip level from the on-screen picture size (+ blur bias for the back ribbon)
    float lod = log2(max(ImageWidth / max(ImgW * h * iResolution.y, 1.0), 1.0)) + lodBias;
    vec4 tex = samplePicture(idx, luv, lod);
    vec3 pic = mix(FilmColor, tex.rgb, tex.a);
    pic = mix(pic, EwLumaRec601(pic) * SepiaTone, Sepia);
    pic *= EwFilmGrain(fc, iTime, 1.0, Grain);

    // film base with a warm glow around each frame
    vec3 col = FilmColor;
    col += FrameGlow * exp(-max(imgD, 0.0) * 60.0);
    col = mix(col, pic, imgMask);

    // 3D: depth slope tilts the normal sideways, a gentle curl across the width
    vec3 n = normalize(vec3(-r.w * 1.8, v * Curl, 1.0));
    vec3 L = normalize(LightDir);
    float dif = max(dot(n, L), 0.0);
    vec3 hv = normalize(L + vec3(0.0, 0.0, 1.0));
    float nh = max(dot(n, hv), 0.0);
    float spec = 0.3 * pow(nh, 30.0) + 0.7 * pow(nh, 300.0);   // broad + sharp highlight
    float edge = smoothstep(0.38, 0.5, abs(v));
    col *= mix(1.0, 0.35 + 0.8 * dif, Shading) * (1.0 - EdgeDarken * edge);
    col += Sheen * spec * HighlightColor;
    // glossy clear-coat: a soft reflected light band that rides the curves
    float band = n.x * 2.2 + n.y * 0.9 + 0.12 * sin(p.x * 3.0 + 1.0);
    col += Sheen * BandStrength * exp(-band * band * 18.0) * BandColor;
    col += Sheen * RimLight * smoothstep(0.42, 0.49, v) * (1.0 - smoothstep(0.49, 0.5, v)); // lit top rim
    col = clamp(col, 0.0, 1.0);
    col = clamp(col, 0.0, 1.0);

    float a = body * (1.0 - holes) * mix(FilmOpacity, 1.0, imgMask);
    return vec4(col * a, a);
}

// Composites back ribbon, both shadows and the front ribbon, then fades to transparent at the borders
void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 p = EwCenteredUV(fragCoord, iResolution.xy);

    vec4 acc = vec4(0.0);
    if (BackRibbon)
    {
        float sb = 0.0;
        vec4 back = filmLayer(p, fragCoord, FilmSize * BackSize,
                              vec4(BackPhase, BackOffset, Tilt * BackTilt, WaveFreq * BackFreq),
                              BackScroll, BackPictureShift, BackBlur, sb);
        // mute: partly desaturate, then darken
        float l = EwLumaRec601(back.rgb);
        back.rgb = mix(back.rgb, vec3(l), BackDim * 0.6) * (1.0 - BackDim);
        acc = back + vec4(0.0, 0.0, 0.0, sb * BackShadow) * (1.0 - back.a);
    }

    float sf = 0.0;
    vec4 front = filmLayer(p, fragCoord, FilmSize, vec4(0.0, FrontOffset, Tilt, WaveFreq), 1.0, 0.0, 0.0, sf);
    acc = vec4(0.0, 0.0, 0.0, sf) + acc * (1.0 - sf);   // front shadow over the back layer
    acc = front + acc * (1.0 - front.a);                // premultiplied "over"

    // edge fade: always exactly 0 at every screen border (no hard edges when embedded)
    float fw = fadeWidth();
    vec2 ed = vec2(0.5 * iResolution.x / iResolution.y - abs(p.x), 0.5 - abs(p.y));
    float vig = smoothstep(0.0, fw * SideFade, ed.x) * smoothstep(0.0, fw, ed.y);
    fragColor = acc * vig;
}
