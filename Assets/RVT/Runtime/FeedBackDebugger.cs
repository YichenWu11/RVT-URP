using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RuntimeVirtualTexture
{
    [Serializable]
    public class FeedBackDebugger
    {
        public RenderTexture _FeedBackTexture;

        Material m_FeedBackMaterial;
        private Mesh m_tileMesh;

        public void InitializeRT(int height, int width)
        {
            _FeedBackTexture = new RenderTexture(width, height, 0);
            _FeedBackTexture.name = "FeedBackTexture";
            _FeedBackTexture.filterMode = FilterMode.Point;
            _FeedBackTexture.wrapMode = TextureWrapMode.Clamp;
            _FeedBackTexture.Create();

            m_FeedBackMaterial = new Material(Shader.Find("RVT/FeedbackDebug"));
            InitializeMesh();
        }

        void InitializeMesh()
        {
            List<Vector3> vertexList = new List<Vector3>();
            List<int> triangleList = new List<int>();
            List<Vector2> uvList = new List<Vector2>();

            vertexList.Add(new Vector3(0, 1, 0));
            uvList.Add(new Vector2(0, 1));
            vertexList.Add(new Vector3(0, 0, 0));
            uvList.Add(new Vector2(0, 0));
            vertexList.Add(new Vector3(1, 0, 0));
            uvList.Add(new Vector2(1, 0));
            vertexList.Add(new Vector3(1, 1, 0));
            uvList.Add(new Vector2(1, 1));

            triangleList.Add(0);
            triangleList.Add(1);
            triangleList.Add(2);

            triangleList.Add(2);
            triangleList.Add(3);
            triangleList.Add(0);

            m_tileMesh = new Mesh();
            m_tileMesh.SetVertices(vertexList);
            m_tileMesh.SetUVs(0, uvList);
            m_tileMesh.SetTriangles(triangleList, 0);
        }

        public void DrawFeedBack(ComputeBuffer debugBuffer)
        {
            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "FeedbackDebug";

            cmd.SetRenderTarget(_FeedBackTexture);

            var mat = new Matrix4x4
            {
                m00 = 2,
                m03 = -1,
                m11 = 2,
                m13 = -1,
                m23 = -1,
                m33 = 1
            };
            m_FeedBackMaterial.SetBuffer(Shader.PropertyToID("_DebugBuffer"), debugBuffer);
            //Graphics.SetRandomWriteTarget(0, feedbackBuffer, true);
            m_FeedBackMaterial.SetMatrix(Shader.PropertyToID("_ImageMVP"), GL.GetGPUProjectionMatrix(mat, true));
            cmd.DrawMesh(m_tileMesh, Matrix4x4.identity, m_FeedBackMaterial, 0, 0);
            Graphics.ExecuteCommandBuffer(cmd);
        }

        public void Dispose()
        {
            _FeedBackTexture.Release();
        }
    }
}