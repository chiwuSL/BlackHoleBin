cbuffer State : register(b0) { float time; float strength; float activity; float theme; };
Texture2D background : register(t0);
SamplerState linearClamp : register(s0);
struct Vertex { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
Vertex VS(uint id : SV_VertexID) {
    Vertex v; v.uv = float2((id << 1) & 2, id & 2);
    v.pos = float4(v.uv * float2(2,-2) + float2(-1,1), 0, 1); return v;
}
float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float noise(float2 p) {
    float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
}
float filament(float radius,float angle) {
    // Low-amplitude advection keeps long connected strands instead of granular clumps.
    float drift=time*(.24+.08/max(radius,.1));
    float a=angle-drift;
    uint width,height; background.GetDimensions(width,height);
    float frequency=min(510.0,(float)width*1.05);
    float phase=radius*frequency + sin(a*4+radius*28)*.75 + sin(a*9-radius*17)*.3;
    float strand=pow(.5+.5*sin(phase),3);
    return lerp(strand,.42,saturate(fwidth(phase)*.045));
}
float3 plasma(float v) {
    float3 hot=lerp(float3(.88,.64,.25),float3(1,.94,.65),saturate(v));
    if(theme<.5) hot=lerp(float3(.25,.48,1),float3(.85,.95,1),saturate(v));
    else if(theme<1.5) hot=lerp(float3(.6,.18,.85),float3(1,.85,1),saturate(v));
    return hot;
}
float4 PS(Vertex input) : SV_TARGET {
    float2 p=(input.uv-.5)*2;
    float r=length(p), edge=1-smoothstep(.74,.99,r);
    float lens=exp(-pow((r-.46)/.18,2))*.10*edge*strength;
    float2 uv=input.uv - p/max(r,.001)*lens;
    float3 col=background.Sample(linearClamp,uv).rgb;
    // Screen Y increases downward: this basis rises from bottom-left to top-right.
    float2 q=float2(p.x*.966-p.y*.259,p.x*.259+p.y*.966);
    float horizon=.263;
    float aa=max(fwidth(r),.001);
    float shadow=1-smoothstep(horizon-aa,horizon+aa,r);
    col*=1-shadow;
    float angle=atan2(q.y,q.x);
    // Lensed rear disk: dense asymmetric filaments around the silhouette.
    float ringRadius=.332 + .0015*sin(angle*2);
    float ring=smoothstep(.269,.282,r)*(1-smoothstep(.385,.414,r));
    float detail=filament(r,angle);
    float beaming=.88+.12*cos(angle-.5);
    float envelope=exp(-pow((r-ringRadius)/.073,4));
    float3 ringColor=plasma(.35+detail*.6)*(.57+.43*detail)*beaming;
    float photon=exp(-abs(r-.269)/max(aa,.002))* .75;
    float ringOpacity=ring*envelope*(.76+.22*detail)*strength;
    col=lerp(col,ringColor,saturate(ringOpacity));
    col=lerp(col,plasma(1),photon*strength);
    // Near side crosses the horizon as a tapered hot sheet, not a closed ellipse.
    float x=abs(q.x), taper=saturate(1-x/.92);
    float center=.004*sin(q.x*4);
    float join=exp(-pow((x-.32)/.14,2));
    float thickness=.0025+.023*join+.003*taper;
    float distance=abs(q.y-center);
    float disk=exp(-pow(distance/max(thickness,fwidth(q.y)),2))*smoothstep(0,.14,taper);
    float d=filament(distance*.9+.30,q.x*4);
    float3 emit=plasma(.7+d*.3)*(.84+.16*d);
    col=lerp(col,emit,saturate(disk*strength*(1+activity*.2)));
    // Tone-map only emitted light by compressing the final highlights gently.
    col=min(col,1);
    return float4(col,1);
}
