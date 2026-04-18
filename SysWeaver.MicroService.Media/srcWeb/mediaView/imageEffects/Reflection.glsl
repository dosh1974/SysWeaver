precision highp float;

uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;


const float ImageWidth = 16.0; // var:
const float ImageHeight = 16.0; // var:


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



float Vingette(vec2 uv, float exp)
{
    uv *=  1.0 - uv.yx;   //vec2(1.0)- uv.yx; -> 1.-u.yx; Thanks FabriceNeyret !
    float vig = uv.x*uv.y * 20.0; // multiply with sth for intensity
    vig = pow(vig, exp); // change pow for modifying the extend of the  vignette
    return vig;
}

vec4 TraceOne(out float reflection, out vec3 colNormal, inout vec3 rayOrigin, vec3 rayDir)
{
	const vec4 imagePlane = vec4(0, 0, 1, 0.75);
	const vec4 groundPlane = vec4(0, -1, 0, 0.5);


	float hitDist = 1000000.0;
	int hit = -1;
	vec3 hitPos = vec3(0);
	vec2 hitData = vec2(0);
	float dist;
	//	Check image plane
	dist = PlaneIntersect(imagePlane, rayDir, rayOrigin);
	if ((dist > 0.1) && (dist < hitDist))
	{
		vec3 newPos = rayDir * dist + rayOrigin;
		float aspect = ImageWidth / ImageHeight;
		vec2 pos = newPos.xy + 0.5;
		pos.y *= aspect;
//		if ((pos.x >= 0.0) && (pos.y >= 0.0) && (pos.x <= 1.0) && (pos.y <= 1.0))
		{
			hit = 0;
			hitDist = dist;
			hitPos = newPos;
			hitData = fract(pos);
		}
	}
	//	Check ground plane
	dist = PlaneIntersect(groundPlane, rayDir, rayOrigin);
	if ((dist > 0.1) && (dist < hitDist))
	{
		hit = 1;
		hitDist = dist;
		hitPos = rayDir * dist + rayOrigin;
	}
	//	Return
	rayOrigin = hitPos;
	if (hit == 0)
	{
		reflection = 0.0;
		colNormal = imagePlane.xyz;
		return vec4(hitData.x, hitData.y, 0, 1);
//		return vec4(0.4, 1, 0.4, 1);
		//return texture2D(tex, hitData);
	}
	if (hit == 1)
	{
		reflection = 1.0;
		colNormal = groundPlane.xyz;
		return vec4(1, 1, 1, 1);
	}
	reflection = 0.0;
	colNormal = groundPlane.xyz;
	return vec4(0.3, 0.8, 1.0, 1.0);
}



void main(void)
{
	

	vec3 rayDir = normalize(vec3((gl_FragCoord.xy - resolution * 0.5) / min(resolution.x, resolution.y), 0.5));
	vec3 rayOrigin = vec3(0, 0, 0);
	rayDir = rotateX(rayDir, sin(time) * 0.1 + 0.09);
	rayDir = rotateY(rayDir, sin(time * 0.12) * 0.2);
	vec3 colNormal;
	float reflection;
	vec4 color = TraceOne(reflection, colNormal, rayOrigin, rayDir);
	if (reflection > 0.5)
	{
		color = vec4(fract(rayOrigin), 1.0) * 0.5;
		float k = dot(colNormal, rayDir) * -2.0;
		rayDir += colNormal * k;
		rayDir = normalize(rayDir);
		color += TraceOne(reflection, colNormal, rayOrigin, rayDir) * 0.5;
	}
		//color = vec4(fract(rayOrigin.xyz), 1.0);
		//color = vec4(rayDir * 0.5 + 0.5, 1);
	/*
	//color = vec4(fract(rayOrigin.yyy * 1.5), 1.0);
	//color = vec4(rayDir * 0.5 + 0.5, 1);
	if (reflection > 0.0)
	{
		float k = dot(colNormal, rayDir) * -2.0;
		rayDir += colNormal * k;
		
		float m = reflection;
		//color = vec4(rayDir * 0.5 + 0.5, 1);
		//color = vec4(fract(rayOrigin * 0.5), 1.0);
		vec4 refColor = TraceOne(reflection, colNormal, rayOrigin, rayDir, 0);
		color.xyz = vec3(reflection);
		//color = mix(color, refColor, reflection);
	}
	*/
	
    vec2 uv = gl_FragCoord.xy / resolution;
    gl_FragColor = color;// * Vingette(uv, 1.2);

}