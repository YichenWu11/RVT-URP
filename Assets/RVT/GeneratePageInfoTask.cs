using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace RuntimeVirtualTexture
{
    [BurstCompile]
    internal struct GeneratePageInfoTask : IJobParallelFor
    {
        internal int pageNum;
        [ReadOnly]
        internal NativeArray<PageTableUpdateRequest> requests;
        /*[WriteOnly]
        internal NativeArray<Vector4> pageInfos;
        [WriteOnly]
        internal NativeArray<Matrix4x4> MatrixMvp;*/
        [WriteOnly]
        internal NativeArray<PageTableParams> pageParams;
        public void Execute(int i)
        {
            PageTableParams param;
            
            param.PageInfo = new float4(requests[i].PhysicalCoord.x/ 256.0f, requests[i].PhysicalCoord.y/ 256.0f, requests[i].Mip/256.0f, 1);
            
            //param.PageInfo = new float4(12.0f / 255.0f, 15.0f / 255.0f, 18.0f /255.0f, 1);

            //uint encodedMipLevel = ((uint)requests[i].Mip & 0xff) << 24;
            //uint encodedPageX = ((uint)requests[i].PhysicalCoord.x & 0xfff) << 12;
            //uint encodedPageY = (uint)requests[i].PhysicalCoord.y & 0xfff;
            //param.PageInfo = encodedMipLevel | encodedPageX | encodedPageY;
            
            float regionSize = 1 << requests[i].Mip;
            float posX = Mathf.Floor(requests[i].VirtualCoord.x / regionSize) * regionSize;
            float posY = Mathf.Floor(requests[i].VirtualCoord.y / regionSize) * regionSize;
            // Debug.Log($"{posX},{posY}");
            float l = 2.0f * posX/ pageNum - 1;
            float r = 2.0f * (posX + regionSize) / pageNum - 1;
            float b = 2.0f * posY / pageNum - 1;
            float t = 2.0f * (posY + regionSize) / pageNum - 1;
            var mat = new Matrix4x4
            {
                m00 = r - l,
                m03 = l,
                m11 = t - b,
                m13 = b,   
                m23 = 1,     // multiply by -1
                m33 = 1
            };
            param.Matrix_MVP=mat;
            pageParams[i]=param;
        }
    }
}