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

float Vingette(vec2 uv, float exp)
{
    uv *=  1.0 - uv.yx;   //vec2(1.0)- uv.yx; -> 1.-u.yx; Thanks FabriceNeyret !
    float vig = uv.x*uv.y * 20.0; // multiply with sth for intensity
    vig = pow(vig, exp); // change pow for modifying the extend of the  vignette
    return vig;
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

	vec4 col = vec4(0);
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
    gl_FragColor = col * Vingette(uv, 1.2);

}