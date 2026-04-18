const vec3 Color = vec3(0.5,0.25,0.125);//	var: { "type": "colhdr", "desc": "This is not a true rgb value, it just controls the colors to some extent"}
const float RollSpeed = 1.0;//var:{}
const float AltSpeed = 1.0;//var:{}
const float RollAmount = 1.0;//var:{}

float tanh_approx(float x) {
  float x2 = x*x;
  return clamp(x*(27.0 + x2)/(27.0+9.0*x2), -1.0, 1.0);
}


void mainImage(out vec4 o, vec2 u) {
    float i,a,d,s,t=iTime*0.21;
    vec3  p = iResolution;    
    u = (u+u-p.xy)/p.y;
    
    float roll = sin(t*.23*RollSpeed )*.3*RollAmount + sin(t*0.17*RollSpeed)*.15*RollAmount - .785;
    float alt  = sin(t*.97*AltSpeed )*.8 + sin(t*.53*AltSpeed )*4.4;
   
    float c=cos(roll), sn=sin(roll);
    vec2 ru = u * mat2(c,-sn,sn,c);
    o = vec4(0);
	vec4 col = vec4(Color * 8.0, 0);
    for(int i = 0; i<128; ++ i) {
        p = vec3(ru * d, d+t/.1);
        s = 8.+p.y+p.x + alt;
		float a = .01;
		for (int j = 0; j < 7; ++ j)
		{
            p += cos(t-p.yzx)*.2;
            s -= abs(dot(sin(t+t-.2*p.z+.3*p / a), vec3(a+a)));
			a += a;
		}
        d += s = .1 + abs(s)*.1;
        o +=  col/s + .1*col/abs(ru.y+ru.x);
    }
    o = o /1e3 / length(ru -= vec2(.5, .3)) + .1*dot(ru,ru);
	o.r = tanh_approx(o.r);
	o.g = tanh_approx(o.g);
	o.b = tanh_approx(o.b);
}