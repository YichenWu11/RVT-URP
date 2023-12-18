using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace RuntimeVirtualTexture
{
    [BurstCompile]
    internal struct GeneratePhysicalInfoTask : IJobParallelFor
    {
        internal int m_tileSize;
        internal int TileBorder;
        internal int m_tilePaddingSize;
        internal int m_textureSize;
        internal float m_tilingScale;
        internal float m_pageNum;
        [ReadOnly]
        internal NativeArray<PageTableUpdateRequest> requests;
        [WriteOnly]
        internal NativeArray<PhysicalTextureParam> physicalParams;
        public void Execute(int i)
        {
            PhysicalTextureParam param;
            float2 virtualCoord = requests[i].VirtualCoord;
            float2 physicalCoord = requests[i].PhysicalCoord;
            int mipLevel = requests[i].Mip;
            float regionSize = (1 << mipLevel);
            float posX = Mathf.Floor(virtualCoord.x / regionSize) * regionSize;
            float posY = Mathf.Floor(virtualCoord.y / regionSize) * regionSize; 
            
            float offsetX = (posX*m_tileSize - TileBorder*regionSize) /  (m_pageNum * m_tileSize);
            float offsetY = (posY*m_tileSize - TileBorder*regionSize) /  (m_pageNum * m_tileSize);;
            float scale = regionSize * m_tilePaddingSize / (m_pageNum * m_tileSize);
            
            float l = 2.0f * physicalCoord.x * m_tilePaddingSize / m_textureSize - 1;
            float r = 2.0f * (physicalCoord.x * m_tilePaddingSize + m_tilePaddingSize) / m_textureSize - 1;
            float b = 2.0f * physicalCoord.y * m_tilePaddingSize / m_textureSize - 1;
            float t = 2.0f * (physicalCoord.y * m_tilePaddingSize + m_tilePaddingSize) / m_textureSize - 1;
            var mat = new Matrix4x4
            {
                m00 = r - l,
                m03 = l,
                m11 = t - b,
                m13 = b,    
                m23 = 1,    // multiply by -1
                m33 = 1
            };

            
            param.Matrix_MVP=mat;
            param.TransformUV = new float4(offsetX, offsetY ,scale , m_tilingScale);
            physicalParams[i]=param;
        }
    }
}