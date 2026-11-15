// Spectrum: draws the sound of the media (ShaderToy style: its audio texture is iChannel0, 512 x 2 texels,
// row 0 (y = 0.25) the spectrum, row 1 (y = 0.75) the waveform; r = mono, g = left, b = right).
// The left channel is drawn on the left half, the right channel on the right half, the bass in the middle.

const vec3 Low = vec3(0.1, 0.45, 1.0);			// var: { "type": "colhdr", "name": "Low color", "desc": "The color of the bars at the bottom" }
const vec3 High = vec3(1.0, 0.2, 0.6);			// var: { "type": "colhdr", "name": "High color", "desc": "The color of the bars at the top" }
const float Bars = 48.0;						// var: { "min": 8, "max": 256, "step": 1, "name": "Bars", "desc": "The number of bars per channel" }
const float Wave = 1.0;							// var: { "min": 0, "max": 1, "step": 0.05, "name": "Waveform", "desc": "The brightness of the waveform line" }

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
	vec2 uv = fragCoord / iResolution.xy;
	//	0 in the middle, 1 at the edges
	float x = abs(uv.x * 2.0 - 1.0);
	float bar = (floor(x * Bars) + 0.5) / Bars;
	//	More room for the low frequencies
	vec3 s = texture2D(iChannel0, vec2(bar * bar, 0.25)).rgb;
	float level = uv.x < 0.5 ? s.g : s.b;
	float inBar = step(fract(x * Bars), 0.8);
	vec3 c = mix(Low, High, uv.y) * step(uv.y, level) * inBar;
	//	A glow over the bars
	c += mix(Low, High, level) * 0.2 * level * (1.0 - uv.y);
	//	The waveform (mono) as a line over the whole width
	float w = texture2D(iChannel0, vec2(uv.x, 0.75)).r;
	c += Wave * smoothstep(3.0, 0.0, abs(uv.y - w) * iResolution.y);
	fragColor = vec4(c, 1.0);
}
