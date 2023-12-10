#ifndef UNIVERSAL_RVT_PAGETABLE_INCLUDED
#define UNIVERSAL_RVT_PAGETABLE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


struct Attributes
{
    float4 positionOS : POSITION;
    half2 texcoord : TEXCOORD0;
    #if defined(TARGET45)
    uint InstanceId : SV_InstanceID;
    #else
    UNITY_VERTEX_INPUT_INSTANCE_ID
    #endif
};

struct Varyings
{
    float4 pos : SV_POSITION;
    float4 info : TEXCOORD0;
};

#if defined(TARGET45)
struct PageTableParams
{
    float4 pageInfo;
    float4x4 matrix_MVP;
};

StructuredBuffer<PageTableParams> _PageParamBuffer;
#else
UNITY_INSTANCING_BUFFER_START(PageTableProperty)
UNITY_DEFINE_INSTANCED_PROP(float4, _PageInfo)
UNITY_DEFINE_INSTANCED_PROP(float4x4, _ImageMVP)
UNITY_INSTANCING_BUFFER_END(PageTableProperty)
#endif
/**/
Varyings RenderPageTableVertex(Attributes v)
{
    Varyings o;

    #if defined(TARGET45)
    float4x4 mat = _PageParamBuffer[v.InstanceId].matrix_MVP;
    o.info = _PageParamBuffer[v.InstanceId].pageInfo;
    o.pos = mul(mat, v.positionOS);    
    #else
    UNITY_SETUP_INSTANCE_ID(v);
    float4x4 mat = UNITY_MATRIX_M;
    mat = UNITY_ACCESS_INSTANCED_PROP(PageTableProperty, _ImageMVP);
    o.info = UNITY_ACCESS_INSTANCED_PROP(PageTableProperty, _PageInfo);
    o.pos = mul(mat, v.positionOS);
    #endif

    return o;
}

float4 RenderPageTableFragment(Varyings IN) : SV_TARGET
{
    return IN.info;
}
#endif
