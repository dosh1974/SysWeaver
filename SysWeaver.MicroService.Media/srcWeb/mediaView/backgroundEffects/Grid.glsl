uniform float time;
uniform vec2 resolution;

const vec4 Color = vec4(0.1, 1.0, 0.5, 1.0); //	var: { "type": "colhdr", "desc": "The grid color"}
const vec4 BgColor = vec4(0.0, 0.0, 0.0, 1.0); //var: { "type": "colhdr", "desc": "The background color"}
const float CellCount = 10.0;//var:{}
const float LineThickness = 6.0;//var:{}

const float DistortionAmountX = 0.02;//var:{}
const float DistortionAmountY = 0.02;//var:{}
const float DistortionSpeed = 0.5;//var:{}
const float DistortionFreqX = 4.0;//var:{}
const float DistortionFreqY = 4.0;//var:{}

const float BreathAmount = 0.1;//var:{}
const float BreathSpeed = 0.05;//var:{}


// Post processing parameters

const float NoiseInt = 0.0; 	//	var: { "min": 0, "max": 1.0, "step": 0.05, "desc": "The amount of noise, set to 0 to disable"}

const float VingetteIntensity = 1.0;	//	var: { "min": 0, "max": 1, "step": 0.05, "name": "Vingette intensity", "desc": "The intesity of the vingette effect, set to zero to disable vingetting"}
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
		color.rgb *= (PpNoise(vec2(gl_FragCoord) * vec2(-13.0, 17.0) + (time * vec2(121.12, 1445.23))) * NoiseInt + (1.0 - NoiseInt * 0.5));
	if (VingetteIntensity > 0.0)
		color *= PpVingette(uv);
	if ((TopLeftO < 1.0) || (TopRightO < 1.0) || (BottomRightO < 1.0) || (BottomLeftO < 1.0))
		color *= clamp(mix(mix(BottomLeftO, BottomRightO, uv.x), mix(TopLeftO, TopRightO, uv.x), uv.y), 0.0, 1.0);
	return color;
}


void main()
{
   
    vec2 uv = gl_FragCoord.xy / resolution.xy;
    float aspect = resolution.x / resolution.y;
    
    float t = time * DistortionSpeed;
	vec4 a4 = vec4(1.0, 0.3, 0.5, 0.7) * t + vec4(13.0, 17.0, 23.0, 29.0);
	a4 = uv.xxyy * vec4(DistortionFreqX, DistortionFreqX, DistortionFreqY, DistortionFreqY) + a4;
	a4 = sin(a4);
	a4.xy *= a4.zw;
	a4.xy = a4.xy * vec2(DistortionAmountX, DistortionAmountY) + uv;
	
    float scale = CellCount * 3.14159265359 * 0.5;
	if (BreathAmount > 0.0)
	    scale *= (1.0 + BreathAmount * sin(time * BreathSpeed));
    vec2 scaledUV = a4.xy * vec2(aspect, 1.0) * scale;
    
    // ------------------ Рисуем сетку ------------------
    vec2 gridDeriv = fwidth(scaledUV);
    vec2 w = LineThickness * gridDeriv;
    
    float dx = min(fract(scaledUV.x), 1.0 - fract(scaledUV.x));
    float dy = min(fract(scaledUV.y), 1.0 - fract(scaledUV.y));
    
    float lx = 1.0 - smoothstep(0.0, w.x, dx);
    float ly = 1.0 - smoothstep(0.0, w.y, dy);
    float grid = max(lx, ly);
    
    // Фон (почти чёрный) и цвет сетки
   
    vec4 col = mix(BgColor, Color, grid);

	col.xyz *= col.w;
    gl_FragColor = PostProcess(col, uv);
}