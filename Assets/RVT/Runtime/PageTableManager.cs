using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace RuntimeVirtualTexture
{
    public struct PageTableUpdateRequest
    {
        public float2 VirtualCoord;
        public float2 PhysicalCoord;
        public int Mip;
    }

    public struct PageTableParams
    {
        public float4 PageInfo;
        public float4x4 Matrix_MVP;
    };

    [Serializable]
    public class PageTableManager : IDisposable
    {
        public int pageNum { get; }

        public int mipCount { get; }

        /*
         * Parameters For Page Table Management
         */
        public PageTable pageTable;

        /*
         * Parameters For Page Table Rendering
         */
        public RenderTexture _pageTableTexture;
        private Material m_renderPageTableMaterial;
        private Mesh m_tileMesh;

        /*
         * Used for Update PageTableTexture using GPU Instancing
         */
        MaterialPropertyBlock matBlock;
        Vector4[] pageInfoArray;
        Matrix4x4[] matrixArray;

        private ComputeBuffer _PageParamBuffer;

        public PageTableManager(int tileNum, int pageNum, Mesh mesh)
        {
            m_tileMesh = mesh;
            // pageNumber = textureRect length * tiles per meter (128 * 2)
            this.pageNum = pageNum;
            // total mip number = log2(256) + 1 = 9
            mipCount = (int)math.log2(pageNum) + 1;
            pageTable = new PageTable(mipCount, tileNum);

            // _PageParamBuffer = new ComputeBuffer(tileNum * tileNum, Marshal.SizeOf(typeof(PageTableParams)));
            // Shader.SetGlobalBuffer(Shader.PropertyToID("_PageParamBuffer"),_PageParamBuffer);

            m_renderPageTableMaterial = new Material(Shader.Find("RVT/RenderPageTable"));
            m_renderPageTableMaterial.enableInstancing = true;

            matBlock = new MaterialPropertyBlock();
            pageInfoArray = new Vector4[tileNum * tileNum];
            matrixArray = new Matrix4x4[tileNum * tileNum];
        }

        public void Initialize()
        {
            _pageTableTexture = new RenderTexture(pageNum, pageNum, 0);
            _pageTableTexture.name = "PageTableTexture";
            _pageTableTexture.format = RenderTextureFormat.ARGBHalf;
            _pageTableTexture.filterMode = FilterMode.Point;
            _pageTableTexture.wrapMode = TextureWrapMode.Clamp;
            _pageTableTexture.useMipMap = false;
            _pageTableTexture.autoGenerateMips = false;
            _pageTableTexture.Create();
            Shader.SetGlobalTexture(Shader.PropertyToID("_PageTableTexture"), _pageTableTexture);
            // _PageTableParams x: _pageNum(256) y: 1/_pageNum (1/256) z:_mipCount(9)
            Shader.SetGlobalVector(Shader.PropertyToID("_PageTableParams"),
                new Vector4(pageNum, 1.0f / pageNum, mipCount, 1));
        }

        // For Debug
        public void ReadBackPageTable()
        {
            var req = AsyncGPUReadback.Request(_pageTableTexture);
            req.WaitForCompletion();
            Texture2D texture = new Texture2D(512, 512, TextureFormat.ARGB32, false);
            texture.GetRawTextureData<Color32>().CopyFrom(req.GetData<Color32>());
        }

        /*
         * Draw PageTable Texture
         * updatesNum : the number of new page table updates for current frame
         * requests: new page table to be updated
         */
        public void DrawPageTable(CommandBuffer cmd, int updatesNum, NativeArray<PageTableUpdateRequest> requests)
        {
            Profiler.BeginSample("Prepare Params for PageTable");
            cmd.SetRenderTarget(_pageTableTexture);

            for (int i = 0; i < updatesNum; i++)
            {
                // construct MVP matrix
                float regionSize = 1 << requests[i].Mip;
                float posX = Mathf.Floor(requests[i].VirtualCoord.x / regionSize) * regionSize;
                float posY = Mathf.Floor(requests[i].VirtualCoord.y / regionSize) * regionSize;
                // Debug.Log($"{posX}, {posY}");
                float l = 2.0f * posX / pageNum - 1;
                float r = 2.0f * (posX + regionSize) / pageNum - 1;
                float b = 2.0f * posY / pageNum - 1;
                float t = 2.0f * (posY + regionSize) / pageNum - 1;
                var mat = new Matrix4x4
                {
                    m00 = r - l,
                    m03 = l,
                    m11 = t - b,
                    m13 = b,
                    m23 = 1,
                    m33 = 1
                };
                matrixArray[i] = GL.GetGPUProjectionMatrix(mat, true);
                pageInfoArray[i] = new Vector4(requests[i].PhysicalCoord.x / 256.0f,
                    requests[i].PhysicalCoord.y / 256.0f, requests[i].Mip / 256.0f, 1);
                // Debug.Log(new Vector4(requests[i].PhysicalCoord.x,
                //     requests[i].PhysicalCoord.y, requests[i].Mip, 1));
            }

            MaterialPropertyBlock matBlock = new MaterialPropertyBlock();
            matBlock.SetVectorArray(Shader.PropertyToID("_PageInfo"), pageInfoArray);
            matBlock.SetMatrixArray(Shader.PropertyToID("_ImageMVP"), matrixArray);
            Profiler.EndSample();

            cmd.DrawMeshInstanced(m_tileMesh, 0, m_renderPageTableMaterial, 0, matrixArray, updatesNum, matBlock);
            // Graphics.ExecuteCommandBuffer(cmd); // this cmd will be Executed in the Update() of RuntimeVirtualTextureVolume
        }

        public void Dispose()
        {
            if (_pageTableTexture != null)
            {
                _pageTableTexture.Release();
            }

            pageTable.Clear();
            // _PageParamBuffer.Release();
        }
    }
}