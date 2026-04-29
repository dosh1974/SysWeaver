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




// Post processing parameters

const float NoiseInt = 0.0; 	//	var: { "min": 0, "max": 1.0, "step": 0.05, "desc": "The amount of noise, set to 0 to disable"}

const float VingetteIntensity = 0.0;	//	var: { "min": 0, "max": 1, "step": 0.05, "name": "Vingette intensity", "desc": "The intesity of the vingette effect, set to zero to disable vingetting"}
const float VingetteSpread = 16.0;	//	var: { "min": 10, "max": 1000, "step": 10, "name": "Vingette spread", "desc": "The spread of the vingette effect"}
const float VingettePow = 0.5;	//	var: { "min": 0.1, "max": 10, "step": 0.1, "name": "Vingette power", "desc": "The curve of the vingetting"}

const float TopLeftO = 1.0;	//var:	{ "min": 0, "max": 1, "step": 0.05, "name": "Top left opacity", "desc": "The opacity of the output in the top left corner"}
const float TopRightO = 1.0;//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Top right opacity", "desc": "The opacity of the output in the top right corner"}
const float BottomRightO = 1.0;//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Bottom right opacity", "desc": "The opacity of the output in the bottom right corner"}
const float BottomLeftO = 1.0;//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Bottom left opacity", "desc": "The opacity of the output in the bottom left corner"}


float PpRand(vec2 n) { 
	return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

float PpNoise(vec2 n) {
	vec2 b = floor(n), f = smoothstep(vec2(0.0), vec2(1.0), fract(n));
	return mix(mix(PpRand(b), PpRand(b + vec2(1.0, 0.0)), f.x), mix(PpRand(b + vec2(0.0, 1.0)), PpRand(b + 1.0), f.x), f.y);
}
	
float PpVingette(vec2 uv)
{
    vec2 suv = -uv * uv.yx + uv;   // MAD
    float vig = suv.x * suv.y * VingetteSpread; //
	if (VingettePow == 0.5)
		return sqrt(vig) * VingetteIntensity + (1.0 - VingetteIntensity);
	if (VingettePow == 1.0)
		return vig * VingetteIntensity + (1.0 - VingetteIntensity);
	if (VingettePow == 2.0)
		return vig * vig * VingetteIntensity + (1.0 - VingetteIntensity);
	return pow(vig, VingettePow) * VingetteIntensity + (1.0 - VingetteIntensity);
}

vec4 PostProcess(vec4 color, vec2 uv)
{
	if (NoiseInt > 0.0)
		color.rgb *= (PpNoise(vec2(gl_FragCoord) * vec2(-13.0, 17.0) + (iTime * vec2(121.12, 1445.23))) * NoiseInt + (1.0 - NoiseInt * 0.5));
	if (VingetteIntensity > 0.0)
		color *= PpVingette(uv);
	if ((TopLeftO < 1.0) || (TopRightO < 1.0) || (BottomRightO < 1.0) || (BottomLeftO < 1.0))
		color *= mix(mix(BottomLeftO, BottomRightO, uv.x), mix(TopLeftO, TopRightO, uv.x), uv.y);
	return color;
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

	
	fragColor = PostProcess(color, uv);
}