using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DevionGames.StatSystem
{
    public static class StatSystemMenu
    {
        [MenuItem("Tools/Devion Games/Stat System/Editor", false, 0)]
        private static void OpenItemEditor()
        {
            StatSystemEditor.ShowWindow();
        }

        [MenuItem("Tools/Devion Games/Stat System/Create Stats Manager", false, 1)]
        private static void CreateStatManager()
        {
            GameObject go = new GameObject("Stats Manager");
            go.AddComponent<StatsManager>();
            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/Devion Games/Stat System/Create Stats Manager", true)]
        private static bool ValidateCreateStatusSystem()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<StatsManager>() == null;
#else
            return UnityEngine.Object.FindObjectOfType<StatsManager>() == null;
#endif
        }
    }
}
