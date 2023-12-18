using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace RuntimeVirtualTexture
{
    public struct PhysicalTextureParam
    {
        public float4 TransformUV;
        public float4x4 Matrix_MVP;
    }

    internal struct DecalParam
    {
        internal MeshRenderer Renderer;
        internal Mesh Mesh;
        internal Material RenderDecalMaterial;
    }

    [Serializable]
    public class PhysicalTextureManager : IDisposable
    {
        public bool BakeHeight;

        public int TileBorder = 4;

        [HideInInspector]
        public bool enableCompress = true;

        /*
         * PhysicalTexture configuration
         */
        private int m_tileSize;
        private int m_tileNum;
        private int m_pageNum;

        private int m_tilePaddingSize;
        private int m_textureSize;

        private float m_textureTilingScale;

        public RenderTexture _physicalTileA;
        public RenderTexture _physicalTileB;
        private RenderTargetIdentifier[] _physicalTextureIDs;

        private RenderTexture terrainHeightMapTexture;
        private RenderTexture originHeightMapTexture;

        /*
         * Physics Texture Runtime Compression
         */
        [SerializeField]
        private RenderTexture compressResultA;

        [SerializeField]
        private RenderTexture compressResultB;

        [SerializeField]
        private Texture2D _compressedPhysicalTextureA;

        [SerializeField]
        private Texture2D _compressedPhysicalTextureB;

        [SerializeField]
        private Texture2D _uncompressedPhysicalTextureA;

        [SerializeField]
        private Texture2D _uncompressedPhysicalTextureB;

        /* Interface for inspector editor */
        public Texture2D PhysicalTextureA =>
            (enableCompress) ? _compressedPhysicalTextureA : _uncompressedPhysicalTextureA;

        public Texture2D PhysicalTextureB =>
            (enableCompress) ? _compressedPhysicalTextureB : _uncompressedPhysicalTextureB;

        private GraphicsFormat m_CompressFormat;
        public ComputeShader runtimeCompressShader;

        private CommandBuffer cmd;

        /*
         * Physics Texture Resources
         */
        private Material m_renderTileMaterial;
        private Shader m_renderDecalShader;

        private Mesh m_tileMesh;
        private Terrain m_terrain;
        private float4 m_virtualTextureRect;
        private int m_tilesPerMeter;
        private ComputeBuffer _PhysicalParamBuffer;

        // decal renderers
        private List<DecalParam> m_Decals;

        /*
         * Used for the RenderTile of Instancing Version.
         */
        Matrix4x4[] mvps;
        Vector4[] transformUVs;

        private static readonly int MaxHeightScaleId = Shader.PropertyToID("_MaxHeightScale");
        private static readonly int BlendScaleId = Shader.PropertyToID("_BlendScale");
        private static readonly int BlendBiasId = Shader.PropertyToID("_BlendBias");

        private static readonly int VirtualTextureRectId = Shader.PropertyToID("_VirtualTextureRect");
        private static readonly int HeightMapResolutionId = Shader.PropertyToID("_HeightMapResolution");
        private static readonly int TransformUVId = Shader.PropertyToID("_TransformUV");
        private static readonly int MatrixMvpId = Shader.PropertyToID("_ImageMVP");

        private static readonly int OriginHeightMapId = Shader.PropertyToID("_OriginHeightMap");

        private static readonly int ControlMapId = Shader.PropertyToID("_Control");


        public PhysicalTextureManager(int tileNum, int tileSize, int pageNum, Mesh mesh, float4 virtualTextureRect,
            int tilesPerMeter, Terrain terrain, MeshRenderer[] renderers)
        {
            cmd = new CommandBuffer()
            {
                name = "RenderPhysicalTexture"
            };

            m_tileNum = tileNum;
            m_tileSize = tileSize;
            /* The actual tile size (size + padding_size) */
            m_tilePaddingSize = (m_tileSize + TileBorder * 2);
            /* The actual PhysicalTexture size */
            m_textureSize = m_tileNum * m_tilePaddingSize;
            m_pageNum = pageNum; // 128 * 2
            m_tileMesh = mesh;
            m_virtualTextureRect = virtualTextureRect;
            m_tilesPerMeter = tilesPerMeter;
            m_terrain = terrain;

            m_renderTileMaterial = new Material(Shader.Find("RVT/RenderTile"));
            m_renderDecalShader = Shader.Find("RVT/RenderDecalTile");
            m_renderTileMaterial.enableInstancing = true;

            m_Decals = new List<DecalParam>();
            foreach (var renderer in renderers)
            {
                DecalParam newDecal;
                var decalMesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (decalMesh == null)
                {
                    continue;
                }

                newDecal.Renderer = renderer;
                newDecal.Mesh = decalMesh;
                newDecal.RenderDecalMaterial = new Material(renderer.material);
                newDecal.RenderDecalMaterial.shader = m_renderDecalShader;
                m_Decals.Add(newDecal);
            }

            m_textureTilingScale = m_virtualTextureRect.z;

            mvps = new Matrix4x4[tileNum * tileNum];
            transformUVs = new Vector4[tileNum * tileNum];

            runtimeCompressShader = Resources.Load<ComputeShader>("Shaders/Compress/ComputeCompress");

            BakeHeight = true;
        }

        public void Initialize()
        {
            // _PhysicalParamBuffer =
            //     new ComputeBuffer(m_tileNum * m_tileNum, Marshal.SizeOf(typeof(PhysicalTextureParam)));
            // Shader.SetGlobalBuffer(Shader.PropertyToID("_PhysicalParamBuffer"), _PhysicalParamBuffer);
            InitializePhysicalTileTexture();
            InitializeCompressionTexture();
            InitializeHeightMapTexture();
            ActivateDecals();
        }

        private void InitializePhysicalTileTexture()
        {
            RenderTextureDescriptor textureDescriptor = new RenderTextureDescriptor
            {
                width = m_tilePaddingSize,
                height = m_tilePaddingSize,
                dimension = TextureDimension.Tex2D,
                depthBufferBits = 0,
                volumeDepth = 1,
                colorFormat = RenderTextureFormat.ARGB32,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = false,
                bindMS = false,
                useDynamicScale = false,
                msaaSamples = 1
            };
            _physicalTileA = new RenderTexture(textureDescriptor);
            _physicalTileA.bindTextureMS = false;
            _physicalTileA.name = "PhysicsTextureA";
            _physicalTileA.wrapMode = TextureWrapMode.Repeat;
            _physicalTileA.Create();

            _physicalTileB = new RenderTexture(textureDescriptor);
            _physicalTileB.bindTextureMS = false;
            _physicalTileB.name = "PhysicsTextureB";
            _physicalTileB.wrapMode = TextureWrapMode.Repeat;
            _physicalTileB.Create();

            _physicalTextureIDs = new RenderTargetIdentifier[2];
            _physicalTextureIDs[0] = new RenderTargetIdentifier(_physicalTileA);
            _physicalTextureIDs[1] = new RenderTargetIdentifier(_physicalTileB);

            // _PhysicalTextureParams x:TextureSize(4224) y:tileSize(256) z:tileBorder(4) 
            Shader.SetGlobalVector("_PhysicalTextureParams",
                new Vector4(m_textureSize, m_tileSize, TileBorder, m_tilePaddingSize));

            /*
             * Used for RenderTile (We only render a quad-mesh)
             */
            var mat = new Matrix4x4
            {
                m00 = 2,
                m03 = -1,
                m11 = 2,
                m13 = -1,
                m23 = 1,
                m33 = 1
            };
            // Shader.SetGlobalMatrix("mat_identity", mat);
            Shader.SetGlobalMatrix("mat_identity", GL.GetGPUProjectionMatrix(mat, true));
        }

        private void InitializeCompressionTexture()
        {
            /*
             *  Initialize runtime compression texture 
             */
#if UNITY_ANDROID && !UNITY_EDITOR
            m_CompressFormat = GraphicsFormat.RGB_ETC2_UNorm;
            // m_CompressFormat = GraphicsFormat.RGBA_ASTC4X4_UNorm;
            
            runtimeCompressShader.DisableKeyword("_COMPRESS_BC3");
            runtimeCompressShader.EnableKeyword("_COMPRESS_ETC2");
            compressResultA = new RenderTexture(m_tilePaddingSize / 4, m_tilePaddingSize / 4,0)
            {
                graphicsFormat = GraphicsFormat.R16G16B16A16_UInt,
                // graphicsFormat = GraphicsFormat.R32G32B32A32_UInt,
                enableRandomWrite = true,
            };
            compressResultA.name = "compressResultA";
            compressResultB = new RenderTexture(m_tilePaddingSize / 4, m_tilePaddingSize / 4,0)
            {
                graphicsFormat = GraphicsFormat.R16G16B16A16_UInt,
                // graphicsFormat = GraphicsFormat.R32G32B32A32_UInt,
                enableRandomWrite = true,
            };
            compressResultB.name = "compressResultB";
#else
            m_CompressFormat = GraphicsFormat.RGBA_DXT5_UNorm;

            runtimeCompressShader.DisableKeyword("_COMPRESS_ETC2");
            runtimeCompressShader.EnableKeyword("_COMPRESS_BC3");
            compressResultA = new RenderTexture(m_tilePaddingSize / 4, m_tilePaddingSize / 4, 0)
            {
                graphicsFormat = GraphicsFormat.R32G32B32A32_UInt,
                enableRandomWrite = true,
            };
            compressResultB = new RenderTexture(m_tilePaddingSize / 4, m_tilePaddingSize / 4, 0)
            {
                graphicsFormat = GraphicsFormat.R32G32B32A32_UInt,
                enableRandomWrite = true,
            };
#endif

            /*
             * Create the PhysicalTextures of Compressed Version
             */
            _compressedPhysicalTextureA =
                new Texture2D(m_textureSize, m_textureSize, m_CompressFormat, TextureCreationFlags.None);
            _compressedPhysicalTextureA.name = "_compressedPhysicsTextureA";
            _compressedPhysicalTextureB =
                new Texture2D(m_textureSize, m_textureSize, m_CompressFormat, TextureCreationFlags.None);
            _compressedPhysicalTextureB.name = "_compressedPhysicsTextureB";
            _compressedPhysicalTextureA.Apply(true, true);
            _compressedPhysicalTextureB.Apply(true, true);

            /*
             * Create the PhysicalTextures of Uncompressed Version
             */
            _uncompressedPhysicalTextureA =
                new Texture2D(m_textureSize, m_textureSize, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
            _uncompressedPhysicalTextureA.name = "_uncompressedPhysicsTextureA";
            _uncompressedPhysicalTextureB =
                new Texture2D(m_textureSize, m_textureSize, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
            _uncompressedPhysicalTextureB.name = "_uncompressedPhysicsTextureB";
            _uncompressedPhysicalTextureA.Apply(true, true);
            _uncompressedPhysicalTextureB.Apply(true, true);
            Shader.SetGlobalTexture("_PhysicsTextureA",
                enableCompress ? _compressedPhysicalTextureA : _uncompressedPhysicalTextureA);
            Shader.SetGlobalTexture("_PhysicsTextureB",
                enableCompress ? _compressedPhysicalTextureB : _uncompressedPhysicalTextureB);
        }

        private void InitializeHeightMapTexture()
        {
            /*
             * Initialize and replace height map texture
             */
            var terrainData = m_terrain.terrainData;
            terrainHeightMapTexture = terrainData.heightmapTexture;
            Shader.SetGlobalVector(VirtualTextureRectId, m_virtualTextureRect);

            /* https://forum.unity.com/threads/terraindata-heightmaptexture-float-value-range.672421/ */
            // 0.5f represent the max height
            Shader.SetGlobalFloat(MaxHeightScaleId, 0.5f / terrainData.heightmapScale.y);

            Shader.SetGlobalFloat(HeightMapResolutionId, terrainData.heightmapResolution);

            if (BakeHeight)
            {
                originHeightMapTexture = new RenderTexture(terrainHeightMapTexture.descriptor);
                originHeightMapTexture.name = "origin HeightMap Texture";
                originHeightMapTexture.Create();
                Graphics.CopyTexture(terrainHeightMapTexture, originHeightMapTexture);

                Shader.SetGlobalTexture(OriginHeightMapId, originHeightMapTexture);
            }
            else
            {
                Shader.SetGlobalTexture(OriginHeightMapId, terrainHeightMapTexture);
            }
        }

        public void ActivateDecals()
        {
            var renderDecalHeightMat = new Material(Shader.Find("RVT/RenderDecalHeight"));
            foreach (var decal in m_Decals)
            {
                var renderer = decal.Renderer;
                if (BakeHeight)
                {
                    renderer.gameObject.SetActive(false);
                    cmd.SetRenderTarget(terrainHeightMapTexture);
                    var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null)
                    {
                        continue;
                    }

                    /*
                     * Render the height of model to HeightmapTexture
                     */
                    cmd.DrawMesh(mesh, renderer.transform.localToWorldMatrix, renderDecalHeightMat,
                        0, 0);
                }
                else
                {
                    renderer.material = decal.RenderDecalMaterial;
                    renderer.material.shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }

            Graphics.ExecuteCommandBuffer(cmd);

            // Note : refresh terrain LOD!!!
            var terrainData = m_terrain.terrainData;
            var region = new RectInt(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution);
            terrainData.DirtyHeightmapRegion(region, TerrainHeightmapSyncControl.HeightAndLod);

            /*
             * no need to Invoke the terrainData.SyncHeight()
             * https://docs.unity3d.com/cn/2022.3/ScriptReference/TerrainData.DirtyHeightmapRegion.html
             */
            // terrainData.SyncHeightmap();
            cmd.Clear();
        }

        /*
         * Compress: runtime compress a tile to ETC or DXT5 format using compute shader, and copy it to the physical texture. 
         */
        public void Compress(float2 physicalCoord)
        {
            int l = (int)physicalCoord.x * m_tilePaddingSize;
            int b = (int)physicalCoord.y * m_tilePaddingSize;
            /*
             * In Compress Version:
             *     Compress the PhysicalTile to the medium compressResult,
             *     then Copy to Actual PhysicalTexture
             * In non-Compress Version:
             *     Directly Copy to Actual PhysicalTexture
             */
            if (enableCompress)
            {
                runtimeCompressShader.SetInt("_Size", m_tilePaddingSize);
                runtimeCompressShader.SetTexture(0, "_SrcTexture", _physicalTileA);
                runtimeCompressShader.SetTexture(0, "_DstTexture", compressResultA);
                int size = m_tilePaddingSize / 4;
                int kernelIndex = runtimeCompressShader.FindKernel("Compress");
                runtimeCompressShader.Dispatch(kernelIndex, (size + 7) / 8, (size + 7) / 8, 1);
                Graphics.CopyTexture(compressResultA, 0, 0, 0, 0, size, size, _compressedPhysicalTextureA, 0, 0, l, b);
                runtimeCompressShader.SetInt("_Size", m_tilePaddingSize);
                runtimeCompressShader.SetTexture(0, "_SrcTexture", _physicalTileB);
                runtimeCompressShader.SetTexture(0, "_DstTexture", compressResultB);
                runtimeCompressShader.Dispatch(kernelIndex, (size + 7) / 8, (size + 7) / 8, 1);
                Graphics.CopyTexture(compressResultB, 0, 0, 0, 0, size, size, _compressedPhysicalTextureB, 0, 0, l, b);
            }
            else
            {
                Graphics.CopyTexture(_physicalTileA, 0, 0, 0, 0, m_tilePaddingSize, m_tilePaddingSize,
                    _uncompressedPhysicalTextureA, 0, 0, l, b);
                Graphics.CopyTexture(_physicalTileB, 0, 0, 0, 0, m_tilePaddingSize, m_tilePaddingSize,
                    _uncompressedPhysicalTextureB, 0, 0, l, b);
            }
        }

        /*
        * RenderTile: render a region of virtual texture (tile) to physical texture
        *
        * PhysicalCoord: Tile Coordinates on physical texture
        * VirtualCoord: Tile Coordinates on virtual texture
        */
        public void RenderTile(PageTableUpdateRequest req)
        {
            int mipLevel = req.Mip;
            float2 virtualCoord = req.VirtualCoord;
            float2 physicalCoord = req.PhysicalCoord;
            /*
             * For LOD0, regionSize = 1
             * For LOD1, regionSize = 2
             */
            float regionSize = (1 << mipLevel);

            // float posX = Mathf.Floor(virtualCoord.x / regionSize) * regionSize;
            // float posY = Mathf.Floor(virtualCoord.y / regionSize) * regionSize;
            float posX = virtualCoord.x;
            float posY = virtualCoord.y;

            /*
             * the offset of the uv
             * cuz the TileBorder(4), we need this param so that we can sample in the inner tile
            */
            float offsetX = (posX * m_tileSize - TileBorder * regionSize) / (m_pageNum * m_tileSize);
            float offsetY = (posY * m_tileSize - TileBorder * regionSize) / (m_pageNum * m_tileSize);

            // float scale = (regionSize / m_pageNum) * ((int)m_tilePaddingSize / (int)m_tileSize);
            /*
             * the scale of the uv
             * such as in LOD0, we scale the initial mesh uv [0,1] to [0, 1 / regionSize(256)] (border)
             */
            float scale = regionSize * m_tilePaddingSize / (m_pageNum * m_tileSize);

            var terrainData = m_terrain.terrainData;
            var layerIndex = 0;

            /* the transform uv */
            m_renderTileMaterial.SetVector(TransformUVId, new Vector4(offsetX, offsetY, scale, m_textureTilingScale));

            for (var layer = 0; layer < terrainData.alphamapTextures.Length; layer++)
            {
                m_renderTileMaterial.SetTexture(ControlMapId, terrainData.alphamapTextures[layer]);
                for (int subIndex = 0; subIndex < 4; subIndex++)
                {
                    m_renderTileMaterial.SetTexture($"_Diffuse{subIndex}",
                        terrainData.terrainLayers[layerIndex].diffuseTexture);
                    m_renderTileMaterial.SetTexture($"_Normal{subIndex}",
                        terrainData.terrainLayers[layerIndex].normalMapTexture);
                    layerIndex++;
                    if (layerIndex >= terrainData.terrainLayers.Length)
                    {
                        break;
                    }
                }

                var shaderPass = layer > 0 ? 2 : 1; // base pass (not instancing) : 1; add pass : 2
                cmd.SetRenderTarget(_physicalTextureIDs, _physicalTextureIDs[0]);
                cmd.DrawMesh(m_tileMesh, Matrix4x4.identity, m_renderTileMaterial, 0, shaderPass);
                Graphics.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            var tileRect = new Rect(posX / m_tilesPerMeter, posY / m_tilesPerMeter, regionSize / m_tilesPerMeter,
                regionSize / m_tilesPerMeter);
            var tileRectVec = new Vector4(tileRect.x, tileRect.y, 1 / tileRect.width, 1 / tileRect.height);
            cmd.SetRenderTarget(_physicalTextureIDs, _physicalTextureIDs[0]);
            cmd.SetGlobalVector("_VirtualTextureTileRect", tileRectVec);
            var matBlock = new MaterialPropertyBlock();
            foreach (var decal in m_Decals)
            {
                var renderer = decal.Renderer;
                var bounds = renderer.bounds;
                var boundSize = new Vector2(bounds.size.x, bounds.size.z);
                var boundPosition = new Vector2(bounds.center.x, bounds.center.z) - boundSize / 2;
                var rendererRect = new Rect(boundPosition, boundSize);

                var blendScale = 1.0f / bounds.size.y * 10.0f;
                var blendBias = 0.2f;
                matBlock.SetFloat(BlendScaleId, blendScale);
                matBlock.SetFloat(BlendBiasId, blendBias);
                if (rendererRect.Overlaps(tileRect))
                {
                    cmd.DrawMesh(decal.Mesh, renderer.transform.localToWorldMatrix, decal.RenderDecalMaterial, 0, 0,
                        matBlock); // RenderDecal
                }

                matBlock.Clear();
            }

            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            Compress(physicalCoord);
        }

        /*
         * RenderTileInstanced not used in mobile version
         */
        public void RenderTileInstanced(CommandBuffer cmd, int updatesNum, NativeArray<PageTableUpdateRequest> requests)
        {
            // CommandBuffer cmd = new CommandBuffer();
            // cmd.name = "RenderTile";
            cmd.SetRenderTarget(_physicalTextureIDs, _physicalTextureIDs[0]);

            Profiler.BeginSample("Prepare Params for Physical Texture");

            for (int i = 0; i < updatesNum; i++)
            {
                float2 virtualCoord = requests[i].VirtualCoord;
                float2 physicalCoord = requests[i].PhysicalCoord;
                int mipLevel = requests[i].Mip;
                float regionSize = (1 << mipLevel);
                float posX = Mathf.Floor(virtualCoord.x / regionSize) * regionSize;
                float posY = Mathf.Floor(virtualCoord.y / regionSize) * regionSize;

                float offsetX = (posX * m_tileSize - TileBorder * regionSize) / (m_pageNum * m_tileSize);
                float offsetY = (posY * m_tileSize - TileBorder * regionSize) / (m_pageNum * m_tileSize);
                ;
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
                    m23 = 1,
                    m33 = 1
                };
                mvps[i] = GL.GetGPUProjectionMatrix(mat, true);
                transformUVs[i] = new float4(offsetX, offsetY, scale, m_textureTilingScale);
            }

            MaterialPropertyBlock matBlock = new MaterialPropertyBlock();
            matBlock.SetVectorArray(TransformUVId, transformUVs);
            matBlock.SetMatrixArray(MatrixMvpId, mvps);
            cmd.DrawMeshInstanced(m_tileMesh, 0, m_renderTileMaterial, 0, mvps, updatesNum, matBlock);
            Profiler.EndSample();
        }

        public void Dispose()
        {
            _physicalTileA.Release();
            _physicalTileB.Release();
            compressResultA.Release();
            compressResultB.Release();
            UnityEngine.Object.Destroy(_compressedPhysicalTextureA);
            UnityEngine.Object.Destroy(_compressedPhysicalTextureB);

            if (BakeHeight && originHeightMapTexture != null)
            {
                RenderTexture.active = originHeightMapTexture;
                var terrainData = m_terrain.terrainData;
                var region = new RectInt(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution);
                terrainData.CopyActiveRenderTextureToHeightmap(region, new Vector2Int(0, 0),
                    TerrainHeightmapSyncControl.HeightAndLod);
                /*
                 * no need to Invoke the terrainData.SyncHeight()
                 * https://docs.unity3d.com/cn/2022.3/ScriptReference/TerrainData.DirtyHeightmapRegion.html
                 */
                // terrainData.SyncHeightmap();
                RenderTexture.active = null;
                originHeightMapTexture.Release();
                originHeightMapTexture = null;
            }

            foreach (var decal in m_Decals)
            {
                var renderer = decal.Renderer;
                renderer.gameObject.SetActive(true);
                renderer.material = decal.RenderDecalMaterial;
                renderer.material.shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            // _PhysicalParamBuffer.Release();
        }
    }
}