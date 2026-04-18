precision highp float;

uniform float time;
uniform vec2 resolution;
uniform vec2 mouse;
uniform vec4 scroll;

uniform sampler2D tex;

//	Variables

const float TileCountX = 6.0; // var:
const float TileCountY = 9.0; // var:
const float TileCount = 54.0; // var:

const float ImageWidth = 16.0; // var:
const float ImageHeight = 9.0; // var:

const float PageWidth = 16.0; // var:
const float PageHeight = 9.0; // var:

const float ScrollSpeed = 0.25; // var:
const float ScrollSpeedX = 0.0; // var:
const float ScrollSpeedY = 0.0; // var:
const float ScrollSpeedRX = 0.0; // var:
const float ScrollSpeedRY = 0.0; // var:
const float MouseSpeedX = 0.0; // var:
const float MouseSpeedY = 0.0; // var:

const float ImageCount = 32.0; // var:
const float LeftDist = 0.75; // var:

const float ShadowDistTop = 0.1; // var:
const float ShadowDistBottom = 1.0; // var:


const float ShadowIntTop = 0.7; // var:
const float ShadowIntBottom = 0.2; // var:

const float VingetteInt = 20.0; // var:
const float VingettePow = 1.0; // var:
const float NoiseInt = 0.0; // var:

//	Computed

const float CHA = -cos(3.14159265359 / ImageCount);
const float A = 3.14159265359 * 2.0/ ImageCount;
const float SHA = sin(3.14159265359 * 2.0/ ImageCount);
const float EXP = 1.0 + SHA * ImageWidth / ImageHeight;
const float SPX = 1.5 / PageWidth;
const float SPY = 1.5 / PageHeight;


vec4 Image(float index, vec2 uv, bool blur)
{
	float u = (mod(index, TileCountX) + uv.x) / TileCountX;
	float v = (floor(index / TileCountX) + uv.y) / TileCountY;
	vec4 t = texture2D(tex, vec2(u, v));
	if (blur)
	{
		t += texture2D(tex, vec2(u - SPX, v));
		t += texture2D(tex, vec2(u + SPX, v));
		t += texture2D(tex, vec2(u, v - SPY));
		t += texture2D(tex, vec2(u, v + SPY));
		t += texture2D(tex, vec2(u - SPX * 2.0, v));
		t += texture2D(tex, vec2(u + SPX * 2.0, v));
		t += texture2D(tex, vec2(u, v - SPY * 2.0));
		t += texture2D(tex, vec2(u, v + SPY * 2.0));
		t *= (1.0 / 9.0);
	}
	t.xyz *= t.a;
    return t;
}

float rand(vec2 n) { 
	return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

float noise(vec2 n) {
	vec2 b = floor(n), f = smoothstep(vec2(0.0), vec2(1.0), fract(n));
	return mix(mix(rand(b), rand(b + vec2(1.0, 0.0)), f.x), mix(rand(b + vec2(0.0, 1.0)), rand(b + 1.0), f.x), f.y);
}

float Vingette(vec2 uv)
{
    uv = -uv * uv.yx + uv;   // MAD
    float vig = uv.x*uv.y * VingetteInt; //
	if (VingettePow == 0.5)
		return sqrt(vig);
	if (VingettePow == 1.0)
		return vig;
	if (VingettePow == 2.0)
		return vig * vig;
	return pow(vig, VingettePow);
}

vec4 GetWheel(float innerRad, float outerRad, float invSize, vec2 center, bool blur)
{
	vec2 v = gl_FragCoord.xy - center;
	float r = sqrt(dot(v, v));
	if (r > outerRad)
		return vec4(0,0,0,0);
	float tilePos = atan(v.x, v.y) * (-ImageCount / (3.14159265359 * 2.0)) + ImageCount;
	if (ScrollSpeed != 0.0)
		tilePos += time * (-ScrollSpeed);
	if (MouseSpeedX != 0.0)
		tilePos += mouse.x * (-MouseSpeedX / ImageWidth);
	if (MouseSpeedY != 0.0)
		tilePos += mouse.y * (-MouseSpeedY / ImageHeight);
	if (ScrollSpeedX != 0.0)
		tilePos += scroll.x * ScrollSpeedX;
	if (ScrollSpeedY != 0.0)
		tilePos += scroll.y * ScrollSpeedY;
	if (ScrollSpeedRX != 0.0)
		tilePos += scroll.z * ScrollSpeedRX;
	if (ScrollSpeedRY != 0.0)
		tilePos += scroll.w * ScrollSpeedRY;
	tilePos = mod(tilePos, TileCount);
	float tileIndex = floor(tilePos);
	float tx = (r - innerRad) * invSize;
	float ty = fract(tilePos);
	float s = ty - 0.5;
	float c = CHA / cos(abs(s) * A);
	tx = (c * innerRad + innerRad) * invSize + tx;
	tx = clamp(tx, 0.0, 1.0);

	ty = mix(ty, s * EXP + 0.5, tx);
	ty = clamp(ty, 0.0, 1.0);

	return Image(tileIndex, vec2(tx, ty), blur);
}

void main(void)
{
	float innerRad = (resolution.x + resolution.y) * 0.3;
	float outerRad = innerRad * EXP;
	float size = outerRad - innerRad;
	float invSize = 1.0 / size;
	vec2 center = vec2(size * LeftDist - innerRad, resolution.y * 0.5);
	
	vec4 color = GetWheel(innerRad, outerRad, invSize, center, false);
	vec2 uv = gl_FragCoord.xy / resolution;
	if (((ShadowIntBottom > 0.0) || (ShadowIntTop > 0.0)) && ((ShadowDistBottom > 0.0) || (ShadowDistTop > 0.0)))
	{
		float shadowBias = dot(uv, vec2(-0.2, 0.8)) + 0.2;
		float shadowDist = mix(ShadowDistBottom, ShadowDistTop, shadowBias) * size;
		float shadowAlpha = mix(ShadowIntBottom, ShadowIntTop, shadowBias);
		vec4 shadow = GetWheel(innerRad, outerRad, invSize, vec2(0.15, -0.2) * shadowDist + center, true) * shadowAlpha;
		shadow.rgb = vec3(0.01, 0.02, 0.03) * shadow.a;
		color += (shadow * -color.a + shadow);
	}
	if (VingetteInt > 0.0)
		color *= Vingette(uv);
	if (NoiseInt > 0.0)
		color *= (noise(vec2(gl_FragCoord) * -15.5 + (time * vec2(121.12, 1445.23))) * NoiseInt + (1.0 - NoiseInt * 0.5));
    gl_FragColor = color;
    //gl_FragColor = vec4(shadowBias, shadowBias, shadowBias, 1.0);

}