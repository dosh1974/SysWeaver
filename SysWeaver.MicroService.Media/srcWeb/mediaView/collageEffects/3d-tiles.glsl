precision highp float;

uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;


const int PlaneCount = 16; // var:
const float ZoomSpeed = 0.5; // var:
const float TileCountX = 6.0; // var:
const float TileCountY = 9.0; // var:
const float TileCount = 54.0; // var:
const float FillWidth = 0.5; // var:
const float PlaneSpread = 0.5; // var:
const float GridScale = 2.0; // var:

const float ImageWidth = 16.0; // var:
const float ImageHeight = 9.0; // var:

const float JitterStrength = 0.9; // var:

const vec4 BackgroundColor = vec4(0,0,0,0); //	var: { "type": "colhdr", "desc": "The background color"}



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



vec4 FastTv(vec4 col, vec2 uv, float iTime)
{
	//     float dist = sin(0.7*iTime+uv.y*17.0) * 0.003;

    //vec4 col = texture2D(tex, vec2(uv.x + 0.000, uv.y));
    //col.r = texture2D(tex, vec2(uv.x + dist, uv.y)).x;
    //col.b = texture2D(tex, vec2(uv.x - dist, uv.y)).z;

    //col.rgb = clamp(col * 0.5 + 0.5 * col * col * 1.2, 0.0, 1.0); // Contract

    col.rgb *= 0.5 + 0.5 * 16.0 * uv.x * uv.y * (1.0 - uv.x) * (1.0 - uv.y); // Vingetting

    //col.rgb *= vec3(0.95, 1.05, 0.95); // Tint

    col.rgb *= 0.9 + 0.1 * sin(10.0 * iTime + uv.y * 1000.0); // TV-lines

    //col.rgb *= 0.99 + 0.01 * sin(110.0 * iTime); // Brightness flicker
	return col;
}


float PlaneIntersect(vec4 plane, vec3 rayDir, vec3 rayOrigin)
{
	float d = dot(rayDir, plane.xyz);
	if (d > 0.0001)
	{
		float t = dot(rayOrigin, plane.xyz) + plane.w;
		t /= d;
		return t;
	}
	return -1.0;
}


vec3 rotateY(vec3 ray, float angle)
{
	float ca = cos(angle);
	float sa = sin(angle);
	return vec3(
		ray.x * ca - ray.z * sa,
		ray.y,
		ray.x * sa + ray.z * ca
		);
}

vec3 rotateX(vec3 ray, float angle)
{
	float ca = cos(angle);
	float sa = sin(angle);
	return vec3(
		ray.x,
		ray.y * ca - ray.z * sa,
		ray.y * sa + ray.z * ca
		);
}

vec4 Image(float index, vec2 uv)
{
	float u = (mod(index, TileCountX) + uv.x) / TileCountX;
	float v = (floor(index / TileCountX) + uv.y) / TileCountY;
	vec4 t = texture2D(tex, vec2(u, v));
	t.xyz *= t.a;
    return t;
	
	float iTime = time + index;
	float dist1 = sin(0.7*iTime+uv.y*17.0) * 0.001;
	float dist2 = sin(0.3*iTime+uv.y*19.0) * 0.001;
	t.r = texture2D(tex, vec2(u + dist1, v)).r;
	t.b = texture2D(tex, vec2(u - dist2, v)).b;
	t = FastTv(t, uv, iTime);
	t.xyz *= t.a;
    return t;
}

void main(void)
{
	
	float space = 1.0 - FillWidth;
	float amp = space * 0.1;
	float offset = FillWidth + space * 0.5;
	float jitterMax = (space - amp * 2.0) * JitterStrength;
	float aspect = ImageWidth / ImageHeight;
	
	float posZ = time * ZoomSpeed;
	int offsetZ = int(floor(posZ));
	posZ *= PlaneSpread;
	
	vec3 rayDir = normalize(vec3((gl_FragCoord.xy - resolution * 0.5) / max(resolution.x, resolution.y), 0.5));
	vec3 rayOrigin = vec3(0, 0, -posZ);


	float ay = sin(time * 0.31) * 0.15 + sin(time * 0.23) * 0.2;
	float ax = sin(time * 0.29) * 0.15 + sin(time * 0.19) * 0.2;
	rayDir = rotateY(rayDir, ay);
	rayDir = rotateX(rayDir, ax);
	
	float fogScale = 1.0 / (float(PlaneCount - 1) * PlaneSpread);

	vec4 col = BackgroundColor;
	col.xyz *= col.a;
	float opacity = 1.0;
	for (int i = 0; i < PlaneCount; ++ i)
	{
	
		int zi = i + offsetZ;
		float zzi = float(zi);
		float si = fract(zzi * 0.5) * 2.0;
		vec4 plane = vec4(0, 0, 1, zzi * PlaneSpread);
	
		float dist = PlaneIntersect(plane, rayDir, rayOrigin);
		if (dist >= 0.0)
		{
			vec2 offs = sin(vec2(zzi, zzi)) * (vec2(si, 1.0 - si) * 3.0 + amp) + offset;
			vec2 pos = (rayDir * dist + rayOrigin).xy;
			pos *= GridScale;
			pos.y *= aspect;
			pos += offs;

			vec4 id = floor(pos.xyxy) * vec4(4241.13, -3163.312, -5669.31, 4051.313);
			id = sin(id);
			vec2 jitter = fract((abs(id.xz) + abs(id.yw)) * 3.0);
			vec2 uv = fract(pos) / FillWidth;
			uv -= (jitter * jitterMax);
			if ((uv.x >= 0.0) && (uv.y >= 0.0) && (uv.x < 1.0) && (uv.y < 1.0))
			{
			
				//gl_FragColor = vec4(jitter.x * 0.5, jitter.y * 0.5, 0, 1);
				//return;
			
				float rid = mod(floor((id.x + id.y) * 100.0 * TileCount), TileCount);
				float fog = max(0.0, 1.0 - (dist * fogScale)) * opacity;
				vec4 tileCol = Image(rid, uv);
				opacity *= (1.0 - tileCol.a);
				col += (tileCol * fog); 
				
				if (opacity <= 0.01)
					break;
			}
		}
	}
	
    vec2 uv = gl_FragCoord.xy / resolution;
    gl_FragColor = PostProcess(col, uv);

}