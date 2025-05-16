using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class FindBrokenCallbackHandlers
{
    [MenuItem("Tools/DevionGames/Find Broken CallbackHandlers")]
    static void FindAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:GameObject");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!go) continue;

            var handlers = go.GetComponentsInChildren<DevionGames.CallbackHandler>(true);
            foreach (var handler in handlers)
            {
                SerializedObject so = new SerializedObject(handler);
                if (so == null || handler == null)
                {
                    Debug.LogWarning($"❌ Broken CallbackHandler in prefab: {path}", go);
                }
            }
        }
        Debug.Log("✅ Scan complete.");
    }
}