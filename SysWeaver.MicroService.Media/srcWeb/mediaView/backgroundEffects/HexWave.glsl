#define RT3 1.7320508075688772
#define RT3_2 0.8660254037844386
#define PI 3.141592653589793
#define PI_3 1.0471975511965976

// side and height of the isometric grid
const float s = 0.035;
const float h = s*RT3_2;

const float BarThick = 1.0; // var:{}
const float LineThick = 0.0;// var:{}
const float Fade = 0.001;// var:{}

const vec3 Light1 = vec3(0.7, 0.6, 0.5);//	var: { "type": "colhdr" }
const vec3 Light2 = vec3(0.3, 0.7, 0.9);//	var: { "type": "colhdr" }
const vec3 Light3 = vec3(1.8, 1.9, 1.7);//	var: { "type": "colhdr" }
const vec4 BgColor = vec4(0.01,0.02,0.04, 1.0);//	var: { "type": "colhdr" }

const vec2 t = vec2(1.0, RT3);

float sdLine(vec2 p, vec2 a, vec2 b)
{
    vec2 pa = p - a;
    vec2 ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h);
}

struct Hex
{
    vec2 coord;
    int segment;
};

Hex hexify(vec2 p)
{
    float a = atan(p.y, p.x);
    float a_mod = mod(a, PI_3);
    int o = int(mod(floor((a + PI) / PI_3)+3.0,6.0));
    return Hex(vec2(cos(a_mod), sin(a_mod)) * length(p), o);
}

float sdDoubledLine(vec2 uv, vec4 l, vec2 r)
{
    float dmid = sdLine(uv, l.xy, l.zw);
    float dl = sdLine(uv, l.xy-r, l.zw-r);
    float dr = sdLine(uv, l.xy+r, l.zw+r);
    return min(dmid, min(dl, dr));
}

int shadeSideUpper(vec2 uv, vec4 l, float th)
{
    float d = uv.y - l.y;
    return (d > 0.0 && d < th) ? 0 : (d < 0.0 && -d < th) ? 1 : -1;
}

int shadeSideLower(vec2 uv, vec4 l, float th)
{
    float d = uv.x -  (l.x + 1.0/RT3 * uv.y);
    return (d > 0.0 && d < th) ? 1 : (d < 0.0 && -d < th) ? 0 : -1;
}

vec3 normal(int fi)
{
    return vec3(fi == 0, fi == 1, fi == 2);
}

float lighting(vec3 n, vec3 l)
{
    float diff = max(dot(n, l), 0.0);
    return pow(diff, 4.0);
}

void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
    vec2 uv = fragCoord/iResolution.xy;
    uv = uv * 2.0 - 1.0;
    uv.x *= iResolution.x / iResolution.y;
    uv /= s*16.0;

    vec2 t_2 = t*.5;
    vec2 uva = mod(uv,t)-t_2;
    vec2 uvb = mod(uv-t_2,t)-t_2;

    vec2 gv = length(uva) < length(uvb) ? uva : uvb;
    Hex hex = hexify(gv);
    gv = hex.coord;
    gv *= s*16.0;

    vec4 barLower = vec4(s*4.0, h*0.0, s*8.0, h*8.0);
    vec4 barUpper = vec4(s*1.5, h*4.0, s*8.0, h*4.0);
    
    float d = sdDoubledLine(gv, barUpper, vec2(0.0, h*BarThick));
    int fi = shadeSideUpper(gv, barUpper, h*BarThick-LineThick);

    if (sdLine(gv, barUpper.xy, barUpper.zw) > h*BarThick)
    {
        d = min(d, sdDoubledLine(gv, barLower, vec2(s*BarThick, 0.0)));
        int i = shadeSideLower(gv, barLower, s*BarThick);
        fi = i == -1 ? -1 : 2 + i;
    }


    int o = hex.segment;
    vec3 n = normal(int(mod( floor((float(o) + mod(float(fi*3),5.0))* 0.5), 3.0)));
    vec3 l = normalize(vec3(0.3, 0.4, 0.5+0.2*sin(iTime*0.5)));
    vec3 light1 = Light1 * lighting(n, l);
    vec3 l2 = normalize(vec3(1.0, 0.6+(0.5*sin(iTime*0.33)*length(uv)), 0.3));
    vec3 light2 = Light2 * lighting(n, l2)  / (0.2*length(uv) + 0.3);
    vec3 l3 = normalize(vec3(0.3, 0.7, sin(-iTime*0.4 + length(uv)*3.0)));
    vec3 light3 = Light3 * pow(lighting(n, l3), 6.0) / (0.7*length(uv) + 0.1);

    vec4 col = fi == -1 ? BgColor : vec4((light1 + light2 + light3) * smoothstep(LineThick,LineThick+Fade, d), 1.0);
	col.rgb *= col.a;
    fragColor = col;
}