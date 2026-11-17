//  media: collage
//  iMouse.x = Glow | Neon bloom and beam brightness | 0.5 | 0.0 .. 2.0 | x
//  iMouse.y = Fog | Fog of war density: distance fades to transparent | 0.35 | 0.01 .. 0.15
//  iMouse.z = Matrix Tint | Turns the pictures into green terminal monochrome | 0.05 | 0.0 .. 1.0
//  iMouse.w = Hue Shift | Rotates all neon colors | 0.0 | 0.0 .. 360.0 | °

#define GlowAmount mix(0.0, 2.0, iMouse.x)
#define FogDensity mix(0.01, 0.15, iMouse.y)
#define MatrixTint mix(0.0, 1.0, iMouse.z)
#define HueShift mix(0.0, 360.0, iMouse.w)

// ---- collage layout (written by the editor) ----
const float TileCountX = 4.0; // var: { "name": "Tile Count X", "desc": "Atlas columns (set by the editor)" }
const float TileCountY = 3.0; // var: { "name": "Tile Count Y", "desc": "Atlas rows (set by the editor)" }
const float TileCount = 12.0; // var: { "name": "Tile Count", "desc": "Tiles in the atlas (set by the editor)" }
const float TileWidth = 384.0; // var: { "name": "Tile Width", "desc": "Tile width in pixels (set by the editor)", "unit": "px" }
const float TileHeight = 216.0; // var: { "name": "Tile Height", "desc": "Tile height in pixels (set by the editor)", "unit": "px" }
const float ImageWidth = 380.0; // var: { "name": "Image Width", "desc": "Picture width in pixels (set by the editor)", "unit": "px" }
const float ImageHeight = 212.0; // var: { "name": "Image Height", "desc": "Picture height in pixels (set by the editor)", "unit": "px" }

// ---- motion ----
const float FlySpeed = 2.0; // var: { "min": 0.0, "max": 8.0, "step": 0.1, "name": "Fly Speed", "desc": "Camera speed through the rain", "unit": "u/s" }
const float FallSpeed = 3.0; // var: { "min": 0.2, "max": 5.0, "step": 0.1, "name": "Fall Speed", "desc": "How fast the pictures rain down", "unit": "u/s" }
const float StreakSpeed = 7.0; // var: { "min": 1.0, "max": 20.0, "step": 0.5, "name": "Streak Speed", "desc": "Falling speed of the neon streaks", "unit": "u/s" }
const float CameraHeight = 8.0; // var: { "min": 0.8, "max": 20.0, "step": 0.1, "name": "Camera Height", "desc": "Height of the camera above the floor", "unit": "u" }
const float SpawnHeight = 16.5; // var: { "min": 3.0, "max": 40.0, "step": 0.5, "name": "Spawn Height", "desc": "Height where pictures and streaks appear (fade in) before falling", "unit": "u" }
const float CameraTilt = 10.0; // var: { "min": 0.0, "max": 35.0, "step": 0.5, "name": "Camera Tilt", "desc": "How far the camera looks down (higher = horizon closer to the top)", "unit": "°" }

// ---- scene ----
const float PanelDensity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Picture Density", "desc": "Share of lanes holding a falling picture", "scale": 100, "unit": "%" }
const float StreakDensity = 0.02; // var: { "min": 0.0, "max": 1.0, "step": 0.02, "name": "Streak Density", "desc": "Share of columns with a neon streak", "scale": 100, "unit": "%" }
const float LaserHeight = 0.4; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Dissolve Band", "desc": "Height above the floor in which the pictures break into pixels (0 = clean cut at the floor)", "unit": "u" }
const float Reflectivity = 0.6; // var: { "min": 0.0, "max": 1.5, "step": 0.05, "name": "Reflectivity", "desc": "Strength of the ground reflection" }
const float ReflectionBlur = 3.5; // var: { "min": 0.0, "max": 6.0, "step": 0.25, "name": "Reflection Blur", "desc": "How diffuse (blurry, fading with height) the floor reflection is" }
const float ImageBrightness = 1.6; // var: { "min": 0.2, "max": 3.0, "step": 0.05, "name": "Picture Brightness", "desc": "Emissive brightness of the pictures", "unit": "x" }

// ---- colors ----
const vec3 NeonColor = vec3(0.1, 1.0, 0.3); // var: { "type": "col", "name": "Neon Color", "desc": "Main neon (streaks, frames, dissolving pixels)" }
const vec3 AccentColor = vec3(0.0, 0.75, 1.0); // var: { "type": "col", "name": "Accent Color", "desc": "Second neon color" }
const vec3 LaserColor = vec3(1.0, 0.08, 0.45); // var: { "type": "col", "name": "Laser Color", "desc": "Third neon color (some streaks, frames, dissolving pixels)" }
const float Exposure = 1.3; // var: { "min": 0.3, "max": 3.0, "step": 0.05, "name": "Exposure", "desc": "Overall brightness", "unit": "x" }

// ---- output ----
const bool EnableVignette = true; // var: { "name": "Vignette", "desc": "Fade the screen edges to transparent" }
const float VignetteStrength = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Vignette Strength", "desc": "How strong the edge fade is", "scale": 100, "unit": "%" }
const int Quality = 2; // 0 fast, 1 default, 2 high

const float SliceGap = 2.45;   // depth between picture rows
const float LaneW = 3.2;      // lane width
const float PanelW = 2.6;     // picture width (16:9)

float gTime = 0.0;
vec3 gNeon = vec3(0.0);
vec3 gAccent = vec3(0.0);
vec3 gLaser = vec3(0.0);
const float FogStart = 5.0;   // fog of war begins at this distance
float gFogOff = 0.0;

vec3 neonPick(float h)
{
    return h < 0.6 ? gNeon : (h < 0.88 ? gAccent : gLaser);
}

vec4 samplePic(vec2 uv, float idx, float bias)
{
    float tile = mod(idx, max(TileCount, 1.0));
    float c = mod(tile, TileCountX);
    float r = floor(tile / TileCountX);
    vec2 sc = vec2(ImageWidth / TileWidth, ImageHeight / TileHeight);
    uv = clamp(uv, 0.003, 0.997);
    vec2 t = vec2((c + 0.5 + (uv.x - 0.5) * sc.x) / TileCountX,
                  1.0 - (r + 0.5 - (uv.y - 0.5) * sc.y) / TileCountY);
    return texture(iChannel0, t, bias);
}

// falling picture on one depth slice: rgb (HDR, not premultiplied) + opaque coverage a; glow = frame halo
vec4 panelLayer(vec3 p, float sid, float pw, float bias, out vec3 glow)
{
    glow = vec3(0.0);
    float off = (EwHash2To4(vec2(sid, 7.3)).x - 0.5) * LaneW;
    float cx = floor((p.x + off) / LaneW);
    float centerX = (cx + 0.5) * LaneW - off;
    vec4 h = EwHash2To4(vec2(cx, sid));
    if (abs(centerX) < 2.6 || h.x > PanelDensity || p.y < 0.0) return vec4(0.0);
    float ph = PanelW * 9.0 / 16.0;
    float top = SpawnHeight;
    float bottom = -ph * 0.5 - 0.05;          // sinks fully into the floor
    float cyc = (top - bottom) * 1.3;
    float fall = FallSpeed * (0.7 + 0.6 * h.y);
    float ft = gTime * fall + h.z * cyc;
    float yc = top - mod(ft, cyc);
    if (yc < bottom) return vec4(0.0);
    float cycN = floor(ft / cyc);

    vec2 q = vec2(p.x - centerX, p.y - yc);
    vec2 hs = vec2(PanelW, ph) * 0.5;
    float fr = 0.03;
    vec2 e2 = hs + fr - abs(q);
    float e = min(e2.x, e2.y);
    if (e < -0.4) return vec4(0.0);
    vec3 fcol = neonPick(fract(h.y * 3.7 + cycN * 0.37));
    float fadeIn = smoothstep(top, top - 1.5, yc);

    // blocky dissolve at the floor: each block vanishes at its own height inside the band
    const vec2 BN = vec2(40.0, 22.5);
    vec2 uv = q / (hs * 2.0) + 0.5;
    vec2 blk = floor(clamp(uv, 0.0, 0.9999) * BN);
    vec4 bh = EwHash3To4(vec3(blk, cycN + h.w * 50.0));
    float blockY = yc - hs.y + (blk.y + 0.5) / BN.y * 2.0 * hs.y;
    float thr = LaserHeight * bh.x;
    float alive = step(thr, blockY);
    float hot = alive * (1.0 - smoothstep(0.0, 0.12, blockY - thr)) * step(0.001, LaserHeight);

    // frame halo (fades out near the floor so nothing glows down the sides)
    glow = fcol * exp(-max(-e, 0.0) * 14.0) * 0.22 * GlowAmount * fadeIn
         * smoothstep(0.0, LaserHeight + 0.3, p.y) * step(e, 0.0);

    float cover = smoothstep(-pw, pw, e) * alive * fadeIn;
    if (cover <= 0.0) return vec4(0.0);
    vec3 col;
    if (max(abs(q.x) - hs.x, abs(q.y) - hs.y) > 0.0)
    {
        col = fcol * 2.5;      // neon frame
    }
    else
    {
        vec4 img = samplePic(uv, floor(h.w * 97.0 + cycN * 7.0), bias);
        vec3 lin = pow(max(img.rgb, 0.0), vec3(2.2));
        vec3 mtx = gNeon * EwLuminance(lin) * 1.8;
        lin = mix(lin, mtx, MatrixTint);
        float scan = 0.88 + 0.12 * sin(uv.y * 140.0 - gTime * 25.0);
        col = lin * ImageBrightness * scan;
    }
    // blocks about to vanish flash as digital pixels
    col = mix(col, mix(gNeon, gLaser, step(0.7, bh.y)) * (1.0 + 2.0 * bh.z), hot * 0.85);
    return vec4(col, cover);
}

// neon rain streaks on one plane
vec4 streakLayer(vec3 p, float sid, float pw, float widen)
{
    const float SW = 0.45;
    float off = EwHash2To4(vec2(sid, 3.1)).y * SW;
    float cx = floor((p.x + off) / SW);
    vec4 h = EwHash2To4(vec2(cx, sid + 0.5));
    if (h.x > StreakDensity || p.y < 0.0) return vec4(0.0);
    float colX = (cx + 0.2 + 0.6 * h.y) * SW - off;
    float d = abs(p.x - colX);
    float spd = StreakSpeed * (0.6 + 0.8 * h.z);
    float cyc = SpawnHeight + 6.0;
    float head = SpawnHeight + 1.5 - mod(gTime * spd + h.w * cyc, cyc);
    float len = 1.0 + 3.0 * h.y;
    float s = p.y - head;
    if (s < -0.4 || s > len) return vec4(0.0);
    float tail = clamp(1.0 - s / len, 0.0, 1.0);
    tail *= tail;
    float headF = exp(-abs(s) * 7.0);
    float seg = 0.55 + 0.45 * step(0.3, fract(p.y * 5.0 + h.w * 13.0));
    float w = 0.007 + pw + widen;
    float core = 1.0 - smoothstep(0.0, w, d);
    float halo = exp(-d / (w * 4.0 + 0.02));
    vec3 c = neonPick(h.z);
    vec3 rgb = c * (core * (tail * 1.6 * seg + headF * 3.0) + halo * GlowAmount * (tail * 0.3 + headF * 0.7))
             + vec3(core * headF * 2.0);
    float a = clamp(max(rgb.r, max(rgb.g, rgb.b)) * 0.7, 0.0, 1.0);
    return vec4(rgb, a);
}

// slices front to back: pictures occlude (T), streaks add light; fog fades to transparent
// hFade > 0 (reflections): fainter and blurrier the higher above the floor
vec4 traceScene(vec3 ro, vec3 rd, float tMax, int n, float bias, float widen, float pixA, float hFade, out float T)
{
    vec4 acc = vec4(0.0);
    T = 1.0;
    if (rd.z < 0.02) return acc;
    float hg = SliceGap * 0.5;
    float j0 = floor(ro.z / hg) + 1.0;
    for (int i = 0; i < 64; i++)
    {
        if (i >= n) break;
        float j = j0 + float(i);
        float t = (j * hg - ro.z) / rd.z;
        if (t > tMax) break;
        float fog = exp(-max(t + gFogOff - FogStart, 0.0) * FogDensity);
        if (fog < 0.015) break;
        vec3 p = ro + rd * t;
        float sid = mod(j, 4096.0);
        float pw = t * pixA;
        float hy = max(p.y, 0.0) * hFade;
        float att = fog * exp(-hy * 1.2);
        float wd = widen * (1.0 + hy * 4.0);
        vec4 P = vec4(0.0);
        vec3 glow = vec3(0.0);
        if (mod(j, 2.0) < 0.5)
        {
            P = panelLayer(p, sid, pw + wd * 4.0, bias + hy * 2.0, glow);
        }
        vec4 S = streakLayer(p, sid, pw, wd);
        vec3 light = P.rgb * P.a + (S.rgb + glow) * (1.0 - P.a);
        float la = P.a + clamp(S.a + max(glow.r, max(glow.g, glow.b)), 0.0, 1.0) * (1.0 - P.a);
        acc.rgb += T * att * light;
        acc.a += T * att * la * (1.0 - acc.a);
        T *= 1.0 - P.a;
        if (T < 0.02) break;
    }
    return acc;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    gTime = mod(iTime, 3600.0);
    float hr = radians(HueShift);
    gNeon = max(EwHueRotate(NeonColor, hr), 0.0);
    gAccent = max(EwHueRotate(AccentColor, hr), 0.0);
    gLaser = max(EwHueRotate(LaserColor, hr), 0.0);

    int n = Quality == 0 ? 20 : (Quality == 1 ? 32 : 48);
    int nr = Quality == 0 ? 8 : (Quality == 1 ? 14 : 22);

    vec2 uv = EwCenteredUV(fragCoord, iResolution.xy);
    const float focal = 1.2;
    float pixA = 1.0 / (iResolution.y * focal);
    vec3 ro = vec3(sin(gTime * 0.37) * 0.9, CameraHeight + 0.3 * sin(gTime * 0.23), gTime * FlySpeed);
    vec3 ta = ro + vec3(sin(gTime * 0.29) * 0.7, -4.0 * tan(radians(CameraTilt)) + 0.12 * sin(gTime * 0.17), 4.0);
    mat3 cam = EwCameraLookAt(ro, ta, 0.05 * sin(gTime * 0.31));
    vec3 rd = cam * normalize(vec3(uv, focal));

    float tG = rd.y < -1e-4 ? -ro.y / rd.y : 1e9;
    float T = 1.0;
    vec4 acc = traceScene(ro, rd, tG, n, 0.0, 0.0, pixA, 0.0, T);

    if (tG < 1e8 && T > 0.01)
    {
        // plain dark floor: only a soft, diffuse reflection of the scene
        vec3 gp = ro + rd * tG;
        float fog = exp(-max(tG - FogStart, 0.0) * FogDensity);
        vec3 rrd = vec3(rd.x, -rd.y, rd.z);
        float rT = 1.0;
        gFogOff = FogStart;   // reflected path continues the fog of the floor point
        vec4 refl = traceScene(gp, rrd, 1e9, nr, ReflectionBlur, 0.004 * ReflectionBlur, pixA * 1.5, 0.08 * ReflectionBlur, rT);
        float fres = 0.25 + 0.75 * pow(1.0 - clamp(-rd.y, 0.0, 1.0), 5.0);
        vec3 gcol = vec3(0.004) + refl.rgb * Reflectivity * fres;
        acc.rgb += T * fog * gcol;
        acc.a += T * fog;
    }

    vec3 c = EwTonemapAces(max(acc.rgb, 0.0) * Exposure);
    float a = clamp(max(acc.a, max(c.r, max(c.g, c.b))), 0.0, 1.0);
    vec3 un = clamp(c / max(a, 1e-4), 0.0, 1.0);
    un = EwLinearToSrgb(un);
    fragColor = vec4(un * a, a);
    if (EnableVignette)
    {
        fragColor *= EwVignette(fragCoord / iResolution.xy, 16.0, 0.35, VignetteStrength);
    }
}
