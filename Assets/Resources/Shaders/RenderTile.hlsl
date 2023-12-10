#ifndef UNIVERSAL_RVT_TILE_INCLUDED
#define UNIVERSAL_RVT_TILE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"


TEXTURE2D(_Control);
SAMPLER(sampler_Control);
TEXTURE2D(_Diffuse0);
SAMPLER(sampler_Diffuse0);
TEXTURE2D(_Diffuse1);
TEXTURE2D(_Diffuse2);
TEXTURE2D(_Diffuse3);
TEXTURE2D(_Normal0);
SAMPLER(sampler_Normal0);
TEXTURE2D(_Normal1);
TEXTURE2D(_Normal2);
TEXTURE2D(_Normal3);

#if defined(TARGET45)
    struct PhysicalTextureParam
    {
        float4 TransformUV;
        float4x4 Matrix_MVP;
    };

    StructuredBuffer<PhysicalTextureParam> _PhysicalParamBuffer;
#else
UNITY_INSTANCING_BUFFER_START(PageTableProperty)
UNITY_DEFINE_INSTANCED_PROP(float4, _TransformUV)
UNITY_DEFINE_INSTANCED_PROP(float4x4, _ImageMVP)
UNITY_INSTANCING_BUFFER_END(PageTableProperty)
#endif

float4 _Smoothness;

struct Attributes
{
    float4 positionOS : POSITION;
    float2 texcoord : TEXCOORD0;
    #if defined(TARGET45)
        uint InstanceId : SV_InstanceID;
    #else
    UNITY_VERTEX_INPUT_INSTANCE_ID
    #endif
};

struct Varyings
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    #if defined(TARGET45)
        uint InstanceId : SV_InstanceID;
    #else
    UNITY_VERTEX_INPUT_INSTANCE_ID
    #endif
};

Varyings RenderTileVertex(Attributes v)
{
    Varyings o;
    o.uv = v.texcoord;

    #if defined(TARGET45)
        float4x4 mat = _PhysicalParamBuffer[v.InstanceId].Matrix_MVP;
        o.InstanceId = v.InstanceId;
    #else
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    float4x4 mat = UNITY_MATRIX_M;
    mat = UNITY_ACCESS_INSTANCED_PROP(PageTableProperty, _ImageMVP);
    #endif

    o.pos = mul(mat, v.positionOS);
    o.pos.z = 0;

    return o;
}

void RenderTileFragment(Varyings IN, out half4 ColorBuffer : SV_Target0, out half4 NormalBuffer : SV_Target1)
{
    #if defined(TARGET45)
        float4 transformUV = _PhysicalParamBuffer[IN.InstanceId].TransformUV;
    #else
    UNITY_SETUP_INSTANCE_ID(IN);
    float4 transformUV = UNITY_ACCESS_INSTANCED_PROP(PageTableProperty, _TransformUV);
    #endif

    float2 controlUV = IN.uv * transformUV.z + transformUV.xy;

    float4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control, controlUV);

    half weight = dot(control, 1.0h);
    #ifndef _TERRAIN_BASEMAP_GEN
    // Normalize weights before lighting and restore weights in final modifier functions so that the overal
    // lighting result can be correctly weighted.
    control /= (weight + HALF_MIN);
    #endif

    // float2 uv = frac(controlUV * transformUV.w);
    float2 uv = frac(controlUV * transformUV.w);

    half4 diffuse0 = _Diffuse0.Sample(sampler_Diffuse0, uv);
    half4 diffuse1 = _Diffuse1.Sample(sampler_Diffuse0, uv);
    half4 diffuse2 = _Diffuse2.Sample(sampler_Diffuse0, uv);
    half4 diffuse3 = _Diffuse3.Sample(sampler_Diffuse0, uv);

    half4 defaultSmoothness = half4(diffuse0.a, diffuse1.a, diffuse2.a, diffuse3.a);
    defaultSmoothness *= _Smoothness;

    /*
    half _NormalScale0 = 1, _NormalScale1 = 1, _NormalScale2 = 1, _NormalScale3 = 1;
    half3 normal0 = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal0, sampler_Normal0, uv), _NormalScale0);
    half3 normal1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal1, sampler_Normal0, uv), _NormalScale1);
    half3 normal2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal2, sampler_Normal0, uv), _NormalScale2);
    half3 normal3 = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal3, sampler_Normal0, uv), _NormalScale3);
    */

    half4 normal0 = _Normal0.Sample(sampler_Normal0, uv);
    half4 normal1 = _Normal1.Sample(sampler_Normal0, uv);
    half4 normal2 = _Normal2.Sample(sampler_Normal0, uv);
    half4 normal3 = _Normal3.Sample(sampler_Normal0, uv);
    ColorBuffer = diffuse0 * control.r + diffuse1 * control.g + diffuse2 * control.b + diffuse3 * control.a;

    ColorBuffer.a = dot(control, defaultSmoothness);

    half smoothness = dot(control, defaultSmoothness);

    half4 nrm = normal0 * control.r + normal1 * control.g + normal2 * control.b + normal3 * control.a;
    // avoid risk of NaN when normalizing

    // Note: !!! When use the DXT5nm Format, We only check the nrm.g nrm.a of the nrm.
    NormalBuffer = half4(nrm.g, nrm.a, smoothness, 1.0h);
    // NormalBuffer = half4(nrm.g, nrm.b, nrm.a, smoothness);
}
#endif
