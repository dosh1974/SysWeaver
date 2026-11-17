//	Scroll - a row of the user's pictures gliding along the top edge of the screen,
//	with a soft drop shadow and a rippling, tinted water reflection below.
//	Transparent output: made as an overlay / web page header over other content.
//
//	Live controls:
//  iMouse.x = Reflection | Brightness of the water reflection | 0.2 | 0.0 .. 1.0 | {0:2}
//  iMouse.y = Ripples | Strength of the reflection / shadow ripples | 0.25 | 0.0 .. 4.0 | {0:2}x
//  iMouse.z = Size | Picture size (zoom of the strip) | 0.25 | 0.5 .. 2.5 | {0:2}x
//  iMouse.w = Spacing | Gap between the pictures (share of a picture width) | 0.2 | 0.0 .. 1.0 | {0:2}

precision highp float;

uniform sampler2D tex;

// glslsandbox uniforms
uniform float time;
uniform vec2 resolution;
uniform vec2 mouse;
uniform vec4 scroll;
uniform vec4 iMouse;	// live parameters (each 0..1)

//	Texture props (atlas layout, written by the editor from the user's pictures)
const float TileCountX = 6.0;         //var:
const float TileCountY = 9.0;         //var:
const float TileWidth = 168.0;        //var:
const float TileHeight = 98.0;        //var:
const float ImageWidth = 160.0;        //var:
const float ImageHeight = 90.0;        //var:

// --- Live controls ---
#define ReflectionIntensity mix(0.0, 1.0, iMouse.x)
#define WaveAmplitude mix(0.0, 4.0, iMouse.y)
#define Size mix(0.5, 2.5, iMouse.z)
#define Spacing mix(0.0, 1.0, iMouse.w)

// --- Colors ---
const vec3 ReflectionTint = vec3(0.8, 0.9, 1.0); // var: { "type": "col", "name": "Reflection Tint", "desc": "Color filter of the water reflection (cool blue by default)" }
const vec3 ShadowColor = vec3(0.01, 0.02, 0.03); // var: { "type": "col", "name": "Shadow Color", "desc": "Color of the drop shadow behind the pictures" }

// --- Motion ---
const float ScrollSpeed = 0.5; // var: { "min": -2.0, "max": 2.0, "step": 0.05, "name": "Scroll Speed", "desc": "Pictures per second the strip slides (negative: to the right)", "unit": "pic/s" }

// --- Reflection & shadow ---
const float ReflectionStretch = 1.25; // var: { "min": 0.25, "max": 3.0, "step": 0.05, "name": "Reflection Length", "desc": "Height of the reflection relative to a picture", "unit": "x" }
const float ShadowIntBottom = 0.25; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Shadow Bottom", "desc": "Shadow opacity at the lower edge of the pictures" }
const float ShadowIntTop = 0.01; // var: { "min": 0.0, "max": 0.1, "step": 0.005, "name": "Shadow Top", "desc": "Shadow opacity in the middle of the pictures" }

// --- Ripples ---
const float WaveSpeed = 1.0; // var: { "min": 0.0, "max": 5.0, "step": 0.05, "name": "Ripple Speed", "desc": "How fast the ripples move", "unit": "x" }
const float WaveFrequency = 1.0; // var: { "min": 0.1, "max": 5.0, "step": 0.05, "name": "Ripple Frequency", "desc": "Number of ripples down the reflection", "unit": "x" }
const float WaveHeight = 1.0; // var: { "min": 0.0, "max": 5.0, "step": 0.05, "name": "Ripple Detail", "desc": "Horizontal variation of the ripples (0 = straight bands)", "unit": "x" }

// --- Vignette ---
const float VingetteInt = 20.0; // var: { "min": 0.0, "max": 100.0, "step": 1.0, "name": "Edge Fade", "desc": "Fade toward the left / right screen edges (0 = off, higher = sharper edges)" }
const float VingettePow = 1.0; // var: { "min": 0.25, "max": 4.0, "step": 0.05, "name": "Edge Fade Curve", "desc": "Shape of the edge fade (higher = softer, wider)" }

// --- Page scroll / pointer coupling ---
const float MouseSpeedX = 0.0; // var: { "min": -1.0, "max": 1.0, "step": 0.01, "name": "Pointer X Shift", "desc": "Strip follows the horizontal pointer position" }
const float MouseSpeedY = 0.0; // var: { "min": -1.0, "max": 1.0, "step": 0.01, "name": "Pointer Y Shift", "desc": "Strip follows the vertical pointer position" }
const float ScrollSpeedX = 0.0; // var: { "min": -1.0, "max": 1.0, "step": 0.01, "name": "Page Scroll X", "desc": "Strip moves with the horizontal page scroll" }
const float ScrollSpeedY = 0.0; // var: { "min": -1.0, "max": 1.0, "step": 0.01, "name": "Page Scroll Y", "desc": "Strip moves with the vertical page scroll" }
const float ScrollSpeedRX = 0.0; // var: { "min": -1.0, "max": 1.0, "step": 0.01, "name": "Relative Scroll X", "desc": "Strip moves with the relative horizontal page scroll" }
const float ScrollSpeedRY = 0.0; // var: { "min": -1.0, "max": 1.0, "step": 0.01, "name": "Relative Scroll Y", "desc": "Strip moves with the relative vertical page scroll" }

//	Computed
#define SpaceScale (1.0 + max(0.0, Spacing))
#define SpacedScaledHeight (TileHeight / SpaceScale)
#define SizeScale (1.0 / (Size * SpaceScale))

//	Horizontal edge fade: 0 at the screen edges, u in [0, 1] across the screen
float Vingette(float u)
{
	u = -u * u + u;
    float vig = u * u * VingetteInt;
	if (VingettePow == 0.5)
		return sqrt(vig);
	if (VingettePow == 1.0)
		return vig;
	if (VingettePow == 2.0)
		return vig * vig;
	return pow(vig, VingettePow);
}

//	Premultiplied atlas sample: integer part of imageIndex = picture, fraction = u inside it
//	(stretched by the spacing so the gap reads the transparent border), dv = v (0 top)
vec4 SampleImage(float imageIndex, float dv)
{
	float du = fract(imageIndex);
	if (SpaceScale > 1.0)
		du = clamp(du * SpaceScale, 0.0, 1.0);
	imageIndex = floor(imageIndex);
	float imageU = (mod(imageIndex, TileCountX) + du) / TileCountX;
	float imageV = fract((floor(imageIndex / TileCountX)+ dv) / -TileCountY);
    vec4 t = texture2D(tex, vec2(imageU, imageV));
	t.xyz *= t.a;
    return t;
}


void main(void)
{
	//	Strip space: pixels scaled by size and spacing, y measured down from the top edge
    vec2 fragCoord = gl_FragCoord.xy * SizeScale;
    float y = resolution.y * SizeScale - fragCoord.y;
	//	Animation (scroll): position along the strip in pictures
    float imageIndex = fragCoord.x * (1.0 / TileWidth); 
	if (ScrollSpeed != 0.0)
		imageIndex += time * (ScrollSpeed);
	if (MouseSpeedX != 0.0)
		imageIndex += mouse.x * (-MouseSpeedX / ImageWidth);
	if (MouseSpeedY != 0.0)
		imageIndex += mouse.y * (-MouseSpeedY / ImageHeight);
	if (ScrollSpeedX != 0.0)
		imageIndex += scroll.x * ScrollSpeedX;
	if (ScrollSpeedY != 0.0)
		imageIndex += scroll.y * ScrollSpeedY;
	if (ScrollSpeedRX != 0.0)
		imageIndex += scroll.z * ScrollSpeedRX;
	if (ScrollSpeedRY != 0.0)
		imageIndex += scroll.w * ScrollSpeedRY;


	float dx = gl_FragCoord.x / resolution.x;	// 0..1 across the screen (edge fade, shadow skew)

    vec4 color = vec4(0.0, 0.0, 0.0, 0.0);
    if (y < SpacedScaledHeight)
    {
		//	Picture row at the top, with a skewed, rippled drop shadow in its lower half
        float dv = y / SpacedScaledHeight;
		color = SampleImage(imageIndex, dv);
		color = SampleImage(imageIndex, dv);
		
		if ((ShadowIntBottom > 0.0) || (ShadowIntTop > 0.0))
		{
			if (y >= (SpacedScaledHeight * 0.5))
			{
				float shadowShift = dx * 1.0 + 0.25;
				imageIndex += dv * shadowShift - shadowShift;
				dv = clamp(dv * 2.0 - 1.0, 0.0, 1.0);
				
				if (WaveAmplitude > 0.0)
				{
					float dist = 1.0 - dv;
					float amp = min(dist * (0.025 * WaveAmplitude), 0.0125 * WaveAmplitude);
					vec2 a = vec2(WaveSpeed * 1.21, WaveSpeed * 2.41) * time;
					a = vec2(WaveFrequency * 15.6, WaveFrequency * 45.1) * dist + a;
					a.x = fragCoord.x * (0.05 * WaveHeight) + a.x;
					imageIndex += cos(a.x) * sin(a.y) * amp;
				}
				vec4 shadow = SampleImage(imageIndex, dv);
				shadow *= mix(ShadowIntTop, ShadowIntBottom, dv);
				shadow.rgb = ShadowColor * shadow.a;	// premultiplied shadow color
				color += (shadow * -color.a + shadow);
			}
		}
		
		
		
    }else {
        if (y < (SpacedScaledHeight * (1.0 + ReflectionStretch)))
        {
			//	Reflection
            float dv = y  * (1.0 / (SpacedScaledHeight * ReflectionStretch)) + (-1.0 / ReflectionStretch);
			if (WaveAmplitude > 0.0)
			{
				float amp = min(dv * (0.025 * WaveAmplitude), 0.0125 * WaveAmplitude);
				vec2 a = vec2(WaveSpeed * 1.21, WaveSpeed * 2.41) * time;
				a = vec2(WaveFrequency * ReflectionStretch * 21.6, WaveFrequency * ReflectionStretch * 53.1) * (1.0 - dv) + a;
				a.x = fragCoord.x * (0.05 * WaveHeight) + a.x;
				imageIndex += cos(a.x) * sin(a.y) * amp;
			}
			dv = 1.0 - dv;
			color = SampleImage(imageIndex, dv);
            color *= vec4(ReflectionTint * ReflectionIntensity, ReflectionIntensity);	// tinted, faded mirror image
            color *= dv;	// fade out away from the pictures


        }
    }
	if (VingetteInt > 0.0)
		color *= Vingette(dx);
    
	gl_FragColor = color;
}
