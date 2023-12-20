using UnityEngine;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditor(typeof(UIPosSwitch))]
public class UIPosSwitchEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Switch"))
        {
            UIPosSwitch posSwitch = (UIPosSwitch)target;
            posSwitch.SwitchUIPos();
        }
    }
}