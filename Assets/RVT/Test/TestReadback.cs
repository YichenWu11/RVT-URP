using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class TestReadback : MonoBehaviour
{
    public Texture2D tex;
    private ComputeBuffer buffer;
    public ComputeShader computeShader;

    // Start is called before the first frame update
    void Start()
    {
        tex = new Texture2D(2, 2, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
        Color[] colors = new Color[4];
        colors[0] = new Color(0, 0, 0, 1);
        colors[1] = new Color(1, 0, 0, 1);
        colors[2] = new Color(0, 1, 0, 1);
        colors[3] = new Color(0, 0, 1, 1);
        tex.SetPixels(colors);
        tex.Apply();

        buffer = new ComputeBuffer(4, 4);
        uint[] data = new uint[4];
        // data[0] = 0;
        // data[1] = 1;
        // data[2] = 2;
        // data[3] = 3;
        // buffer.SetData(data);

        Shader.SetGlobalBuffer(Shader.PropertyToID("_Buffer"), buffer);
        var cmd = new CommandBuffer();
        cmd.SetRandomWriteTarget(1, buffer, true);
        Graphics.ExecuteCommandBuffer(cmd);
    }

    // Update is called once per frame
    void Update()
    {
        // AsyncGPUReadback.Request(tex, callback: ReadbackCallback0).WaitForCompletion();
        AsyncGPUReadback.Request(buffer, callback: ReadbackCallback1);
    }

    void ReadbackCallback0(AsyncGPUReadbackRequest request)
    {
        if (request is { done: true, hasError: false })
        {
            var data = request.GetData<Color32>();
            Color32 tmp = data[0];
            Debug.Log($"{data[0]} {data[1]} {data[2]} {data[3]}");
        }
    }

    void ReadbackCallback1(AsyncGPUReadbackRequest request)
    {
        if (request is { done: true, hasError: false })
        {
            var data = request.GetData<uint>();
            uint tmp = data[0];
            Debug.Log($"{data[0]} {data[1]} {data[2]} {data[3]}");
        }
    }

    private void OnDisable()
    {
        Destroy(tex);
        buffer.Release();
    }
}