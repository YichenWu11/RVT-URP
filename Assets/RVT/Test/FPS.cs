using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class FPS : MonoBehaviour
{
    public Text display;
    float deltaTime = 0.0f;

    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 300;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();

        Application.targetFrameRate = -1;

        /* calculate the dps */
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        float fps = 1.0f / deltaTime;
        display.text = new string($"FPS: {Mathf.FloorToInt(fps)}");
    }
}