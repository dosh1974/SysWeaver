

//  media: audio2
// =============================================================================
//  Oklab Spectrum Bars: a music visualizer of the sound of the media line (choose it in the Media tab):
//  2^Bars (default 64) log frequency bars, smoothed or live, in a perceptual (Oklab) color gradient
//  with a cap at the level of the moment, the glowing waveform, a flash on the kick drum and a ring
//  that grows with the beat. Calm in silence (dark bars, a flat waveform line), punchy with music.
// -----------------------------------------------------------------------------
//  Live controls:
//  iMouse.x = Glow | Glow of the waveform line | 0.5 | 0.2 .. 1.5 | x
//  iMouse.y = Bar Brightness | Brightness of the spectrum bars | 0.5 | 0.0 .. 2.0 | x
//  iMouse.z = Smoothed | Toggle: below 0.5 the live spectrum, from 0.5 the smoothed (delayed) one | 0.5 | 0 .. 1
//  iMouse.w = Hue Shift | Turns both colors around the hue circle | 0.5 | -180 .. 180 | °
// =============================================================================

// --- Colors ---
const vec3 Low = vec3(0.4825, 0.1, 1.0); // var: { "type": "col", "name": "Low", "desc": "Color at the bottom of the bars and of the kick flash." }
const vec3 High = vec3(0.0, 1.0, 0.4); // var: { "type": "col", "name": "High", "desc": "Color at the top of the bars, of the caps, the waveform and the beat ring." }

// --- Hits ---
const float Kick = 0.6; // var: { "min": 0.0, "max": 1.5, "step": 0.05, "name": "Kick flash", "desc": "Brightness of the full screen flash on a kick." }

// --- Bars ---
const int Bars = 5; // var: { "min": 0, "max": 9, "step": 1, "name": "Bars", "desc": "Spectrum resolution: 2^x bars (0 = 1 bar, 6 = 64, 9 = 512).", "format": "2^{0} bars" }
const float BarFill = 0.76; // var: { "min": 0.2, "max": 1.0, "step": 0.02, "name": "Bar Width", "desc": "Width of a bar as a share of its band: lower = thinner bars, wider gaps.", "scale": 100, "unit": "%" }
const float CapBrightness = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Cap Brightness", "desc": "Brightness of the caps at the level of the moment (0 = no caps)." }
const float CapWidth = 0.008; // var: { "min": 0.002, "max": 0.03, "step": 0.001, "name": "Cap Width", "desc": "Thickness of the caps as a share of the screen height." }

// --- Waveform ---
const float WaveWidth = 0.003; // var: { "min": 0.001, "max": 0.01, "step": 0.0005, "name": "Wave Width", "desc": "Core width of the waveform line; also widens its glow." }

// --- Beat ring ---
const float RingRadius = 0.1; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Ring Start", "desc": "Radius of the ring on the beat (share of the screen height)." }
const float RingGrowth = 0.4; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Ring Growth", "desc": "How far the ring grows until the next beat." }
const float RingWidth = 0.01; // var: { "min": 0.002, "max": 0.05, "step": 0.001, "name": "Ring Width", "desc": "Thickness of the ring." }
const float RingStrength = 0.6; // var: { "min": 0.0, "max": 1.2, "step": 0.05, "name": "Ring Brightness", "desc": "Brightness of the ring on the beat (0 = off)." }

// --- Live controls (iMouse) ---
#define Glow mix(0.2, 1.5, iMouse.x)
#define BarBrightness mix(0.0, 2.0, iMouse.y)
#define Smoothed (iMouse.z >= 0.5)
#define HueShift mix(-180.0, 180.0, iMouse.w)

// Per frame values from vertexMain: the colors in Oklab (for the gradient) and in sRGB (hue shifted)
flat varying vec3 vLowLab;
flat varying vec3 vHighLab;
flat varying vec3 vLow;
flat varying vec3 vHigh;

// Per frame (3 runs): the two colors in Oklab, turned around the hue circle by HueShift,
// plus their sRGB versions for the cap, waveform, flash and ring
void vertexMain(vec2 fragCoord)
{
    vec3 lo = EwLinearToOklab(EwSrgbToLinear(Low));
    vec3 hi = EwLinearToOklab(EwSrgbToLinear(High));
    float a = radians(HueShift);
    lo.yz = EwRotateVec2(lo.yz, a);
    hi.yz = EwRotateVec2(hi.yz, a);
    vLowLab = lo;
    vHighLab = hi;
    // No shift: the picked colors exactly; else the turned colors back in sRGB
    bool shifted = abs(HueShift) > 0.01;
    vLow = shifted ? clamp(EwLinearToSrgb(EwOklabToLinear(lo)), 0.0, 1.0) : Low;
    vHigh = shifted ? clamp(EwLinearToSrgb(EwOklabToLinear(hi)), 0.0, 1.0) : High;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord / iResolution.xy;
    // 2^Bars log frequency bands: spectrum row 9 - Bars (row 0 = 512 bands .. row 9 = 1 band)
    float n = exp2(float(Bars));
    float row = (float(9 - Bars) + 0.5) / 64.0;
    float u = (floor(clamp(uv.x, 0.0, 1.0) * (n - 0.001)) + 0.5) / n;
    // The bar: the smoothed (delayed) or the live spectrum (Smoothed toggle); the cap: always live
    float bar = texture(iChannel0, vec2(u, row + (Smoothed ? EwAudioDiffuseAddV : 0.0))).r;
    float now = texture(iChannel0, vec2(u, row)).r;
    // Bar body: the centered BarFill share of each band cell
    float cell = fract(uv.x * n);
    float inBar = step(0.5 - 0.5 * BarFill, cell) * step(cell, 0.5 + 0.5 * BarFill);
    // Gradient in Oklab (perceptually even), back to sRGB for display
    vec3 grad = clamp(EwLinearToSrgb(EwOklabToLinear(mix(vLowLab, vHighLab, uv.y))), 0.0, 1.0);
    vec3 col = grad * BarBrightness * inBar * step(uv.y, bar);
    // Cap: a thin line at the level of the moment
    col += vHigh * CapBrightness * inBar * (1.0 - smoothstep(0.0, CapWidth, abs(uv.y - now)));
    // The waveform (the last 512 samples, 0.5 = silence) as a glowing line
    float wave = texture(iChannel0, vec2(uv.x, EwAudioWave512v)).r;
    col += vHigh * Glow * WaveWidth / max(abs(uv.y - wave), WaveWidth);
    // A flash of the bottom color on the kick drum
    float kick = texture(iChannel0, vec2(EwAudioKickPulseU, EwAudioKickPulseV)).r;
    col += vLow * kick * Kick * 0.5;
    // A ring on the beat: grows from RingRadius by RingGrowth and fades until the next beat
    float phase = texture(iChannel0, vec2(EwAudioBeatPhaseU, EwAudioBeatPhaseV)).r;
    vec2 p = (fragCoord - 0.5 * iResolution.xy) / iResolution.y;
    float ring = 1.0 - smoothstep(0.0, RingWidth, abs(length(p) - RingRadius - phase * RingGrowth));
    col += vHigh * RingStrength * (1.0 - phase) * ring;
    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
