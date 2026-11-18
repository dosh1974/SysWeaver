//  media: image stock:NitroFusion2_00001_.png
// Silver Keepsake: a polished silver photo frame with pearl beading and corner rosettes,
// standing on a glossy satin surface that dissolves into transparent fog.
//  iMouse.x = Camera Sway | How far the camera drifts around its head-on view | 0.5 | 0.0 .. 2.0 | x
//  iMouse.y = Fog Reach | Radius of the visible floor before it fades into transparent fog | 0.5 | 1.5 .. 6.0
//  iMouse.z = Floor Gloss | Strength of the mirror-like reflection in the floor | 0.6 | 0.0 .. 1.0 | {0:2}
//  iMouse.w = Light Angle | Direction of the key light, left to right | 0.35 | -1.2 .. 1.2 | rad

const float Speed = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Speed", "desc": "Speed of the camera drift and fog motion", "unit": "x" }
const vec3 FrameColor = vec3(0.92, 0.92, 0.95); // var: { "type": "col", "name": "Frame Color", "desc": "Metal tint of the frame (silver by default)" }
const float BorderWidth = 0.2; // var: { "min": 0.1, "max": 0.35, "step": 0.01, "name": "Border Width", "desc": "Width of the frame moulding" }
const float OrnamentDensity = 1.0; // var: { "min": 0.5, "max": 2.0, "step": 0.05, "name": "Ornament Density", "desc": "How closely the pearl beads are packed", "unit": "x" }
const bool Ornaments = true; // var: { "name": "Ornaments", "desc": "Show pearl beading, corner rosettes and crests" }
const vec3 FloorColor = vec3(0.10, 0.10, 0.11); // var: { "type": "col", "name": "Floor Color", "desc": "Base color of the satin floor" }
const float FloorBlur = 2.5; // var: { "min": 0.0, "max": 8.0, "step": 0.1, "name": "Floor Blur", "desc": "How quickly the reflection blurs and fades with height" }
const float Exposure = 1.1; // var: { "min": 0.4, "max": 2.5, "step": 0.05, "name": "Exposure", "desc": "Overall brightness" }
const float PictureGloss = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Picture Gloss", "desc": "Strength of the glossy reflection and glare on the picture (0 = matte print)", "unit": "x" }
const float GlossFalloff = 3.4; // var: { "min": 0.5, "max": 15.0, "step": 0.1, "name": "Gloss Falloff", "desc": "Camera angle off the head-on view at which the picture gloss reaches full strength: low = appears quickly, high = only at strong angles", "unit": "°" }
const float VignetteStrength = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Vignette Strength", "desc": "Transparency at the screen border (1 = fully transparent edges, 0 = off)", "scale": 100, "unit": "%" }
const float VignetteWidth = 0.08; // var: { "min": 0.01, "max": 0.3, "step": 0.01, "name": "Vignette Width", "desc": "Width of the fade inwards from every screen edge (fraction of the screen height); the camera keeps the frame inside the opaque area", "scale": 100, "unit": "%" }
const float VignetteCorner = 0.2; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Vignette Corner", "desc": "Rounding of the vignette corners: 0 = square, higher = rounder, softer corners (fraction of the screen height)", "scale": 100, "unit": "%" }

#define CameraSway mix(0.0, 2.0, iMouse.x)
#define FogReach mix(1.5, 6.0, iMouse.y)
#define FloorGloss mix(0.0, 1.0, iMouse.z)
#define LightAngle mix(-1.2, 1.2, iMouse.w)

const float FrameDepth = 0.12;

vec2 gPhotoH = vec2(0.7);   // half size of the visible picture
vec2 gOuterH = vec2(0.9);   // outer half size of the frame
float gFloorY = -0.9;
float gSheen = 0.0;   // picture gloss strength (0 when the camera is head on)
// per-pixel cache of shadow / AO from the centre sample, reused by the AA sub-samples
bool gUseCache = false;
float gCSh = 1.0, gCAo = 1.0, gCMat = -1.0, gCRough = 0.04;
float gSheenShift = 0.0;   // position of the glare band (follows the camera)
vec3 gLight = vec3(0.0, 1.0, 0.0);

// ---------------------------------------------------------------- scene
float frameSdf(vec3 p)
{
    float B = BorderWidth;
    float D = FrameDepth;
    vec2 q = p.xy;
    float u = -EwSdBox2D(q, gOuterH);      // distance inwards from the outer edge (mitred corners)
    vec2 pr = vec2(u, p.z);

    // conservative bound: far from the moulding ring (in xy or z) skip the detailed shape
    float zMax = D * 0.6 + 0.012 + 0.2 * B;
    float dRing = max(max(-u, u - B), max(p.z - zMax, -0.04 - p.z)) - 0.012;
    if (dRing > 0.15) return dRing;   // > AO radius: AO / normals stay exact

    float zTop = D * 0.6;
    float body = EwSdRoundedBox2D(pr - vec2(B * 0.5, (zTop - 0.03) * 0.5), vec2(B * 0.5, (zTop + 0.03) * 0.5), 0.015);
    float roll = length(pr - vec2(B * 0.17, zTop + 0.005)) - B * 0.16;
    float lip  = length(pr - vec2(B * 0.9, zTop * 0.7)) - B * 0.1;
    float d = EwSmoothMin(body, roll, 0.02);
    d = EwSmoothMin(d, lip, 0.015);

    if (Ornaments)
    {
        float u0 = B * 0.55;
        float zb = zTop + 0.004;
        // pearl beading along the moulding
        vec2 aq = abs(q) - gOuterH;
        float s = aq.x > aq.y ? q.y : q.x;
        float sp = B * 0.26 / OrnamentDensity;
        float ls = s - sp * floor(s / sp + 0.5);
        float pearl = length(vec3(ls, u - u0, (p.z - zb) * 1.3)) - sp * 0.34;
        d = min(d, pearl * 0.75);

        // corner rosettes (skipped where a lower bound shows they can't change d: exact)
        vec3 cq = vec3(abs(q) - (gOuterH - vec2(u0)), p.z - zb);
        float rr = B * 0.36;
        if (0.4 * (length(cq.xy) - rr) < d + 0.01)
        {
            float ang = atan(cq.y, cq.x);
            float petal = 0.78 + 0.22 * cos(8.0 * ang);
            float ros = (length(vec3(cq.xy / petal, cq.z * 2.2)) - rr) * 0.4;
            ros = max(ros, -(length(cq - vec3(0.0, 0.0, rr * 0.45)) - rr * 0.22)); // dimple
            float bud = length(cq - vec3(0.0, 0.0, rr * 0.3)) - rr * 0.17;
            d = EwSmoothMin(d, min(ros, bud), 0.01);
        }

        // shell crests at the top and bottom centre (same exact skip)
        vec3 c = vec3(q.x, abs(q.y) - (gOuterH.y - u0), p.z - zb);
        if ((length(c) / (B * 0.75) - 1.0) * B * 0.18 - 0.0036 < d + 0.012)
        {
            vec3 e = c / vec3(B * 0.75, B * 0.36, B * 0.2);
            float crest = (length(e) - 1.0) * B * 0.18;
            crest += 0.0035 * cos(atan(c.x, c.y + B * 0.4) * 11.0) * smoothstep(0.0, 0.3, length(e));
            d = EwSmoothMin(d, crest, 0.012);
        }
    }
    return d;
}

vec2 map(vec3 p)
{
    vec2 res = vec2(frameSdf(p), 1.0);
    // picture (backing board), front face at z = 0.005
    float photo = EwSdBox3D(p - vec3(0.0, 0.0, -0.02), vec3(gPhotoH + BorderWidth * 0.5, 0.025));
    if (photo < res.x) res = vec2(photo, 2.0);
    // easel leg behind
    float leg = EwSdCapsule(p, vec3(0.0, gOuterH.y * 0.2, -0.06), vec3(0.0, gFloorY + 0.02, -0.75), 0.03);
    if (leg < res.x) res = vec2(leg, 3.0);
    return res;
}

#define EW_SDF(p) map(p).x

// tetrahedral normal (same samples as EwCalcNormal) as a loop: map() is inlined once (compile time)
vec3 calcNormalLoop(vec3 p, float eps)
{
    vec3 n = vec3(0.0);
    for (int i = min(iFrame, 0); i < 4; i++)
    {
        vec3 e = vec3(float(((i + 3) >> 1) & 1), float((i >> 1) & 1), float(i & 1)) * 2.0 - 1.0;
        n += e * map(p + e * eps).x;
    }
    return normalize(n);
}

// ray vs bounding box of the frame + leg, returns (tNear, tFar)
vec2 boxHit(vec3 ro, vec3 rd)
{
    vec3 bmin = vec3(-gOuterH.x - 0.05, gFloorY - 0.05, -0.85);
    vec3 bmax = vec3(gOuterH.x + 0.05, gOuterH.y + 0.05, FrameDepth + 0.05);
    vec3 inv = 1.0 / (abs(rd) + 1e-6) * sign(rd + 1e-7);
    vec3 t0 = (bmin - ro) * inv;
    vec3 t1 = (bmax - ro) * inv;
    vec3 tmin = min(t0, t1);
    vec3 tmax = max(t0, t1);
    return vec2(max(max(tmin.x, tmin.y), tmin.z), min(min(tmax.x, tmax.y), tmax.z));
}

float gPix = 0.0005;   // pixel footprint per unit of ray distance

// sphere trace; also records the closest near-miss (local minimum of the distance)
// returns (hit t, material, near-miss t, near-miss distance in pixels)
vec4 march(vec3 ro, vec3 rd, int steps)
{
    vec2 bb = boxHit(ro, rd);
    if (bb.y < max(bb.x, 0.0)) return vec4(-1.0, 0.0, -1.0, 1e9);
    float t = max(bb.x, 0.001);
    float h1 = 1e9, h2 = 1e9, t1 = t;
    float eT = -1.0, eR = 1e9;
    for (int i = 0; i < 30; i++)   // 30 steps are enough: rays start at the scene box
    {
        if (i >= steps) break;
        vec2 h = map(ro + rd * t);
        if (h.x < 0.0004 * t) return vec4(t, h.y, eT, eR);
        if (h1 < h2 && h1 <= h.x)
        {
            float r = h1 / (gPix * t1);
            if (r < eR) { eR = r; eT = t1; }
        }
        h2 = h1; h1 = h.x; t1 = t;
        t += h.x * 0.85;
        if (t > bb.y) break;
    }
    if (h1 < h2)
    {
        float r = h1 / (gPix * t1);
        if (r < eR) { eR = r; eT = t1; }
    }
    return vec4(-1.0, 0.0, eT, eR);
}

// ---------------------------------------------------------------- lighting
// w: roughness, widens (and dims) the light lobes so sub-pixel highlights don't sparkle
vec3 envLight(vec3 d, float w)
{
    // studio: dark floor below, soft gradient above, horizon strip + softboxes
    vec3 col = mix(vec3(0.03, 0.03, 0.035), vec3(0.3, 0.31, 0.34), smoothstep(-0.15 - w, 0.6 + w, d.y));
    float sw = 0.12 + w * 0.5;
    col += vec3(0.9) * (0.12 / sw) * smoothstep(sw, 0.0, abs(d.y - 0.18)) * smoothstep(-0.2, 0.6, d.z);
    float bw = w * 0.6;
    col += vec3(2.5, 2.4, 2.2) * (0.09 / (0.09 + bw)) * smoothstep(0.88 - bw, 0.97, dot(d, gLight));
    col += vec3(1.2, 1.25, 1.4) * (0.08 / (0.08 + bw)) * smoothstep(0.9 - bw, 0.98, dot(d, normalize(vec3(0.9, 0.5, 0.6))));
    col += vec3(1.0, 1.0, 1.05) * (0.1 / (0.1 + bw)) * smoothstep(0.85 - bw, 0.95, dot(d, normalize(vec3(-0.7, 0.3, 0.8))));
    return col;
}

// the picture extends a little under the frame lip: its own border is never visible
const float PhotoBleed = 1.04;

vec3 photoColor(vec2 q, float lod)
{
    vec2 uv = clamp(q / (gPhotoH * PhotoBleed) * 0.5 + 0.5, 0.0, 1.0);
    vec4 c = textureLod(iChannel0, uv, lod);
    vec3 rgb = mix(vec3(0.85), c.rgb, c.a);           // transparent pictures sit on white card
    return pow(rgb, vec3(2.2));
}

// foot: pixel footprint at p (scene units), lod: picture mip level
vec3 shade(vec3 p, vec3 rd, float mat, bool cheap, float lod, float foot)
{
    // normal filtered over the pixel footprint (pre-filtered geometry)
    float e = max(0.0005, foot * 0.6);
    // picture face far from the moulding: flat, unoccluded -> exact analytic normal, AO = 1
    bool flatPhoto = mat > 1.5 && mat < 2.5 && p.z > 0.0
                  && -EwSdBox2D(p.xy, gOuterH) - BorderWidth - 0.012 > 0.15;
    vec3 n = flatPhoto ? vec3(0.0, 0.0, 1.0) : calcNormalLoop(p, e);
    vec3 v = -rd;
    float ndl = max(dot(n, gLight), 0.0);
    float sh = 1.0;
    float ao = 1.0;
    if (!cheap)
    {
        if (gUseCache && abs(gCMat - mat) < 0.5)
        {
            // AA sub-sample on the same surface as the pixel centre: shadow / AO are
            // smooth over a pixel, reuse the centre's values (saves 2 x 5 shadow+AO per pixel)
            sh = gCSh;
            ao = flatPhoto ? 1.0 : gCAo;
        }
        else
        {
            // nothing exists outside the scene box: stop the shadow ray where it leaves it
            float tEx = clamp(boxHit(p, gLight).y, 0.02, 4.0);
            sh = EwSoftShadow(p + n * 0.003, gLight, 0.01, tEx, 14.0);
            if (!flatPhoto)
            {
                ao = EwAmbientOcclusion(p, n);
                ao = 0.35 + 0.65 * clamp(ao, 0.0, 1.0);
            }
            if (!gUseCache) { gCSh = sh; gCAo = ao; gCMat = mat; }
        }
    }
    vec3 col;
    if (mat < 1.5)
    {
        // polished silver; roughness from the normal variation inside the pixel
        float rough = 0.04;
        if (!cheap)
        {
            if (gUseCache && gCMat < 1.5) rough = gCRough;   // footprint property: same for the whole pixel
            else
            {
                vec3 nW = calcNormalLoop(p, e * 2.5);
                rough += clamp(length(n - nW) * 1.6, 0.0, 1.0);
                if (!gUseCache) gCRough = rough;
            }
        }
        else rough = 0.3;
        vec3 r = reflect(rd, n);
        vec3 f0 = FrameColor * vec3(0.97, 0.96, 0.93);
        vec3 F = EwFresnelSchlick(max(dot(n, v), 0.0), f0);
        vec3 env = envLight(r, rough);
        // the floor seen in the silver: dark glossy
        env = mix(env, FloorColor * 0.6, smoothstep(0.0, -0.25 - rough, r.y));
        float sp = 90.0 / (1.0 + rough * 40.0);
        float spec = pow(max(dot(r, gLight), 0.0), sp) * 6.0 * (sp + 2.0) / 92.0 * sh;
        col = F * (env * ao + spec) + f0 * 0.04 * ndl * sh;
    }
    else if (mat < 2.5)
    {
        vec3 alb = n.z > 0.5 ? photoColor(p.xy, lod) : vec3(0.05);
        vec3 h = normalize(gLight + v);
        float spec = pow(max(dot(n, h), 0.0), 60.0) * 0.25;
        col = alb * (0.45 * ao + 1.0 * ndl * sh) + spec * sh * 0.3;
        col += 0.02 * envLight(reflect(rd, n), 0.1);
        // glossy coating: a soft studio reflection that appears only when seen at an angle
        if (gSheen > 0.0 && n.z > 0.5)
        {
            vec3 r = reflect(rd, n);
            float fr = EwFresnelSchlick(max(dot(n, v), 0.0), 0.04);
            // broad diagonal window glare that slides across the glass with the camera
            float s = dot(p.xy, vec2(0.8, 0.6)) + gSheenShift;
            float band = exp(-s * s * 3.0) + 0.5 * exp(-(s - 0.7) * (s - 0.7) * 25.0);
            vec3 gl = envLight(r, 0.3) * fr * 2.0 + vec3(0.95, 0.97, 1.0) * band * 0.09;
            col += gl * gSheen * (0.6 + 0.4 * ao);
        }
    }
    else
    {
        vec3 alb = vec3(0.05, 0.035, 0.025);
        col = alb * (0.3 * ao + ndl * sh);
    }
    return col;
}

vec3 toDisplay(vec3 c)
{
    return EwLinearToSrgb(EwTonemapAces(c * Exposure));
}

// ---------------------------------------------------------------- render one sample
vec3 gRo = vec3(0.0);
mat3 gCam = mat3(1.0);
float gFocal = 2.0;
float gT = 0.0;
bool gAllowEdge = true;   // pixel inside the scene's screen bounding box
float gTexPerUnit = 1.0;

// premultiplied display color of one sub-pixel sample; id: surface id; edge: near a geometric edge
vec4 renderSample(vec2 fragCoord, out float id, out bool edge)
{
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / iResolution.y;
    vec3 ro = gRo;
    vec3 rd = gCam * normalize(vec3(uv, gFocal));
    id = 0.0;
    edge = false;

    vec4 hit = march(ro, rd, 120);
    float tF = rd.y < -1e-4 ? (gFloorY - ro.y) / rd.y : -1.0;
    // the ray grazed a surface closer than ~2 px: silhouette pixel
    if (hit.z > 0.0 && hit.w < 2.0 && (tF < 0.0 || hit.z < tF)) edge = true;

    vec3 col = vec3(0.0);
    float alpha = 0.0;
    if (hit.x > 0.0 && (tF < 0.0 || hit.x < tF))
    {
        vec3 p = ro + rd * hit.x;
        float foot = gPix * hit.x;
        id = hit.y;
        float ew = 2.5 * foot;
        // the ornate silver frame: always supersampled (fine detail + sharp highlights)
        if (hit.y < 1.5) edge = true;
        else if (hit.y < 2.5)
        {
            if (frameSdf(p) < ew) edge = true;
        }
        col = toDisplay(shade(p, rd, hit.y, false, log2(max(foot * gTexPerUnit, 1.0)), foot));
        alpha = 1.0;
    }
    else if (tF > 0.0)
    {
        vec3 p = ro + rd * tF;
        vec3 n = vec3(0.0, 1.0, 0.0);
        id = 4.0;
        // footprint along the floor (stretched at grazing angles)
        float footF = gPix * tF / max(-rd.y, 0.05);
        if (gAllowEdge && frameSdf(p) < 2.5 * gPix * tF) edge = true;    // contact line with the frame
        // fog of war: soft cloudy edge drifting slowly
        // (the noise term lies in [-0.27, 0.31] * R: outside that band alpha is known exactly, skip the fbm)
        float dz = p.z - 0.3;
        float dist = length(vec2(p.x * 0.7, dz * (dz > 0.0 ? 0.3 : 1.0)));
        float R = FogReach;
        alpha = dist > 1.28 * R ? 0.0 : 1.0;
        if (dist > 0.14 * R && dist <= 1.28 * R)
        {
            float fn = EwFbm2D(p.xz * 0.9 + vec2(gT * 0.05, -gT * 0.03), 5);
            alpha = 1.0 - smoothstep(R * 0.45, R, dist + (fn - 0.45) * R * 0.6);
        }

        if (alpha > 0.002)
        {
            float sh = 1.0;
            float ao = 1.0;
            if (gUseCache && gCMat > 3.5)
            {
                sh = gCSh; ao = gCAo;      // AA sub-sample: reuse the centre's floor shadow / AO
            }
            else
            {
                vec2 sb = boxHit(p, gLight);
                if (sb.y > max(sb.x, 0.0))
                    sh = EwSoftShadow(p + n * 0.002, gLight, 0.01, clamp(sb.y, 0.02, 4.0), 10.0);
                ao = EwAmbientOcclusion(p, n);
                if (!gUseCache) { gCSh = sh; gCAo = ao; gCMat = 4.0; }
            }
            float ndl = max(gLight.y, 0.0);
            vec3 base = FloorColor * (0.25 * ao + 0.9 * ndl * sh);
            // satin texture, faded out where it would be finer than a pixel
            float texFade = 1.0 - smoothstep(0.15, 0.5, footF * 14.0);
            base *= 1.0 + texFade * 0.16 * (EwFbm2D(p.xz * vec2(14.0, 3.0), 3) - 0.45);
            vec3 h = normalize(gLight - rd);
            base += vec3(0.5) * pow(max(h.y, 0.0), 40.0) * sh * 0.4;

            // semi diffused reflection: wavy satin normal, blur + fade with height
            float wobFade = 1.0 - smoothstep(0.1, 0.4, footF * 9.0);
            vec2 wob = (vec2(EwFbm2D(p.xz * 9.0, 2), EwFbm2D(p.xz * 9.0 + 7.3, 2)) - 0.375) * wobFade;
            vec3 rr = normalize(reflect(rd, n) + vec3(wob.x, 0.0, wob.y) * 0.02);
            float fres = EwFresnelSchlick(max(-rd.y, 0.0), 0.06);
            vec3 refl = envLight(rr, 0.2) * 0.12;
            vec2 rh = march(p + n * 0.002, rr, 70).xy;
            if (rh.x > 0.0)
            {
                vec3 q = p + rr * rh.x;
                float hgt = max(q.y - gFloorY, 0.0);
                float blur = hgt * FloorBlur;
                float foot = gPix * (tF + rh.x) * (1.0 + blur * 8.0);
                vec3 rc = shade(q, rr, rh.y, true, 1.5 + blur * 3.0, foot);
                float rAmt = exp(-blur * 1.3) * 0.9;
                refl = mix(refl, rc * 0.65, rAmt);
            }
            float k = FloorGloss * (0.25 + 0.75 * fres);
            col = base + refl * k;
            col *= ao * 0.4 + 0.6;
        }
        col = toDisplay(col);
    }
    return vec4(col * alpha, alpha);
}

// ---------------------------------------------------------------- main
void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    gT = mod(iTime * Speed, 628.3185307);

    // picture size from its aspect
    vec2 ts = vec2(textureSize(iChannel0, 0));
    float asp = (ts.x > 0.5 && ts.y > 0.5) ? ts.x / ts.y : 1.0;
    asp = clamp(asp, 0.25, 4.0);
    gPhotoH = vec2(asp, 1.0) * min(0.82, 1.25 / asp);
    gOuterH = gPhotoH + BorderWidth;
    gFloorY = -gOuterH.y;
    float la = LightAngle;
    gLight = normalize(vec3(sin(la) * 1.2, 1.3, 0.6 + cos(la) * 0.8));

    // camera: nearly head on, gentle drift; zoomed so the frame nearly fills the view
    float sway = CameraSway;
    float camD = 5.6;
    gRo = vec3(sway * 0.4 * sin(gT * 0.23), 0.1 + sway * 0.12 * sin(gT * 0.17 + 1.0), camD);
    vec3 ta = vec3(sway * 0.05 * sin(gT * 0.19 + 2.0), -0.12, 0.0);
    float aspect = iResolution.x / iResolution.y;
    // keep the frame inside the opaque part of the vignette: the fade band (m) is reserved
    // around the frame's top and sides, so the edges can be fully transparent without cutting it
    float vm = VignetteWidth * VignetteStrength;
    gFocal = (0.5 * aspect - vm) * camD / (gOuterH.x * 1.08 + 0.05);
    gFocal = min(gFocal, (0.5 - vm) * camD / (gOuterH.y + 0.12 + 0.07));
    gFocal = min(gFocal, 3.0);
    gCam = EwCameraLookAt(gRo, ta, 0.0);
    gPix = 1.0 / (iResolution.y * gFocal);
    gTexPerUnit = max(ts.x, ts.y) / (2.0 * max(gPhotoH.x, gPhotoH.y) * PhotoBleed);
    // screen space bounding box of the scene (frame + leg) projected from its 8 corners:
    // pixels outside it (plus a 2 px margin) can't contain an edge -> single sample, no edge tests
    vec3 bmin = vec3(-gOuterH.x - 0.05, gFloorY - 0.05, -0.85);
    vec3 bmax = vec3(gOuterH.x + 0.05, gOuterH.y + 0.05, FrameDepth + 0.05);
    vec2 smin = vec2(1e9), smax = vec2(-1e9);
    for (int k = min(iFrame, 0); k < 8; k++)
    {
        vec3 cw = vec3((k & 1) == 0 ? bmin.x : bmax.x, (k & 2) == 0 ? bmin.y : bmax.y, (k & 4) == 0 ? bmin.z : bmax.z);
        vec3 cc = (cw - gRo) * gCam;                       // camera space (right, up, forward)
        vec2 sp = cc.xy / max(cc.z, 1e-3) * gFocal * iResolution.y + 0.5 * iResolution.xy;
        smin = min(smin, sp);
        smax = max(smax, sp);
    }
    gAllowEdge = all(greaterThan(fragCoord, smin - 2.0)) && all(lessThan(fragCoord, smax + 2.0));

    // picture gloss: only when the camera looks at the picture at an angle (0 head on)
    float offAxis = length(gRo.xy - vec2(0.0, 0.1)) / camD;
    float fullAt = tan(radians(GlossFalloff));
    gSheen = PictureGloss * smoothstep(fullAt * 0.067, fullAt, offAxis);
    gSheenShift = gRo.x * 4.0 - (gRo.y - 0.1) * 2.0;

    // vignette to transparent: a rounded-rectangle fade inwards from the screen border, so every
    // edge (and corner) reaches the full Vignette Strength exactly at the border (any aspect)
    vec2 vh = 0.5 * iResolution.xy / iResolution.y;                 // half screen, height units
    vec2 vp = (fragCoord - 0.5 * iResolution.xy) / iResolution.y;
    float vr = min(VignetteCorner, min(vh.x, vh.y));
    float vd = -EwSdRoundedBox2D(vp, vh, vr);                       // distance inside the border
    float vt = clamp(vd / max(VignetteWidth, 1e-3), 0.0, 1.0);
    vt = vt * vt * (3.0 - 2.0 * vt);
    float vig = 1.0 - VignetteStrength * (1.0 - vt);
    if (vig <= 0.0) { fragColor = vec4(0.0); return; }     // fully faded: skip the rendering

    // edge-only multisampling: the centre sample, plus 4 rotated-grid samples where geometry meets
    // (a real loop, not unrolled: keeps the compile time short)
    vec4 c = vec4(0.0);
    float cnt = 0.0;
    bool edge = false;
    vec2 off = vec2(0.125, 0.375);
    int n = 5 + min(iFrame, 0);
    for (int i = 0; i < n; i++)
    {
        if (i > 0 && !edge) break;
        float id;
        bool e;
        vec2 o = i == 0 ? vec2(0.0) : off;
        c += renderSample(fragCoord + o, id, e);
        cnt += 1.0;
        if (i == 0) { edge = e && gAllowEdge; gUseCache = true; }
        else off = vec2(-off.y, off.x);
    }
    c /= cnt;

    vec3 col = c.a > 1e-4 ? c.rgb / c.a : vec3(0.0);
    col += EwDither(fragCoord, iTime, 1.0);
    col = clamp(col, 0.0, 1.0);
    float a = c.a * vig;
    fragColor = vec4(col * a, a);
}
