//  media: collage
// Cover Flow Carousel - a 3D cover-flow style picture carousel on a glossy, transparent stage.
// One big picture in the center, turned and shadowed neighbours on the sides (a second set on
// ultra wide screens), soft floor reflection + contact shadows, a gently swaying camera and a
// fade to transparent at the screen edges. Slides to the next picture every Interval seconds.
//
//  iMouse.x = Side Angle | Turn of the side pictures | 0.2 | 0 .. 80 | °
//  iMouse.y = Reflection | Strength of the floor reflection | 0.5 | 0 .. 1
//  iMouse.z = Spread | Distance of the side pictures from the center | 1.0 | 1.1 .. 2.0
//  iMouse.w = Camera Height | Height of the viewpoint above the stage | 0.35 | -0.2 .. 1.0

#define SideAngle   radians(mix(0.0, 80.0, iMouse.x))
#define Reflection  mix(0.0, 1.0, iMouse.y)
#define Spread      mix(1.1, 2.0, iMouse.z)
#define CamHeight   mix(-0.2, 1.0, iMouse.w)

// --- Collage layout (set by the editor from the chosen pictures) ---
const float TileCountX = 4.0; // var: { "name": "TileCountX", "desc": "Atlas columns" }
const float TileCountY = 3.0; // var: { "name": "TileCountY", "desc": "Atlas rows" }
const float TileWidth = 384.0; // var: { "name": "TileWidth", "desc": "Tile width", "unit": "px" }
const float TileHeight = 216.0; // var: { "name": "TileHeight", "desc": "Tile height", "unit": "px" }
const float ImageWidth = 380.0; // var: { "name": "ImageWidth", "desc": "Picture width inside a tile", "unit": "px" }
const float ImageHeight = 212.0; // var: { "name": "ImageHeight", "desc": "Picture height inside a tile", "unit": "px" }
const float UniqueCount = 11.0; // var: { "name": "UniqueCount", "desc": "Number of different pictures" }

// --- Animation ---
const float Interval = 4.0; // var: { "min": 1.0, "max": 20.0, "step": 0.5, "name": "Interval", "desc": "Time between two picture changes", "unit": "s" }
const float TransitionTime = 0.9; // var: { "min": 0.2, "max": 3.0, "step": 0.05, "name": "Transition", "desc": "Duration of the slide to the next picture", "unit": "s" }
const float CamSwaySpeed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Camera Sway Speed", "desc": "Speed of the drifting camera motion (0 = still camera)", "unit": "x" }
const float CamHeading = 2.0; // var: { "min": 0.0, "max": 25.0, "step": 0.5, "name": "Camera Heading", "desc": "How far the camera swings left / right around the carousel", "unit": "°" }
const float CamTilt = 4.0; // var: { "min": 0.0, "max": 10.0, "step": 0.1, "name": "Camera Tilt", "desc": "Up / down tilt of the camera (roll is half of it)", "unit": "°" }

// --- Layout ---
const float Zoom = 4.0; // var: { "min": 2.0, "max": 6.0, "step": 0.1, "name": "Zoom", "desc": "Camera zoom (focal length)", "unit": "x" }
const float SideDepth = 1.0; // var: { "min": 0.3, "max": 2.0, "step": 0.05, "name": "Side Depth", "desc": "How far back the side pictures stand" }
const float CamDistance = 3.4; // var: { "min": 2.5, "max": 6.0, "step": 0.05, "name": "Camera Distance", "desc": "Distance of the camera from the center picture: lower = stronger perspective" }
const float WideAspect = 2.0; // var: { "min": 1.0, "max": 3.6, "step": 0.05, "name": "Wide Aspect", "desc": "Screen aspect ratio (width / height) from which a second set of side pictures appears (1 = always)", "format": "{0:2} : 1" }
const float OuterGap = 0.2; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Outer Gap", "desc": "Gap between the side pictures and the outer (ultra wide) pictures" }
const float OuterDepth = 0.6; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Outer Depth", "desc": "How much further back the outer (ultra wide) pictures stand" }
const float CornerRadius = 0.05; // var: { "min": 0.0, "max": 0.2, "step": 0.005, "name": "Corner Radius", "desc": "Rounding of the picture corners" }

// --- Look ---
const float SideShade = 0.55; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Side Shade", "desc": "Darkening of the side pictures", "scale": 100, "unit": "%" }
const float OuterShade = 0.3; // var: { "min": 0.0, "max": 0.45, "step": 0.01, "name": "Outer Shade", "desc": "Extra darkening of the outer (ultra wide) pictures", "scale": 100, "unit": "%" }
const float ShadowStrength = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shadow", "desc": "Strength of the soft floor shadows", "scale": 100, "unit": "%" }
const vec3 ShadowColor = vec3(0.0, 0.0, 0.02); // var: { "type": "col", "name": "Shadow Color", "desc": "Color of the floor shadows" }
const float ShadowSize = 1.0; // var: { "min": 0.3, "max": 3.0, "step": 0.05, "name": "Shadow Size", "desc": "Softness / spread of the floor shadows", "unit": "x" }
const float ShadowOffset = 0.06; // var: { "min": 0.0, "max": 0.3, "step": 0.01, "name": "Shadow Offset", "desc": "How far the shadows fall behind the pictures" }
const float ReflBlur = 0.12; // var: { "min": 0.0, "max": 0.4, "step": 0.01, "name": "Reflection Blur", "desc": "Diffuseness of the floor reflection" }
const float ReflFalloff = 1.8; // var: { "min": 0.5, "max": 6.0, "step": 0.1, "name": "Reflection Falloff", "desc": "How fast the reflection fades away from the floor" }
const float EdgeFade = 0.22; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Edge Fade", "desc": "Width of the fade to transparent at the screen edges" }

// --- Lighting ---
const float Gloss = 0.25; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Gloss", "desc": "Specular sheen on the pictures", "scale": 100, "unit": "%" }
const vec3 LightDir = vec3(-0.35, 0.55, 1.0); // var: { "min": -1.0, "max": 1.0, "step": 0.05, "name": "Light Direction", "desc": "Direction toward the key light (x right, y up, z toward the viewer)" }
const float Diffuse = 0.2; // var: { "min": 0.0, "max": 0.8, "step": 0.01, "name": "Diffuse Shading", "desc": "How much the pictures darken when turned away from the light", "scale": 100, "unit": "%" }
const float SpecSharpness = 24.0; // var: { "min": 4.0, "max": 128.0, "step": 1.0, "name": "Gloss Sharpness", "desc": "Size of the gloss highlight: higher = smaller, sharper highlight" }
const float TopSheen = 0.08; // var: { "min": 0.0, "max": 0.3, "step": 0.005, "name": "Top Sheen", "desc": "Soft brightening toward the top of the pictures (scaled by Gloss)" }

// --- Fine tuning ---
const float DitherStrength = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.1, "name": "Dither", "desc": "Noise against banding in soft gradients (0 = off)" }

const vec2 PicHalf = vec2(0.8, 0.45);   // 16:9 picture half size, world units
const float FloorY = -0.455;            // floor height, just below the picture bottoms

// Atlas lookup of picture idx at picture uv (0..1), clamped inside the picture.
// gx / gy: uv change per pixel (explicit gradients: correct filtering inside branches)
vec4 samplePic(float idx, vec2 uv, vec2 gx, vec2 gy)
{
    float c = mod(idx, TileCountX);
    float r = floor(idx / TileCountX);
    vec2 frac = vec2(ImageWidth / TileWidth, ImageHeight / TileHeight);
    vec2 inset = 1.0 / vec2(TileWidth, TileHeight);
    vec2 tuv = 0.5 + (clamp(uv, 0.0, 1.0) - 0.5) * frac;
    tuv = clamp(tuv, 0.5 - 0.5 * frac + inset, 0.5 + 0.5 * frac - inset);
    vec2 tiles = vec2(TileCountX, TileCountY);
    vec2 auv = (vec2(c, TileCountY - 1.0 - r) + tuv) / tiles;
    return textureGrad(iChannel0, auv, gx * frac / tiles, gy * frac / tiles);
}

// Pose of a slot at carousel offset o (0 = center)
void slotPose(float o, out vec3 c, out vec3 rgt, out vec3 n, out float shade, out float fade)
{
    float s = o < 0.0 ? -1.0 : 1.0;
    float a = min(abs(o), 1.0);
    float a2 = a * a * (3.0 - 2.0 * a);
    float e = max(abs(o) - 1.0, 0.0);
    float step2 = 2.0 * PicHalf.x * cos(SideAngle) + OuterGap;   // outer pictures: one (turned) picture width apart
    c = vec3(s * (Spread * a2 + step2 * e), 0.0, -SideDepth * a2 - OuterDepth * e);
    float ang = s * SideAngle * a2;
    n = vec3(sin(ang), 0.0, cos(ang));
    rgt = vec3(cos(ang), 0.0, -sin(ang));
    shade = 1.0 - SideShade * a2 - OuterShade * min(e, 1.0);
    // wide screens show a second set of side pictures
    float aspect = iResolution.x / max(iResolution.y, 1.0);
    float wide = smoothstep(WideAspect - 0.2, WideAspect + 0.2, aspect);
    fade = 1.0 - smoothstep(1.05 + wide, 1.75 + wide, abs(o));
}

// Ray / picture plane hit in picture-local coordinates (x along the picture, y up)
vec2 planeLocal(vec3 ro, vec3 d, vec3 c, vec3 n, vec3 rgt)
{
    float dn = dot(d, n);
    dn = abs(dn) < 1e-5 ? (dn < 0.0 ? -1e-5 : 1e-5) : dn;
    vec3 p = ro + d * (dot(c - ro, n) / dn) - c;
    return vec2(dot(p, rgt), p.y);
}

// Ray trace + shade one picture (or its floor reflection) and composite it premultiplied "over" acc
void drawSlot(float k, float pos, vec3 ro, vec3 rd, vec3 rdx, vec3 rdy, bool refl, inout vec4 acc)
{
    float o = k - pos;
    vec3 c, rgt, n; float shade, fade;
    slotPose(o, c, rgt, n, shade, fade);
    if (fade <= 0.0) return;
    float den = dot(rd, n);
    if (abs(den) < 1e-4 || (refl && den > 0.0)) return;
    float t = dot(c - ro, n) / den;
    if (t <= 0.0) return;
    vec3 p = ro + rd * t - c;
    vec2 lp = vec2(dot(p, rgt), p.y);

    // analytic pixel coverage: exact screen-space footprint of the picture plane
    // (ray differentials one pixel right / up), sd gradient per pixel by finite differences
    vec2 lpx = planeLocal(ro, rdx, c, n, rgt) - lp;
    vec2 lpy = planeLocal(ro, rdy, c, n, rgt) - lp;
    float sd  = EwSdRoundedBox2D(lp, PicHalf, CornerRadius);
    float sdx = EwSdRoundedBox2D(lp + lpx, PicHalf, CornerRadius) - sd;
    float sdy = EwSdRoundedBox2D(lp + lpy, PicHalf, CornerRadius) - sd;
    float w = max(length(vec2(sdx, sdy)), 1e-6);        // world units per pixel across the edge
    float h = refl ? max(p.y - FloorY, 0.0) : 0.0;     // height above floor (reflection)
    // smooth ~1.5 px filter (calmer than a 1 px box on slowly moving, nearly horizontal edges)
    float cov = 1.0 - smoothstep(-0.75, 0.75, sd / (w + h * ReflBlur * 0.6));
    if (cov <= 0.0) return;

    float idx = mod(k, max(UniqueCount, 1.0));
    vec2 uv = lp / (2.0 * PicHalf) + 0.5;
    vec2 gx = lpx / (2.0 * PicHalf);
    vec2 gy = lpy / (2.0 * PicHalf);
    if (den > 0.0) { uv.x = 1.0 - uv.x; gx.x = -gx.x; gy.x = -gy.x; }   // back side
    vec3 col;
    if (refl) {
        float b = h * ReflBlur * 0.8;
        vec2 bo = vec2(b / (2.0 * PicHalf.x), b / (2.0 * PicHalf.y));
        col = samplePic(idx, uv, gx, gy).rgb * 0.36
            + (samplePic(idx, uv + vec2(bo.x, bo.y * 0.5), gx, gy).rgb
            +  samplePic(idx, uv - vec2(bo.x, bo.y * 0.5), gx, gy).rgb
            +  samplePic(idx, uv + vec2(-bo.x * 0.5, bo.y), gx, gy).rgb
            +  samplePic(idx, uv + vec2(bo.x * 0.5, -bo.y), gx, gy).rgb) * 0.16;
    } else {
        col = samplePic(idx, uv, gx, gy).rgb;
    }

    // lighting: key light (default from front left above), mostly flat so the pictures stay true
    vec3 L = normalize(LightDir);
    vec3 nn = den > 0.0 ? -n : n;
    float dif = (1.0 - Diffuse) + Diffuse * max(dot(nn, L), 0.0);
    col *= shade * dif;
    // soft gloss sheen + subtle top-down gradient
    float spec = pow(max(dot(reflect(rd, nn), L), 0.0), SpecSharpness);
    col += Gloss * (0.35 * spec + TopSheen * smoothstep(-0.2, 0.45, lp.y)) * shade;

    float al = cov * fade;
    if (refl) al *= Reflection * exp(-h * ReflFalloff);
    acc = vec4(col * al, al) + acc * (1.0 - al);
}

// Painter's order: far side pictures first, center last
void drawAll(float pos, vec3 ro, vec3 rd, vec3 rdx, vec3 rdy, bool refl, inout vec4 acc)
{
    float n = floor(pos);
    float fr = pos - n;
    drawSlot(n + 3.0, pos, ro, rd, rdx, rdy, refl, acc);
    drawSlot(n - 2.0, pos, ro, rd, rdx, rdy, refl, acc);
    drawSlot(n + 2.0, pos, ro, rd, rdx, rdy, refl, acc);
    drawSlot(n - 1.0, pos, ro, rd, rdx, rdy, refl, acc);
    if (fr < 0.5) {
        drawSlot(n + 1.0, pos, ro, rd, rdx, rdy, refl, acc);
        drawSlot(n, pos, ro, rd, rdx, rdy, refl, acc);
    } else {
        drawSlot(n, pos, ro, rd, rdx, rdy, refl, acc);
        drawSlot(n + 1.0, pos, ro, rd, rdx, rdy, refl, acc);
    }
}

// Soft contact shadow of one picture on the floor
float slotShadow(float k, float pos, vec2 q)
{
    vec3 c, rgt, n; float shade, fade;
    slotPose(k - pos, c, rgt, n, shade, fade);
    vec2 a = c.xz - rgt.xz * PicHalf.x * 0.98;
    vec2 b = c.xz + rgt.xz * PicHalf.x * 0.98;
    vec2 back = -n.xz * ShadowOffset;               // light from the front: shadow falls back
    float d = EwSdSegment2D(q, a + back, b + back) / max(ShadowSize, 1e-3);
    return fade * (0.6 * exp(-d * 22.0) + 0.4 * exp(-d * 6.0));   // tight contact core + wide soft halo
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = EwCenteredUV(fragCoord, iResolution.xy);

    // carousel position: hold, then slide to the next picture
    float iv = max(Interval, 0.1);
    float tr = min(TransitionTime, iv * 0.95);
    float tt = mod(iTime, iv * max(UniqueCount, 1.0));
    float ph = mod(tt, iv);
    float f = EwQuinticFade(EwLinearStep(iv - tr, iv, ph));   // eased slide progress
    float pos = floor(tt / iv) + f;

    // camera: gentle drifting heading (orbit), pitch and roll
    float ct = mod(iTime, 3600.0) * CamSwaySpeed;
    float yaw   = radians(CamHeading) * (0.7 * sin(ct * 0.31) + 0.3 * sin(ct * 0.73 + 1.3));
    float pitch = radians(CamTilt) * (0.6 * sin(ct * 0.23 + 0.7) + 0.4 * sin(ct * 0.57 + 2.1));
    float roll  = radians(CamTilt) * 0.5 * (0.6 * sin(ct * 0.19 + 2.4) + 0.4 * sin(ct * 0.47));
    float camDist = CamDistance;
    vec3 ta = vec3(0.0, -0.2 + CamHeight * 0.15, 0.0);
    vec3 ro = ta + camDist * vec3(sin(yaw) * cos(pitch), sin(pitch), cos(yaw) * cos(pitch))
            + vec3(0.0, CamHeight + 0.2 - CamHeight * 0.15, 0.0);
    mat3 cam = EwCameraLookAt(ro, ta, roll);
    float fl = Zoom * 0.5;
    float px = 1.0 / iResolution.y;
    vec3 rd  = cam * normalize(vec3(uv, fl));
    vec3 rdx = cam * normalize(vec3(uv + vec2(px, 0.0), fl));   // ray one pixel right
    vec3 rdy = cam * normalize(vec3(uv + vec2(0.0, px), fl));   // ray one pixel up

    vec4 acc = vec4(0.0);

    // floor: shadows + diffuse reflection
    if (rd.y < 0.0 && ro.y > FloorY) {
        float tf = (FloorY - ro.y) / rd.y;
        vec3 pf = ro + rd * tf;
        float n = floor(pos);
        float keep = 1.0;
        for (int i = -2; i <= 3; i++) {
            keep *= 1.0 - ShadowStrength * clamp(slotShadow(n + float(i), pos, pf.xz), 0.0, 1.0);
        }
        float sa = 1.0 - keep;
        acc = vec4(ShadowColor * sa, sa);
        if (Reflection > 0.001) {
            // mirrored camera below the floor: same rays as the reflected ones, exact differentials
            vec3 mf = vec3(1.0, -1.0, 1.0);
            vec3 rom = vec3(ro.x, 2.0 * FloorY - ro.y, ro.z);
            vec4 rc = vec4(0.0);
            drawAll(pos, rom, rd * mf, rdx * mf, rdy * mf, true, rc);
            acc = rc + acc * (1.0 - rc.a);
        }
    }

    // the pictures
    drawAll(pos, ro, rd, rdx, rdy, false, acc);

    // fade to transparent at the screen edges
    vec2 suv = fragCoord / iResolution.xy;
    float asp = iResolution.x / iResolution.y;
    vec2 ed = min(suv, 1.0 - suv) * vec2(asp, 1.0);
    float ef = max(EdgeFade, 1e-3);
    float vig = smoothstep(0.0, ef, ed.x) * smoothstep(0.0, ef, ed.y);
    acc *= vig;

    // dither against banding in the soft gradients (premultiplied: scaled by alpha)
    acc.rgb = clamp(acc.rgb + EwDither(fragCoord, iTime, DitherStrength) * acc.a, 0.0, acc.a);
    fragColor = acc;
}
