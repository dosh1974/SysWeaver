// Corridor: a hazy corridor drawn by the sound (ShaderToy style: the audio texture is iChannel0, 512 x 2 texels,
// row 0 (y = 0.25) the spectrum, row 1 (y = 0.75) the waveform; r = mono, g = left, b = right).
//  - the left / right walls are LED spectrum meters of the left / right channel (bass near the camera, treble far away)
//  - the floor is glossy: a rough (diffuse) reflection of the room, lit by the lamps, the walls and the TV
//  - the far wall is a retro TV: noise, scan lines, a rolling bar and the waveform on top
//  - the ceiling is two rows of colored lamps that flash on the bass drum
//  - haze: fog and light shafts under the lamps
// The camera looks around and sways sideways / up and down, it never moves along the corridor.
//  media: audio stock:YuE2_00056.flac

//  Live controls:
//  iMouse.x = Haze | Fog and light shafts | 0.06 | 0.0 .. 0.3
//  iMouse.y = Floor gloss | Strength of the floor reflection | 1.0 | 0.0 .. 1.5
//  iMouse.z = Bass threshold | Bass level where the lamps start to flash | 0.55 | 0.0 .. 1.0
//  iMouse.w = Camera motion | How much the camera looks and sways | 0.25 | 0.0 .. 2.0
#define Haze mix(0.0, 0.3, iMouse.x)
#define Gloss mix(0.0, 1.5, iMouse.y)
#define BassFrom mix(0.0, 1.0, iMouse.z)
#define Motion mix(0.0, 2.0, iMouse.w)

// --- Look ---
const float Exposure = 1.35;					// var: { "min": 0.5, "max": 3, "step": 0.05, "name": "Exposure", "desc": "Overall brightness of the picture" }
const vec3 Phosphor = vec3(0.35, 1.0, 0.55);	// var: { "type": "colhdr", "name": "Waveform color", "desc": "The color of the waveform on the TV" }
const float LampHueSpeed = 0.02;				// var: { "min": 0, "max": 0.2, "step": 0.005, "name": "Lamp color drift", "desc": "How fast the lamp colors cycle" }
const float LampIdle = 0.06;					// var: { "min": 0, "max": 0.5, "step": 0.01, "name": "Lamp idle glow", "desc": "Lamp brightness without bass" }
const float Roughness = 0.12;					// var: { "min": 0, "max": 0.5, "step": 0.01, "name": "Floor roughness", "desc": "How blurry (diffuse) the floor reflection is" }

// --- Structure and fine tuning ---
const float Bars = 32.0;						// var: { "min": 8, "max": 96, "step": 1, "name": "Bars", "desc": "The number of spectrum bars on each wall" }
const float StaticRate = 24.0;					// var: { "min": 0, "max": 60, "step": 1, "name": "TV static rate", "desc": "How often the TV noise changes", "unit": "fps" }
const float VignetteAmount = 0.55;				// var: { "min": 0, "max": 1, "step": 0.01, "name": "Vignette", "desc": "Darkening of the corners" }
const float Grain = 0.018;						// var: { "min": 0, "max": 0.1, "step": 0.002, "name": "Grain", "desc": "Film grain strength" }
const float YawAmount = 0.4;					// var: { "min": 0, "max": 2, "step": 0.05, "name": "Yaw", "desc": "How far the camera turns left / right (heading), 1 = the original swing", "scale": 100, "unit": "%" }
const float CamZ = -1.5;						// var: { "min": -5, "max": 1.5, "step": 0.05, "name": "Camera depth", "desc": "The camera's position along the corridor (negative = further back, the meters start at 1.6, the TV is at 11)", "unit": "m" }

//  The room: x in [-W, W], y in [0, H], z in [BackZ, D] (the camera at z = CamZ, the TV wall at z = D)
const float W = 2.0;
const float H = 3.0;
const float D = 11.0;
const float LampRows = 8.0;
const float LampX = 1.0;
//  The wall behind the camera, and where the spectrum meters start / end along the corridor
const float BackZ = CamZ - 1.5;
const float MeterZ0 = 1.6;
const float MeterZ1 = D - 1.0;

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
//  (textureLod: the texture has no mip levels, and without screen derivatives the callers can really branch)
vec3 spectrum(float u)
{
	float x = 0.004 + pow(clamp(u, 0.0, 1.0), 1.7) * 0.4;
	return textureLod(iChannel0, vec2(x, 0.25), 0.0).rgb;
}

//  The mono waveform at x (0..1): -1..1
float wave(float x)
{
	return textureLod(iChannel0, vec2(clamp(x, 0.0, 1.0), 0.75), 0.0).r * 2.0 - 1.0;
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

//  Lamp i (0..15): two rows along the ceiling, lamp i = row k = i / 2, left (even i, x = -LampX) or right (x = LampX),
//  at (x, LampY, (k + 0.6) * LampDz)
const float LampY = H - 0.03;
const float LampDz = D / LampRows;

//  From the vertex stage (the same for every pixel, but the ray)
flat varying vec4 vCam;			// xyz the camera position, w the kick
varying vec3 vRay;				// the ray direction, not normalized
flat varying vec4 vLamp[8];		// rgb the color of the left lamp of row k (= of the right lamp of row k - 4), a its power
flat varying vec4 vLampR[2];	// the powers of the right lamps
//  color * power of the left / right lamp of a row (set in mainImage)
vec3 gLampL[8];
vec3 gLampR[8];

vec3 lampColor(float i)
{
	float row = mod(i, 2.0);
	float k = floor(i * 0.5);
	return hsv(fract(k / LampRows + row * 0.5 + iTime * LampHueSpeed), 0.75, 1.0);
}

//  The brightness of lamp i: a glow, flashing on the kick (each lamp a bit differently, a wave running along the corridor)
float lampPower(float i, float kk)
{
	float k = floor(i * 0.5);
	float w = 0.5 + 0.5 * sin(iTime * 2.1 - k * 0.9 + mod(i, 2.0) * 1.7);
	return LampIdle + kk * (0.5 + 2.2 * w * w);
}

//  The nearest surface from inside the room: (t, id) id 0 = left wall, 1 = right wall, 2 = floor, 3 = ceiling, 4 = TV wall, 5 = the wall behind
//  (per axis only the plane the ray runs towards can be hit: 3 slabs, no branches)
vec2 trace(vec3 ro, vec3 rd)
{
	vec3 pos = step(0.0, rd);
	vec3 t3 = mix(vec3(W, 0.0, -BackZ) + ro, vec3(W, H, D) - ro, pos) / max(abs(rd), 1e-7);
	float t = t3.x;
	float id = pos.x;
	if (t3.y < t) { t = t3.y; id = 2.0 + pos.y; }
	if (t3.z < t) { t = t3.z; id = 5.0 - pos.z; }
	return vec2(t, id);
}

//  A spectrum wall: LED bars along z (bass near the camera), the level of its channel (id 0 = left, 1 = right) up the wall; blur > 0 for a reflection
vec3 meterWall(vec3 p, float id, float blur)
{
	float u = clamp((p.z - MeterZ0) * (1.0 / (MeterZ1 - MeterZ0)), 0.0, 1.0);
	float bx = u * Bars;
	float bar = (floor(bx) + 0.5) / Bars;
	vec3 sp = spectrum(bar);
	float level = clamp((id < 0.5 ? sp.g : sp.b) * 1.1, 0.0, 1.0);
	const float Segs = 22.0;
	float yv = (p.y - 0.15) / (H - 0.45);
	float sy = yv * Segs;
	float seg = (floor(sy) + 0.5) / Segs;
	float ey = 0.002 + blur * 0.04, ez = 0.01 + blur * 0.25;
	float inside = smoothstep(-ey, ey, yv) * smoothstep(-ey, ey, 1.0 - yv) * smoothstep(-ez, ez, p.z - MeterZ0) * smoothstep(-ez, ez, MeterZ1 - p.z);
	//  The LED cells (a reflection sees them blurred: the light of the bar, not the cells)
	vec2 c = vec2(fract(bx), fract(sy)) - 0.5;
	float led = smoothstep(0.5, 0.28, abs(c.x)) * smoothstep(0.5, 0.22, abs(c.y));
	led = mix(led, 0.55, blur);
	float lit = mix(step(seg, level), smoothstep(level + 0.05, level - 0.05, yv), blur);
	vec3 col = mix(vec3(0.1, 1.0, 0.25), vec3(1.0, 0.85, 0.1), smoothstep(0.35, 0.65, yv));
	col = mix(col, vec3(1.0, 0.12, 0.08), smoothstep(0.7, 0.9, yv));
	//  The top lit cell of a bar burns brighter
	float top = smoothstep(0.9, 1.0, 1.0 - abs(seg - level) * (Segs * 0.5)) * step(seg, level);
	vec3 e = col * led * inside * (0.035 + lit * (1.6 + top * 1.5));
	//  The dark metal panel around the LEDs
	vec3 panel = vec3(0.018, 0.02, 0.026) * (0.8 + 0.2 * step(0.5, fract(p.z * 0.5)));
	//  A metal frame around the meter (its start reads as the end of a display, not a cut), lit a bit by the nearest bars
	float fd = max(max(MeterZ0 - p.z, p.z - MeterZ1), max(0.15 - p.y, p.y - (H - 0.3)));
	float fw = 0.035 + blur * 0.1;
	float rim = smoothstep(fw, fw * 0.5, abs(fd - 0.06));
	vec3 spill = col * (0.04 + 0.25 * level) * exp(-max(fd, 0.0) * 5.0);
	panel += vec3(0.07, 0.075, 0.09) * rim + spill * (1.0 - inside) * 0.5;
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
		float n = hash(floor(uv * vec2(220.0, 160.0)) + floor(iTime * StaticRate) * vec2(17.3, 3.7));
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

//  The light of one ceiling lamp at the squared distance d2 on the ceiling (its disc and its light on the panels)
float lampSpot(float d2, float beam, float r)
{
	//  (a blurred disc, r grown, keeps about its energy: no bright sparkles in the rough reflection)
	float e = min(1.0, 0.075 / (r * r));
	return 0.035 * (1.0 + beam) / (1.0 + d2 * 1.5) + 3.0 * e * smoothstep(r, r * 0.55, sqrt(d2)) + 0.12 / (1.0 + d2 * 30.0);
}

//  The ceiling: dark, with the lamps (the one under this point is enough, they are far apart)
vec3 ceiling(vec3 p, float blur)
{
	float kz = p.z * (1.0 / LampDz);
	float k = clamp(floor(kz), 0.0, LampRows - 1.0);
	//  Panels between beams across the corridor
	float bz = fract(kz);
	float beam = smoothstep(0.06, 0.03, min(bz, 1.0 - bz)) + smoothstep(0.04, 0.02, abs(abs(p.x) - LampX * 1.6) / W);
	vec3 col = vec3(0.02, 0.02, 0.024) * (1.0 + beam * 0.8);
	//  The two lamps of this row (and their light on the panels)
	float dz = p.z - (k + 0.6) * LampDz;
	float dz2 = dz * dz;
	float r = 0.2 + blur * 0.35;
	vec2 dx = p.x + vec2(LampX, -LampX);
	int ki = int(k);
	col += gLampL[ki] * lampSpot(dx.x * dx.x + dz2, beam, r) + gLampR[ki] * lampSpot(dx.y * dx.y + dz2, beam, r);
	return col;
}

//  The light the lamps, the walls and the TV give a point on the floor (diffuse)
vec3 floorLight(vec3 p)
{
	vec3 l = vec3(0.0);
	vec2 dx = p.x + vec2(LampX, -LampX);
	vec2 c = dx * dx + LampY * LampY;
	for (int k = 0; k < 8; k++)
	{
		float dz = (float(k) + 0.6) * LampDz - p.z;
		vec2 dd = c + dz * dz;
		vec2 f = LampY * inversesqrt(dd) / (1.0 + dd * 0.6);
		l += gLampL[k] * f.x + gLampR[k] * f.y;
	}
	//  The meters light the floor next to them (the level of the bars around this z)
	float u = clamp((p.z - MeterZ0) * (1.0 / (MeterZ1 - MeterZ0)), 0.0, 1.0);
	vec3 sp = spectrum(u);
	vec3 meter = vec3(0.4, 0.9, 0.3);
	l += meter * (0.5 * (sp.g * exp(-(p.x + W) * 1.6) + sp.b * exp(-(W - p.x) * 1.6)));
	//  The TV
	l += vec3(0.25, 0.45, 0.35) * (0.35 * exp(-(D - p.z) * 0.35 - abs(p.x) * 0.5));
	return l;
}

//  The color of a surface seen along rd (not the floor), blur > 0 for a reflection
vec3 surface(vec3 p, float id, float blur)
{
	if (id < 1.5)
		return meterWall(p, id, blur);
	if (id < 2.5)
		return vec3(0.02);
	if (id < 3.5)
		return ceiling(p, blur);
	if (id < 4.5)
		return tvWall(p, blur);
	return vec3(0.01);
}

//  The glow of one lamp along a ray: o = ray origin - lamp, s = the point of the ray nearest to the lamp (up to the hit)
float lampGlow(vec3 o, vec3 rd, float s)
{
	vec3 q = o + rd * s;
	//  A cone of light down from the lamp
	float below = clamp(q.y * (-1.0 / H), 0.0, 1.0);
	float r2 = dot(q.xz, q.xz);
	return exp(r2 * (-2.5 / (0.03 + below * below * 0.6))) * (0.4 + below) + 0.15 / (1.0 + (r2 + q.y * q.y) * 6.0);
}

//  The haze along a ray: the fog and the light shafts of the lamps (in-scattering, stronger right under a lamp)
vec3 haze(vec3 ro, vec3 rd, float t, float kk, vec3 col)
{
	vec3 glow = vec3(0.0);
	float roRd = dot(ro, rd);
	float sx = rd.x * LampX;
	for (int k = 0; k < 8; k++)
	{
		float z = (float(k) + 0.6) * LampDz;
		vec3 o = ro - vec3(0.0, LampY, z);
		float s = rd.y * LampY + rd.z * z - roRd;
		glow += gLampL[k] * lampGlow(vec3(o.x + LampX, o.yz), rd, clamp(s - sx, 0.0, t));
		glow += gLampR[k] * lampGlow(vec3(o.x - LampX, o.yz), rd, clamp(s + sx, 0.0, t));
	}
	float fog = 1.0 - exp(-t * Haze);
	vec3 fogCol = vec3(0.03, 0.035, 0.05) + kk * 0.03;
	return mix(col, fogCol, fog) + glow * Haze * 1.6;
}

//  Per frame work (runs for 3 vertices instead of per pixel): the camera, the kick, the lamp colors and powers
void vertexMain(vec2 fragCoord)
{
	float tm = iTime;
	float kk = kick();
	//  The camera: sways sideways and up / down and looks around (its depth stays)
	vec3 ro = vec3(0.55 * sin(tm * 0.23) * Motion, 1.45 + 0.22 * sin(tm * 0.31 + 1.0) * Motion, CamZ);
	float yaw = (0.26 * sin(tm * 0.17) + 0.08 * sin(tm * 0.43)) * Motion * YawAmount;
	float pitch = (0.07 * sin(tm * 0.13 + 2.0) + 0.03 * sin(tm * 0.37)) * Motion - 0.02;
	float roll = 0.02 * sin(tm * 0.21) * Motion;
	vec2 q = (fragCoord - 0.5 * iResolution.xy) / iResolution.y;
	//  (not normalized: affine in fragCoord, the rotations are linear)
	vec3 rd = vec3(q, 1.3);
	rd.xy = mat2(cos(roll), -sin(roll), sin(roll), cos(roll)) * rd.xy;
	rd.yz = mat2(cos(pitch), -sin(pitch), sin(pitch), cos(pitch)) * rd.yz;
	rd.xz = mat2(cos(yaw), sin(yaw), -sin(yaw), cos(yaw)) * rd.xz;
	vCam = vec4(ro, kk);
	vRay = rd;
	float pr[8];
	for (int k = 0; k < 8; k++)
	{
		float i = float(k) * 2.0;
		vLamp[k] = vec4(lampColor(i), lampPower(i, kk));
		pr[k] = lampPower(i + 1.0, kk);
	}
	vLampR[0] = vec4(pr[0], pr[1], pr[2], pr[3]);
	vLampR[1] = vec4(pr[4], pr[5], pr[6], pr[7]);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
	float kk = vCam.w;
	vec3 ro = vCam.xyz;
	vec3 rd = normalize(vRay);
	//  The lamps: color * power (the right lamp of row k has the color of the left lamp 4 rows on)
	for (int k = 0; k < 8; k++)
	{
		gLampL[k] = vLamp[k].rgb * vLamp[k].a;
		gLampR[k] = vLamp[(k + 4) & 7].rgb * vLampR[k >> 2][k & 3];
	}

	vec2 h = trace(ro, rd);
	vec3 p = ro + rd * h.x;
	//  (derivatives in uniform control flow: the floor's footprint, and the angle of one pixel)
	vec2 fwp = fwidth(p.xz);
	float pixA = 1.0 / (1.3 * iResolution.y);
	vec3 col;
	if (h.y > 1.5 && h.y < 2.5)
	{
		//  The floor: dark tiles lit by the room, and a rough reflection (a few jittered rays)
		//  Anti aliased grout: a filtered line over the pixel footprint, fading to its mean when the tiles get tiny
		vec2 tile = fract(p.xz) - 0.5;
		float fw = min(max(fwp.x, fwp.y), 1.0);
		float de = 0.5 - max(abs(tile.x), abs(tile.y));
		float grout = 1.0 - smoothstep(-0.5 * fw, 0.03 + 0.5 * fw, de);
		grout = mix(grout, 0.07, smoothstep(0.08, 0.35, fw));
		vec3 base = vec3(0.05, 0.05, 0.055) * (1.0 - 0.5 * grout) * (0.9 + 0.1 * hash(floor(p.xz)));
		col = base * floorLight(p);
		float rough = Roughness * (1.0 + grout * 2.0);
		float blur = clamp(rough * 4.0, 0.0, 1.0);
		//  (a stable pattern per pixel, interleaved gradient noise: no flicker)
		float a0 = fract(52.9829189 * fract(dot(fragCoord, vec2(0.06711056, 0.00583715)))) * 6.2832;
		//  The golden angle spiral: one cos / sin, then a constant rotation per ray
		vec2 cs = vec2(cos(a0), sin(a0));
		const mat2 GoldenRot = mat2(-0.73736888, 0.67549029, -0.67549029, -0.73736888);
		const vec3 fogCol = vec3(0.03, 0.035, 0.05);
		vec3 refl = vec3(0.0);
		for (int k = 0; k < 6; k++)
		{
			float r = rough * sqrt((float(k) + 0.5) / 6.0);
			vec3 n = vec3(cs.x * r, 1.0, cs.y * r) * inversesqrt(1.0 + r * r);
			cs = GoldenRot * cs;
			vec3 rr = rd - 2.0 * dot(rd, n) * n;
			if (rr.y <= 0.0)
				rr.y = 0.001;
			vec2 h2 = trace(p + rr * 0.001, rr);
			//  Prefilter the reflected surface: the glossy cone (grows with the reflected distance) and the pixel footprint
			//  (grows with the total distance) blur the LED cells, the static, the scan lines instead of noisy sampling
			float bk = clamp(max(blur, h2.x * rough * 3.0 + (h.x + h2.x) * pixA * 8.0), 0.0, 1.0);
			vec3 c2 = surface(p + rr * h2.x, h2.y, bk);
			refl += (c2 - fogCol) * exp(-h2.x * Haze);
		}
		refl = refl / 6.0 + fogCol;
		float f = clamp(1.0 + rd.y, 0.0, 1.0);
		float f2 = f * f;
		float fres = 0.08 + 0.92 * f2 * f2 * f;
		col += refl * Gloss * mix(0.45, 1.0, fres) * (1.0 - 0.6 * grout);
	}
	else
		col = surface(p, h.y, 0.0);
	col = haze(ro, rd, h.x, kk, col);

	//  Grading: a soft tone curve, a vignette, grain
	col = 1.0 - exp(-col * Exposure);
	col.rg = pow(max(col.rg, 0.0), vec2(0.92, 0.95));
	vec2 v = fragCoord / iResolution.xy - 0.5;
	col *= 1.0 - VignetteAmount * dot(v, v) * 1.6;
	col += (hash(fragCoord + fract(iTime * 13.0) * 100.0) - 0.5) * Grain;
	fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
