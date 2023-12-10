#ifndef UNIVERSAL_RVT_DECAL_HEIGHT_INCLUDED
#define UNIVERSAL_RVT_DECAL_HEIGHT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_OriginHeightMap);
SAMPLER(sampler_OriginHeightMap);

float4 _VirtualTextureRect;
float _MaxHeightScale;
float _HeightMapResolution;

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float3 positionWS : TEXCOORD0;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

Varyings RenderDecalHeightVertex(Attributes input)
{
    Varyings output;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    float2 posXZ = vertexInput.positionWS.xz;
    posXZ = (posXZ - _VirtualTextureRect.xy) / _VirtualTextureRect.zw;
    #if SHADER_API_D3D11
    posXZ.y = 1 - posXZ.y;
    #endif
    posXZ = posXZ * 2 - 1;

    output.positionCS = float4(posXZ, 0.5, 1);
    output.positionWS = vertexInput.positionWS;
    return output;
}

void RenderDecalHeightFragment(Varyings input, out real4 Height : SV_Target0)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float height = input.positionWS.y * _MaxHeightScale;
    float2 sampleCoords =
        (input.positionWS.xz - _VirtualTextureRect.xy) * _HeightMapResolution / _VirtualTextureRect.zw;
    float originHeight = UnpackHeightmap(_OriginHeightMap.Load(int3(sampleCoords, 0)));
    if (originHeight > height)
    {
        height = originHeight;
    }
    Height = PackHeightmap(height);
}
#endif
