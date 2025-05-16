using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class FullBrokenScriptScanner : EditorWindow
{
    [MenuItem("Tools/Devion Games/Scan for Broken Scripts (Scene + Project)")]
    public static void ScanAll()
    {
        int sceneCount = ScanSceneObjects();
        int prefabCount = ScanPrefabs();
        int soCount = ScanScriptableObjects();

        Debug.Log($"✅ Scan Complete.\nScene: {sceneCount} broken scripts\nPrefabs: {prefabCount}\nScriptableObjects: {soCount}");
    }

    private static int ScanSceneObjects()
    {
        int brokenCount = 0;
        GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject go in sceneObjects)
        {
            MonoBehaviour[] components = go.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour comp in components)
            {
                if (comp == null)
                {
                    Debug.LogError($"🛑 MISSING script in scene on: {GetFullPath(go)}", go);
                    brokenCount++;
                }
            }
        }
        return brokenCount;
    }

    private static int ScanPrefabs()
    {
        int brokenCount = 0;
        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab");

        foreach (string guid in prefabPaths)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            MonoBehaviour[] components = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour comp in components)
            {
                if (comp == null)
                {
                    Debug.LogError($"🧱 MISSING script in prefab: {path}", prefab);
                    brokenCount++;
                    break;
                }
            }
        }
        return brokenCount;
    }

    private static int ScanScriptableObjects()
    {
        int brokenCount = 0;
        string[] soGUIDs = AssetDatabase.FindAssets("t:ScriptableObject");

        foreach (string guid in soGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj == null)
            {
                Debug.LogError($"📦 MISSING ScriptableObject at: {path}");
                brokenCount++;
            }
        }
        return brokenCount;
    }

    private static string GetFullPath(GameObject obj)
    {
        return obj.transform.parent == null
            ? obj.name
            : GetFullPath(obj.transform.parent.gameObject) + "/" + obj.name;
    }
}
