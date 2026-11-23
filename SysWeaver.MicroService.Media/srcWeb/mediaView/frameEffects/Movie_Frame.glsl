//  media: image none
// =============================================================================
//  Film Reel Frame
// =============================================================================
//  iMouse.x = Inset | Distance of the film strip center line from the screen edges | 0.3 | 8 .. 128 | {0} px
//  iMouse.y = Corner Radius | Rounding of the frame corners | 0.5 | 0 .. 128 | {0} px

const float Speed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Speed", "desc": "Film scroll speed", "unit": "x" }
const float Opacity = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Opacity", "desc": "Cross fade from transparent to the background color", "scale": 100, "unit": "%" }
const vec3 Background = vec3(0.3, 0.0886363636, 0.0886363636); // var: { "type": "col", "name": "Background", "desc": "Background color (shown by Opacity)" }
const float InnerOpacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Inner Opacity", "desc": "Opacity of the image or animated velvet curtain inside the frame", "scale": 100, "unit": "%" }
const vec3 InnerColor = vec3(0.3313, 0.0105, 0.0217); // var: { "type": "col", "name": "Inner Color", "desc": "Base color of the velvet curtain (shown by Inner Opacity)" }
const vec3 FilmColor = vec3(0.07, 0.05, 0.04); // var: { "type": "col", "name": "Film Color", "desc": "Color of the film base" }
const float Weave = 0.8; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Weave", "desc": "How strongly the strip snakes in and out", "scale": 100, "unit": "%" }
const float WeaveSpeed = 0.1; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Weave Speed", "desc": "Speed of the weaving motion", "unit": "x" }
const float Sepia = 0.3; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Sepia", "desc": "Old film tint of the pictures", "scale": 100, "unit": "%" }
const float PictureOpacity = 0.6; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Picture Opacity", "desc": "Opacity of the pictures on the film", "scale": 100, "unit": "%" }
const float Additive = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Additive", "desc": "0 = alpha blend, 1 = pure additive (alpha 0)", "scale": 100, "unit": "%" }

#define Inset mix(8.0, 128.0, iMouse.x)
#define CornerRadius mix(0.0, 128.0, iMouse.y)

#define ZERO min(iFrame, 0)

varying vec2 vP;
varying vec4 vCurtainCoords;      // xy: uv, zw: nuv
flat varying vec4 vFrameA;        // xy: hs, z: rad, w: W
flat varying vec4 vFrameB;        // x: amp, y: ampIn, z: Q, w: P
flat varying vec4 vFrameC;        // x: tau*nw/P, y: weavePh, z: fl, w: scroll
flat varying vec4 vMisc;          // xy: imgHs, z: innerEdge, w: fineFade
flat varying vec3 vCurtainPh;
flat varying vec3 vAlbedo;
flat varying vec3 vSheenCol;
flat varying vec3 vAmbCol;

void vertexMain(vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vP = fragCoord - 0.5 * res;
    float inset = min(Inset, 0.25 * min(res.x, res.y));
    float W = max(min(inset * 1.2, 2.0 * inset - 8.0), 6.0);
    float amp = max(inset - 0.5 * W - 14.0, 0.0) * Weave;
    float ampIn = 0.6 * W * Weave;
    vec2 hs = 0.5 * res - inset;
    vec2 imgHs = hs;
    if (SwHaveImage) {
        vec2 imgRes = max(iChannelResolution[0].xy, vec2(1.0));
        float imgAspect = imgRes.x / imgRes.y;
        float margin = 0.5 * W - amp;
        vec2 maxImgHs = hs - margin;
        float maxAspect = maxImgHs.x / max(maxImgHs.y, 1.0);
        imgHs = (imgAspect > maxAspect)
            ? vec2(maxImgHs.x, maxImgHs.x / imgAspect)
            : vec2(maxImgHs.y * imgAspect, maxImgHs.y);
        hs = imgHs + margin;
        ampIn = min(ampIn, max(min(hs.x, hs.y) - 0.5 * W, 0.0));
    }
    float rad = min(CornerRadius, min(hs.x, hs.y));
    vec2 b = hs - rad;
    float Q = b.x + 1.5707963 * rad + b.y;
    float P = 4.0 * Q;
    float nw = max(floor(P / (W * 9.0)), 1.0);
    float t = iTime;
    float weavePh = mod(t * WeaveSpeed, 6.2831853);
    float nf = max(floor(P / W), 1.0);
    float fl = P / nf;
    float scroll = mod(t * Speed * 60.0, P);
    float innerEdge = -0.5 * W + clamp(0.05 * W - 1.0, 0.0, 1.0);

    vec2 curtainRes = SwHaveImage ? 2.0 * imgHs : res;
    vCurtainCoords = vec4(vP / curtainRes.y, vP / curtainRes);
    float fineFade = smoothstep(0.008, 0.0015, 1.0 / curtainRes.y);

    vFrameA = vec4(hs, rad, W);
    vFrameB = vec4(amp, ampIn, Q, P);
    vFrameC = vec4(6.2831853 * nw / P, weavePh, fl, scroll);
    vMisc = vec4(imgHs, innerEdge, fineFade);

    vCurtainPh = vec3(
        mod(t * 0.46, 6.2831853),
        mod(t * 0.29, 6.2831853),
        mod(t * 0.72, 6.2831853)
    );
    vec3 albedo = EwSrgbToLinear(InnerColor);
    float lum = dot(albedo, vec3(0.2126, 0.7152, 0.0722));
    vAlbedo = albedo;
    vSheenCol = mix(albedo * 2.1, vec3(1.0, 0.76, 0.64) * (lum + 0.11), 0.30);
    vAmbCol = (albedo * 0.42 + albedo * albedo * 1.5) * vec3(0.09, 0.06, 0.08);
}

// arc length position along a rounded box outline (clockwise from top center), total = 4 * quarter
float perimPos(vec2 p, vec2 hs, float r, float quarter)
{
    vec2 b = hs - r;
    vec2 a = abs(p);
    float u;
    if (a.x < b.x) u = a.x;
    else if (a.y < b.y) u = b.x + 1.5707963 * r + (b.y - a.y);
    else u = b.x + atan(a.x - b.x, a.y - b.y) * r;
    if (p.x >= 0.0) return p.y >= 0.0 ? u : 2.0 * quarter - u;
    return p.y < 0.0 ? 2.0 * quarter + u : 4.0 * quarter - u;
}

// little procedural "movie still"
vec3 still(vec2 uv, float id)
{
    vec3 h = EwHash2To3(vec2(id, 3.7));
    vec3 sky = EwCosinePalette(h.x, vec3(0.5), vec3(0.5), vec3(1.0), vec3(0.0, 0.33, 0.67));
    vec3 col = mix(sky, vec3(1.0, 0.85, 0.6), uv.y * 0.6);
    float sun = length(uv - vec2(0.25 + 0.5 * h.y, 0.65)) - 0.15 - 0.08 * h.z;
    col = mix(col, vec3(1.0, 0.95, 0.7), smoothstep(0.03, 0.0, sun));
    float hill = 0.35 + 0.12 * sin(uv.x * (4.0 + 6.0 * h.z) + h.y * 6.0);
    col = mix(col, sky * 0.25, smoothstep(0.02, 0.0, uv.y - hill));
    float lum = dot(col, vec3(0.3, 0.59, 0.11));
    return mix(col, lum * vec3(1.1, 0.85, 0.6), Sepia);
}

// plush velvet shading for one light source (view-dependent terms precomputed by caller)
vec3 velvetLight(vec3 V, vec3 L, float ndlRaw, float ndvRaw, vec3 albedoCore, vec3 sheenGrazing, vec3 sheenSilk, vec3 lightCol, float shadow)
{
    float diff = clamp((ndlRaw + 0.24) * (1.0 / 1.24), 0.0, 1.0);
    float lightWrap = smoothstep(-0.18, 0.58, ndlRaw);
    float silk = 0.0;
    if (ndlRaw > 0.0) {
        float ndh = clamp((ndlRaw + ndvRaw) * inversesqrt(max(2.0 + 2.0 * dot(L, V), 1e-6)), 0.0, 1.0);
        float t = ndh * (1.0 - ndh);
        silk = t * sqrt(t) * (0.764 + 0.608 * t - 0.316 * ndh) * ndlRaw;
    }
    return (albedoCore * diff + sheenGrazing * lightWrap + sheenSilk * silk) * lightCol * shadow;
}

// animated dark red velvet movie-theatre curtain
vec3 velvetCurtain()
{
    vec2 uv = vCurtainCoords.xy;
    vec2 nuv = vCurtainCoords.zw;
    float ux = uv.x;
    float hang = clamp(0.5 - nuv.y, 0.0, 1.0);
    vec3 ph = vCurtainPh;

    // hang-dependent smooth envelopes + exact derivatives
    float uSway = clamp(hang * (1.0 / 0.92), 0.0, 1.0);
    float sway = uSway * uSway * (3.0 - 2.0 * uSway);
    float dSway_dh = (6.0 / 0.92) * uSway * (1.0 - uSway);

    float omh = max(1.0 - hang, 0.0);
    float gPow = pow(omh, 1.45);
    float gather = 1.0 + 0.15 * gPow;
    float dGather_dh = omh > 1e-6 ? (-0.2175 * gPow / omh) : 0.0;

    float uHp = clamp(hang * 2.0, 0.0, 1.0);
    float headerPinch = 0.20 - 0.16 * uHp * uHp * (3.0 - 2.0 * uHp);
    float dHp_dh = -1.92 * uHp * (1.0 - uHp);

    // lateral drift waves (vectorized vec3 sin/cos)
    vec3 pv = ux * vec3(2.2, 4.4, 7.8) + hang * vec3(2.3, -3.0, 4.2) + vec3(-ph.x, ph.y, -ph.z);
    vec3 sp = sin(pv);
    vec3 cp = cos(pv);
    float dSum = dot(vec3(0.028, 0.017, 0.008), sp);
    float sx = ux + sway * dSum;
    float dsx_dx = 1.0 + sway * dot(vec3(0.0616, 0.0748, 0.0624), cp);
    float dsx_dh = dSway_dh * dSum + sway * dot(vec3(0.0644, -0.051, 0.0336), cp);

    // fold asymmetry + secondary pleat phase (vectorized vec3 sin/cos)
    float ax = abs(sx);
    float sgn = clamp(sx * (1.0 / 0.0028), -1.0, 1.0);
    float uAs = clamp(ax * 5.0, 0.0, 1.0);
    float asymW = uAs * uAs * (3.0 - 2.0 * uAs);
    float dAsymW_dax = 30.0 * uAs * (1.0 - uAs);
    vec3 qsp = sx * vec3(3.2, 6.7, 3.5) + vec3(0.85, -1.35, hang * 2.2 + 0.4);
    vec3 sqsp = sin(qsp);
    vec3 cqsp = cos(qsp);
    float asymS = dot(vec2(0.038, 0.021), sqsp.xy);
    float asym = asymW * asymS;
    float dAsym_dsx = dAsymW_dax * sgn * asymS + asymW * dot(vec2(0.1216, 0.1407), cqsp.xy);

    float x = ax * gather + asym;
    float foldX = sx * gather;
    float dx_dsx = sgn * gather + dAsym_dsx;
    float dx_dx = dx_dsx * dsx_dx;
    float dx_dh = dx_dsx * dsx_dh + ax * dGather_dh;

    // plush rounded ridges + softer secondary header creases
    float a1 = x * 35.0;
    float spMod = 1.1 * sqsp.z;
    float spModC = 1.1 * cqsp.z;
    vec2 a12 = vec2(a1, 2.0 * a1 + spMod);
    vec2 c12 = cos(a12);
    vec2 s12 = sin(a12);
    float c1 = c12.x, s1 = s12.x;
    float c2 = c12.y, s2 = s12.y;
    float c2a1 = 2.0 * c1 * c1 - 1.0;
    float primary = -c1 - 0.25 * c2a1;
    float dPrimary_dx = 35.0 * s1 * (1.0 + c1);
    float subPleat = -c2 * headerPinch;

    // organic fold-depth variation + slow travelling billow (vectorized vec4 sin/cos)
    vec4 mb = sx * vec4(5.0, 9.1, 2.5, 5.0) + hang * vec4(-1.5, 2.1, -2.4, 3.1) + vec4(ph.y, -ph.x, -ph.x, ph.z);
    vec4 smb = sin(mb);
    vec4 cmb = cos(mb);
    float depthMod = 0.84 + dot(vec2(0.22, 0.14), smb.xy);
    float dDm_dsx = dot(vec2(1.10, 1.274), cmb.xy);
    float dDm_dh = dot(vec2(-0.33, 0.294), cmb.xy);

    float bSum = dot(vec2(0.26, 0.15), smb.zw);
    float billow = sway * bSum;
    float dB_dsx = sway * dot(vec2(0.65, 0.75), cmb.zw);
    float dB_dh = dSway_dh * bSum + sway * dot(vec2(-0.624, 0.465), cmb.zw);

    float seamDip = (ax < 0.13) ? (-0.36 * exp2(-sx * sx * 1298.4255)) : 0.0;
    float dSeam_dsx = seamDip * (-1800.0 * sx);

    float s2hp = s2 * headerPinch;
    float dCommon_dsx = primary * dDm_dsx + dB_dsx + dSeam_dsx;
    float gradX = dPrimary_dx * dx_dx * depthMod + s2hp * (70.0 * dx_dx + spModC * 3.5 * dsx_dx) + dCommon_dsx * dsx_dx;
    float gradY = dPrimary_dx * dx_dh * depthMod + primary * dDm_dh
                + s2hp * (70.0 * dx_dh + spModC * (3.5 * dsx_dh + 2.2)) - c2 * dHp_dh
                + dB_dh + dCommon_dsx * dsx_dh;
    float h0 = primary * depthMod + subPleat + billow + seamDip;

    // analytic multi-scale AO (both scales packed in vec2 via trig sum-to-product identity)
    float dxAbs_dx = abs((sx >= 0.0 ? gather : -gather) + dAsym_dsx) * dsx_dx;
    float d2Dm = dot(vec2(-2.75, -5.7967), smb.xy);
    float d2Billow = sway * dot(vec2(-0.8125, -1.875), smb.zw);
    const vec2 aoD = vec2(0.022, 0.058);
    vec2 aoDsx = aoD * dsx_dx;
    vec2 aoDsx2 = aoDsx * aoDsx;
    vec2 da1 = (35.0 * dxAbs_dx) * aoD;
    vec2 cd = cos(da1);
    vec2 sd = sin(da1);
    vec2 c2d = 2.0 * cd * cd - 1.0;
    vec2 avgP = -c1 * cd - 0.25 * c2a1 * c2d;
    vec2 avgDm = depthMod + aoDsx2 * d2Dm;
    vec2 halfDiffP = s1 * sd * (1.0 + c1 * cd);
    vec2 aoDiff = (avgP * avgDm - primary * depthMod) + halfDiffP * (dDm_dsx * aoDsx)
                - c2 * (c2d - 1.0) * headerPinch + aoDsx2 * d2Billow;
    if (ax < 0.13) {
        vec2 sL = sx - aoDsx, sR = sx + aoDsx;
        aoDiff += -0.18 * (exp2(-sL * sL * 1298.4255) + exp2(-sR * sR * 1298.4255)) - seamDip;
    }
    vec2 occ = max(vec2(0.0), aoDiff);
    float ao = clamp(1.0 - dot(occ, vec2(1.25, 0.78)), 0.03, 1.0);
    ao *= mix(0.15, 1.0, smoothstep(-1.35, 0.78, h0));

    // soft brushed-velvet directional nap + resolution-aware microfiber shimmer
    float brushed = EwValueNoise2D(vec2(foldX * 9.0, hang * 4.5));
    float fineFade = vMisc.w;
    float fineNap = 0.5;
    float microNap = 0.5;
    if (fineFade > 0.0) {
        fineNap = mix(0.5, EwValueNoise2D(vec2(foldX * 210.0, hang * 280.0)), fineFade);
        microNap = mix(0.5, EwValueNoise2D(vec2(foldX * 420.0, hang * 140.0) + vec2(7.1, 3.3)), fineFade);
    }
    float luster = (0.78 + 0.30 * brushed) * (0.90 + 0.16 * fineNap);

    const float depthScale = 0.165;
    vec3 N = normalize(vec3(
        -gradX * depthScale + (fineNap - 0.5) * 0.025,
         gradY * depthScale + (microNap - 0.5) * 0.020,
        1.0
    ));
    vec3 V = normalize(vec3(-uv.x * 0.30, -uv.y * 0.30, 1.0));
    float ndvRaw = dot(N, V);
    float ndv = max(ndvRaw, 0.0);

    float valance = mix(0.14, 1.0, smoothstep(0.0, 0.28, hang));

    // precompute view-dependent velvet shading factors once for both lights
    float core = mix(1.0, 0.34, ndv * ndv);
    float uNdv = max(1.0 - ndv, 0.0);
    float grazing = uNdv * uNdv * (0.695 + uNdv * (0.500 - 0.195 * uNdv));
    vec3 albedoCore = vAlbedo * core;
    vec3 sheenGrazing = vSheenCol * (grazing * luster * 1.05);
    vec3 sheenSilk = vSheenCol * (luster * 0.945);

    // Light 1: warm overhead movie-theatre spotlight
    vec3 surfP = vec3(uv, h0 * depthScale);
    vec3 toL1 = vec3(0.06, 0.66, 0.88) - surfP;
    float d1Sq = dot(toL1, toL1);
    vec3 L1 = toL1 * inversesqrt(d1Sq);
    float ndlRaw1 = dot(N, L1);

    // deep saturated multiple-scatter bounce inside velvet crevices
    vec3 col = vAmbCol * (ao * valance);

    if (ndlRaw1 > -0.24) {
        vec2 sp1 = uv * vec2(0.82, 1.18) - vec2(0.0, 0.08);
        float spot1 = exp2(-dot(sp1, sp1) * 3.0296596) / (0.62 + 0.38 * d1Sq);

        // 3-step soft heightfield self-shadow toward overhead spotlight (vec4-packed state + tight upper bound)
        float maxDiff = 0.0;
        float hRayStep = L1.z * (0.026 / depthScale);
        float dsxStep = (dsx_dx * L1.x - dsx_dh * L1.y) * 0.026;
        float dhStep = -L1.y * 0.026;
        vec4 dB = vec4(dDm_dsx * dsxStep + dDm_dh * dhStep, dHp_dh * dhStep,
                       dB_dsx * dsxStep + dB_dh * dhStep, hRayStep);
        float maxStep = 0.75 * dB.x + dB.y + dB.z;
        if (h0 + hRayStep < 0.75 * depthMod + headerPinch + billow + max(maxStep, 0.0) * 3.0) {
            vec4 sA = vec4(sx, gather, asym, spMod);
            vec4 dA = vec4(dsxStep, dGather_dh * dhStep, dAsym_dsx * dsxStep,
                           spModC * (3.5 * dsxStep + 2.2 * dhStep));
            vec4 sB = vec4(depthMod, headerPinch, billow, h0);
            for (int i = 1 + ZERO; i <= 3; i++) {
                sA += dA;
                sB += dB;
                if (sB.w < 0.75 * sB.x + sB.y + sB.z) {
                    float a1S = 35.0 * (abs(sA.x) * sA.y + sA.z);
                    vec2 csS = cos(vec2(a1S, 2.0 * a1S + sA.w));
                    float primS = 0.25 - csS.x * (1.0 + 0.5 * csS.x);
                    float hs = primS * sB.x - csS.y * sB.y + sB.z;
                    if (abs(sA.x) < 0.13) hs -= 0.36 * exp2(-sA.x * sA.x * 1298.4255);
                    maxDiff = max(maxDiff, hs - sB.w);
                    if (maxDiff >= 0.65) break;
                }
            }
        }
        float sh1 = 1.0 - smoothstep(0.0, 0.65, maxDiff) * 0.80;
        col += velvetLight(V, L1, ndlRaw1, ndvRaw, albedoCore, sheenGrazing, sheenSilk,
                           vec3(1.0, 0.84, 0.64) * (1.35 * spot1), sh1 * mix(0.22, 1.0, ao) * valance);
    }

    // Light 2: warm dimmed theatre footlights from below center stage
    vec3 toL2 = vec3(0.0, -0.76, 0.68) - surfP;
    float d2Sq = dot(toL2, toL2);
    vec3 L2 = toL2 * inversesqrt(d2Sq);
    float ndlRaw2 = dot(N, L2);
    if (ndlRaw2 > -0.24) {
        vec2 sp2 = uv * vec2(0.85, 1.05) - vec2(0.0, -0.46);
        float spot2 = exp2(-dot(sp2, sp2) * 2.8132553) / (0.68 + 0.32 * d2Sq);
        col += velvetLight(V, L2, ndlRaw2, ndvRaw, albedoCore, sheenGrazing, sheenSilk,
                           vec3(1.0, 0.56, 0.26) * (0.50 * spot2), mix(0.18, 1.0, ao));
    }

    // theatrical dark periphery vignette
    vec2 vuv = nuv * vec2(1.48, 1.32);
    float vig = mix(0.20, 1.0, exp2(-dot(vuv, vuv) * 2.2361773));
    col = EwTonemapAces(col * vig);
    return EwLinearToSrgbFast(col);
}

// premultiplied "over"
vec4 over(vec4 top, vec4 base) { return top + base * (1.0 - top.a); }

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 res = iResolution.xy;
    vec2 p = vP;
    vec2 hs = vFrameA.xy;
    float rad = vFrameA.z;
    float W = vFrameA.w;
    float amp = vFrameB.x;
    float ampIn = vFrameB.y;
    float Q = vFrameB.z;
    float P = vFrameB.w;

    float d = EwSdRoundedBox2D(p, hs, rad); // px, 0 on the frame center line
    vec4 bg = vec4(Background * Opacity, Opacity);
    vec4 acc = vec4(0.0);

    float inM = 0.0;
    if (d < amp + W && d > -(ampIn + W)) {
        float s = perimPos(p, hs, rad, Q);
        float sw = sin(s * vFrameC.x + vFrameC.y);
        float off = sw * (sw > 0.0 ? amp : ampIn);
        float v = d - off;
        float halfW = 0.5 * W;
        float y = v / halfW;
        float fl = vFrameC.z;
        float sa = mod(s - vFrameC.w, P);
        float fx = sa / fl;
        float id = floor(fx);
        float lx = fract(fx);
        float body = clamp(halfW - abs(v) + 0.5, 0.0, 1.0);
        vec3 col = FilmColor;
        // sprocket holes
        float pitch = fl * 0.25;
        float hx = (fract(lx * 4.0) - 0.5) * pitch;
        float hy = abs(v) - 0.39 * W;
        float hole = EwSdRoundedBox2D(vec2(hx, hy), vec2(0.22 * pitch, 0.06 * W), 0.03 * W);
        float holeA = clamp(hole + 0.5, 0.0, 1.0);
        // picture
        vec2 pd = vec2((lx - 0.5) * fl, v);
        float pic = EwSdRoundedBox2D(pd, vec2(0.5 * fl - max(0.06 * W, 1.5), 0.28 * W), 0.02 * W);
        float picM = clamp(0.5 - pic, 0.0, 1.0);
        if (picM > 0.0) {
            vec2 uv = vec2(lx, 0.5 - v / (0.56 * W));
            vec3 img = still(uv, id);
            col = mix(col, img, picM);
        }
        // edge sheen
        col += vec3(0.08, 0.06, 0.04) * smoothstep(0.75, 1.0, abs(y));
        float a = body * holeA * mix(1.0, PictureOpacity, picM);
        acc = vec4(col * a, a);
        // soft drop shadow below the film
        float sh = 0.35 * smoothstep(halfW + 8.0, halfW, abs(v - 3.0)) * (1.0 - a);
        acc = over(acc, vec4(0.0, 0.0, 0.0, sh));
        inM = clamp(vMisc.z - v + 0.5, 0.0, 1.0);
    } else if (d <= -(ampIn + W)) {
        inM = 1.0;
    }
    if (InnerOpacity > 0.0 && inM > 0.0 && acc.a < 1.0) {
        if (SwHaveImage) {
            vec2 imgUv = p / (2.0 * vMisc.xy) + 0.5;
            vec4 img = texture(iChannel0, imgUv);
            if (SwTransparentImage) {
                vec3 innerCol = img.rgb;
                if (img.a < 0.999) {
                    innerCol = mix(velvetCurtain(), img.rgb, img.a);
                }
                float ia = InnerOpacity * inM;
                acc = over(acc, vec4(innerCol * ia, ia));
            } else {
                float ia = img.a * InnerOpacity * inM;
                acc = over(acc, vec4(img.rgb * ia, ia));
            }
        } else {
            float ia = InnerOpacity * inM;
            vec3 innerCol = velvetCurtain();
            acc = over(acc, vec4(innerCol * ia, ia));
        }
    }
    // guaranteed soft fade to zero at the screen border (outermost pixel fully transparent)
    vec2 eb2 = min(fragCoord, res - fragCoord);
    float eb = min(eb2.x, eb2.y);
    acc *= smoothstep(1.0, 12.0, eb);

    float dz = EwDither(fragCoord, iTime, 2.0);
    vec3 col = clamp(acc.rgb + dz, 0.0, 1.0);
    float a = acc.a;
    col = min(col, vec3(a));
    a *= 1.0 - Additive;
    fragColor = vec4(col + bg.rgb * (1.0 - a), a + bg.a * (1.0 - a));
}
