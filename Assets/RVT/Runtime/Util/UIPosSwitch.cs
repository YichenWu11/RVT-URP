using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPosSwitch : MonoBehaviour
{
    public List<Vector2> uiPosListsWin; // windows
    public List<Vector2> uiPosListsAnd; // android
    public List<RectTransform> canvasTransformList;

    void Start()
    {
        SwitchUIPos();
    }

    public void SwitchUIPos()
    {
#if UNITY_ANDROID
        for (int i = 0; i < uiPosListsAnd.Count; ++i)
        {
            canvasTransformList[i].anchoredPosition = uiPosListsAnd[i];
        }
#else
        for (int i = 0; i < uiPosListsWin.Count; ++i)
        {
            canvasTransformList[i].anchoredPosition = uiPosListsWin[i];
        }
#endif
    }
}