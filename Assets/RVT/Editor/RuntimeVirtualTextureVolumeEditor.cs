using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace RuntimeVirtualTexture
{
    [CustomEditor(typeof(RuntimeVirtualTextureVolume))]
    public class RuntimeVirtualTextureVolumeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (Application.isPlaying)
            {
                var rvt = (RuntimeVirtualTextureVolume)target;
                DrawTexture(rvt.physicalTextureManager._physicalTileA, "TileA");
                DrawTexture(rvt.physicalTextureManager._physicalTileB, "TileB");
                DrawTexture(rvt.physicalTextureManager.PhysicalTextureA, "PhysicalTextureA");
                DrawTexture(rvt.physicalTextureManager.PhysicalTextureB, "PhysicalTextureB");
                DrawTexture(rvt.pageTableManager._pageTableTexture, "PageTableTexture");
            }
            else
            {
                base.OnInspectorGUI();
            }
        }

        public static void DrawTexture(Texture texture, string label = null)
        {
#if UNITY_EDITOR
            if (texture == null)
                return;

            EditorGUILayout.Space();
            if (!string.IsNullOrEmpty(label)) EditorGUILayout.LabelField(label);
            EditorGUILayout.LabelField($"Size: {texture.width} X {texture.height}");
            EditorGUI.DrawPreviewTexture(GUILayoutUtility.GetAspectRect(texture.width / (float)texture.height),
                texture);
#endif
        }
    }
}