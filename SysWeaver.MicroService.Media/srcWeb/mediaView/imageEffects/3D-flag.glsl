/*
 * 3D Flag - the chosen picture printed on a cloth flag waving in the wind
 * Original shader from: https://www.shadertoy.com/view/fsfBD8
 *
 * A raymarched rippling cloth on a pole, lit by a warm sun, with a fine thread
 * weave, light shining through the fabric and a sky with drifting clouds.
 * Transparent picture parts show a checker pattern; Opacity 0 gives a
 * transparent background (flag + pole only).
 */
// Live controls:
//  iMouse.x = Wind Strength | Height of the ripples in the cloth (0 = flat) | 0.6 | 0.0 .. 2.0 | x
//  iMouse.y = Exposure | Overall brightness of the scene | 0.5 | 0.25 .. 1.25 | x
//  iMouse.z = Camera Angle | Turns the view around the flag | 0.5 | -40 .. 40 | °
//  iMouse.w = Ripple Frequency | Higher = smaller, busier folds | 0.5 | 0.6 .. 1.4 | x

#extension GL_OES_standard_derivatives : enable

#ifdef GL_ES
precision highp float;
#endif

// glslsandbox uniforms
uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;
uniform vec4 iMouse; // live controls, each component 0..1

// shadertoy emulation
#define iTime time
#define iResolution resolution

// --- Live controls ---
#define WindStrength mix(0.0, 2.0, iMouse.x)
#define Exposure mix(0.25, 1.25, iMouse.y)
#define CameraYaw mix(-40.0, 40.0, iMouse.z)
#define RippleScale mix(0.6, 1.4, iMouse.w)

// --- Colors ---
const vec3 SkyColor = vec3(0.1, 0.3, 0.6); // var: { "type": "col", "name": "Sky Color", "desc": "Air tint: brighter channels dominate the sky gradient (and the haze on the pole)" }
const vec3 LightColor = vec3(1.8, 1.6, 1.3); // var: { "type": "colhdr", "name": "Sun Color", "desc": "Color and strength of the sunlight on flag and pole" }
const vec3 AmbientSky = vec3(0.15, 0.2, 0.25); // var: { "type": "colhdr", "name": "Sky Ambient", "desc": "Fill light from above, colors the shaded side" }
const vec3 AmbientGround = vec3(0.1, 0.07, 0.05); // var: { "type": "colhdr", "name": "Ground Ambient", "desc": "Fill light from below" }

// --- Animation ---
const float WindSpeed = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Wind Speed", "desc": "How fast the ripples travel along the flag", "unit": "x" }
const float CameraSpeed = 0.1; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Camera Speed", "desc": "Speed of the slow automatic camera orbit (0 = still)" }
const float CloudSpeed = 0.03; // var: { "min": 0.0, "max": 0.2, "step": 0.005, "name": "Cloud Speed", "desc": "How fast the clouds drift" }

// --- Cloth ---
const float WeaveDensity = 425.0; // var: { "min": 100.0, "max": 1000.0, "step": 5.0, "name": "Weave Density", "desc": "Number of threads across the flag (fine fabric texture)" }
const float FlagBow = 0.08; // var: { "min": 0.0, "max": 0.2, "step": 0.005, "name": "Edge Bow", "desc": "How much the pole side sags between its two mounting points" }
const float SpecularStrength = 0.2; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Sheen", "desc": "Strength of the glossy highlight on the threads" }
const float SpecularPower = 80.0; // var: { "min": 8.0, "max": 200.0, "step": 1.0, "name": "Sheen Tightness", "desc": "Higher = smaller, sharper highlights" }
const float AmbientStrength = 0.3; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Ambient", "desc": "Brightness of the shaded side of the folds" }

// --- Camera ---
const float CameraDistance = 20.0; // var: { "min": 10.0, "max": 40.0, "step": 0.5, "name": "Camera Distance", "desc": "Distance to the flag: lower = flag fills more of the view" }
const float CameraZoom = 5.0; // var: { "min": 2.0, "max": 10.0, "step": 0.1, "name": "Lens Zoom", "desc": "Lens length: higher = tighter framing, flatter perspective", "unit": "x" }

// --- Sky ---
const float Opacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Sky Opacity", "desc": "Opacity of the sky background (0 = transparent, only flag and pole)", "scale": 100, "unit": "%" }
const float CloudAmount = 1.0; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Clouds", "desc": "Strength of the clouds in the sky" }

// --- Pole ---
const vec3 PoleColor = vec3(0.6, 0.6, 0.6); // var: { "type": "col", "name": "Pole Color", "desc": "Paint color of the flagpole" }
const float PoleThickness = 0.04; // var: { "min": 0.01, "max": 0.1, "step": 0.005, "name": "Pole Thickness", "desc": "Radius of the flagpole" }

// --- Transparent picture parts ---
const vec3 BgColor1 = vec3(0.05,0.05,0.05);//	var: { "type": "colhdr", "name": "Checker Dark", "desc": "Dark checker squares behind transparent picture pixels" }
const vec3 BgColor2 = vec3(0.9,0.9,0.9);//	var: { "type": "colhdr", "name": "Checker Light", "desc": "Light checker squares behind transparent picture pixels" }
const float GridSize = 2.0; // var: { "min": 0.5, "max": 10.0, "step": 0.5, "name": "Checker Density", "desc": "Number of checker squares behind transparent picture pixels" }

// Hazel Quantock
// This work is licensed under a Creative Commons Attribution-ShareAlike 4.0 International License. https://creativecommons.org/licenses/by-sa/4.0/

// Trans day of visibility is March 31st.

vec2 gFragCoord;

// Tone Mapping

// Exposure curve parameters
#define TONEMAP 1

const vec3 gradient = vec3(1.4,1.5,1.6);
const vec3 whiteSoftness = vec3(.1);
const vec3 blackClip = vec3(.0);
const vec3 blackSoftness = vec3(.05);

// filmic curve: soft black toe, soft white shoulder, then sRGB
vec3 HDRtoLDR( vec3 col )
{
    col *= Exposure;

#if TONEMAP
    // soft cut off near black to enhance contrast
   	// this is good for correcting for atmospheric fog
	col = max(col-blackClip,0.); 
    col = sqrt(col*col+blackSoftness*blackSoftness)-blackSoftness;

    col *= gradient;
    
    // soft clamp to white (oh this is so good)
    vec3 w2 = whiteSoftness*whiteSoftness;
    col += w2;
    col = (1.-col)*.5;
    col = 1. - (sqrt(col*col+w2) + col);
#else
    // skip tone mapping
	col*=1.2;
#endif
    
	return EwLinearToSrgb(col);
}

// Set up a camera looking at the scene.
// origin - camera is positioned relative to, and looking at, this point
// distance - how far camera is from origin
// rotation - about x & y axes, by left-hand screw rule, relative to camera looking along +z
// zoom - the relative length of the lens
void CamPolar( out vec3 pos, out vec3 ray, in vec3 origin, in vec2 rotation, in float distance, in float zoom )
{
	// get rotation coefficients
	vec2 c = vec2(cos(rotation.x),cos(rotation.y));
	vec4 s;
	s.xy = vec2(sin(rotation.x),sin(rotation.y));
	s.zw = -s.xy;

	// ray in view space
	ray.xy = gFragCoord.xy - iResolution.xy*.5;
	ray.z = iResolution.y*zoom;
	ray = normalize(ray);
	
	// rotate ray
	ray.yz = ray.yz*c.xx + ray.zy*s.zx;
	ray.xz = ray.xz*c.yy + ray.zx*s.yw;
	
	// position camera
	pos = origin - distance*vec3(c.x*s.y,s.z,c.x*c.y);
}

// smooth value noise (4 independent channels), on a tilted grid
vec4 Noise( in vec2 x )
{
    x = x*sqrt(3./4.) + x.yx*vec2(1,-1)*sqrt(1./4.); // tilt the grid so it's not aligned to the flag to make it less visible

    vec2 p = floor(x);
    vec2 f = fract(x);
	f = f*f*(3.0-2.0*f);

	vec2 uv = p + f;
    vec2 iuv = floor(uv);
    vec2 fuv = uv - vec2(iuv);
    return mix( mix( EwHash2To4(iuv),              EwHash2To4(iuv + vec2(1,0)), fuv.x ),
                mix( EwHash2To4(iuv + vec2(0,1)),  EwHash2To4(iuv + vec2(1,1)), fuv.x ),
                fuv.y );
}


// ----------------------

// height of the cloth (z) at a point of the flag plane: 3 octaves of moving noise
float RippleHeight( vec2 pos )
{
    float time = iTime * WindSpeed;

	vec2 p = pos*RippleScale+vec2(-2.12781,.213122)*time;
	
	p += vec2(1,0)*Noise(p).y; // more natural looking ripples
	float f = Noise(p).x-.5;
	p *= 1.97;
	p += vec2(0,-1)*time;
	f += (Noise(p).x-.5)*.2;
	p *= 1.87;
	p += vec2(-2,0)*time;
	f += (Noise(p).x-.5)*.05;
	
	f = f*(1.0-exp2(-abs(pos.x))); // calm near the pole
	return f*WindStrength;
}

// signed distance (under estimate) to the cloth height field
float DistanceField( vec3 pos )
{
	return (RippleHeight(pos.xy)-pos.z)*.5;
}

// tetrahedral normal of the cloth (loop form: one inlined cloth evaluation, faster compile)
vec3 Normal( vec3 pos )
{
	vec3 n = vec3(0);
	for ( int i=0; i < 4; i++ )
	{
		// corners (-1,-1,-1), (-1,1,1), (1,-1,1), (1,1,-1) * .01 (gets moire artefacts if too small)
		vec3 e = 1.0 - 2.0*vec3( ((i+3)>>1)&1, (i>>1)&1, i&1 );
		n += DistanceField( pos + e*.01 )*e;
	}
	return normalize(n);
}

// map a uv space onto a distorted surface:
// walk along the cloth in x and y, adding the slope lengths, so the picture stays "printed" on the folds
vec2 UVMapping( vec2 target )
{
    // bow the left edge so it's just mounted at 2 points
    float bow = cos(target.y*EW_TAU/4.)*FlagBow;
    target.x -= bow;

    float droop = 2.; // the technique isn't really robust enough for this to look realistic at bigger values
    target.y += droop;
    
	// need to march vertically to absorb vertical creases, and horizontally for horizontal ones
	// cheat, by seperating these two
	vec2 uv = vec2(0);
    
	const int n = 4;
	vec2 d = target/float(n);
	vec2 l;
	l.x = RippleHeight( vec2(0,target.y) );
	l.y = RippleHeight( vec2(target.x,0) );
	for ( int i=0; i < n; i++ )
	{
		vec2 s;
		s.x = RippleHeight( vec2(d.x*float(i),target.y) );
		s.y = RippleHeight( vec2(target.x,d.y*float(i)) );
		vec2 dh = s-l;
		uv += sign(d)*sqrt(dh*dh+d*d);
		l = s;
	}
	
    uv.y -= droop;
    
	return (uv+vec2(0,1))/vec2(3.0,2.0);
}

// flag colour: the picture, transparent parts over a checker pattern
vec3 Pattern( vec2 uv )
{
	vec4 col = texture2D(tex, uv);
	if (col.w >= 1.0)
		return col.xyz;
    float pattern = (fract(uv.x*(GridSize/.2))-.5)*(fract(uv.y*(GridSize/.3))-.5);
	vec3 bg = mix( BgColor1, BgColor2, smoothstep( -fwidth(pattern)*.5, fwidth(pattern)*.5, pattern ) );
	return mix(bg, col.xyz, col.w);
}

// 1 outside the flag rectangle (soft edge)
float Mask( vec2 uv )
{
	return max(
            smoothstep(.495,.5,abs(uv.x-.5)),
            smoothstep(.495,.5,abs(uv.y-.5))
        );
}

// fine cloth thread pattern, faded to grey where it would alias
float Weave( vec2 uv )
{
	vec2 a = uv*vec2(3.0,2.0)*WeaveDensity;
	float f = (sin(a.x)+sin(a.y))*.25+.5;

    f = mix( f, .5, min( 1., .2*max( fwidth(a.x), fwidth(a.y) ) ) ); // prevent moire

    return f;
}

// 0 on the hem along the flag's edges, 1 inside
float Seam( vec2 uv )
{
    return smoothstep( .5, .48, abs(uv.y-.5) )
          *smoothstep( 1., .985, uv.x )
          *smoothstep( .02, .03, uv.x );
}



// quick and pretty sky colour: blue gradient + drifting clouds
vec3 SkyColour( vec3 ray )
{
    vec3 col = exp2(-ray.y/SkyColor); // blue - from https://www.shadertoy.com/view/4ljBRy
    
    // add some clouds
    vec2 cloudUV = ray.xz/(ray.y+.2) + iTime*vec2(-CloudSpeed,0);
    vec4 clouds = (
          Noise(4.*cloudUV)
        + Noise(10.*cloudUV)*.4
        + Noise(25.*cloudUV)*.16
        + Noise(50.*cloudUV)*.04
        )/1.6;
    
    col = mix( col, clouds.yyy, pow(smoothstep(.05,.6,clouds.x),8.)*CloudAmount*max(0.,ray.y) );
    return col;
}

// sky / ground ambient light by normal direction
vec3 Ambient( vec3 normal )
{
    return mix( AmbientGround, AmbientSky, normal.y*.3+.7 );
}

// premultiplied "over": foreground with straight alpha over a premultiplied background
vec4 blend(vec4 background, vec4 foreground)
{
	foreground.rgb *= foreground.a;
	return foreground + background * (1.0 - foreground.a);
}

// Render: raymarch the cloth, shade it with the picture, add sky + pole, tone map
void mainImage( out vec4 fragColour, in vec2 fragCoord )
{
    gFragCoord = fragCoord;

    // slow automatic camera orbit (+ live yaw offset)
    vec3 camPos, ray;
    vec2 orbit = (.5-.5*cos(vec2(1.,.618)*CameraSpeed*iTime)) * vec2(1,.5);
    CamPolar( camPos, ray, vec3(1.5,0,0), vec2(-.8,-.5)+vec2(1.2,1.5)*orbit.yx + vec2(0.0, radians(CameraYaw)), CameraDistance, CameraZoom );

    // march to the cloth surface (a height field around the z = 0 plane)
    float t = 0.0;
    for ( int i=0; i < 20; i++ ) // holds up surprisingly well at low counts
    {
        float h = DistanceField( camPos+t*ray );
        t += h;
        if ( abs(h) < .0005 ) break; // converged
    }

    vec3 pos = camPos + t*ray;

    vec2 uv = UVMapping( pos.xy );   // picture coordinates on the cloth (0..1 inside the flag)
    vec3 albedo = Pattern( uv );
    float mask = Mask(uv);           // 1 outside the flag
    float weave = Weave(uv);
    float seam = Seam(uv);
    // normal only where the flag is visible (saves 4 cloth evaluations on sky pixels)
    vec3 normal = vec3(0,0,-1);
    if ( mask < 1.0 )
        normal = Normal( pos );

    const vec3 lightDir = normalize(vec3(-3,.7,-.6));

    float nl = dot(normal,lightDir);
    float l = max( nl, .0 );
    // light scattered through the thin cloth, favouring back light
    vec3 scatteredLight = pow(albedo,vec3(2)) * smoothstep( .7, -1., nl );
    vec3 ambient = Ambient( normal ) * AmbientStrength;

    // the weave modulates how much light passes through / reflects
    scatteredLight *= mix( .3, .7, weave );
    ambient *= mix( 1.7, .3, weave );
    l *= mix( 1.15, .85, weave );

    scatteredLight *= mix( .5, 1., seam ); // thicker hem along the edges

    vec3 col = albedo;
    col *= (l + scatteredLight)*LightColor + ambient;

    // sheen on the threads
    col += LightColor * weave * pow(max(0.,dot(normalize(lightDir-ray),normal)),SpecularPower)*SpecularStrength;

    vec4 flagCol = vec4( col, 1. - mask );

    // sky background (Opacity 0 = transparent)
    if (Opacity > 0.0)
        fragColour = vec4(SkyColour(ray), 1.0) * Opacity;
    else
        fragColour = vec4(0.0);

    // flagpole: a camera facing slice through a capped cylinder (cheap, fine unless the camera gets very close)
    vec3 polePos = vec3(-PoleThickness,1.1,0) - camPos; // centred on sphere at top of pole
    float distanceOfPoleAlongRay = dot( ray.xz, polePos.xz )/dot(ray.xz,ray.xz);

    float poleT = 1.;
    bool intersection = false;

    float poleOutlineSDF = length( vec2( length(ray.xz*distanceOfPoleAlongRay-polePos.xz), max(0.,ray.y*distanceOfPoleAlongRay-polePos.y) ) )  - PoleThickness;
    float halfFWidthPoleSDF = fwidth(poleOutlineSDF)*.5;
    if ( poleOutlineSDF < halfFWidthPoleSDF )
    {
        poleT = distanceOfPoleAlongRay;
        intersection = true;

        vec3 polePoint = poleT*ray - polePos;

        // approximate round normal
        vec3 poleNorm = polePoint / PoleThickness;
        poleNorm.y = max(poleNorm.y,0.);
        poleNorm -= ray*sqrt(1.-min(1.,dot(poleNorm,poleNorm)));
        poleNorm = normalize(poleNorm);

        vec3 poleCol = PoleColor * ( LightColor*max( dot( poleNorm, lightDir ), .0 ) + Ambient(poleNorm)*.5 );
        poleCol = mix( vec3(1), poleCol, exp2( -poleT * SkyColor / 200. ) ); // aerial haze

        // antialiased edge via the screen space width of the outline
        fragColour = blend(fragColour, vec4(poleCol, 1.0 - EwLinearStep(-halfFWidthPoleSDF,halfFWidthPoleSDF,poleOutlineSDF)));
    }

    // flag in front of the pole?
    if ( !intersection || poleT > t )
        fragColour = blend(fragColour, flagCol);

    // tone mapping on the straight colour, output stays premultiplied
    float aa = fragColour.a;
    if (aa > 0.0)
        fragColour.rgb = HDRtoLDR( fragColour.rgb / aa) * aa;
}





void main(void)
{
    mainImage(gl_FragColor, gl_FragCoord.xy);
}
