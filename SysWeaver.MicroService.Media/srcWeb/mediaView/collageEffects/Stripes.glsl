precision highp float;

uniform sampler2D tex;
uniform float time;
uniform vec2 resolution;

const float TileCountX = 6.0;   //var:
const float TileWidth = 168.0;  //var:
const float TileHeight = 98.0;  //var:


float Vingette(vec2 uv, float exp)
{
    uv *=  1.0 - uv.yx;   //vec2(1.0)- uv.yx; -> 1.-u.yx; Thanks FabriceNeyret !
    float vig = uv.x*uv.y * 20.0; // multiply with sth for intensity
    vig = pow(vig, exp); // change pow for modifying the extend of the  vignette
    return vig;
}

vec4 Image(vec2 uv)
{
    vec4 t = texture2D(tex, fract(uv));
	t.xyz *= t.a;
    return t;
}

void main(void)
{
    vec2 fragCoord = gl_FragCoord.xy;
    const float screenStripeHeight = TileCountX * TileHeight;


    
    float screenStripe = (fragCoord.x + fragCoord.y * 0.4) / TileWidth;
    float sourceU = fract(screenStripe);
    screenStripe = floor(screenStripe);
    
    
    
    float flip = mod(screenStripe, 2.0) - 0.5;
    
    float speedV = (abs(sin(screenStripe * 13.0)) * 0.1 + 0.025) * flip;
    float dv = time * speedV;
    
    float u = (mod(screenStripe, TileCountX) + sourceU) / TileCountX;
    float v = fragCoord.y / screenStripeHeight + dv + sourceU * -(0.16 * screenStripeHeight / (TileWidth * TileCountX));

    vec4 col = Image(vec2(u, v));

    vec2 uv = fragCoord/resolution.xy;	

    // Output to screen

    gl_FragColor = col * Vingette(uv, 1.2);
}