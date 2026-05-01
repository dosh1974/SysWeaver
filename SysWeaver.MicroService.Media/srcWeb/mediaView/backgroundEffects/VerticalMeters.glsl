
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
		color *= clamp(mix(mix(BottomLeftO, BottomRightO, uv.x), mix(TopLeftO, TopRightO, uv.x), uv.y), 0.0, 1.0);
	return color;
}

const float Radius = 0.3; 			// var:	{ "min": 0, "max": 1, "step": 0.05, "desc": "The relative corner radius"}
const float BarWidth = 128.0; 		// var:	{ "min": 1, "max": 256, "step": 1, "desc": "The width in pixels of each bar (including spacing)"}
const float BarFill = 1.02; 		// var:	{ "min": 0.01, "max": 1.00, "step": 0.01, "desc": "How much of the allocated space that a bar should fill"}
const float Frequency = 0.15; 		// var:	{ "min": 0, "max": 100, "step": 0.01, "desc": "The frequency of the waves"}
const vec4 BgColorTop = vec4(0.0, 0.5, 1.0, 0.4); 		// var: { "type": "colhdr", "desc": "Background color at the top"}
const vec4 BgColorBottom = vec4(0.6, 0.7, 1.0, 0.5); 	// var: { "type": "colhdr", "desc": "Background color at the bottom"}

const int Palette = 2;				// var:	{ "min": 0, "max": 4, "step": 1, "desc": "What palette to use"}

const float PaletteRange = 1.0; 	// var:	{ "min": 0, "max": 4, "step": 0.05, "desc": "How much to compress the range of the palette"}
const float PaletteOffset = 0.0; 	// var:	{ "min": 0, "max": 1, "step": 0.05, "desc": "How much to offset the palette"}

const float GradientCompress = 0.0; // var:	{ "min": 0, "max": 1, "step": 0.05, "desc": "How much to compress the gradient when the bar compresses"}

const int BarDepth = 3;				// var:	{ "min": 1, "max": 30, "step": 1, "desc": "Number of bars"} 
const float DepthDecay = 0.7; 		// var:	{ "min": 0.1, "max": 2, "step": 0.1, "desc": "Latency decay of each bar"} 
const float MinBarOpacity = 0.2; 	// var:	{ "min": 0.05, "max": 0.9, "step": 0.05, "desc": "The opacity of the faintest bar"} 
const float DeltaTime = 0.2; 		// var:	{ "min": 0, "max": 10, "step": 0.05, "desc": "The initial time difference between each bar"} 
const float MinHeight = 0.03; 		// var:	{ "min": 0.1, "max": 0.9, "step": 0.05, "desc": "The minimum height of a bar as a fraction of the height"} 
const float ScrollSpeed = 0.73; 	// var: {}
const float TransformSpeed = 0.87; 	// var: {}

const float Attach = 0.5;			// var: { "min": 0.0, "max": 1.0, "step": 0.05, "desc": "Where to attach the bars, 0 = bottom, 0.5 = center, 1.0 = top"} 

void Blend(inout vec4 color, vec4 newColor, float i)
{
	i = clamp(i, 0.0, 1.0);
	float a = i * newColor.a;
	newColor.rgb *= a;
	newColor.a = a;
	color = color * (1.0 - newColor.a) + newColor;
}

vec3 palette( in float t, in vec3 a, in vec3 b, in vec3 c, in vec3 d )
{
    return a + b*cos( 6.28318*(c*t+d) );
}

vec3 pal1(in float t)
{
	return palette(t, vec3(0.5, 0.5, 0.5), vec3(0.5, 0.5, 0.5), vec3(1.0, 1.0, 1.0), vec3(0.00, 0.33, 0.67));
}

vec3 pal2(in float t)
{
	return palette(t, vec3(0.5, 0.5, 0.5), vec3(0.5, 0.5, 0.5), vec3(1.0, 1.0, 1.0), vec3(0.00, 0.10, 0.20));
}

vec3 pal3(in float t)
{
	return palette(t, vec3(0.5, 0.5, 0.5), vec3(0.5, 0.5, 0.5), vec3(1.0, 1.0, 1.0), vec3(0.30, 0.20, 0.20));
}

vec3 pal4(in float t)
{
	return palette(t, vec3(0.5, 0.5, 0.5), vec3(0.5, 0.5, 0.5), vec3(1.0, 1.0, 0.5), vec3(0.80, 0.90, 0.30));
}

vec3 pal5(in float t)
{
	return palette(t, vec3(0.5, 0.5, 0.5), vec3(0.5, 0.5, 0.5), vec3(1.0, 0.7, 0.4), vec3(0.00, 0.15, 0.20));
}

vec4 GetCol(float x, float a)
{
	x = x * PaletteRange + PaletteOffset;
	if (Palette == 0)
		return vec4(pal1(x), a);
	if (Palette == 1)
		return vec4(pal2(x), a);
	if (Palette == 2)
		return vec4(pal3(x), a);
	if (Palette == 3)
		return vec4(pal4(x), a);
	else
		return vec4(pal5(x), a);
}

float BoxDistance(vec2 boxCenter, vec2 boxExtents, vec2 point)
{
	return length(max(abs(point - boxCenter) - boxExtents, 0.0));
}


vec2 GetBarIntensity(vec2 relPos, float barIndex, float time)
{
	float p = PpNoise(vec2(barIndex + time * ScrollSpeed, time * TransformSpeed)) * (1.0 - MinHeight) + MinHeight;
	float height = p * iResolution.x;

	
	vec2 extent = vec2(BarWidth * BarFill * 0.5 - BarWidth * BarFill * 0.5 * Radius, height * 0.5 - (BarWidth * BarFill * 0.5 * Radius));

	vec2 center = vec2(BarWidth * 0.5, height * (0.5 - Attach) + iResolution.x * Attach);
	float i = clamp((BarWidth * BarFill * 0.5 * Radius) - BoxDistance(center, extent, relPos), 0.0, 1.0);


	float baseY = (center.y - height * 0.5);
	float compY = (relPos.y - baseY);
	compY += Attach * (1.0 - height);
	compY /= mix(height, iResolution.x, GradientCompress);
	return vec2(i, compY);
}


void mainImage( out vec4 fragColor, in vec2 fragCoord )
{    
	vec2 uv = fragCoord.yx / iResolution.yx;
	float bg = fragCoord.y * (1.0 / BarWidth);
	float ry = -uv.y;
	float dBar = fract(bg);
	float barIndex = bg - dBar;
	vec4 color = mix(BgColorBottom, BgColorTop, uv.x);
	color.rgb *= color.w;
	
	float time = iTime;
	float opacity = MinBarOpacity;
	float deltaOpacity = (1.0 - MinBarOpacity) / float(BarDepth - 1);
	float deltaTime = DeltaTime;
	barIndex *= Frequency;
	vec2 rPos = vec2(dBar * BarWidth, fragCoord.x);
	for (int i = 0; i < BarDepth; ++ i)
	{
		vec2 p = GetBarIntensity(rPos, barIndex, time);
		//Blend(color, vec4(p.y, p.y, p.y, opacity * opacity), p.x);
		Blend(color, GetCol(p.y, opacity * opacity), p.x);
		opacity += deltaOpacity;
		time += deltaTime;
		deltaTime *= DepthDecay;
	}

	fragColor = PostProcess(color, uv);
}