const vec3 BG = vec3(0.006, 0.008, 0.014); // var: {"type": "col", "name": "Background Color", "desc": "The background color of the scene"}
const float VIGNETTE = 0.55; // var: {"min": 0.0, "max": 2.0, "step": 0.05, "name": "Vignette", "desc": "Strength of the vignette effect"}
const float EXPOSURE = 1.00; // var: {"min": 0.1, "max": 3.0, "step": 0.05, "name": "Exposure", "desc": "Overall exposure multiplier"}
const float GAMMA = 0.85; // var: {"min": 0.1, "max": 3.0, "step": 0.05, "name": "Gamma", "desc": "Gamma correction value"}
const float IRID_AMT = 0.55; // var: {"min": 0.0, "max": 1.0, "step": 0.05, "name": "Iridescence Amount", "desc": "Amount of iridescent coloring"}
const float IRID_EDGE = 3.4; // var: {"min": 0.0, "max": 10.0, "step": 0.1, "name": "Iridescence Edge", "desc": "Iridescence effect at the edges"}
const float IRID_DRIFT = 0.04; // var: {"min": 0.0, "max": 0.5, "step": 0.01, "name": "Iridescence Drift", "desc": "Speed of iridescence color shift"}
const float RIM_STR = 0.85; // var: {"min": 0.0, "max": 2.0, "step": 0.05, "name": "Rim Strength", "desc": "Strength of the cell rim highlight"}
const float RIM_W = 2.2; // var: {"min": 0.1, "max": 10.0, "step": 0.1, "name": "Rim Width", "desc": "Width of the cell rim"}
const float HALO_STR = 0.055; // var: {"min": 0.0, "max": 0.5, "step": 0.005, "name": "Halo Strength", "desc": "Strength of the outer glow halo"}
const float HALO_W = 7.0; // var: {"min": 1.0, "max": 20.0, "step": 0.5, "name": "Halo Width", "desc": "Spread of the outer glow halo"}
const float FILL_STR = 0.030; // var: {"min": 0.0, "max": 0.5, "step": 0.01, "name": "Fill Strength", "desc": "Base internal fill brightness"}
const float ORN_STR = 0.80; // var: {"min": 0.0, "max": 2.0, "step": 0.05, "name": "Ornament Strength", "desc": "Strength of internal patterns and ornaments"}
const float SETAE_STR = 0.90; // var: {"min": 0.0, "max": 2.0, "step": 0.05, "name": "Setae Strength", "desc": "Strength of the hair-like setae structures"}
const float ALPHA_STR = 0.90; // var: {"min": 0.0, "max": 1.0, "step": 0.05, "name": "Alpha Strength", "desc": "Base opacity of the cells"}
const int LAYERS = 4; // var: {"min": 1, "max": 8, "step": 1, "name": "Layer Count", "desc": "Number of depth layers to render"}
const float LAYER_SCALE = 1.75; // var: {"min": 1.0, "max": 3.0, "step": 0.05, "name": "Layer Scale", "desc": "Scale factor between depth layers"}
const float CELL_FILL = 0.60; // var: {"min": 0.0, "max": 1.0, "step": 0.05, "name": "Cell Fill", "desc": "Probability of a cell spawning in a grid slot"}
const float ZOOM = 3.0; // var: {"min": 0.5, "max": 10.0, "step": 0.1, "name": "Zoom", "desc": "Camera zoom level"}
const int AA = 2; // var: {"min": 1, "max": 4, "step": 1, "name": "Anti-Aliasing", "desc": "Number of AA samples per pixel"}
const vec2 DRIFT = vec2(0.042, 0.016); // var: {"name": "Drift", "desc": "Constant directional drift speed"}
const float SPIN = 0.22; // var: {"min": 0.0, "max": 2.0, "step": 0.05, "name": "Spin", "desc": "Rotational speed of the cells"}
const float WOBBLE = 0.035; // var: {"min": 0.0, "max": 0.5, "step": 0.005, "name": "Wobble", "desc": "Amount of positional wobble"}
const float BREATH = 0.02; // var: {"min": 0.0, "max": 0.2, "step": 0.005, "name": "Breath", "desc": "Pulsating scale animation amount"}
const float FOCUS_BASE = 1.3; // var: {"min": 0.0, "max": 5.0, "step": 0.1, "name": "Focus Base", "desc": "Base focal depth plane"}
const float FOCUS_AMP = 1.4; // var: {"min": 0.0, "max": 5.0, "step": 0.1, "name": "Focus Amplitude", "desc": "Amplitude of focal plane oscillation"}
const float FOCUS_SPEED = 0.11; // var: {"min": 0.0, "max": 1.0, "step": 0.01, "name": "Focus Speed", "desc": "Speed of focal plane oscillation"}
const float SETAE_SPEED = 1.6; // var: {"min": 0.0, "max": 5.0, "step": 0.1, "name": "Setae Speed", "desc": "Animation speed of the setae hairs"}
const float SETAE_BEND = 0.34; // var: {"min": 0.0, "max": 2.0, "step": 0.05, "name": "Setae Bend", "desc": "Amount of bending in the setae hairs"}
const float BLUR_GAIN = 3.0; // var: {"min": 0.0, "max": 10.0, "step": 0.1, "name": "Blur Gain", "desc": "Multiplier for defocus blur amount"}
const float BLUR_MAX = 2.0; // var: {"min": 1.0, "max": 5.0, "step": 0.1, "name": "Blur Max", "desc": "Maximum limit for defocus blur"}
const float DEFOCUS_DIM = 1.0; // var: {"min": 0.0, "max": 2.0, "step": 0.1, "name": "Defocus Dim", "desc": "Darkening factor for out-of-focus layers"}
const float JITTER = 0.50; // var: {"min": 0.0, "max": 1.0, "step": 0.05, "name": "Jitter", "desc": "Random positional jitter for cells"}
const float ORN_CLAMP = 1.50; // var: {"min": 0.5, "max": 3.0, "step": 0.1, "name": "Ornament Clamp", "desc": "Maximum brightness clamp for ornaments"}
const float R_MIN = 0.17; // var: {"min": 0.05, "max": 0.5, "step": 0.01, "name": "Radius Min", "desc": "Minimum base radius of cells"}
const float R_VAR = 0.19; // var: {"min": 0.0, "max": 0.5, "step": 0.01, "name": "Radius Var", "desc": "Variation range of cell radius"}
const float PORE_MIN = 6.0; // var: {"min": 1.0, "max": 20.0, "step": 1.0, "name": "Pore Min", "desc": "Minimum number of pores/cells in pattern"}
const float PORE_VAR = 13.0; // var: {"min": 0.0, "max": 30.0, "step": 1.0, "name": "Pore Var", "desc": "Variation range of pore count"}
const float PUNCTA = 0.28; // var: {"min": 0.0, "max": 1.0, "step": 0.05, "name": "Puncta", "desc": "Strength of secondary puncta pattern"}
const float TAU = 6.28318530718;
const float PI = 3.14159265359;

float h21(vec2 p){ p=fract(p*vec2(123.34,456.21)); p+=dot(p,p+45.32); return fract(p.x*p.y); }
mat2 rot(float a){ float c=cos(a),s=sin(a); return mat2(c,-s,s,c); }

vec3 irid(float t, float b){
    vec3 c = 0.5+0.5*cos(TAU*(t+vec3(0.0,0.10,0.21)));
    return mix(vec3(0.72,0.86,1.00), c, b);
}

float grid(vec2 g, float w){ vec2 f=fract(g)-0.5; return smoothstep(w,0.0,abs(length(f)-0.30)); }
float hexg(vec2 g, float w){ g.x += 0.5*floor(g.y); return grid(g,w); }
float lineAt(float x, float w){ return smoothstep(w,0.0,abs(x)); }
float stripe(float x, float w){ return smoothstep(w,0.0,abs(abs(fract(x)-0.5)-0.5)); }

float sdEll(vec2 p, vec2 r){ return (length(p/r)-1.0)*min(r.x,r.y); }

float sdPoly(vec2 p, float r, float n){
    float an = PI/n;
    vec2 acs = vec2(cos(an),sin(an));
    float bn = mod(atan(p.x,p.y), 2.0*an)-an;
    p = length(p)*vec2(cos(bn), abs(sin(bn)));
    p -= r*acs;
    p.y += clamp(-p.y, 0.0, r*acs.y);
    return length(p)*sign(p.x);
}
float sdStar(vec2 p, float r, float n, float m){
    float an = PI/n, en = PI/m;
    vec2 acs = vec2(cos(an),sin(an)), ecs = vec2(cos(en),sin(en));
    float bn = mod(atan(p.x,p.y), 2.0*an)-an;
    p = length(p)*vec2(cos(bn), abs(sin(bn)));
    p -= r*acs;
    p += ecs*clamp(-dot(p,ecs), 0.0, r*acs.y/ecs.y);
    return length(p)*sign(p.x);
}
float sdPie(vec2 p, vec2 c, float r){
    p.x = abs(p.x);
    float l = length(p)-r;
    float m = length(p-c*clamp(dot(p,c),0.0,r));
    return max(l, m*sign(c.y*p.x-c.x*p.y));
}

float setae(vec2 q, float R, float n, float len, float t, float ph, float px){
    float r = length(q);
    if(r < R*0.75 || r > R+len) return 0.0;
    float a  = atan(q.y+1e-7, q.x+1e-7);
    float an = TAU/n;
    float k  = floor(a/an + 0.5);
    float s  = clamp((r-R)/len, 0.0, 1.0);
    float bend = SETAE_BEND*s*sin(t*SETAE_SPEED + k*2.4 + ph*3.1 + s*2.4);
    float da = a - k*an - bend;
    da = atan(sin(da), cos(da));
    float d = abs(da)*r;
    float th = mix(0.008, 0.0010, s);
    return smoothstep(th+2.5*px, th*0.25, d)*smoothstep(R*0.80,R,r)*smoothstep(1.0,0.72,s)*SETAE_STR;
}

vec4 shell(vec2 q, float sd, float hue, float px, float t){
    float rr = h21(vec2(sd,11.3));
    float R  = R_MIN + R_VAR*h21(vec2(sd,5.1));
    float d  = 1e5, orn = 0.0, extra = 0.0;

    int ty = 11;
    if(rr<0.14) ty=0; else if(rr<0.21) ty=1; else if(rr<0.27) ty=2;
    else if(rr<0.34) ty=3; else if(rr<0.50) ty=4; else if(rr<0.57) ty=5;
    else if(rr<0.65) ty=6; else if(rr<0.73) ty=7; else if(rr<0.80) ty=8;
    else if(rr<0.87) ty=9; else if(rr<0.94) ty=10;

    if(ty<=3){
        d = length(q)-R;
        float ang = atan(q.y+1e-7, q.x+1e-7);
        float rad = length(q)/R;
        float n = PORE_MIN+floor(PORE_VAR*h21(vec2(sd,3.3)));
        float cell = R/n;
        float pg = px/cell;
        float w = max(pg*1.5, 0.055);
        float fade = smoothstep(0.62, 0.16, pg);
        float wl = px/R*1.7;
        float sel = h21(vec2(sd,21.7));
        float m;
        if(sel<0.45) m = hexg(q/cell, w);
        else if(sel<0.78){
            float ri = max(1.0, floor(rad*n));
            m = grid(vec2(ang/TAU*floor(6.0*ri), rad*n), w)*smoothstep(0.05,0.16,rad);
        } else m = grid(q/cell, w);
        orn  = m*fade;
        orn += hexg(q/(cell*0.34), max(pg*4.2,0.12))*smoothstep(0.34,0.09,pg)*PUNCTA;
        orn += lineAt(rad-0.93, wl)*0.55;
        float nt = 30.0+floor(40.0*h21(vec2(sd,61.0)));
        orn += stripe(ang/TAU*nt, max(px*nt/(TAU*R),0.05))
             * smoothstep(0.93,0.955,rad)*smoothstep(1.0,0.965,rad)*0.85;

        if(ty==1){
            float sec = floor(fract(ang/TAU)*6.0);
            orn *= mix(0.30,1.0,mod(sec,2.0));
            orn += stripe(ang/TAU*6.0, max(px*6.0/(TAU*R*max(rad,0.10)),0.02))*smoothstep(0.05,0.25,rad)*0.7;
        }
        if(ty==2){
            float nc = 14.0+floor(14.0*h21(vec2(sd,55.0)));
            float nb = 6.0+floor(8.0*h21(vec2(sd,57.0)));
            orn *= 0.45;
            orn += stripe(ang/TAU*nc, max(px*nc/(TAU*R*max(rad,0.08)),0.02))*smoothstep(0.06,0.20,rad)*0.85;
            orn += stripe(rad*nb, max(px*nb/R,0.02))*0.70;
        }
        if(ty==3){
            orn *= smoothstep(0.40,0.56,rad);
            orn += lineAt(rad-0.50, wl)*0.5;
            float ns = 34.0+floor(30.0*h21(vec2(sd,63.0)));
            orn += stripe(ang/TAU*ns, max(px*ns/(TAU*R*max(rad,0.5)),0.03))
                 * smoothstep(0.60,0.70,rad)*smoothstep(1.0,0.92,rad)*0.9;
        }
        if(ty==0){
            float np = 6.0+floor(4.0*h21(vec2(sd,67.0)));
            orn += lineAt(length(q)-R*0.10*(1.0+0.35*cos(ang*np)), px*1.9)*0.8;
            float na = 3.0+floor(3.0*h21(vec2(sd,71.0)));
            float a2 = mod(ang+PI/na, TAU/na)-PI/na;
            vec2 pp = length(q)*vec2(cos(a2),sin(a2)) - vec2(R*0.86,0.0);
            orn += smoothstep(px*2.0+R*0.026,0.0,length(pp))*0.9;
        }
        if(h21(vec2(sd,73.0))>0.74)
            extra = setae(q, R, 3.0+floor(3.0*h21(vec2(sd,77.0))), R*2.4, t, sd, px);
    }
    else if(ty==4 || ty==5){
        vec2 qq = q;
        float e = 0.07+0.30*h21(vec2(sd,8.9));
        if(ty==5) qq.y -= R*0.26*sin(PI*clamp(qq.x/R,-1.0,1.0));
        d = sdEll(qq, vec2(R, R*e));
        float ns = 16.0+floor(34.0*h21(vec2(sd,4.4)));
        float cell = R*2.0/ns;
        float pg = px/cell;
        float w = max(pg*1.5,0.06);
        float ry = abs(qq.y)/(R*e+1e-5);
        float m = (h21(vec2(sd,79.0))>0.5) ? stripe(qq.x/cell, w*0.9) : hexg(qq/cell, w);
        orn  = m*smoothstep(0.62,0.18,pg)*smoothstep(0.12,0.34,ry)*smoothstep(1.06,0.90,ry);
        orn += lineAt(qq.y, px*1.4)*smoothstep(0.10,0.26,abs(qq.x)/R)*smoothstep(0.99,0.88,abs(qq.x)/R)*0.75;
        orn += lineAt(length(qq)-R*0.055, px*1.7)*0.8;
    }
    else if(ty==6){
        float gap = R*0.42;
        float cnt = 1.0+floor(3.0*h21(vec2(sd,41.0)));
        float ix  = clamp(floor(q.x/gap+0.5), -cnt, cnt);
        vec2 qc = q - vec2(ix*gap, 0.0);
        d = sdEll(qc, vec2(gap*0.46, R*0.80));
        float ns = 12.0+floor(18.0*h21(vec2(sd,43.0)));
        float cell = R*1.6/ns;
        float pg = px/cell;
        orn  = stripe(qc.y/cell, max(pg*1.4,0.07))*smoothstep(0.60,0.18,pg)
             * smoothstep(0.04,0.28,1.0-abs(qc.x)/(gap*0.46));
        orn += lineAt(qc.x, px*1.4)*0.40;
    }
    else if(ty==7){
        float nn = 3.0+floor(2.0*h21(vec2(sd,17.0)));
        d = sdPoly(q+1e-5, R*0.86, nn) - R*0.12;
        float n = PORE_MIN+1.0+floor(10.0*h21(vec2(sd,3.3)));
        float cell = R/n;
        float pg = px/cell;
        orn  = hexg(q/cell, max(pg*1.5,0.06))*smoothstep(0.62,0.16,pg);
        orn += lineAt(d+R*0.10, px*1.7)*0.60;
        orn += lineAt(length(q)-R*0.16, px*1.7)*0.50;
    }
    else if(ty==8){
        vec2 r = vec2(R, R*(0.40+0.20*h21(vec2(sd,19.0))));
        d = sdEll(q, r);
        d = min(d, length(vec2(abs(q.x)-R*0.82, q.y)) - R*0.15);
        float n = PORE_MIN+2.0+floor(10.0*h21(vec2(sd,3.3)));
        float cell = R/n;
        float pg = px/cell;
        orn  = hexg(q/cell, max(pg*1.5,0.06))*smoothstep(0.62,0.16,pg);
        orn += lineAt(sdEll(q,r*0.86), px*1.6)*0.5;
    }
    else if(ty==9){
        float nn = 5.0+floor(4.0*h21(vec2(sd,23.0)));
        d = sdStar(q+1e-5, R, nn, 2.3+0.9*h21(vec2(sd,29.0))) - R*0.035;
        orn  = stripe(length(q)/R*10.0, max(px*10.0/R,0.03))*0.45;
        orn += lineAt(length(q)-R*0.12, px*1.7)*0.60;
    }
    else if(ty==10){
        float Rs  = R*0.42;
        float gap = Rs*2.0;
        float ix  = clamp(floor(q.x/gap+0.5), -1.0, 1.0);
        vec2 qc = q - vec2(ix*gap, 0.0);
        d = sdEll(qc, vec2(Rs*0.88, Rs*0.55));
        extra = setae(qc, Rs*0.88, 4.0, Rs*7.0, t, sd+ix*2.7, px);
        float cell = Rs*0.16;
        float pg = px/cell;
        orn = hexg(qc/cell, max(pg*1.5,0.08))*smoothstep(0.60,0.18,pg)*0.6;
    }
    else {
        float ap = 0.26+0.14*h21(vec2(sd,31.0));
        vec2 base = q + vec2(R*0.85,0.0);
        vec2 pw = rot(PI*0.5)*base;
        d = sdPie(pw, vec2(sin(ap),cos(ap)), R*1.7) - R*0.03;
        float rr2 = length(base);
        float aw  = atan(pw.x+1e-7, pw.y+1e-7);
        float nsr = 18.0+floor(20.0*h21(vec2(sd,33.0)));
        orn  = stripe(aw/TAU*nsr, max(px*nsr/(TAU*max(rr2,0.05)),0.02))*smoothstep(0.08*R,0.32*R,rr2)*0.80;
        orn += stripe(rr2/R*7.0, max(px*7.0/R,0.03))*0.35;
    }

    orn = min(orn, ORN_CLAMP);
    float da = abs(d);
    float inside = smoothstep(px,-px,d);
    float rim  = smoothstep(RIM_W*px, 0.0, da)*RIM_STR;
    float halo = HALO_STR*exp(-da/(px*HALO_W));
    float em = rim + halo + orn*inside*ORN_STR + inside*FILL_STR + extra;
    vec3  c  = irid(hue + IRID_EDGE*da + IRID_DRIFT*t, IRID_AMT)*em;
    float a  = clamp(inside*ALPHA_STR + clamp(extra,0.0,1.0)*0.35, 0.0, 1.0);
    return vec4(c, a);
}

vec3 render(vec2 uv, float px, float t){
    vec3 col = BG;
    float focus = FOCUS_BASE + FOCUS_AMP*sin(t*FOCUS_SPEED);
    for(int L=LAYERS-1; L>=0; L--){
        float fl = float(L);
        float sc = pow(LAYER_SCALE, fl);
        float fo = abs(fl-focus);
        float blur = min(1.0 + BLUR_GAIN*fo*fo, BLUR_MAX);
        float dim  = 1.0/(1.0 + (blur-1.0)*DEFOCUS_DIM);
        vec2 p  = uv*sc + DRIFT*t;
        vec2 id = floor(p);
        vec2 f  = fract(p)-0.5;
        for(int j=-1;j<=1;j++)
        for(int i=-1;i<=1;i++){
            vec2 o   = vec2(float(i),float(j));
            vec2 cid = id+o+fl*67.0;
            float k = h21(cid*1.31);
            if(k>CELL_FILL) continue;
            float ha = h21(cid+3.1), hb = h21(cid+7.7), hc = h21(cid+13.3);
            vec2 jit = (vec2(hc,ha)-0.5)*JITTER;
            jit += WOBBLE*vec2(sin(t*0.47+ha*9.0), cos(t*0.41+hb*7.0));
            vec2 q = rot(hb*TAU + t*(ha-0.5)*SPIN) * (f-o-jit);
            q /= 1.0+BREATH*sin(t*0.9+hc*6.0);
            vec4 s = shell(q, k*97.0+fl*11.0, hc+0.15*fl, px*sc*blur, t+ha*30.0);
            col = col*(1.0-s.a*dim) + s.rgb*dim;
        }
    }
    return col;
}

void mainImage(out vec4 O, in vec2 F){
    float t = iTime;
    vec3 acc = vec3(0.0);
    float px = ZOOM/iResolution.y/float(AA);
    for(int m=0;m<AA;m++)
    for(int n=0;n<AA;n++){
        vec2 o = (vec2(float(m),float(n))+0.5)/float(AA)-0.5;
        acc += render((F+o-0.5*iResolution.xy)/iResolution.y*ZOOM, px, t);
    }
    vec3 col = acc/float(AA*AA)*EXPOSURE;
    vec2 v = (F-0.5*iResolution.xy)/iResolution.xy;
    col *= 1.0-VIGNETTE*dot(v,v);
    O = vec4(pow(col, vec3(GAMMA)), 1.0);
}