

float GetEdge(float t, float v, float base, float slope)
{
    vec4 ot = vec4(3.0, 0.9, 1.37, 3.29) * t * 2.0 + vec4(10.0, 20.0, 30.0, 40.0);
    vec4 uvs = vec4(-2.7, 4.3, 5.3, 7.3) * v;
    vec4 oy = uvs + ot;
    float s = dot(sin(oy), vec4(0.1, 0.05, 0.03, 0.02) * 0.2) + slope * v;
    s += base;
    return s;
}


float Ss(float x)
{
    return x * x * (3.0f - 2.0f * x);
}

float GetEdgeDistance(float edge, float u, float hardness)
{

    float dist = edge - u;
    float alpha = dist * hardness;
    alpha = clamp(alpha, 0.0, 1.0);
    alpha = Ss(alpha);
    return alpha;
}

const vec4 ShadowColor = vec4(0.0, 0.0, 0.0, 0.7);
const vec4 BgColor = vec4(0.1, 0.15, 0.1, 1.0);



const vec4 Color0 = vec4(0.3, 0.8, 0.5, 1.0) * 0.5;
const vec4 Color1 = vec4(0.4, 0.5, 0.3, 1.0) * 0.8;
const vec4 EdgeColor = vec4(0.9, 0.8, 0.1, 1.0) * 0.75;

const float EdgeWidth = 0.02;


vec4 blend(vec4 background, vec4 foreground)
{
	return foreground + background * (1.0 - foreground.a);
}


vec4 One(float time, vec2 uv, float base, float slope, float shadowDist)
{
    float edge = GetEdge(time, uv.y, base, slope);

    float dist = edge - uv.x;
    float left = dist / edge;//* (1.0 / 0.3);
    vec4 col = mix(Color0, Color1, left);

    float alpha = GetEdgeDistance(edge, uv.x, iResolution.x);
    float shadowAlpha = GetEdgeDistance(edge, uv.x + shadowDist, 30.0);
    float alphaEdge = GetEdgeDistance(edge, uv.x + EdgeWidth, iResolution.x);
    
    vec4 shadowCol = ShadowColor * (shadowAlpha * ShadowColor.a);
    col = mix(EdgeColor, col, alphaEdge);
    col *= alpha;
    
    return blend(shadowCol, col);
}



void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
    float dv = 4.0 / iResolution.y;
    vec2 uv = fragCoord/iResolution.xy;
    
    
    vec4 color = BgColor;
    
    color = blend(color, One(iTime * 0.2, uv, 0.15, -0.1, -0.03));
    color = blend(color, One(iTime * 0.512, uv, 0.05, 0.1, -0.04));

    vec2 uvi = vec2(1.0) - uv;
   
    color = blend(color, One(iTime * 0.312, uvi, 0.15, -0.1, -0.02));
    color = blend(color, One(iTime * 0.412, uvi, 0.05, 0.1, -0.02));

    
   

    fragColor = color;
}