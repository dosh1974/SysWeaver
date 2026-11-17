uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;

#define iTime time
#define iResolution resolution
#define iChannel0 tex
#define texture texture2D


// The MIT License
// Copyright © 2013 Javier Meseguer
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions: The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

// Old Movie - turns the picture into a projected silent film: monochrome tinted print,
// gate weave (frame jitter), a breathing lamp vignette with flicker, vertical scratches,
// dust blotches and film grain. The defects change at a film-like frame rate.
//
//  iMouse.x = Flicker | Brightness flicker of the projector lamp | 0.4 | 0.0 .. 1.0
//  iMouse.y = Grain | Strength of the film grain | 0.3 | 0.0 .. 0.5
//  iMouse.z = Vignette | Darkening of the frame edges and corners | 0.4 | 0.0 .. 1.0
//  iMouse.w = Monochrome | Black and white amount (0 = original colors) | 1.0 | 0.0 .. 1.0

#define Flicker       mix(0.0, 1.0, iMouse.x)
#define GrainStrength mix(0.0, 0.5, iMouse.y)
#define VignettePower mix(0.0, 1.0, iMouse.z)
#define Monochrome    mix(0.0, 1.0, iMouse.w)

// --- Film look ---
const vec3 FilmTint = vec3(0.7, 0.7, 0.7); // var: { "type": "col", "name": "Film Tint", "desc": "Color and density of the monochrome print (darker = denser, try a warm sepia)" }
const float FrameRate = 15.0; // var: { "min": 1.0, "max": 30.0, "step": 1.0, "name": "Defect Rate", "desc": "How often per second jitter, flicker, scratches and blotches change", "unit": "fps" }

// --- Projector ---
const float Jitter = 0.002; // var: { "min": 0.0, "max": 0.01, "step": 0.0005, "name": "Gate Weave", "desc": "How far the picture shakes from frame to frame (share of the view)", "scale": 100, "unit": "%" }
const float LampPulse = 0.3; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Lamp Pulse", "desc": "How much the bright center spot changes from frame to frame", "scale": 100, "unit": "%" }

// --- Scratches ---
const bool EnableScratches = true; // var: { "name": "Scratches", "desc": "Vertical scratch lines running through the frame" }
const int MaxScratches = 8; // var: { "min": 0, "max": 8, "step": 1, "name": "Max Scratches", "desc": "Upper limit of scratches per frame (the actual count is random)" }
const float ScratchStrength = 0.5; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Scratch Strength", "desc": "Contrast of the scratches", "scale": 100, "unit": "%" }
const float ScratchSoftness = 8.0; // var: { "min": 2.0, "max": 32.0, "step": 1.0, "name": "Scratch Softness", "desc": "Higher = wider, softer scratch halos; lower = thin crisp lines" }
const float ScratchTilt = 0.01; // var: { "min": 0.0, "max": 0.1, "step": 0.005, "name": "Scratch Tilt", "desc": "Maximum slant of the scratches" }
const float BrightScratchChance = 0.2; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Bright Scratches", "desc": "Share of scratches that are bright instead of dark", "scale": 100, "unit": "%" }

// --- Blotches ---
const bool EnableBlotches = true; // var: { "name": "Blotches", "desc": "Dark dust and dirt spots" }
const float BlotchDensity = 8.0; // var: { "min": 2.0, "max": 8.0, "step": 0.5, "name": "Blotch Density", "desc": "How many dust spots appear (up to 6 per frame)" }
const float BlotchSize = 0.01; // var: { "min": 0.002, "max": 0.05, "step": 0.001, "name": "Blotch Size", "desc": "Maximum radius of a dust spot (share of the view height)" }
const float BlotchCore = 0.2; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Blotch Core", "desc": "Brightness inside the spots (0 = solid dark, 1 = only a soft halo)" }

// --- Grain ---
const bool EnableGrain = true; // var: { "name": "Grain", "desc": "Film grain noise" }

vec2 uv;

// Random value in [0, 1) from a float seed (sine hash on a 2D point).
// No overloaded functions in effects: the shader minifier of the build (shader_minifier 1.6.0) renamed randomLine(float)
// to the minified name of rand(float), two functions with the same signature, the built effect failed to compile.
float rand(float c){
	return EwHashSin2To1(vec2(c,1.0));
}

// One random scratch: a nearly vertical line, darkening (or brightening) factor for this pixel.
float randomLine(float seed)
{
	float b = ScratchTilt * rand(seed);
	float a = rand(seed+1.0);
	float c = rand(seed+2.0) - 0.5;
	float mu = rand(seed+3.0);
	
	float d = pow( abs(a * uv.x + b * uv.y + c), 1.0 / ScratchSoftness );
	// most scratches are dark, some bright
	float l = (mu > BrightScratchChance) ? d : 2.0 - d;
	
	return mix(1.0 - ScratchStrength, 1.0, l);
}

// One random dust blotch: a wobbly dark disc with a soft halo, factor for this pixel.
float randomBlotch(float seed)
{
	float x = rand(seed);
	float y = rand(seed+1.0);
	float s = BlotchSize * rand(seed+2.0);
	
	vec2 p = vec2(x,y) - uv;
	p.x *= iResolution.x / iResolution.y;
	float a = atan(p.y,p.x);
	float v = 1.0;
	float ss = s*s * (sin(6.2831*a*x)*0.1 + 1.0);  // wobbly outline
	
	if ( dot(p,p) < ss ) v = BlotchCore;
	else
		v = pow(dot(p,p) - ss, 1.0/16.0);
	
	// smaller blotches are darker
	return mix(0.3 + 0.2 * (1.0 - (s / (2.0 * BlotchSize))), 1.0, v);
}


void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
	uv = fragCoord.xy / iResolution.xy;
	
	// Frame counter: all defects change FrameRate times per second
	float t = float(int(iTime * FrameRate));
		
	// Gate weave: shake the picture a little per frame
	vec2 suv = uv + Jitter * vec2( rand(t), rand(t + 23.0));
		
	vec4 image = texture( iChannel0, suv );
		
	// Monochrome print (tinted luminance)
	float luma = EwLuminance( image.xyz );
	vec4 oldImage = vec4(mix(image.xyz, luma * FilmTint, Monochrome), image.w);
		
	// Time-varying bright center spot of the lamp
	float vig = 16.0 * (uv.x * (1.0-uv.x) * uv.y * (1.0-uv.y));
	float vI = vig * mix( 1.0 - LampPulse, 1.0, rand(t + 0.5));
		
	// Additive flicker
	vI += 1.0 + Flicker * rand(t+8.);
		
	// Fixed vignetting (independent of the flicker)
	vI *= pow(vig, VignettePower);
		
	// Random scratches (count random per frame)
	if (EnableScratches) {
		int l = int(float(MaxScratches) * rand(t+7.0));
		for (int i = 0; i < 8; i++) {
			if (i >= l) break;
			vI *= randomLine( t+6.0+17.* float(i));
		}
	}
		
	// Random dust blotches (0..5 per frame)
	if (EnableBlotches) {
		int s = int( max(BlotchDensity * rand(t+18.0) -2.0, 0.0 ));
		for (int i = 0; i < 6; i++) {
			if (i >= s) break;
			vI *= randomBlotch( t+6.0+19.* float(i));
		}
	}
	
	// Show the image modulated by the defects
	oldImage.xyz *= vI;
    fragColor = oldImage;
		
	// Film grain (thanks, Jose!)
	if (EnableGrain) {
		fragColor.xyz *= (1.0+(EwHashSin2To1(uv+t*.01)-.2)*GrainStrength);
	}
		
	// premultiplied alpha for transparent pictures
	fragColor.xyz *= fragColor.w;

}
