// Corridor: a hazy corridor drawn by the sound (ShaderToy style: the audio texture is iChannel0, 512 x 2 texels,
// row 0 (y = 0.25) the spectrum, row 1 (y = 0.75) the waveform; r = mono, g = left, b = right).
//  - the left / right walls are LED spectrum meters of the left / right channel (bass near the camera, treble far away)
//  - the floor is glossy: a rough (diffuse) reflection of the room, lit by the lamps, the walls and the TV
//  - the far wall is a retro TV: noise, scan lines, a rolling bar and the waveform on top
//  - the ceiling is two rows of colored lamps that flash on the bass drum
//  - haze: fog and light shafts under the lamps
// The camera looks around and sways sideways / up and down, it never moves along the corridor.

const float Haze = 0.06;						// var: { "min": 0, "max": 0.3, "step": 0.005, "name": "Haze", "desc": "How hazy the room is (fog and light shafts)" }
const float Motion = 1.0;						// var: { "min": 0, "max": 2, "step": 0.05, "name": "Camera motion", "desc": "How much the camera looks and sways around" }
const float Bars = 32.0;						// var: { "min": 8, "max": 96, "step": 1, "name": "Bars", "desc": "The number of spectrum bars on each wall" }
const float Gloss = 1.0;					// var: { "min": 0, "max": 1.5, "step": 0.05, "name": "Floor gloss", "desc": "The strength of the floor reflection" }
const float Roughness = 0.12;					// var: { "min": 0, "max": 0.5, "step": 0.01, "name": "Floor roughness", "desc": "How blurry (diffuse) the floor reflection is" }
const float BassFrom = 0.55;					// var: { "min": 0, "max": 1, "step": 0.01, "name": "Bass threshold", "desc": "The bass level where the lamps start to flash" }
const vec3 Phosphor = vec3(0.35, 1.0, 0.55);	// var: { "type": "colhdr", "name": "Waveform color", "desc": "The color of the waveform on the TV" }

//  The room: x in [-W, W], y in [0, H], the camera at z = 0, the TV wall at z = D
const float W = 2.0;
const float H = 3.0;
const float D = 11.0;
const float LampRows = 8.0;
const float LampX = 1.0;

float hash(vec2 p)
{
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

vec3 hsv(float h, float s, float v)
{
	vec3 k = clamp(abs(mod(h * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
	return v * mix(vec3(1.0), k, s);
}

//  The spectrum (0..1) at u (0 = bass .. 1 = treble, a log like scale up to ~9 kHz), r = mono, g = left, b = right
vec3 spectrum(float u)
{
	float x = 0.004 + pow(clamp(u, 0.0, 1.0), 1.7) * 0.4;
	return texture(iChannel0, vec2(x, 0.25)).rgb;
}

//  The mono waveform at x (0..1): -1..1
float wave(float x)
{
	return texture(iChannel0, vec2(clamp(x, 0.0, 1.0), 0.75)).r * 2.0 - 1.0;
}

//  The bass drum: the lowest bins (~45..170 Hz at 48 kHz), 0..1 past the threshold
float kick()
{
	float b = 0.0;
	b += texture(iChannel0, vec2(0.003, 0.25)).r;
	b += texture(iChannel0, vec2(0.005, 0.25)).r;
	b += texture(iChannel0, vec2(0.007, 0.25)).r;
	b += texture(iChannel0, vec2(0.009, 0.25)).r;
	b *= 0.25;
	float k = smoothstep(BassFrom, 1.0, b);
	return k * k * (3.0 - 2.0 * k);
}

//  Lamp i (0..15): two rows along the ceiling
vec3 lampPos(float i)
{
	float row = mod(i, 2.0);
	float k = floor(i * 0.5);
	return vec3(row < 0.5 ? -LampX : LampX, H - 0.03, (k + 0.6) * D / LampRows);
}

vec3 lampColor(float i)
{
	float row = mod(i, 2.0);
	float k = floor(i * 0.5);
	return hsv(fract(k / LampRows + row * 0.5 + iTime * 0.02), 0.75, 1.0);
}

//  The brightness of lamp i: a glow, flashing on the kick (each lamp a bit differently, a wave running along the corridor)
float lampPower(float i, float kk)
{
	float k = floor(i * 0.5);
	float w = 0.5 + 0.5 * sin(iTime * 2.1 - k * 0.9 + mod(i, 2.0) * 1.7);
	return 0.06 + kk * (0.5 + 2.2 * w * w);
}

//  The nearest surface: (t, id) id 0 = left wall, 1 = right wall, 2 = floor, 3 = ceiling, 4 = TV wall, 5 = the wall behind
vec2 trace(vec3 ro, vec3 rd)
{
	float t = 1e5;
	float id = -1.0;
	float s;
	if (rd.x < 0.0) { s = (-W - ro.x) / rd.x; if (s > 0.0 && s < t) { t = s; id = 0.0; } }
	if (rd.x > 0.0) { s = (W - ro.x) / rd.x; if (s > 0.0 && s < t) { t = s; id = 1.0; } }
	if (rd.y < 0.0) { s = -ro.y / rd.y; if (s > 0.0 && s < t) { t = s; id = 2.0; } }
	if (rd.y > 0.0) { s = (H - ro.y) / rd.y; if (s > 0.0 && s < t) { t = s; id = 3.0; } }
	if (rd.z > 0.0) { s = (D - ro.z) / rd.z; if (s > 0.0 && s < t) { t = s; id = 4.0; } }
	if (rd.z < 0.0) { s = (-1.5 - ro.z) / rd.z; if (s > 0.0 && s < t) { t = s; id = 5.0; } }
	return vec2(t, id);
}

//  A spectrum wall: LED bars along z (bass near the camera), the level of the channel (mask) up the wall; blur > 0 for a reflection
vec3 meterWall(vec3 p, vec3 mask, float blur)
{
	float u = clamp((p.z - 0.6) / (D - 1.6), 0.0, 1.0);
	float bx = u * Bars;
	float bar = (floor(bx) + 0.5) / Bars;
	float level = dot(spectrum(bar), mask);
	level = clamp(level * 1.1, 0.0, 1.0);
	const float Segs = 22.0;
	float yv = (p.y - 0.15) / (H - 0.45);
	float sy = yv * Segs;
	float seg = (floor(sy) + 0.5) / Segs;
	float inside = step(0.0, yv) * step(yv, 1.0) * step(0.0, p.z - 0.6) * step(p.z, D - 1.0);
	//  The LED cells (a reflection sees them blurred: the light of the bar, not the cells)
	vec2 c = vec2(fract(bx), fract(sy)) - 0.5;
	float led = smoothstep(0.5, 0.28, abs(c.x)) * smoothstep(0.5, 0.22, abs(c.y));
	led = mix(led, 0.55, blur);
	float lit = mix(step(seg, level), smoothstep(level + 0.05, level - 0.05, yv), blur);
	vec3 col = mix(vec3(0.1, 1.0, 0.25), vec3(1.0, 0.85, 0.1), smoothstep(0.35, 0.65, yv));
	col = mix(col, vec3(1.0, 0.12, 0.08), smoothstep(0.7, 0.9, yv));
	//  The top lit cell of a bar burns brighter
	float top = smoothstep(Segs * 0.9 / Segs, 1.0, 1.0 - abs(seg - level) * Segs * 0.5) * step(seg, level);
	vec3 e = col * led * inside * (0.035 + lit * (1.6 + top * 1.5));
	//  The dark metal panel around the LEDs
	vec3 panel = vec3(0.018, 0.02, 0.026) * (0.8 + 0.2 * step(0.5, fract(p.z * 0.5)));
	return panel + e;
}

//  The retro TV on the far wall: its screen (noise, scan lines, a rolling bar, the waveform on top), the case and the wall
vec3 tvWall(vec3 p, float blur)
{
	vec2 center = vec2(0.0, 1.5);
	vec2 size = vec2(1.3, 0.97);
	vec2 s = (p.xy - center) / size;
	//  The case (rounded, a bit wider than the screen; lit from above by the lamps), with two knobs on the right
	vec2 cq = abs(s - vec2(0.1, 0.0)) - vec2(1.28, 1.2);
	float caseD = length(max(cq, 0.0)) + min(max(cq.x, cq.y), 0.0) - 0.1;
	vec3 wall = vec3(0.025, 0.02, 0.022) * (0.85 + 0.15 * hash(floor(p.xy * 6.0)));
	vec3 caseCol = vec3(0.11, 0.075, 0.05) * (0.6 + 0.4 * clamp(s.y * 0.5 + 0.5, 0.0, 1.0));
	vec2 kq = s - vec2(1.2, 0.35);
	vec2 kq2 = s - vec2(1.2, -0.1);
	caseCol += vec3(0.08) * smoothstep(0.1, 0.07, length(kq * vec2(1.0, 1.3))) + vec3(0.08) * smoothstep(0.1, 0.07, length(kq2 * vec2(1.0, 1.3)));
	vec3 col = mix(caseCol, wall, smoothstep(0.0, 0.03, caseD));
	//  The bulged glass: a barrel distortion
	vec2 g = (s + vec2(0.12, 0.0)) * (1.0 + 0.07 * dot(s, s));
	vec2 sq = abs(g) - vec2(1.0);
	float screenD = length(max(sq, 0.0)) + min(max(sq.x, sq.y), 0.0) - 0.08;
	float inScreen = smoothstep(0.03, -0.02, screenD);
	if (inScreen > 0.0)
	{
		vec2 uv = g * 0.5 + 0.5;
		//  Static, a rolling bar, scan lines
		float n = hash(floor(uv * vec2(220.0, 160.0)) + floor(iTime * 24.0) * vec2(17.3, 3.7));
		n = mix(n, 0.5, blur);
		float roll = smoothstep(0.0, 0.08, abs(fract(uv.y * 0.6 - iTime * 0.13) - 0.5));
		float scan = mix(0.72 + 0.28 * sin(uv.y * 160.0 * 3.14159), 0.86, blur);
		vec3 tv = vec3(0.3, 0.33, 0.4) * (0.3 + 0.7 * n) * (0.7 + 0.3 * roll);
		//  The waveform (mono), a glowing phosphor line
		float w = wave(uv.x) * 0.8;
		float dy = abs(g.y - w);
		float line = exp(-dy * mix(40.0, 12.0, blur)) + 0.4 * exp(-dy * 7.0);
		tv += Phosphor * line * 2.2;
		//  Vignette of the tube
		tv *= 1.0 - 0.45 * dot(g * 0.7, g * 0.7);
		tv *= scan;
		col = mix(col, tv, inScreen);
	}
	return col;
}

//  The ceiling: dark, with the lamps (the one under this point is enough, they are far apart)
vec3 ceiling(vec3 p, float kk, float blur)
{
	float k = clamp(floor(p.z / (D / LampRows)), 0.0, LampRows - 1.0);
	//  Panels between beams across the corridor
	float bz = fract(p.z / (D / LampRows));
	float beam = smoothstep(0.06, 0.03, min(bz, 1.0 - bz)) + smoothstep(0.04, 0.02, abs(abs(p.x) - LampX * 1.6) / W);
	vec3 col = vec3(0.02, 0.02, 0.024) * (1.0 + beam * 0.8);
	//  The two lamps of this row (and their light on the panels)
	for (int j = 0; j < 2; j++)
	{
		float i = k * 2.0 + float(j);
		vec3 lp = lampPos(i);
		float d = length(p.xz - lp.xz);
		float pw = lampPower(i, kk);
		vec3 lc = lampColor(i);
		float r = 0.2 + blur * 0.15;
		col += lc * pw * (0.035 * (1.0 + beam) / (1.0 + d * d * 1.5) + 3.0 * smoothstep(r, r * 0.55, d) + 0.12 / (1.0 + d * d * 30.0));
	}
	return col;
}

//  The light the lamps, the walls and the TV give a point on the floor (diffuse)
vec3 floorLight(vec3 p, float kk)
{
	vec3 l = vec3(0.0);
	for (int j = 0; j < 16; j++)
	{
		float i = float(j);
		vec3 d = lampPos(i) - p;
		float dd = dot(d, d);
		l += lampColor(i) * lampPower(i, kk) * (d.y / sqrt(dd)) / (1.0 + dd * 0.6);
	}
	//  The meters light the floor next to them (the level of the bars around this z)
	float u = clamp((p.z - 0.6) / (D - 1.6), 0.0, 1.0);
	vec3 sp = spectrum(u);
	vec3 meter = vec3(0.4, 0.9, 0.3);
	l += meter * sp.g * 0.5 * exp(-(p.x + W) * 1.6);
	l += meter * sp.b * 0.5 * exp(-(W - p.x) * 1.6);
	//  The TV
	l += vec3(0.25, 0.45, 0.35) * 0.35 * exp(-(D - p.z) * 0.35) * exp(-abs(p.x) * 0.5);
	return l;
}

//  The color of a surface seen along rd (not the floor), blur > 0 for a reflection
vec3 surface(vec3 p, float id, float kk, float blur)
{
	if (id < 0.5)
		return meterWall(p, vec3(0.0, 1.0, 0.0), blur);
	if (id < 1.5)
		return meterWall(p, vec3(0.0, 0.0, 1.0), blur);
	if (id < 2.5)
		return vec3(0.02);
	if (id < 3.5)
		return ceiling(p, kk, blur);
	if (id < 4.5)
		return tvWall(p, blur);
	return vec3(0.01);
}

//  The haze along a ray: the fog and the light shafts of the lamps (in-scattering, stronger right under a lamp)
vec3 haze(vec3 ro, vec3 rd, float t, float kk, vec3 col)
{
	vec3 glow = vec3(0.0);
	for (int j = 0; j < 16; j++)
	{
		float i = float(j);
		vec3 lp = lampPos(i);
		float s = clamp(dot(lp - ro, rd), 0.0, t);
		vec3 q = ro + rd * s - lp;
		//  A cone of light down from the lamp
		float below = clamp(-q.y / H, 0.0, 1.0);
		float r2 = dot(q.xz, q.xz) / (0.03 + below * below * 0.6);
		glow += lampColor(i) * lampPower(i, kk) * (exp(-r2 * 2.5) * (0.4 + below) + 0.15 / (1.0 + dot(q, q) * 6.0));
	}
	float fog = 1.0 - exp(-t * Haze);
	vec3 fogCol = vec3(0.03, 0.035, 0.05) + kk * 0.03;
	return mix(col, fogCol, fog) + glow * Haze * 1.6;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
	float tm = iTime;
	float kk = kick();
	//  The camera: sways sideways and up / down and looks around (its depth stays)
	vec3 ro = vec3(0.55 * sin(tm * 0.23) * Motion, 1.45 + 0.22 * sin(tm * 0.31 + 1.0) * Motion, 0.0);
	float yaw = (0.26 * sin(tm * 0.17) + 0.08 * sin(tm * 0.43)) * Motion;
	float pitch = (0.07 * sin(tm * 0.13 + 2.0) + 0.03 * sin(tm * 0.37)) * Motion - 0.02;
	float roll = 0.02 * sin(tm * 0.21) * Motion;
	vec2 q = (fragCoord - 0.5 * iResolution.xy) / iResolution.y;
	vec3 rd = normalize(vec3(q, 1.3));
	rd.xy = mat2(cos(roll), -sin(roll), sin(roll), cos(roll)) * rd.xy;
	rd.yz = mat2(cos(pitch), -sin(pitch), sin(pitch), cos(pitch)) * rd.yz;
	rd.xz = mat2(cos(yaw), sin(yaw), -sin(yaw), cos(yaw)) * rd.xz;

	vec2 h = trace(ro, rd);
	vec3 p = ro + rd * h.x;
	vec3 col;
	if (h.y > 1.5 && h.y < 2.5)
	{
		//  The floor: dark tiles lit by the room, and a rough reflection (a few jittered rays)
		vec2 tile = fract(p.xz * vec2(1.0, 1.0)) - 0.5;
		float grout = smoothstep(0.47, 0.5, max(abs(tile.x), abs(tile.y)));
		vec3 base = vec3(0.05, 0.05, 0.055) * (1.0 - 0.5 * grout) * (0.9 + 0.1 * hash(floor(p.xz)));
		col = base * floorLight(p, kk);
		vec3 refl = vec3(0.0);
		float rough = Roughness * (1.0 + grout * 2.0);
		//  (a stable pattern per pixel, interleaved gradient noise: no flicker)
		float a0 = fract(52.9829189 * fract(dot(fragCoord, vec2(0.06711056, 0.00583715)))) * 6.2832;
		for (int k = 0; k < 6; k++)
		{
			float a = a0 + float(k) * 2.3999632;
			float r = rough * sqrt((float(k) + 0.5) / 6.0);
			vec3 n = normalize(vec3(cos(a) * r, 1.0, sin(a) * r));
			vec3 rr = reflect(rd, n);
			if (rr.y <= 0.0)
				rr.y = 0.001;
			vec2 h2 = trace(p + rr * 0.001, rr);
			vec3 p2 = p + rr * h2.x;
			vec3 c2 = surface(p2, h2.y, kk, clamp(rough * 4.0, 0.0, 1.0));
			refl += mix(c2, vec3(0.03, 0.035, 0.05), 1.0 - exp(-h2.x * Haze));
		}
		refl /= 6.0;
		float fres = 0.08 + 0.92 * pow(1.0 - max(dot(-rd, vec3(0.0, 1.0, 0.0)), 0.0), 5.0);
		col += refl * Gloss * mix(0.45, 1.0, fres) * (1.0 - 0.6 * grout);
	}
	else
		col = surface(p, h.y, kk, 0.0);
	col = haze(ro, rd, h.x, kk, col);

	//  Grading: a soft tone curve, a vignette, grain
	col = 1.0 - exp(-col * 1.35);
	col = pow(col, vec3(0.92, 0.95, 1.0));
	vec2 v = fragCoord / iResolution.xy - 0.5;
	col *= 1.0 - 0.55 * dot(v, v) * 1.6;
	col += (hash(fragCoord + fract(tm * 13.0) * 100.0) - 0.5) * 0.018;
	fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
