// CC0: Trailing the Twinkling Tunnelwisp
// Of all the short shaders I wrote last year, my personal favourite was
// 'Trailing the Twinkling Tunnelwisp'.
// However, I felt it should be possible to remove a few characters.
// In this version, I shaved off 26 chars, and the visuals are mostly the same.

// I probably missed something obvious, as usual.

// The music code by Pestis is too much magic for me to touch :)

// Twigl link (392 chars): https://twigl.app?ol=true&ss=-OlI_T9Te2ustchQpde1

const vec3 Color = vec3(1.0, 0.5, 0.25);//	var: { "type": "colhdr", "desc": "This is not a true rgb value, it just controls the colors to some extent"}
const float Seed = 1.0;//var:{}

float tanh_approx(float x) {
  float x2 = x*x;
  return clamp(x*(27.0 + x2)/(27.0+9.0*x2), -1.0, 1.0);
}


void mainImage(out vec4 o,vec2 C) {
  float d,z,s,Z,t=iTime;
  vec4 O,U=vec4(Color, Seed);
  vec2 r=iResolution.xy;
  for (int i = 0; i<78; ++ i)
	{
      o.y-=.11;
      o.xy*=mat2(cos(11.*U.zywz-2.*o.z));
      o.y-=.2;
      z+=d=5E-4+abs(abs(dot(sin(o*=8.),cos(o.zxwy))-1.)-abs(dot(sin(o*=3.),cos(o.zxwy))-1.)/3.)/32.;
      o=1.+cos(.7*U+5.*Z);
      O+=(s<0.?d*=d*d,.1:1.)*o.w/max(d,5E-4)*o;
      o=vec4(z*normalize(vec3(C-.5*r,r.y)),.2);
      Z=o.z+=t/3E1;
	  o.y=abs(s=o.y+.1);
    }

  O+=(1.4+sin(t)*sin(t/.6)*sin(t/.4))*1E3/length(o.xy)*U;
  O=(O/8E4);
  O.r = tanh_approx(O.r);
  O.g = tanh_approx(O.g);
  O.b = tanh_approx(O.b);
  o=O;
}
