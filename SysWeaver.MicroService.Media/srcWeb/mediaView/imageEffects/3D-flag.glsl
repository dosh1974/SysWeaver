/*
 * Original shader from: https://www.shadertoy.com/view/fsfBD8
 */

#extension GL_OES_standard_derivatives : enable

#ifdef GL_ES
precision highp float;
#endif

// glslsandbox uniforms
uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;

const vec3 BgColor1 = vec3(0.05,0.05,0.05);//	var: { "type": "colhdr" }
const vec3 BgColor2 = vec3(0.9,0.9,0.9);//	var: { "type": "colhdr" }
const float Opacity = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.05 }
const float GridSize = 2.0; // var: {}

// shadertoy emulation
#define iTime time
#define iResolution resolution



// Hazel Quantock
// This work is licensed under a Creative Commons Attribution-ShareAlike 4.0 International License. https://creativecommons.org/licenses/by-sa/4.0/


// Trans day of visibility is March 31st.


const float tau = 6.28318530717958647692;

vec2 gFragCoord;


// Tone Mapping

const float exposure = .75;

// Exposure curve parameters
#define TONEMAP 1

const vec3 gradient = vec3(1.4,1.5,1.6);
const vec3 whiteSoftness = vec3(.1);
const vec3 blackClip = vec3(.0);
const vec3 blackSoftness = vec3(.05);

vec3 LinearToSRGB ( vec3 col )
{
    return mix( col*12.92, 1.055*pow(col,vec3(1./2.4))-.055, step(.0031308,col) );
}

vec3 SRGBToLinear ( vec3 col )
{
    return mix( col/12.92, pow((col+.055)/1.055,vec3(2.4)), step(.04045,col) );
}

vec3 HDRtoLDR( vec3 col )
{
    col *= exposure;

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
    
	return LinearToSRGB(col);
}


float linstep( float a, float b, float c )
{
    return clamp((c-a)/(b-a),0.,1.);
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
	s.xy = vec2(sin(rotation.x),sin(rotation.y)); // worth testing if this is faster as sin or sqrt(1.0-cos);
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


vec4 hash42(vec2 p)
{
	vec4 p4 = fract(vec4(p.xyxy) * vec4(.1031, .1030, .0973, .1099));
    p4 += dot(p4, p4.wzxy+33.33);
    return fract((p4.xxyz+p4.yzzw)*p4.zywx);

}

vec4 Noise( in vec2 x )
{
    x = x*sqrt(3./4.) + x.yx*vec2(1,-1)*sqrt(1./4.); // tilt the grid so it's not aligned to the flag to make it less visible

    vec2 p = floor(x);
    vec2 f = fract(x);
	f = f*f*(3.0-2.0*f);
//	vec2 f2 = f*f; f = f*f2*(10.0-15.0*f+6.0*f2);

	vec2 uv = p + f;
#if 0
	vec4 rg = textureLod( iChannel0, (uv+0.5)/256.0, 0.0 );
#else
	// on some hardware interpolation lacks precision
/*    ivec2 iuv = ivec2(floor(uv));
    vec2 fuv = uv - vec2(iuv);
    
	vec4 rg = mix( mix(
				texelFetch( iChannel0, iuv&255, 0 ),
				texelFetch( iChannel0, (iuv+ivec2(1,0))&255, 0 ),
				fuv.x ),
				  mix(
				texelFetch( iChannel0, (iuv+ivec2(0,1))&255, 0 ),
				texelFetch( iChannel0, (iuv+ivec2(1,1))&255, 0 ),
				fuv.x ),
				fuv.y );
*/
            vec2 iuv = floor(uv);
            vec2 fuv = uv - vec2(iuv);
            vec4 rg = mix( mix(
                hash42(iuv),
                hash42(iuv + vec2(1,0)),
				fuv.x ),
				  mix(
                hash42(iuv + vec2(0,1)),
                hash42(iuv + vec2(1,1)),
				fuv.x ),
				fuv.y );


#endif			  

	return rg;
}


// ----------------------

float RippleHeight( vec2 pos )
{
    float time = iTime;

	vec2 p = pos+vec2(-2.12781,.213122)*time;
	
	p += vec2(1,0)*Noise(p).y; // more natural looking ripples
	float f = Noise(p).x-.5;
	p *= 1.97;
	p += vec2(0,-1)*time;
	f += (Noise(p).x-.5)*.2;
	p *= 1.87;
	p += vec2(-2,0)*time;
	f += (Noise(p).x-.5)*.05;
	
	f = f*(1.0-exp2(-abs(pos.x)));
	return f*1.2;
}

float DistanceField( vec3 pos )
{
	return (RippleHeight(pos.xy)-pos.z)*.5;
}

vec3 Normal( vec3 pos )
{
	vec2 delta = vec2(-1,1)*.01;//*length(fwidth(pos)); // gets moire artefacts if this is too small
	return normalize(
                DistanceField( pos + delta.xxx )*delta.xxx +
                DistanceField( pos + delta.yyx )*delta.yyx +
                DistanceField( pos + delta.yxy )*delta.yxy +
                DistanceField( pos + delta.xyy )*delta.xyy
            );
}

// map a uv space onto a distorted surface
vec2 UVMapping( vec2 target )
{
    // bow the left edge so it's just mounted at 2 points
    float bow = cos(target.y*6.283185/4.)*.08;
    target.x -= bow;

    float droop = 2.; // the technique isn't really robust enough for this to look realistic at bigger values
    target.y += droop;
    
	// need to march vertically to absorb vertical creases, and horizontally for horizontal ones
	// cheat, by seperating these two
	vec2 uv = vec2(0);
	
    // make flag droop toward the right by offsetting target y
    // hopefully this means it will droop more the more disruption there is
//    target.y += target.x*.5;
    
    
	const int n = 4;
	const float fudge = 1.0; // use values > 1 to allow for extra ripples we're not measuring
	vec2 d = target/float(n);
	vec2 l;
	l.x = RippleHeight( vec2(0,target.y) );
	l.y = RippleHeight( vec2(target.x,0) );
	for ( int i=0; i < n; i++ )
	{
		vec2 s;
		s.x = RippleHeight( vec2(d.x*float(i),target.y) );
		s.y = RippleHeight( vec2(target.x,d.y*float(i)) );
		//uv.x += sign(d.x)*sqrt(pow(fudge*,2.0)+d.x*d.x);
		//uv.y += sign(d.y)*sqrt(pow(fudge*,2.0)+d.y*d.y);
        
		uv += sign(d)*sqrt(pow(fudge*(s-l),vec2(2.0))+d*d);
		l = s;
	}
    
//    uv.y += (uv.x+1.)*uv.x*.05; // droop toward the end
	
    uv.y -= droop;
    
	return (uv+vec2(0,1))/vec2(3.0,2.0);
}

vec3 Pattern( vec2 uv )
{
	vec4 col = texture2D(tex, uv);
	if (col.w >= 1.0)
		return col.xyz;
    float pattern = (fract(uv.x*(GridSize/.2))-.5)*(fract(uv.y*(GridSize/.3))-.5);
	vec3 bg = mix( BgColor1, BgColor2, smoothstep( -fwidth(pattern)*.5, fwidth(pattern)*.5, pattern ) ); // this antialiasing doesn't work
	return mix(bg, col.xyz, col.w);
}

float Mask( vec2 uv )
{
    // todo: use fwidth so it is correct for distance

	return max(
            smoothstep(.495,.5,abs(uv.x-.5)),
            smoothstep(.495,.5,abs(uv.y-.5))
        );
}


float Weave( vec2 uv )
{
	vec2 a = uv*vec2(3.0,2.0)*500.*.85;
	float f = (sin(a.x)+sin(a.y))*.25+.5;

    f = mix( f, .5, min( 1., .2*max( fwidth(a.x), fwidth(a.y) ) ) ); // prevent moire

    return f;
}


float Seam( vec2 uv )
{
    return smoothstep( .5, .48, abs(uv.y-.5) )
          *smoothstep( 1., .985, uv.x )
          *smoothstep( .02, .03, uv.x );
}


vec3 airColourLog2 = vec3(.1,.3,.6);

// quick and pretty sky colour
vec3 SkyColour( vec3 ray )
{
    vec3 col = exp2(-ray.y/airColourLog2); // blue - from https://www.shadertoy.com/view/4ljBRy
    
    // add some clouds
    vec2 cloudUV = ray.xz/(ray.y+.2) + iTime*vec2(-.03,0);
    vec4 clouds = (
          Noise(4.*cloudUV)
        + Noise(10.*cloudUV)*.4
        + Noise(25.*cloudUV)*.16
        + Noise(50.*cloudUV)*.04
        )/1.6;
    
    col = mix( col, clouds.yyy, pow(smoothstep(.05,.6,clouds.x),8.)*1.*max(0.,ray.y) );
    /*
    // horizon
    float horizonSDF = ray.y - .09 - .04*Noise(ray.xz*9.).x - .03*(.5-abs(Noise(ray.xz*5.).x-.5));
    col = mix( col, mix( vec3(1), vec3(.1), exp2(-3.*airColourLog2*.01/(.01+max(0.,-horizonSDF))) ), smoothstep(.003, -.003, horizonSDF ) );
    */
    return col;
}


vec3 Ambient( vec3 normal )
{
    return mix( vec3(.1,.07,.05), vec3(.15,.2,.25), normal.y*.3+.7 );
}


vec4 blend(vec4 background, vec4 foreground)
{
	foreground.rgb *= foreground.a;
	return foreground + background * (1.0 - foreground.a);
}

void mainImage( out vec4 fragColour, in vec2 fragCoord )
{
    gFragCoord = fragCoord;


    vec3 camPos, ray;
    vec2 mousePos = (.5-.5*cos(vec2(1.,.618)*0.1*iTime)) * vec2(1,.5);
    CamPolar( camPos, ray, vec3(1.5,0,0), vec2(-.8,-.5)+vec2(1.2,1.5)*mousePos.yx, 20.0, 5. );
    


    float t = 0.0;
    float h = 1.0;
    for ( int i=0; i < 20; i++ ) // this holds up surprisingly well at low counts
    {
        if ( h < .01 )
            break;
        float h = DistanceField( camPos+t*ray );
        t += h;
    }

    vec3 pos = camPos + t*ray;

    vec2 uv = UVMapping( pos.xy );

    vec3 albedo = Pattern( uv );

    float mask = Mask(uv);

    float weave = Weave(uv);
    float seam = Seam(uv);

    vec3 normal = Normal( pos );

    const vec3 lightCol = vec3(1.8,1.6,1.3);
    const vec3 lightDir = normalize(vec3(-3,.7,-.6));

    float nl = dot(normal,lightDir);
    float l = max( nl, .0 );
//    float bl = max( mix(-nl,1.,.3), .0 ); // back light - including some scatter to prevent dark line where nl=0
    vec3 scatteredLight = pow(albedo,vec3(2)) * smoothstep( .7, -1., nl ); // scattered light, favouring back-light
    vec3 ambient = Ambient( normal ) * .3;

    scatteredLight *= mix( .3, .7, weave );
    ambient *= mix( 1.7, .3, weave );
    l *= mix( 1.15, .85, weave );
    
    scatteredLight *= mix( .5, 1., seam );
    
    vec3 col = albedo;
    col *= (l + scatteredLight)*lightCol + ambient;
    
    // todo: do GGX specular
    col += lightCol * weave * pow(max(0.,dot(normalize(lightDir-ray),normal)),80.)*.2;
    
    // rim light - to make it feel a bit fuzzy
    //col += pow( dot(normal,ray)+1., 4. ) * (ambient+lightCol) * albedo *.5;

    // atmospheric fog
    //col = mix( vec3(1), col, exp2( -t * airColourLog2 / 200. ) );

    //if ( uv.x < .0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0 )
    vec4 flagCol = vec4( col, 1. - mask );



	if (Opacity > 0.0)
	{
		fragColour = vec4(SkyColour(ray), 1.0);
		fragColour *= Opacity;
	}else {
		fragColour = vec4(0.0);
	}
    
    // add flagpole
    const float poleThickness = .04;
    vec3 polePos = vec3(-poleThickness,1.1,0) - camPos; // centred on sphere at top of pole
    
    float distanceOfPoleAlongRay = dot( ray.xz, polePos.xz )/dot(ray.xz,ray.xz);
    
    float poleT = 1.;
    bool intersection = false;
    
    // this is hacky and wrong, but it made the maths simpler and the error won't be obvious unless the camera gets close enough for a lot of perspective distortion
    // basically I'm drawing a slice through the centre of the pole, rather than a 3D pole, for no reason other than I'm too lazy to look up/figure out the correct maths!
    float poleOutlineSDF = length( vec2( length(ray.xz*distanceOfPoleAlongRay-polePos.xz), max(0.,ray.y*distanceOfPoleAlongRay-polePos.y) ) )  - poleThickness;
    float halfFWidthPoleSDF = fwidth(poleOutlineSDF)*.5;
    if ( poleOutlineSDF < halfFWidthPoleSDF )
    {
        poleT = distanceOfPoleAlongRay;
        intersection = true;
    
        vec3 polePos = poleT*ray - polePos;
    
        vec3 poleNorm = polePos / poleThickness; // this is a hack
        poleNorm.y = max(poleNorm.y,0.);
    
        poleNorm -= ray*sqrt(1.-min(1.,dot(poleNorm,poleNorm)));
        poleNorm = normalize(poleNorm);
    
        vec3 poleCol = vec3(.6) * ( lightCol*max( dot( poleNorm, lightDir ), .0 ) + Ambient(poleNorm)*.5 );
    
        poleCol = mix( vec3(1), poleCol, exp2( -poleT * airColourLog2 / 200. ) );



		fragColour = blend(fragColour, vec4(poleCol, 1.0 - linstep(-halfFWidthPoleSDF,halfFWidthPoleSDF,poleOutlineSDF)));
        //fragColour.rgb = mix( poleCol, fragColour.rgb, linstep(-halfFWidthPoleSDF,halfFWidthPoleSDF,poleOutlineSDF) );
        // todo - fake AA with rim-alpha
    }

    if ( !intersection || poleT > t )
    {
		fragColour = blend(fragColour, flagCol);
//        fragColour.rgb = mix( fragColour.rgb, flagCol.rgb, flagCol.a );
    }

    // fake flat flagpole - I don't like this, I can replace it with a real one easily enough
//    col = mix( col, vec3(cos(uv.x*50.0)),smoothstep(0.015,0.01,abs(uv.x+.01))*smoothstep(1.01,1.0,uv.y));

    // tone mapping


	float aa = fragColour.a;
	if (aa > 0.0)
		fragColour.rgb = HDRtoLDR( fragColour.rgb / aa) * aa;

    //fragColour.rgb = Noise(fragCoord * 0.1).bbb;

    //fragColour.a = 1.;
}





void main(void)
{
    mainImage(gl_FragColor, gl_FragCoord.xy);
}