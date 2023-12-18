#ifndef UNIVERSAL_RENDER_DECAL_TILE_INCLUDED
#define UNIVERSAL_RENDER_DECAL_TILE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"

float4 _VirtualTextureRect;
float4 _VirtualTextureTileRect;
TEXTURE2D(_OriginHeightMap);
SAMPLER(sampler_OriginHeightMap);
float _MaxHeightScale;
float _HeightMapResolution;

float _BlendScale;
float _BlendBias;

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv : TEXCOORD0;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);

    float3 positionWS : TEXCOORD2;
    float3 normalWS : TEXCOORD3;
    #ifdef _NORMALMAP
    float4 tangentWS : TEXCOORD4; // xyz: tangent, w: sign
    #endif
    float3 viewDirWS : TEXCOORD5;

    half4 fogFactorAndVertexLight : TEXCOORD6; // x: fogFactor, yzw: vertex light

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD7;
    #endif

    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

    #if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
    #endif

    half3 viewDirWS = SafeNormalize(input.viewDirWS);
    #ifdef _NORMALMAP
    float sgn = input.tangentWS.w; // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    inputData.normalWS = TransformTangentToWorld(
        normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
    #else
    inputData.normalWS = input.normalWS;
    #endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    #else
    inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif

    inputData.fogCoord = input.fogFactorAndVertexLight.x;
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
    inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
}


///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings RenderDecalVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    float3 viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

    // already normalized from normal transform to WS.
    output.normalWS = normalInput.normalWS;
    output.viewDirWS = viewDirWS;
    #ifdef _NORMALMAP
    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
    #endif

    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

    output.positionWS = vertexInput.positionWS;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
    #endif

    float z = vertexInput.positionWS.y * _MaxHeightScale;
    float2 posXZ = vertexInput.positionWS.xz;
    posXZ = (posXZ - _VirtualTextureTileRect.xy) * _VirtualTextureTileRect.zw;
    posXZ = posXZ * 2 - 1;

    #if SHADER_API_D3D11
    posXZ.y = -posXZ.y;
    z = 1 - z;
    #else
    z = -z;
    #endif

    output.positionCS = float4(posXZ, z, 1);

    return output;
}

void RenderDecalFragment(Varyings input, out half4 PhysicalTex1 : SV_Target0, out half4 PhysicalTex2 : SV_Target1)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

    // InputData inputData;
    // InitializeInputData(input, surfaceData.normalTS, inputData);

    // height blend:
    float2 sampleCoords =
        (input.positionWS.xz - _VirtualTextureRect.xy) * _HeightMapResolution / _VirtualTextureRect.zw;
    float originHeight = UnpackHeightmap(_OriginHeightMap.Load(int3(sampleCoords, 0)));
    float heightDiff = input.positionWS.y - originHeight / _MaxHeightScale;

    half alpha = clamp(heightDiff * _BlendScale + _BlendBias, 0, 1);
    // half alpha = heightDiff > 0 ? 1 : 0;

    half4 normal = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);

    PhysicalTex1 = half4(surfaceData.albedo, alpha);
    // float height = input.positionWS.y * _MaxHeightScale;
    // if (originHeight > height)
    // {
    //     height = originHeight;
    // }
    //PhysicalTex2 = half4(normal.g, normal.a, height, alpha);
    PhysicalTex2 = half4(normal.g, normal.a, surfaceData.smoothness, alpha);
}
#endif
