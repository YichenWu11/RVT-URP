#ifndef UNIVERSAL_RVT_TILE_SINGLE_INCLUDED
#define UNIVERSAL_RVT_TILE_SINGLE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


TEXTURE2D(_OriginHeightMap);
SAMPLER(sampler_OriginHeightMap);
float _HeightMapResolution;

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

float4 _TransformUV;
float4 _Smoothness;

struct Attributes
{
    float4 positionOS : POSITION;
    float2 texcoord : TEXCOORD0;
};

struct Varyings
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
};

float4x4 mat_identity;

Varyings RenderTileVertex(Attributes v)
{
    Varyings o;
    o.uv = v.texcoord;

    o.pos = mul(mat_identity, v.positionOS);
    o.pos.z = 0;

    return o;
}

void RenderTileFragment(Varyings IN, out half4 PhysicalTex1 : SV_Target0, out half4 PhysicalTex2 : SV_Target1)
{
    float4 transformUV = _TransformUV;

    float2 controlUV = IN.uv * transformUV.z + transformUV.xy;
    float4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control, controlUV);

    half weight = dot(control, 1.0h);

    #ifdef ADD_PASS
    clip(weight <= 0.005h ? -1.0h : 1.0h);
    #endif

    #ifndef _TERRAIN_BASEMAP_GEN
    // Normalize weights before lighting and restore weights in final modifier functions so that the overal
    // lighting result can be correctly weighted.
    weight /= (weight + HALF_MIN);
    #endif

    float2 uv = frac(controlUV * transformUV.w);

    half4 diffuse0 = _Diffuse0.Sample(sampler_Diffuse0, uv);
    half4 diffuse1 = _Diffuse1.Sample(sampler_Diffuse0, uv);
    half4 diffuse2 = _Diffuse2.Sample(sampler_Diffuse0, uv);
    half4 diffuse3 = _Diffuse3.Sample(sampler_Diffuse0, uv);

    // half4 diffuse0 = _Diffuse0.SampleLevel(sampler_Diffuse0, uv, 0);
    // half4 diffuse1 = _Diffuse1.SampleLevel(sampler_Diffuse0, uv, 0);
    // half4 diffuse2 = _Diffuse2.SampleLevel(sampler_Diffuse0, uv, 0);
    // half4 diffuse3 = _Diffuse3.SampleLevel(sampler_Diffuse0, uv, 0);

    half4 defaultSmoothness = half4(diffuse0.a, diffuse1.a, diffuse2.a, diffuse3.a);

    // half _NormalScale0 = 1, _NormalScale1 = 1, _NormalScale2 = 1, _NormalScale3 = 1;
    /*
    half3 normal0 = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal0, sampler_Normal0, uv), 1);
    half3 normal1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal1, sampler_Normal0, uv), 1);
    half3 normal2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal2, sampler_Normal0, uv), 1);
    half3 normal3 = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal3, sampler_Normal0, uv), 1);
    */

    half4 normal0 = _Normal0.Sample(sampler_Normal0, uv);
    half4 normal1 = _Normal1.Sample(sampler_Normal0, uv);
    half4 normal2 = _Normal2.Sample(sampler_Normal0, uv);
    half4 normal3 = _Normal3.Sample(sampler_Normal0, uv);

    // load Height
    float2 heightCoords = controlUV * _HeightMapResolution;
    float originHeight = UnpackHeightmap(_OriginHeightMap.Load(int3(heightCoords, 0)));

    PhysicalTex1 = diffuse0 * control.r + diffuse1 * control.g + diffuse2 * control.b + diffuse3 * control.a;

    half smoothness = dot(control, defaultSmoothness);

    half4 nrm = normal0 * control.r + normal1 * control.g + normal2 * control.b + normal3 * control.a;
    // avoid risk of NaN when normalizing
    PhysicalTex2 = half4(nrm.g, nrm.a, smoothness, 1.0h);
    // PhysicalTex2 = half4(nrm.r, nrm.g, nrm.b, 1.0h);
}
#endif
