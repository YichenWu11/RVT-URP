#ifndef UNIVERSAL_RVT_TERRAIN_LIT_INPUT_INCLUDED
#define UNIVERSAL_RVT_TERRAIN_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
half4 _BaseColor;
half _Cutoff;
CBUFFER_END

#define _Surface 0.0 // Terrain is always opaque

CBUFFER_START(_Terrain)
half _NormalScale0, _NormalScale1, _NormalScale2, _NormalScale3;
half _Metallic0, _Metallic1, _Metallic2, _Metallic3;
half _Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3;
half4 _DiffuseRemapScale0, _DiffuseRemapScale1, _DiffuseRemapScale2, _DiffuseRemapScale3;
half4 _MaskMapRemapOffset0, _MaskMapRemapOffset1, _MaskMapRemapOffset2, _MaskMapRemapOffset3;
half4 _MaskMapRemapScale0, _MaskMapRemapScale1, _MaskMapRemapScale2, _MaskMapRemapScale3;

float4 _Control_ST;
float4 _Control_TexelSize;
half _DiffuseHasAlpha0, _DiffuseHasAlpha1, _DiffuseHasAlpha2, _DiffuseHasAlpha3;
half _LayerHasMask0, _LayerHasMask1, _LayerHasMask2, _LayerHasMask3;
half4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;
half _HeightTransition;
half _NumLayersCount;

#ifdef UNITY_INSTANCING_ENABLED
    float4 _TerrainHeightmapRecipSize;   // float4(1.0f/width, 1.0f/height, 1.0f/(width-1), 1.0f/(height-1))
    float4 _TerrainHeightmapScale;       // float4(hmScale.x, hmScale.y / (float)(kMaxHeight), hmScale.z, 0.0f)
#endif
#ifdef SCENESELECTIONPASS
    int _ObjectId;
    int _PassValue;
#endif
CBUFFER_END

#ifndef ENABLE_RVT
TEXTURE2D(_Control);
SAMPLER(sampler_Control);
TEXTURE2D(_Splat0);
SAMPLER(sampler_Splat0);
TEXTURE2D(_Splat1);
TEXTURE2D(_Splat2);
TEXTURE2D(_Splat3);

#ifdef _NORMALMAP
TEXTURE2D(_Normal0);
SAMPLER(sampler_Normal0);
TEXTURE2D(_Normal1);
TEXTURE2D(_Normal2);
TEXTURE2D(_Normal3);
#endif

#ifdef _MASKMAP
TEXTURE2D(_Mask0);      SAMPLER(sampler_Mask0);
TEXTURE2D(_Mask1);
TEXTURE2D(_Mask2);
TEXTURE2D(_Mask3);

#endif

#endif

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_SpecGlossMap);
SAMPLER(sampler_SpecGlossMap);
TEXTURE2D(_MetallicTex);
SAMPLER(sampler_MetallicTex);


#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
#define ENABLE_TERRAIN_PERPIXEL_NORMAL
#endif


#ifdef UNITY_INSTANCING_ENABLED
TEXTURE2D(_TerrainHeightmapTexture);
TEXTURE2D(_TerrainNormalmapTexture);
SAMPLER(sampler_TerrainNormalmapTexture);
#endif

float4 _VirtualTextureRect;

UNITY_INSTANCING_BUFFER_START(Terrain)
UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData) // float4(xBase, yBase, skipScale, ~)
UNITY_INSTANCING_BUFFER_END(Terrain)

#ifdef _ALPHATEST_ON
TEXTURE2D(_TerrainHolesTexture);
SAMPLER(sampler_TerrainHolesTexture);

void ClipHoles(float2 uv)
{
    float hole = SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, uv).r;
    clip(hole == 0.0f ? -1 : 1);
}
#endif

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;
    specGloss = SAMPLE_TEXTURE2D(_MetallicTex, sampler_MetallicTex, uv);
    specGloss.a = albedoAlpha;
    return specGloss;
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    outSurfaceData = (SurfaceData)0;
    half4 albedoSmoothness = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    outSurfaceData.alpha = 1;

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoSmoothness.a);
    outSurfaceData.albedo = albedoSmoothness.rgb;

    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap));
    outSurfaceData.occlusion = 1;
    outSurfaceData.emission = 0;
}

// ForDebug
float4 _ScreenResolution;
// _TerrainRect: x: TerrainPos.x, y: TerrainPos.z, z:Terrain.width, w: Terrain.height
float4 _TerrainRect;

// Used For RVT: //
#ifdef ENABLE_RVT
TEXTURE2D(_PhysicsTextureA);
TEXTURE2D(_PhysicsTextureB);

TEXTURE2D(_PageTableTexture);

SAMPLER(sampler_PhysicsTextureA);
SAMPLER(sampler_PhysicsTextureB);
SAMPLER(sampler_PageTableTexture);

RWStructuredBuffer<uint> _FeedbackBuffer : register(u1); // make sure you have same id in C# script

// _FeedBackParam x: scaleFactor, y: feedbackHeight, z:feedbackWidth, w:lodBias
float4 _FeedBackParam;
// _PageTableParams x: _pageNum(256), y: 1/_pageNum (1/256), z:_mipCount(9)
float4 _PageTableParams;
// _PhysicalTextureParams x:TextureSize(4160), y:tileSize(512), z:tileBorder(4), w:tilePaddingSize(520)
float4 _PhysicalTextureParams;

float _HeightMapResolution;

struct RVTData
{
    half3 albedo;
    half3 normal;
    half smoothness;
    half metallic;
};

half4 FinalizeFeedbackTexture(float4 clipPos, float2 uv)
{
    int mipLevel;
    
    uint screenX = clipPos.x;
    uint screenY = clipPos.y;

    uint offsetX = screenX % _FeedBackParam.x;
    uint offsetY = screenY % _FeedBackParam.x;

    // multiply by (offsetX == 0) & (offsetY == 0) to reduce texture popping 
    uint scaleX = screenX / _FeedBackParam.x;
    uint scaleY = screenY / _FeedBackParam.x;
    uint activate = (offsetX == 0) & (offsetY == 0);

    uint feedbackPos = scaleY * _FeedBackParam.z + scaleX;
    feedbackPos = feedbackPos * activate;

    /* compute mip level */
    // virtual texture size : _PageTableParams.x(256) * _PhysicalTextureParams.y (512),
    float virtualTextureSize = _PageTableParams.x * _PhysicalTextureParams.y;
    float2 pixel = uv * virtualTextureSize; 
    half2 dx = ddx(pixel);
    half2 dy = ddy(pixel);

    // mipLevel clamp to (0, _mipCount-1)
    mipLevel = clamp(int(0.5 * log2(max(dot(dx, dx), dot(dy, dy))) + 0.5 + _FeedBackParam.w), 0,  _PageTableParams.z - 1);

    /* compute the corresponding tile index based on current uv */
    float2 pageTableIndex = floor(uv * _PageTableParams.x);
    
    /*
    int mipTileSize = pow(2, mipLevel);
    int encodedPageX = (((int) pageTableIndex.x) / ((int)mipTileSize)) * mipTileSize;
    int encodedPageY = (((int) pageTableIndex.y) / ((int)mipTileSize)) * mipTileSize;
    */
    
    int encodedPageX = (((int) pageTableIndex.x) >> mipLevel) << mipLevel;
    int encodedPageY = (((int) pageTableIndex.y) >> mipLevel) << mipLevel;
    
    return half4(encodedPageX / 255.0h, encodedPageY / 255.0h, mipLevel / 255.0h, 1.0h);
    // return half4(1.0h, 0.0h, 0.0h, 1.0h);
}

/** This should be called at the end of the pixel shader to write out the gathered VT feedback info to the _FeedbackBuffer. */
void FinalizeFeedbackBuffer(float4 clipPos, float2 uv, out int mipLevel)
{
    uint screenX = clipPos.x;
    uint screenY = clipPos.y;

    uint offsetX = screenX % _FeedBackParam.x;
    uint offsetY = screenY % _FeedBackParam.x;

    // multiply by (offsetX == 0) & (offsetY == 0) to reduce texture popping 
    uint scaleX = screenX / _FeedBackParam.x;
    uint scaleY = screenY / _FeedBackParam.x;
    uint activate = (offsetX == 0) & (offsetY == 0);

    uint feedbackPos = scaleY * _FeedBackParam.z + scaleX;
    feedbackPos = feedbackPos * activate;

    /* compute mip level */
    // virtual texture size : _PageTableParams.x(256) * _PhysicalTextureParams.y (512),
    float virtualTextureSize = _PageTableParams.x * _PhysicalTextureParams.y;
    float2 pixel = uv * virtualTextureSize; 
    half2 dx = ddx(pixel);
    half2 dy = ddy(pixel);

    // mipLevel clamp to (0, _mipCount-1)
    mipLevel = clamp(int(0.5 * log2(max(dot(dx, dx), dot(dy, dy))) + 0.5 + _FeedBackParam.w), 0,  _PageTableParams.z - 1);

    /* compute the corresponding tile index based on current uv */
    float2 pageTableIndex = floor(uv * _PageTableParams.x);
    
    /*
    int mipTileSize = pow(2, mipLevel);
    int encodedPageX = (((int) pageTableIndex.x) / ((int)mipTileSize)) * mipTileSize;
    int encodedPageY = (((int) pageTableIndex.y) / ((int)mipTileSize)) * mipTileSize;
    */
    
    int encodedPageX = (((int) pageTableIndex.x) >> mipLevel) << mipLevel;
    int encodedPageY = (((int) pageTableIndex.y) >> mipLevel) << mipLevel;
    
    encodedPageX = (encodedPageX & 0xfff) << 12;
    encodedPageY = encodedPageY & 0xfff;
    int encodedMipLevel = (mipLevel & 0xff) << 24;

    /* result: mip(8) | PageX(12) | PageY(12) */
    uint request = encodedMipLevel | encodedPageX | encodedPageY;
    
    _FeedbackBuffer[feedbackPos] = request;
    // _FeedbackBuffer[feedbackPos] = 10;
}


void SamplePhysicalTexture(float2 uv, out RVTData rvt_data)
{
    rvt_data = (RVTData)0;

    float2 pageTableUV = floor(uv * _PageTableParams.x) * _PageTableParams.y;
    
    float4 pageTable = _PageTableTexture.SampleLevel(sampler_PageTableTexture, pageTableUV, 0) * 256.0f;
    
    int mipLevel = pageTable.z;

    float2 pageOffset = pageTable.xy;
    
    float2 tileOffset = frac(uv * exp2(_PageTableParams.z - 1 - mipLevel));
    
    // float2 physicalUV = (pageTable.xy * (512+2*4) + tileOffset * 512 + 4) / 8*(512+2*4);
    float2 physicalUV = (pageOffset * _PhysicalTextureParams.w + tileOffset * _PhysicalTextureParams.y + _PhysicalTextureParams.z) / _PhysicalTextureParams.x;

    float4 physicsTextureA = _PhysicsTextureA.SampleLevel(sampler_PhysicsTextureA, physicalUV, 0);
    rvt_data.albedo = physicsTextureA.rgb;
    
    // half4 physicsTextureB = SAMPLE_TEXTURE2D(_PhysicsTextureB, sampler_PhysicsTextureB, physicalUV);
    // half3 nrm = physicsTextureB.xyz;
    // nrm.z += 1e-5f;
    // rvt_data.normal = normalize(nrm);
    
    half4 physicsTextureB = SAMPLE_TEXTURE2D(_PhysicsTextureB, sampler_PhysicsTextureB, physicalUV);
    // g -> r , a -> g
    half3 nrm = UnpackNormalScale(half4(1, physicsTextureB.r, 1, physicsTextureB.g), 1.0f);
    nrm.z += 1e-5f;
    rvt_data.normal = normalize(nrm);
    
    rvt_data.metallic = 0;
    rvt_data.smoothness = physicsTextureB.z;

#if defined(DEBUG_RVT)
    
    rvt_data.normal = half3(0, 0, 1);
    rvt_data.albedo = half3(pageOffset / 255.0h, mipLevel / 10.0h);
    rvt_data.smoothness = 0;
    
#endif
}
#endif


void TerrainInstancing(inout float4 positionOS, inout float3 normal, inout float2 uv)
{
    #ifdef UNITY_INSTANCING_ENABLED
    float2 patchVertex = positionOS.xy;
    float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);

    float2 sampleCoords = (patchVertex.xy + instanceData.xy) * instanceData.z; // (xy + float2(xBase,yBase)) * skipScale


    float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));

    positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
    positionOS.y = height * _TerrainHeightmapScale.y;

    #ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
    normal = float3(0, 1, 0);
    #else
    normal = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2 - 1;
    #endif
    
    uv = sampleCoords * _TerrainHeightmapRecipSize.zw;
    #endif
}

void TerrainInstancing(inout float4 positionOS, inout float3 normal)
{
    float2 uv = {0, 0};
    TerrainInstancing(positionOS, normal, uv);
}
#endif
