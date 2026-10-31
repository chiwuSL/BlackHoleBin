// HLSL adaptation of GreenScreen410/singularity src/singularity.wgsl.
// Upstream commit d5703c233ce3481915784b321454e5f572cccfa1 (MIT).
// Upstream credits s0xDk/ghostty-blackhole (MIT) and Eric Bruneton's renderer.
// See THIRD-PARTY-NOTICES.md and licenses/ for attribution and permissions.
// Schwarzschild path only. UI placement, roll and theme adapted for BlackHoleBin.
cbuffer State : register(b0) { float time; float strength; float activity; float theme; };
Texture2D desktop : register(t0);
SamplerState linearClamp : register(s0);
struct Vertex { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
Vertex VS(uint id : SV_VertexID) {
    Vertex v; v.uv=float2((id<<1)&2,id&2);
    v.pos=float4(v.uv*float2(2,-2)+float2(-1,1),0,1);return v;
}
float2 rot(float2 v,float a) { float c=cos(a),s=sin(a);return float2(c*v.x-s*v.y,s*v.x+c*v.y); }
float gmod(float x,float y) {return x-y*floor(x/y);}
float hash21(float2 pin) {float2 p=frac(pin*float2(234.34,435.345));p+=dot(p,p+34.23);return frac(p.x*p.y);}
float noiseWrap(float2 p,float period) {
    float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
    float y0=gmod(i.y,period),y1=gmod(i.y+1,period);
    return lerp(lerp(hash21(float2(i.x,y0)),hash21(float2(i.x+1,y0)),f.x),lerp(hash21(float2(i.x,y1)),hash21(float2(i.x+1,y1)),f.x),f.y);
}
float3 blackbody(float T) {
    float t=clamp(T,1500,40000)/100;
    float r=t>66?saturate(1.292936*pow(max(t-60,.001),-.1332047)):1;
    float g=t<=66?saturate(.3900816*log(t)-.6318414):saturate(1.1298909*pow(max(t-60,.001),-.0755148));
    float b=t>=66?1:(t<=19?0:saturate(.5432068*log(t-10)-1.1962540));
    return float3(r,g,b);
}
float3 sampleSky(float2 uv) {
    float2 m=uv-2*floor(uv/2);
    return desktop.SampleLevel(linearClamp,1-abs(1-m),0).rgb;
}
float4 crossing(float3 xc,float3 dir,float3 n,float3 e2,float t,float trans) {
    const float rin=2.2,rout=7;
    float rc=length(xc);if(rc<=rin || rc>=rout)return 0;
    float band=smoothstep(rin,rin*1.25,rc)*(1-smoothstep(rout*.7,rout,rc));
    float phi=atan2(dot(xc,e2),xc.x),turns=phi/6.2831853;
    float kep=pow(rin/rc,1.5),gloc=sqrt(max(1-1.5/rc,.02));
    float swirl=rc*7*.12-t*kep*5*gloc;
    float streaks=noiseWrap(float2(rc*2.8,turns*19+swirl*3),19)*.65
                 +noiseWrap(float2(rc,turns*9+swirl*1.5+7),9)*.35;
    streaks=.35+.5*streaks*streaks;
    float3 gasdir=normalize(cross(n,xc));
    float beta=clamp(rsqrt(max(2*(rc-1),.2)),0,.99);
    float g=lerp(1,gloc/max(1+beta*dot(gasdir,dir),.05),.35);
    float tprof=pow(rin/rc,.75)*pow(max(1-sqrt(rin/rc),0),.25)/.488;
    float3 color=blackbody(4500*tprof*g);
    if(theme<.5)color=lerp(color,float3(.64,.80,1),.65);
    else if(theme<1.5)color=lerp(color,float3(.88,.60,1),.60);
    float density=band*streaks;
    float gain=1.4*strength/.7*(1+activity*.15);
    return float4(trans*color*(gain*2.2*density*tprof*tprof*g*g),density);
}
float4 trace(float2 uv) {
    const float rh=.12,B_CRIT=2.5980762,depth=13,Z0=14,roll=-.26;
    const float W=B_CRIT/rh;
    float2 p=uv-.5;
    float plen=length(p),window=exp(-pow(plen/(7*rh),2));
    // Extra local fade matches the finite widget footprint without clipping a distorted edge.
    float footprint=1-smoothstep(.42,.495,plen);
    float2 pr=rot(float2(p.x,-p.y),roll)*W;
    float b=length(pr);
    if(b>=10) {
        float uu=Z0*rsqrt(Z0*Z0+b*b);
        float defl=(2/(W*W))/max(plen,.0001)*(1.29*uu+.07)*max(depth-2.14*uu+.75,0)*window;
        float2 dir=p/max(plen,.00001);
        return float4(sampleSky(.5+p-dir*defl*footprint),1);
    }
    float3 x=float3(pr.x,pr.y,Z0),v=float3(0,0,-1);
    float h2=dot(pr,pr);
    float ci=cos(1.52),si=sin(1.52);
    float3 n=float3(0,si,ci),e2=float3(0,ci,-si);
    float3 emission=0;float trans=1;bool captured=false;
    float sPrev=dot(x,n);float3 xPrev=x;
    [loop] for(int i=0;i<48;i++) {
        float r2=dot(x,x);
        if(r2<1){captured=true;break;}
        if((x.z < -Z0 && v.z<0) || r2>4*Z0*Z0)break;
        float r=sqrt(r2),dt=clamp(.16*r,.03,1.5);
        float3 acc=-1.5*h2*x/(r2*r2*r);
        v+=acc*(.5*dt);x+=v*dt;
        r2=max(dot(x,x),.00001);r=sqrt(r2);
        acc=-1.5*h2*x/(r2*r2*r);v+=acc*(.5*dt);
        float s=dot(x,n);
        if(s*sPrev<0 && trans>.02) {
            float tc=sPrev/(sPrev-s);
            float4 sh=crossing(lerp(xPrev,x,tc),normalize(v),n,e2,time,trans);
            emission+=sh.rgb;trans*=1-saturate(.85*sh.a);
        }
        sPrev=s;xPrev=x;
    }
    if(!captured && dot(x,x)<4)captured=true;
    float3 bg=0;
    if(!captured) {
        float3 d=normalize(v);
        if(d.z<-.05) {
            float tpl=(-depth-x.z)/d.z;
            float3 hp=x+d*tpl;
            float2 q=rot(hp.xy,-roll)/W;
            float2 sp=float2(q.x,-q.y);
            bg=sampleSky(.5+p+(sp-p)*window*footprint)*smoothstep(.05,.35,-d.z);
        }
    }
    float3 col=bg*trans+(1-exp(-emission*1.2));
    return float4(lerp(sampleSky(uv),col,footprint),1);
}
float4 PS(Vertex input):SV_TARGET {
    // 2x2 supersampling stabilizes the thin high-order disk images at desktop sizes.
    uint w,h;desktop.GetDimensions(w,h);
    float2 d=.25/float2(w,h);
    return (trace(input.uv+d)+trace(input.uv-d)+trace(input.uv+float2(d.x,-d.y))+trace(input.uv+float2(-d.x,d.y)))*.25;
}
