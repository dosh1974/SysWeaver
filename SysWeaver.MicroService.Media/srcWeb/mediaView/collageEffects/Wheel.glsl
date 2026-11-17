// =============================================================================
//  Wheel - photo carousel collage
// =============================================================================
//  The user's pictures sit on a large wheel whose hub is off screen left; the
//  visible arc of photos turns slowly past the viewer, each photo straightened
//  to a flat card, with a soft blurred drop shadow and a vignette. Transparent
//  background (premultiplied alpha), made for overlays / web backgrounds.
//
//  Live controls:
//  iMouse.x = Zoom | Size of the wheel and its photos | 0.5 | 0.5 .. 1.5 | x
//  iMouse.y = Position | Pushes the wheel in from the left / out to the left | 0.5 | -1.0 .. 1.0
//  iMouse.z = Spin | Turns the wheel by hand, in photos | 0.5 | -4.0 .. 4.0 | photos
//  iMouse.w = Shadow | Drop shadow strength multiplier | 0.5 | 0.0 .. 2.0 | x
// =============================================================================

precision highp float;

uniform float time;
uniform vec2 resolution;
uniform vec2 mouse;
uniform vec4 scroll;
uniform vec4 iMouse;		// live controls, each channel 0..1

uniform sampler2D tex;

#define WheelZoom    mix(0.5, 1.5, iMouse.x)
#define WheelShift   mix(-1.0, 1.0, iMouse.y)
#define WheelSpin    mix(-4.0, 4.0, iMouse.z)
#define ShadowAmount mix(0.0, 2.0, iMouse.w)

// --- Collage layout (written by the editor from the user's pictures) ---
const float TileCountX = 6.0; // var:
const float TileCountY = 9.0; // var:
const float TileCount = 54.0; // var:

const float ImageWidth = 16.0; // var:
const float ImageHeight = 9.0; // var:

const float PageWidth = 16.0; // var:
const float PageHeight = 9.0; // var:

// --- Wheel ---
const float ScrollSpeed = 0.25; // var: { "min": -2.0, "max": 2.0, "step": 0.01, "name": "Speed", "desc": "Turning speed of the wheel (negative = reverse)", "unit": "photos/s" }
const float ImageCount = 32.0; // var: { "min": 12.0, "max": 64.0, "step": 1.0, "name": "Photos Per Turn", "desc": "Slots on the full wheel: more = narrower wedges, flatter arc" }
const float WheelRadius = 0.3; // var: { "min": 0.1, "max": 0.6, "step": 0.01, "name": "Wheel Radius", "desc": "Inner radius of the wheel relative to the screen size: larger = bigger photos" }
const float LeftDist = 0.75; // var: { "min": -0.5, "max": 2.0, "step": 0.01, "name": "Left Distance", "desc": "How far the photo ring reaches into the screen, in photo widths" }

// --- Shadow ---
const float ShadowIntTop = 0.7; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shadow Top", "desc": "Shadow opacity at the top right" }
const float ShadowIntBottom = 0.2; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shadow Bottom", "desc": "Shadow opacity at the bottom left" }
const float ShadowDistTop = 0.1; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Shadow Distance Top", "desc": "Shadow offset at the top right (photo widths): larger = photos float higher" }
const float ShadowDistBottom = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.01, "name": "Shadow Distance Bottom", "desc": "Shadow offset at the bottom left (photo widths)" }
const vec3 ShadowColor = vec3(0.01, 0.02, 0.03); // var: { "type": "col", "name": "Shadow Color", "desc": "Tint of the drop shadow" }
const vec2 ShadowOffset = vec2(0.15, -0.2); // var: { "min": -0.5, "max": 0.5, "step": 0.01, "name": "Shadow Direction", "desc": "Direction the shadow falls (x right, y up)" }
const float BlurRadius = 1.5; // var: { "min": 0.0, "max": 4.0, "step": 0.1, "name": "Shadow Blur", "desc": "Softness of the shadow edge", "unit": "px" }

// --- Post ---
const float VingetteInt = 20.0; // var: { "min": 0.0, "max": 60.0, "step": 0.5, "name": "Vignette", "desc": "Vignette spread: lower = darker / more transparent edges, 0 = off, 16 = center unchanged" }
const float VingettePow = 1.0; // var: { "min": 0.1, "max": 3.0, "step": 0.05, "name": "Vignette Falloff", "desc": "Vignette curve: lower = flatter center, harder edges" }
const float NoiseInt = 0.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Noise", "desc": "Flickering grain over the photos", "scale": 100, "unit": "%" }
const float NoiseScale = 15.5; // var: { "min": 1.0, "max": 40.0, "step": 0.5, "name": "Noise Scale", "desc": "Grain frequency: higher = finer grain" }

// --- Web page input (scroll / mouse uniforms, 0 = off) ---
const float ScrollSpeedX = 0.0; // var: { "min": -2.0, "max": 2.0, "step": 0.01, "name": "Scroll X", "desc": "Turns the wheel with the page's horizontal scroll" }
const float ScrollSpeedY = 0.0; // var: { "min": -2.0, "max": 2.0, "step": 0.01, "name": "Scroll Y", "desc": "Turns the wheel with the page's vertical scroll" }
const float ScrollSpeedRX = 0.0; // var: { "min": -2.0, "max": 2.0, "step": 0.01, "name": "Scroll Rel X", "desc": "Turns the wheel with the relative horizontal scroll" }
const float ScrollSpeedRY = 0.0; // var: { "min": -2.0, "max": 2.0, "step": 0.01, "name": "Scroll Rel Y", "desc": "Turns the wheel with the relative vertical scroll" }
const float MouseSpeedX = 0.0; // var: { "min": -2.0, "max": 2.0, "step": 0.01, "name": "Mouse X", "desc": "Turns the wheel with the pointer's x position" }
const float MouseSpeedY = 0.0; // var: { "min": -2.0, "max": 2.0, "step": 0.01, "name": "Mouse Y", "desc": "Turns the wheel with the pointer's y position" }

// --- Derived ---
const float CHA = -cos(EW_PI / ImageCount);						// chord factor of one slot
const float A = EW_TAU / ImageCount;							// slot angle
const float SHA = sin(EW_TAU / ImageCount);
const float EXP = 1.0 + SHA * ImageWidth / ImageHeight;			// outer / inner radius
const float SPX = BlurRadius / PageWidth;						// blur step in atlas uv
const float SPY = BlurRadius / PageHeight;

// Fetches photo `index` from the atlas at uv in [0, 1]; blur = 9 tap cross (shadow). Premultiplied.
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

// Samples the wheel at this pixel: angle -> photo slot, radius -> position across the photo.
// blur: soft 9 tap cross, used for the drop shadow. Returns premultiplied color (0 outside the ring).
vec4 GetWheel(float innerRad, float outerRad, float invSize, vec2 center, bool blur)
{
	vec2 v = gl_FragCoord.xy - center;
	float r = sqrt(dot(v, v));
	if (r > outerRad)
		return vec4(0,0,0,0);
	// Angle in photo slots (ImageCount slots per turn), clockwise
	float tilePos = atan(v.x, v.y) * (-ImageCount / EW_TAU) + ImageCount;
	if (ScrollSpeed != 0.0)
		tilePos -= mod(time * ScrollSpeed, TileCount);	// wrapped: stays precise over long runs
	tilePos += WheelSpin;
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
	// Straighten the wedge: the inner edge follows the chord of the slot, so photos stay flat rectangles
	float s = ty - 0.5;
	float c = CHA / cos(abs(s) * A);
	tx = (c * innerRad + innerRad) * invSize + tx;
	tx = clamp(tx, 0.0, 1.0);

	// Widen the slot outwards so neighbouring photos meet edge to edge
	ty = mix(ty, s * EXP + 0.5, tx);
	ty = clamp(ty, 0.0, 1.0);

	return Image(tileIndex, vec2(tx, ty), blur);
}

void main(void)
{
	// Wheel ring in pixels (innerRad .. outerRad), its hub sits off screen to the left
	float innerRad = (resolution.x + resolution.y) * WheelRadius * WheelZoom;
	float outerRad = innerRad * EXP;
	float size = outerRad - innerRad;
	float invSize = 1.0 / size;
	vec2 center = vec2(size * (LeftDist + WheelShift) - innerRad, resolution.y * 0.5);

	vec4 color = GetWheel(innerRad, outerRad, invSize, center, false);
	vec2 uv = gl_FragCoord.xy / resolution;
	float shadowIntTop = ShadowIntTop * ShadowAmount;
	float shadowIntBottom = ShadowIntBottom * ShadowAmount;
	if (((shadowIntBottom > 0.0) || (shadowIntTop > 0.0)) && ((ShadowDistBottom > 0.0) || (ShadowDistTop > 0.0)))
	{
		// Drop shadow: a blurred copy of the wheel, offset more (and fainter) towards the bottom left
		float shadowBias = dot(uv, vec2(-0.2, 0.8)) + 0.2;
		float shadowDist = mix(ShadowDistBottom, ShadowDistTop, shadowBias) * size;
		float shadowAlpha = mix(shadowIntBottom, shadowIntTop, shadowBias);
		vec4 shadow = GetWheel(innerRad, outerRad, invSize, ShadowOffset * shadowDist + center, true) * shadowAlpha;
		shadow.rgb = ShadowColor * shadow.a;
		color += (shadow * -color.a + shadow);	// shadow behind the photos (premultiplied "under")
	}
	if (VingetteInt > 0.0)
		color *= EwVignette(uv, VingetteInt, VingettePow, 1.0);
	if (NoiseInt > 0.0)
		color *= (EwValueNoise2D(gl_FragCoord.xy * -NoiseScale + (mod(time, 100.0) * vec2(121.12, 1445.23))) * NoiseInt + (1.0 - NoiseInt * 0.5));
	gl_FragColor = color;
}
