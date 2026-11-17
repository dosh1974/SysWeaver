// Bad VHS - a worn video tape look for any picture: wobbling, jittering lines,
// rolling tape creases, head switching noise at the bottom, color bleed with
// chroma shift and a slow brightness hum.
//
// Live controls:
//  iMouse.x = Wobble | Tape wobble and line jitter strength | 0.5 | 0 .. 2 | x
//  iMouse.y = Color Bleed | Horizontal smear / chroma shift width | 0.5 | 0 .. 0.014
//  iMouse.z = Crease | Strength of the rolling tape creases (tear + darkening) | 0.5 | 0 .. 2 | x
//  iMouse.w = Switch Noise | Height of the head switching band at the bottom | 0.5 | 0 .. 0.06

uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;

#define iChannel0 tex
#define texture texture2D

// --- Animation ---
const float Speed = 1.0; // var: { "min": 0.0, "max": 3.0, "step": 0.05, "name": "Speed", "desc": "Overall speed of all tape motion", "unit": "x" }

// --- Color bleed ---
const float Exposure = 0.6; // var: { "min": 0.3, "max": 1.0, "step": 0.01, "name": "Exposure", "desc": "Overall brightness after the bleed" }
const float BleedGain = 0.1; // var: { "min": 0.0, "max": 0.2, "step": 0.005, "name": "Bleed Gain", "desc": "Strength of the smeared, glowing copies" }
const float ChromaShift = 2.0; // var: { "min": 0.0, "max": 4.0, "step": 0.1, "name": "Chroma Shift", "desc": "Offset between the red, green and blue smears (color fringes)" }

// --- Tape wave ---
const float WaveAmount = 0.005; // var: { "min": 0.0, "max": 0.03, "step": 0.001, "name": "Wave Amount", "desc": "Slow horizontal wobble of the whole picture" }
const float JitterAmount = 0.01; // var: { "min": 0.0, "max": 0.05, "step": 0.001, "name": "Jitter Amount", "desc": "Fast sideways jitter of single lines" }
const float JitterLines = 100.0; // var: { "min": 10.0, "max": 400.0, "step": 1.0, "name": "Jitter Lines", "desc": "Fineness of the line jitter (higher = thinner lines)" }
const float JitterSpeed = 10.0; // var: { "min": 0.0, "max": 30.0, "step": 0.5, "name": "Jitter Speed", "desc": "How fast the line jitter changes" }

// --- Tape crease ---
const float CreaseDensity = 8.0; // var: { "min": 2.0, "max": 30.0, "step": 0.5, "name": "Crease Density", "desc": "Number of crease bands over the height" }
const float CreaseSpeed = 1.2; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Crease Speed", "desc": "How fast the creases roll up" }
const float CreaseThreshold = 0.92; // var: { "min": 0.7, "max": 0.99, "step": 0.01, "name": "Crease Threshold", "desc": "Higher = thinner crease bands" }

// --- Switching noise ---
const float SwitchShift = 0.3; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Switch Shift", "desc": "Vertical image shift inside the bottom noise band" }
const float SwitchJitter = 0.2; // var: { "min": 0.0, "max": 0.6, "step": 0.01, "name": "Switch Jitter", "desc": "Sideways tearing inside the bottom noise band" }
const float SwitchHueSwap = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Switch Hue Swap", "desc": "Color channel swap inside the bottom noise band", "scale": 100, "unit": "%" }

// --- Hum & edges ---
const float HumAmount = 0.1; // var: { "min": 0.0, "max": 0.3, "step": 0.01, "name": "Hum Amount", "desc": "Brightness of the slow rolling AC hum bars" }
const float HumSpeed = 0.2; // var: { "min": 0.0, "max": 1.0, "step": 0.01, "name": "Hum Speed", "desc": "How fast the hum bars roll" }
const vec3 EdgeColor = vec3(0.1); // var: { "type": "col", "name": "Edge Color", "desc": "Color shown where the picture is shifted off screen" }

// --- Live controls ---
#define Wobble mix(0.0, 2.0, iMouse.x)
#define Bleed mix(0.0, 0.014, iMouse.y)
#define Crease mix(0.0, 2.0, iMouse.z)
#define SwitchHeight mix(0.0, 0.06, iMouse.w)

// Texture lookup; outside the picture horizontally (tape shifted) shows EdgeColor
vec4 tex2D( sampler2D _tex, vec2 _p ){
  vec4 col = texture( _tex, _p );
  if ( 0.5 < abs( _p.x - 0.5 ) ) {
    col.xyz = EdgeColor;
  }
  return col;
}

// Sine hash (kept local: its constants define the noise pattern)
float hash( vec2 _v ){
  return fract( sin( dot( _v, vec2( 89.44, 19.36 ) ) ) * 22189.22 );
}

// Smooth interpolated hash on a grid of resolution _r
float iHash( vec2 _v, vec2 _r ){
  float h00 = hash( vec2( floor( _v * _r + vec2( 0.0, 0.0 ) ) / _r ) );
  float h10 = hash( vec2( floor( _v * _r + vec2( 1.0, 0.0 ) ) / _r ) );
  float h01 = hash( vec2( floor( _v * _r + vec2( 0.0, 1.0 ) ) / _r ) );
  float h11 = hash( vec2( floor( _v * _r + vec2( 1.0, 1.0 ) ) / _r ) );
  vec2 ip = vec2( smoothstep( vec2( 0.0, 0.0 ), vec2( 1.0, 1.0 ), mod( _v*_r, 1. ) ) );
  return ( h00 * ( 1. - ip.x ) + h10 * ip.x ) * ( 1. - ip.y ) + ( h01 * ( 1. - ip.x ) + h11 * ip.x ) * ip.y;
}

// 8 octave fractal noise, about 0..1
float noise( vec2 _v ){
  float sum = 0.;
  float s = 1.;
  for( int i=1; i<9; i++ )
  {
    s *= 2.; // = 2^i
    sum += iHash( _v + vec2( i ), vec2( 2. * s ) ) / s;
  }
  return sum;
}

void mainImage( out vec4 fragColor, in vec2 fragCoord ){
  vec2 uv = fragCoord.xy / resolution;
  vec2 uvn = uv;
  vec4 col = vec4( 0.0 );
  float t = time * Speed;

  // fast per-line noise, shared by the jitter, the crease tear and the switching noise
  // (all three used the same arguments: computed once)
  float lineNoise = noise( vec2( uv.y * JitterLines, t * JitterSpeed ) ) - 0.5;

  // tape wave: slow horizontal wobble + fast line jitter
  uvn.x += ( noise( vec2( uvn.y, t ) ) - 0.5 ) * WaveAmount * Wobble;
  uvn.x += lineNoise * JitterAmount * Wobble;

  // tape crease: rolling bands that tear lines sideways and darken the picture
  float tcPhase = clamp( ( sin( uvn.y * CreaseDensity - t * EW_PI * CreaseSpeed ) - CreaseThreshold ) * noise( vec2( t ) ), 0.0, 0.01 ) * 10.0 * Crease;
  float tcNoise = max( lineNoise, 0.0 );
  uvn.x = uvn.x - tcNoise * tcPhase;

  // head switching noise: the bottom lines are shifted, torn and hue swapped
  float snPhase = smoothstep( max( SwitchHeight, 1e-4 ), 0.0, uvn.y );
  uvn.y += snPhase * SwitchShift;
  uvn.x += snPhase * ( lineNoise * SwitchJitter );

  col = tex2D( iChannel0, uvn );
  col.xyz *= 1.0 - tcPhase;
  col.xyz = mix( col.xyz, col.yzx, snPhase * SwitchHueSwap );

  // color bleed: horizontal smear with the channels offset (chroma shift)
  for( float x = -4.0; x < 2.5; x += 1.0 ){
    col.xyz += vec3(
      tex2D( iChannel0, uvn + vec2( x, 0.0 ) * Bleed ).x,
      tex2D( iChannel0, uvn + vec2( x - ChromaShift, 0.0 ) * Bleed ).y,
      tex2D( iChannel0, uvn + vec2( x - 2.0 * ChromaShift, 0.0 ) * Bleed ).z
    ) * BleedGain;
  }
  col.xyz *= Exposure;

  // ac beat: slow brightness hum rolling up the screen
  col.xyz *= 1.0 + clamp( noise( vec2( 0.0, uv.y + t * HumSpeed ) ) * 0.6 - 0.25, 0.0, HumAmount );

  // premultiplied alpha output
  col.xyz *= col.w;
  fragColor = col;
}
