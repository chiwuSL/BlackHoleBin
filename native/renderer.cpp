#include <windows.h>
#include <d3d11.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <cstring>
#include "shader.h"
using Microsoft::WRL::ComPtr;
struct Renderer {
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> ctx;
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11Buffer> constants;
    ComPtr<ID3D11SamplerState> sampler;
    ComPtr<ID3D11Texture2D> target, staging, bg;
    ComPtr<ID3D11RenderTargetView> rtv;
    ComPtr<ID3D11ShaderResourceView> srv;
    int size=0;
    HRESULT Init() {
        HRESULT hr=D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&device,nullptr,&ctx);
        if(FAILED(hr))return hr;
        ComPtr<ID3DBlob> code, error;
        hr=D3DCompile(shaderSource,sizeof(shaderSource)-1,nullptr,nullptr,nullptr,"VS","vs_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&code,&error);
        if(FAILED(hr))return hr;
        hr=device->CreateVertexShader(code->GetBufferPointer(),code->GetBufferSize(),nullptr,&vs); if(FAILED(hr))return hr;
        code.Reset(); error.Reset();
        hr=D3DCompile(shaderSource,sizeof(shaderSource)-1,nullptr,nullptr,nullptr,"PS","ps_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&code,&error);
        if(FAILED(hr))return hr;
        hr=device->CreatePixelShader(code->GetBufferPointer(),code->GetBufferSize(),nullptr,&ps); if(FAILED(hr))return hr;
        D3D11_BUFFER_DESC b={}; b.ByteWidth=16;b.Usage=D3D11_USAGE_DEFAULT;b.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        hr=device->CreateBuffer(&b,nullptr,&constants);if(FAILED(hr))return hr;
        D3D11_SAMPLER_DESC s={};s.Filter=D3D11_FILTER_MIN_MAG_MIP_LINEAR;s.AddressU=s.AddressV=s.AddressW=D3D11_TEXTURE_ADDRESS_CLAMP;s.MaxLOD=D3D11_FLOAT32_MAX;
        return device->CreateSamplerState(&s,&sampler);
    }
    HRESULT Resize(int n) {
        ctx->OMSetRenderTargets(0,nullptr,nullptr);
        ID3D11ShaderResourceView* empty=nullptr;ctx->PSSetShaderResources(0,1,&empty);
        target.Reset();staging.Reset();bg.Reset();rtv.Reset();srv.Reset();size=0;
        D3D11_TEXTURE2D_DESC d={};d.Width=d.Height=n;d.MipLevels=d.ArraySize=1;d.Format=DXGI_FORMAT_B8G8R8A8_UNORM;d.SampleDesc.Count=1;d.BindFlags=D3D11_BIND_RENDER_TARGET;
        HRESULT hr=device->CreateTexture2D(&d,nullptr,&target);if(FAILED(hr))return hr;
        hr=device->CreateRenderTargetView(target.Get(),nullptr,&rtv);if(FAILED(hr))return hr;
        d.BindFlags=D3D11_BIND_SHADER_RESOURCE;
        hr=device->CreateTexture2D(&d,nullptr,&bg);if(FAILED(hr))return hr;
        hr=device->CreateShaderResourceView(bg.Get(),nullptr,&srv);if(FAILED(hr))return hr;
        d.BindFlags=0;d.Usage=D3D11_USAGE_STAGING;d.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
        hr=device->CreateTexture2D(&d,nullptr,&staging);if(SUCCEEDED(hr))size=n;return hr;
    }
};
extern "C" __declspec(dllexport) void* __cdecl BHCreate() { auto r=new Renderer(); if(FAILED(r->Init())){delete r;return nullptr;}return r; }
extern "C" __declspec(dllexport) void __cdecl BHDestroy(Renderer* r) { delete r; }
extern "C" __declspec(dllexport) int __cdecl BHRender(Renderer* r,int n,const void* bg,int bgStride,void* output,int outStride,float time,float strength,float activity,float theme) {
    if(!r || !bg || !output || n<1 || n>1024 || bgStride<n*4 || outStride<n*4)return E_INVALIDARG;
    if(r->size!=n){HRESULT hr=r->Resize(n);if(FAILED(hr))return hr;}
    r->ctx->UpdateSubresource(r->bg.Get(),0,nullptr,bg,bgStride,0);
    float values[]={time,strength,activity,theme};r->ctx->UpdateSubresource(r->constants.Get(),0,nullptr,values,0,0);
    D3D11_VIEWPORT vp={0,0,(float)n,(float)n,0,1};r->ctx->RSSetViewports(1,&vp);
    auto target=r->rtv.Get();r->ctx->OMSetRenderTargets(1,&target,nullptr);
    auto cb=r->constants.Get();auto srv=r->srv.Get();auto sampler=r->sampler.Get();
    r->ctx->VSSetShader(r->vs.Get(),nullptr,0);r->ctx->PSSetShader(r->ps.Get(),nullptr,0);
    r->ctx->PSSetConstantBuffers(0,1,&cb);r->ctx->PSSetShaderResources(0,1,&srv);r->ctx->PSSetSamplers(0,1,&sampler);
    r->ctx->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);r->ctx->Draw(3,0);
    r->ctx->CopyResource(r->staging.Get(),r->target.Get());
    D3D11_MAPPED_SUBRESOURCE mapped={};HRESULT hr=r->ctx->Map(r->staging.Get(),0,D3D11_MAP_READ,0,&mapped);if(FAILED(hr))return hr;
    for(int y=0;y<n;y++)memcpy((char*)output+y*outStride,(char*)mapped.pData+y*mapped.RowPitch,n*4);
    r->ctx->Unmap(r->staging.Get(),0);return S_OK;
}
