using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace RuntimeVirtualTexture
{
    public class RuntimeVirtualTextureVolume : MonoBehaviour
    {
        /*
         * Configuration parameters:
         */
        public int tilesPerMeter;
        public int tileSize;
        public int tileNum;
        public int feedbackFactor;
        public int maxTileRenderPerFrame;

        [SerializeField]
        private float4 virtualTextureRect;

        /*
         * Used for switching between RVT on/off:
         */
        public Material rvtMat;
        public Material originalMat;
        private bool enableRvt;
        private bool debugRvt;

        /*
         * Core RVT components :
         */
        public FeedbackReader feedBackReader;
        public PhysicalTextureManager physicalTextureManager;
        public PageTableManager pageTableManager;

//#if UNITY_EDITOR
        /*
         * Debug the Feedback buffer:
         */
//        public FeedBackDebugger feedBackDebugger;
//#endif  

        /*
         * the new page and physical tile per frame:
         */
        private int m_pageUpdateCount;
        private int m_physicalUpdateCount;
        private int m_lastActiveFrame;
        float deltaTime = 0.0f;

        [SerializeField]
        public TextMeshProUGUI display;

        private Mesh m_tileMesh;
        private CommandBuffer cmd;
        private NativeArray<PageTableUpdateRequest> m_pageTableUpdateRequests;
        private NativeArray<PageTableUpdateRequest> m_physicalTextureUpdateRequests;

        void OnEnable()
        {
            cmd = new CommandBuffer
            {
                name = "RuntimeVirtualTexture"
            };

            enableRvt = true;
            Shader.EnableKeyword("ENABLE_RVT");

            debugRvt = false;
            Shader.DisableKeyword("DEBUG_RVT");

            InitializeMesh();

            var currentCamera = Camera.main;
            if (currentCamera == null)
            {
                currentCamera = Camera.current;
            }

            int feedbackHeight = currentCamera.pixelHeight / feedbackFactor;
            int feedbackWidth = currentCamera.pixelWidth / feedbackFactor;

            feedBackReader =
                new FeedbackReader(feedbackHeight, feedbackWidth, feedbackFactor);
            feedBackReader.Initialize();

            var terrain = GetComponentInChildren<Terrain>();
            terrain.materialTemplate = rvtMat;

            var decalRenderers = GetComponentsInChildren<MeshRenderer>();

            var transformPos = transform.position;
            var terrainSize = terrain.terrainData.size;
            virtualTextureRect = new float4(transformPos.x, transformPos.z, terrainSize.x, terrainSize.z);

            Shader.SetGlobalVector(Shader.PropertyToID("_TerrainRect"),
                new Vector4(transformPos.x, transformPos.z, terrainSize.x, terrainSize.z));

            int pageNum = Mathf.CeilToInt(virtualTextureRect.z) * tilesPerMeter; // 128 * 2 = 256
            physicalTextureManager =
                new PhysicalTextureManager(tileNum, tileSize, pageNum, m_tileMesh,
                    virtualTextureRect, tilesPerMeter, terrain, decalRenderers);
            pageTableManager = new PageTableManager(tileNum, pageNum, m_tileMesh);

            physicalTextureManager.Initialize();
            pageTableManager.Initialize();

            m_lastActiveFrame = 0;
//#if UNITY_EDITOR
//            // For Debugging Feedback buffer:
            //           feedBackDebugger = new FeedBackDebugger();
            //           feedBackDebugger.InitializeRT(feedbackHeight, feedbackWidth);
//#endif
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Application.Quit();

            /* calculate the dps */
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            float fps = 1.0f / deltaTime;

            if (enableRvt)
            {
                // physicalTextureManager.ActivateDecals();
                // TestCase();
                // return;

                Profiler.BeginSample("Feedback");
                if (feedBackReader.IsReading())
                {
                    Profiler.BeginSample("ApplyUpdates");
                    /*
                     * update the PhysicalTexture(A and B) and the PageTableTexture 
                     */
                    ApplyUpdates(feedBackReader.RequestList);
                    Profiler.EndSample();
                }
                else
                {
                    if (feedBackReader.HasData())
                    {
//#if UNITY_EDITOR
//                        Profiler.BeginSample("Draw Debug Feedback Buffer");
                        //                       feedBackReader.DrawDebugFeedback(feedBackDebugger);
//                        Profiler.EndSample();
//#endif

                        /*
                         * Get and Analysis Data
                         */
                        Profiler.BeginSample("AnalysisData");
                        feedBackReader.AnalysisData();
                        Profiler.EndSample();
                    }

                    Profiler.BeginSample("ReadFeedback");
                    /*
                     * readback the feedback buffer from GPU to CPU
                     */
                    feedBackReader.ReadFeedback(false);
                    Profiler.EndSample();
                }

                Profiler.EndSample();
                Profiler.BeginSample("Clear Feedback Buffer");
                feedBackReader.Clear();
                Profiler.EndSample();
                display.SetText(
                    "FPS:{2:0} RVT:On\nrequests:{0:0}\nrender tile count:{1:0}\n",
                    m_pageUpdateCount,
                    m_physicalUpdateCount, fps);
            }
            else
            {
                m_pageUpdateCount = 0;
                m_physicalUpdateCount = 0;
                display.SetText("FPS:{0:0} RVT:Off\n", fps);
            }
        }

        void ApplyUpdates(List<uint> requestsList)
        {
            int reqNumber = requestsList.Count;
            if (reqNumber == 0)
                return;

            var time = Time.frameCount;
            m_pageTableUpdateRequests = new NativeArray<PageTableUpdateRequest>(reqNumber, Allocator.TempJob);
            // m_physicalTextureUpdateRequests = new NativeArray<PageTableUpdateRequest>(reqNumber, Allocator.TempJob);

            Profiler.BeginSample("Update PageTable and PhysicalTextures");

            m_pageUpdateCount = 0;
            m_physicalUpdateCount = 0;
            bool needUpdatePageTable = false;
            foreach (var req in requestsList)
            {
                // req: requested page id
                RVTUtils.DecodePageId(req, out var mipLevel, out var pageX, out var pageY);
                // invalid requests
                if (mipLevel < 0 || mipLevel > pageTableManager.mipCount || pageX < 0 || pageY < 0
                    || pageX > pageTableManager.pageNum || pageY > pageTableManager.pageNum)
                {
                    continue;
                }

                PageTableUpdateRequest tempReq;
                tempReq.VirtualCoord = new float2(pageX, pageY);
                tempReq.Mip = mipLevel;
                // Debug.Log($"mip:{mipLevel}, pageX:{pageX}, pageY: {pageY}");
                uint tileId;
                if (pageTableManager.pageTable.IsActive(req, out var activeFrame))
                {
                    if (activeFrame != m_lastActiveFrame)
                    {
                        // Debug.Log($"{m_lastActiveFrame} {activeFrame}");
                        needUpdatePageTable = true;
                    }

                    tileId = pageTableManager.pageTable.GetTileId(req);
                    pageTableManager.pageTable.Refresh(req, time);
                    tempReq.PhysicalCoord = new float2(tileId % tileNum, tileId / tileNum);
                }
                else
                {
                    if (m_physicalUpdateCount >= maxTileRenderPerFrame)
                    {
                        continue;
                    }

                    pageTableManager.pageTable.SetActive(req, time);
                    tileId = pageTableManager.pageTable.GetTileId(req);
                    tempReq.PhysicalCoord = new float2(tileId % tileNum, tileId / tileNum);
                    physicalTextureManager.RenderTile(tempReq);
                    // m_physicalTextureUpdateRequests[m_physicalUpdateCount] = tempReq;
                    m_physicalUpdateCount++;
                }

                m_pageTableUpdateRequests[m_pageUpdateCount] = tempReq;
                m_pageUpdateCount++;
                if (m_pageUpdateCount >= tileNum * tileNum)
                {
                    break;
                }
            }

            Profiler.EndSample();

            // Debug.Log($"total: {pageUpdateCount},  new: {newPageNumber}");
            // if (m_physicalUpdateCount > 0)
            // {
            //     Profiler.BeginSample("Update Physical Texture");
            //     physicalTextureManager.RenderTileInstanced(cmd, m_physicalUpdateCount, m_physicalTextureUpdateRequests);
            //     Profiler.EndSample();
            // }

            /*
             * update the PageTableTexture
             * we only update the pageTableTexture when:
             *   (1) m_physicalUpdateCount > 0 (new page need to be loaded.)
             *   (2) needUpdatePageTable == true
             */
            if (m_physicalUpdateCount > 0 || needUpdatePageTable)
            {
                Profiler.BeginSample("Update PageTableTexture");
                pageTableManager.DrawPageTable(cmd, m_pageUpdateCount, m_pageTableUpdateRequests);
                m_lastActiveFrame = time;
                Profiler.EndSample();
            }

            Profiler.BeginSample("Draw Call");
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            Profiler.EndSample();

            /*
             * clear
             */
            requestsList.Clear();
            m_pageTableUpdateRequests.Dispose();
            // m_physicalTextureUpdateRequests.Dispose();
            m_pageUpdateCount = reqNumber;
        }

        /*
         * prepare the quad mesh
         *
         * id (uv)
         * 0 (0, 1)     3 (1, 1)
         *
         * 1 (0, 0)     2 (1, 0)
         * 
         */
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

        public void OnEnableRVT()
        {
            enableRvt = !enableRvt;
            if (enableRvt)
            {
                var terrain = GetComponentInChildren<Terrain>();
                terrain.materialTemplate = rvtMat;
                Shader.EnableKeyword("ENABLE_RVT");
                // Shader.DisableKeyword("ENABLE_RVT");
                pageTableManager.Initialize();
                physicalTextureManager.Initialize();
                feedBackReader.ReadFeedback(true);
            }
            else
            {
                var terrain = GetComponentInChildren<Terrain>();
                terrain.materialTemplate = originalMat;
                Shader.DisableKeyword("ENABLE_RVT");
                display.SetText("requests:{0:0}  allocate page:{1:0}", 0, 0);
                pageTableManager.Dispose();
                physicalTextureManager.Dispose();
            }
        }

        public void OnEnableDebug()
        {
            debugRvt = !debugRvt;
            if (debugRvt)
            {
                Shader.EnableKeyword("DEBUG_RVT");
            }
            else
            {
                Shader.DisableKeyword("DEBUG_RVT");
            }
        }

        public void OnEnableBakeHeight()
        {
            pageTableManager.Dispose();
            physicalTextureManager.Dispose();
            physicalTextureManager.BakeHeight = !physicalTextureManager.BakeHeight;
            // Debug.Log($"BakeHeight: {physicalTextureManager.BakeHeight}");
            pageTableManager.Initialize();
            physicalTextureManager.Initialize();
            feedBackReader.ReadFeedback(true);
        }

        public void OnEnableCompress()
        {
            pageTableManager.Dispose();
            physicalTextureManager.Dispose();
            physicalTextureManager.enableCompress = !physicalTextureManager.enableCompress;
            pageTableManager.Initialize();
            physicalTextureManager.Initialize();
            feedBackReader.ReadFeedback(true);
        }

        void OnDisable()
        {
            feedBackReader.Dispose();
            // feedBackDebugger.Dispose();
            pageTableManager.Dispose();
            physicalTextureManager.Dispose();
            cmd.Release();
        }

        void TestCase()
        {
            int size = 1;
            NativeArray<PageTableUpdateRequest> requests =
                new NativeArray<PageTableUpdateRequest>(size * size, Allocator.TempJob);
            var physicalTextureUpdateRequests = new NativeArray<PageTableUpdateRequest>(size * size, Allocator.TempJob);
            int tileNum = Mathf.CeilToInt(virtualTextureRect.z) * tilesPerMeter / size;
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    PageTableUpdateRequest req;
                    req.VirtualCoord = new float2(i * tileNum, j * tileNum);
                    req.PhysicalCoord = new float2(i, j);
                    req.Mip = 8 - (int)math.log2(size);
                    requests[i * size + j] = req;
                    physicalTextureUpdateRequests[i * size + j] = req;
                    physicalTextureManager.RenderTile(req);
                }
            }

            pageTableManager.DrawPageTable(cmd, size * size, requests);
            // physicalTextureManager.RenderTileInstanced(cmd, size*size, physicalTextureUpdateRequests);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            requests.Dispose();
            physicalTextureUpdateRequests.Dispose();
            // DrawDebugFeedback();
        }
    }
}