//  media: audio
// Spectrum: a classic mirrored spectrum analyzer with a waveform line, drawn from the sound of the media
// (the audio texture on iChannel0, read with the snippet EwAudio2: the log spectrum, r = mono, g = left, b = right).
// The left channel is drawn on the left half, the right channel on the right half, the bass in the middle.
//
// Live controls (iMouse, each 0..1):
//  iMouse.x = Sensitivity | Scales the height of the bars | 0.5 | 0.5 .. 1.5 | x
//  iMouse.y = Glow | Strength of the soft glow rising from the bars | 0.2 | 0 .. 0.4
//  iMouse.z = Bar Fill | How much of each bar slot is filled (low = thin bars) | 0.8 | 0.6 .. 1
//  iMouse.w = Color Shift | Slides the color gradient toward the Low or the High color | 0 | -0.5 .. 0.5
#define Sensitivity mix(0.5, 1.5, iMouse.x)
#define GlowAmount mix(0.0, 0.4, iMouse.y)
#define BarFill mix(0.6, 1.0, iMouse.z)
#define ColorShift mix(-0.5, 0.5, iMouse.w)

// --- Colors ---
const vec3 Low = vec3(0.1, 0.45, 1.0);			// var: { "type": "colhdr", "name": "Low color", "desc": "The color of the bars at the bottom" }
const vec3 High = vec3(1.0, 0.2, 0.6);			// var: { "type": "colhdr", "name": "High color", "desc": "The color of the bars at the top" }

// --- Bars ---
const float Bars = 48.0;						// var: { "min": 8, "max": 256, "step": 1, "name": "Bars", "desc": "The number of bars per channel" }
const float FreqLow = 0.1;						// var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Lowest Frequency", "desc": "Where the bars start on the log frequency axis (0 = 20 Hz, 0.1 = 40 Hz, 0.23 = 100 Hz)" }
const float FreqHigh = 0.9;						// var: { "min": 0.5, "max": 1.0, "step": 0.01, "name": "Highest Frequency", "desc": "Where the bars end on the log frequency axis (0.77 = 4 kHz, 0.9 = 10 kHz, 1 = 20 kHz)" }

// --- Waveform ---
const float Wave = 1.0;							// var: { "min": 0, "max": 1, "step": 0.05, "name": "Waveform", "desc": "The brightness of the waveform line" }
const float WaveWidth = 3.0;					// var: { "min": 1.0, "max": 8.0, "step": 0.1, "name": "Waveform Width", "desc": "The thickness of the waveform line", "unit": "px" }
const float WaveGain = 1.0;						// var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Waveform Height", "desc": "How far the waveform line swings from the center", "unit": "x" }

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
	vec2 uv = fragCoord / iResolution.xy;
	//	0 in the middle, 1 at the edges
	float x = abs(uv.x * 2.0 - 1.0);
	float bar = (floor(x * Bars) + 0.5) / Bars;
	//	The log frequency axis (the 128 band row: left in g, right in b)
	vec3 s = texture(iChannel0, vec2(mix(FreqLow, FreqHigh, bar), EwAudioFrequency128v)).rgb;
	float level = (uv.x < 0.5 ? s.g : s.b) * Sensitivity;
	float inBar = step(fract(x * Bars), BarFill);
	//	Color gradient from bottom to top (shiftable)
	vec3 c = mix(Low, High, clamp(uv.y + ColorShift, 0.0, 1.0)) * step(uv.y, level) * inBar;
	//	A glow over the bars
	c += mix(Low, High, clamp(level + ColorShift, 0.0, 1.0)) * GlowAmount * level * (1.0 - uv.y);
	//	The waveform (mono) as a line over the whole width (0.5 = silence)
	float w = EwAudioGetWave(iChannel0, uv.x, 0) * 0.5 * WaveGain + 0.5;
	c += Wave * smoothstep(WaveWidth, 0.0, abs(uv.y - w) * iResolution.y);
	fragColor = vec4(c, 1.0);
}
