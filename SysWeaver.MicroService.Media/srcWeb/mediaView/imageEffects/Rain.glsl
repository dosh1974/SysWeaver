//  Rain - raindrops running down a window in front of the picture.
//  Three drop layers (static drops, small and big running drops with trails) act as tiny lenses that refract
//  the picture; optional fog blurs the glass except where the drops and their trails wiped it clear.
//
//  Live controls:
//  iMouse.x = Rain Amount | How much rain: from a dry window to a downpour | 0.5 | 0.0 .. 2.0
//  iMouse.y = Refraction | Strength of the lens effect of the drops | 0.5 | 0.0 .. 2.0
//  iMouse.z = Drop Size | Size of all drops | 0.5 | 0.5 .. 1.5 | x
//  iMouse.w = Fog | Blur of the fogged glass between the drops (0 = clear glass) | 0.0 | 0.0 .. 6.0

uniform float time;
uniform vec2 resolution;
uniform vec4 iMouse;

uniform sampler2D tex;

//#define texture texture2D

#define RainAmount mix(0.0, 2.0, iMouse.x)
#define RefractionAmount mix(0.0, 2.0, iMouse.y)
#define DropSize mix(0.5, 1.5, iMouse.z)
#define Fog mix(0.0, 6.0, iMouse.w)

// --- Motion ---
const float Speed = 0.2; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Speed", "desc": "Animation speed: how fast drops run down and appear / evaporate" }
const float FallSpeed = 0.75; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Fall Speed", "desc": "How fast the running drop layers scroll down relative to the drop motion" }
const float WiggleFrequency = 20.0; // var: { "min": 0.0, "max": 60.0, "step": 1.0, "name": "Wiggle", "desc": "How often running drops swerve sideways (0 = straight paths)" }

// --- Rain ---
const float RainVariation = 0.3; // var: { "min": 0.0, "max": 0.6, "step": 0.01, "name": "Rain Variation", "desc": "How much the rain swells and eases off over time", "scale": 100, "unit": "%" }
const float RainCycleSpeed = 0.05; // var: { "min": 0.0, "max": 0.5, "step": 0.01, "name": "Rain Cycle Speed", "desc": "Speed of the slow swelling of the rain" }
const float StaticDropsAmount = 2.0; // var: { "min": 0.0, "max": 4.0, "step": 0.1, "name": "Static Drops", "desc": "Amount of small drops sitting still on the glass" }

// --- Drops ---
const float StaticDensity = 40.0; // var: { "min": 10.0, "max": 100.0, "step": 1.0, "name": "Static Density", "desc": "Number of static drops (higher = more, smaller drops)" }
const float LayerScale = 1.85; // var: { "min": 1.2, "max": 3.0, "step": 0.05, "name": "Small Drop Scale", "desc": "How much smaller and denser the second running layer is" }
const float TrailDroplets = 10.0; // var: { "min": 2.0, "max": 30.0, "step": 1.0, "name": "Trail Droplets", "desc": "Density of the droplets left along the trails" }

// --- Lens ---
const float MaxRefraction = 0.08; // var: { "min": 0.01, "max": 0.2, "step": 0.01, "name": "Max Refraction", "desc": "Limit of the picture shift inside a drop (higher = drops show farther parts)" }
const float DropBlur = 0.0; // var: { "min": 0.0, "max": 4.0, "step": 0.1, "name": "Drop Blur", "desc": "Blur seen through the drops (only with Fog above 0)" }

#define S(a, b, t) smoothstep(a, b, t)

//  Hash float -> vec3 in [0, 1) (Dave Hoskins, own constants: keeps the drop pattern)
vec3 N13(float p) {
   vec3 p3 = fract(vec3(p) * vec3(.1031,.11369,.13787));
   p3 += dot(p3, p3.yzx + 19.19);
   return fract(vec3((p3.x + p3.y)*p3.z, (p3.x+p3.z)*p3.y, (p3.y+p3.z)*p3.x));
}

//  Sine hash float -> [0, 1)
float N(float t) {
    return fract(sin(t*12345.564)*7658.76);
}

//  Saw shaped pulse on [0, 1]: smooth rise until b, smooth fall to 1
float Saw(float b, float t) {
	return EwAttackRelease(t, b, 1.0, 1.0 - b);
}

//  One layer of running drops: a grid of cells (columns shifted at random), each with a drop that slides down
//  in jerks, wiggles sideways and leaves a trail. x = drop mask, y = trail mask
vec2 DropLayer2(vec2 uv, float t) {
    vec2 UV = uv;
    
    uv.y += t*FallSpeed;
    vec2 a = vec2(6., 1.);
    vec2 grid = a*2.;
    vec2 id = floor(uv*grid);
    
    float colShift = N(id.x); 
    uv.y += colShift;
    
    id = floor(uv*grid);
    vec3 n = N13(id.x*35.2+id.y*2376.1);
    vec2 st = fract(uv*grid)-vec2(.5, 0);
    
    //  Sideways position: random, wiggling along the way
    float x = n.x-.5;
    
    float y = UV.y*WiggleFrequency;
    float wiggle = sin(y+sin(y));
    x += wiggle*(.5-abs(x))*(n.z-.5);
    x *= .7;
    //  Height: slow sticky start, then a fast slide (saw shaped)
    float ti = fract(t+n.z);
    y = (Saw(.85, ti)-.5)*.9+.5;
    vec2 p = vec2(x, y);
    
    float d = length((st-p)*a.yx);
    
    float mainDrop = S(.4, .0, d);
    
    float r = sqrt(S(1., y, st.y));
    float cd = abs(st.x-x);
    float trail = S(.23*r, .15*r*r, cd);
    float trailFront = S(-.02, .02, st.y-y);
    trail *= trailFront*r*r;
    
    //  Small droplets left behind along the trail
    y = UV.y;
    float trail2 = S(.2*r, .0, cd);
    float droplets = max(0., (sin(y*(1.-y)*120.)-st.y))*trail2*trailFront*n.z;
    y = fract(y*TrailDroplets)+(st.y-.5);
    float dd = length(st-vec2(x, y));
    droplets = S(.3, 0., dd);
    float m = mainDrop+droplets*r*trailFront;
    
    //m += st.x>a.y*.45 || st.y>a.x*.165 ? 1.2 : 0.;
    return vec2(m, trail);
}

//  Small round drops sitting on the glass, each appearing and slowly evaporating
float StaticDrops(vec2 uv, float t) {
	uv *= StaticDensity;
    
    vec2 id = floor(uv);
    uv = fract(uv)-.5;
    vec3 n = N13(id.x*107.45+id.y*3543.654);
    vec2 p = (n.xy-.5)*.7;
    float d = length(uv-p);
    
    float fade = Saw(.025, fract(t+n.z));
    float c = S(.3, 0., d)*fract(n.z*10.)*fade;
    return c;
}

//  All drop layers combined: x = drop mask (shapes the lens), y = trail mask (wiped clear of fog)
vec2 Drops(vec2 uv, float t, float l0, float l1, float l2) {
    uv *= (1.0 / DropSize);
    float s = StaticDrops(uv, t)*l0; 
    vec2 m1 = DropLayer2(uv, t)*l1;
    vec2 m2 = DropLayer2(uv*LayerScale, t)*l2;
    
    float c = s+m1.x+m2.x;
    c = S(.3, 1., c);
    
    return vec2(c, max(m1.y*l0, m2.y*l1));
}

void main()
{
    vec2 fragCoord = gl_FragCoord.xy;
    //  Drop space: centered, scaled by the view size (any aspect); UV: the picture, filling the view
	vec2 uv = (fragCoord.xy-.5*resolution.xy) * 1.5 / (resolution.x + resolution.y);
    vec2 UV = fragCoord.xy/resolution.xy;
    float t = time*Speed;
    
    //  The rain swells and eases off slowly around its mean amount
    float rainAmount = sin(time*RainCycleSpeed)*RainVariation*RainAmount+(1.0-RainVariation)*RainAmount;
    
    //  Each layer fades in at its own rain amount: static drops first, then small, then big running drops
    float staticDrops = S(-.5, 1., rainAmount)*StaticDropsAmount;
    float layer1 = S(.25, .75, rainAmount);
    float layer2 = S(.0, .5, rainAmount);
    
    //  Drop mask and its slope (forward differences): the slope bends the view like a lens
    vec2 c = Drops(uv, t, staticDrops, layer1, layer2);
    vec2 e = vec2(.001, 0.);
    float cx = Drops(uv+e, t, staticDrops, layer1, layer2).x;
    float cy = Drops(uv+e.yx, t, staticDrops, layer1, layer2).x;
    vec2 n = vec2(cx-c.x, cy-c.x) * RefractionAmount;
    
    //  Fogged glass is blurred, drops and their trails are clear (DropBlur inside the drops)
    float focus = mix(Fog-c.y, DropBlur, S(.1, .2, c.x));
    
    //  The slope at the sharp rim of a drop can be larger than the picture: limit the refraction (a drop shows what is near it) and
    //  clamp the sampled point to the picture (half a texel in, so filtering never reads outside), so nothing from beyond the edges
    //  (see-through or black) is pulled into the picture
    float nl = length(n);
    if (nl > MaxRefraction)
        n *= MaxRefraction / nl;
    vec2 texel = 0.5 / max(resolution, vec2(1.0));
    vec2 q = clamp(UV + n, texel, 1.0 - texel);

    vec4 col;
    if (Fog > 0.0)
        col = texture2DLod(tex, q, focus);
    else
        col = texture2D(tex, q);
    //  Transparency: the canvas uses premultiplied alpha, so the colour is scaled by its alpha and the alpha is kept (see-through
    //  parts stay see-through, never black)
    gl_FragColor = vec4(col.rgb * col.a, col.a);

}
