const float MATH_PI	= float( 3.14159265359 );

vec4 blend(vec4 background, vec4 foreground)
{
	return foreground + background * (1.0 - foreground.a);
}

void Rotate( inout vec2 p, float a ) 
{
	p = cos( a ) * p + sin( a ) * vec2( p.y, -p.x );
}

float Circle( vec2 p, float r )
{
    return ( length( p / r ) - 1.0 ) * r;
}

float Rand( vec2 c )
{
	return fract( sin( dot( c.xy, vec2( 12.9898, 78.233 ) ) ) * 43758.5453 );
}

float saturate( float x )
{
	return clamp( x, 0.0, 1.0 );
}

void BokehLayer( inout vec4 color, vec2 p, vec3 c )   
{
    float wrap = 450.0;    
    if ( mod( floor( p.y / wrap + 0.5 ), 2.0 ) == 0.0 )
    {
        p.x += wrap * 0.5;
    }    
    
    vec2 p2 = mod( p + 0.5 * wrap, wrap ) - 0.5 * wrap;
    vec2 cell = floor( p / wrap + 0.5 );
    float cellR = Rand( cell );
        
    c *= fract( cellR * 3.33 + 3.33 );    
    float radius = mix( 30.0, 70.0, fract( cellR * 7.77 + 7.77 ) );
    p2.x *= mix( 0.9, 1.1, fract( cellR * 11.13 + 11.13 ) );
    p2.y *= mix( 0.9, 1.1, fract( cellR * 17.17 + 17.17 ) );
    
    float sdf = Circle( p2, radius );
    float circle = 1.0 - smoothstep( 0.0, 1.0, sdf * 0.04 );
    float glow	 = exp( -sdf * 0.025 ) * 0.3 * ( 1.0 - circle );
	color = blend(color, vec4(c * ( circle + glow ), 0.0));
}


const float Size = 1.0; 				   // var: { "min": 0.05, "max": 2.5, "step": 0.01 }
const vec4 Back1 = vec4( 0.3, 0.1, 0.3, 1.0 ); // var:	{ "type": "colhdr" }
const vec4 Back2 = vec4( 0.1, 0.4, 0.5, 1.0 );// var:	{ "type": "colhdr" }

const vec3 Bokeh2 = vec3( 2.1, 1.4, 0.7 ); // var:	{ "type": "colhdr" }
const vec3 Bokeh1 = vec3( 1.2, 0.3, 0.6 ); // var:	{ "type": "colhdr" }
const vec3 Bokeh3 = vec3( 1.2, 0.9, 0.6 ); // var:	{ "type": "colhdr" }
const vec3 Bokeh4 = vec3( 1.2, 0.6, 0.3 ); // var:	{ "type": "colhdr" }
const vec3 Bokeh5 = vec3( 0.6, 0.0, 1.2 ); // var:	{ "type": "colhdr" }

const float VingetteIntensity = 0.0; // var:
const float VingettePow = 1.0; // var:
const float VingetteSpread = 100.0; // var:

const float NoiseInt = 0.2; // var:


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
    float vig = min(1.0, uv.x*uv.y * VingetteSpread); //
	if (VingettePow == 0.5)
		return sqrt(vig) * VingetteIntensity + 1.0 - VingetteIntensity;
	if (VingettePow == 1.0)
		return vig * VingetteIntensity + 1.0 - VingetteIntensity;
	if (VingettePow == 2.0)
		return vig * vig * VingetteIntensity + 1.0 - VingetteIntensity;
	return pow(vig, VingettePow) * VingetteIntensity + 1.0 - VingetteIntensity;
}

void PostProcess(inout vec4 color)
{
	vec2 uv = gl_FragCoord.xy / iResolution.xy;
	if (VingetteIntensity > 0.0)
		color *= Vingette(uv);
	if (NoiseInt > 0.0)
		color *= (noise(vec2(gl_FragCoord) * -15.5 + (iTime * vec2(121.12, 1445.23))) * NoiseInt + (1.0 - NoiseInt * 0.5));
}

void mainImage( out vec4 fragColor, in vec2 fragCoord )
{    
	vec2 uv = fragCoord.xy / iResolution.xy;
	vec2 p = ( 2.0 * fragCoord - iResolution.xy ) / (iResolution.x + iResolution.y);
	p *= 1000.0;
	p /= Size;
    
    // background
	vec4 color = mix( Back1, Back2, dot( uv, vec2( 0.2, 0.7 ) ) );
	color.rgb *= color.a;

    float time = iTime;
    
    Rotate( p, 0.2 + time * 0.03 );
    BokehLayer( color, p + vec2( -50.0 * time +  0.0, 0.0  ), Bokeh1);
	Rotate( p, 0.3 - time * 0.05 );
    BokehLayer( color, p + vec2( -70.0 * time + 33.0, -33.0 ), Bokeh2);
	Rotate( p, 0.5 + time * 0.07 );
    BokehLayer( color, p + vec2( -60.0 * time + 55.0, 55.0 ), Bokeh3);
    Rotate( p, 0.9 - time * 0.03 );
    BokehLayer( color, p + vec2( -25.0 * time + 77.0, 77.0 ), Bokeh4);
    Rotate( p, 0.0 + time * 0.05 );
    BokehLayer( color, p + vec2( -15.0 * time + 99.0, 99.0 ), Bokeh5);     

	PostProcess(color);
	fragColor = color;
}