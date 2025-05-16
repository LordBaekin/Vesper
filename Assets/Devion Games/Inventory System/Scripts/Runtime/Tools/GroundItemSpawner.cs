using UnityEngine;
using DevionGames;
using System.Collections.Generic;
using DevionGames.InventorySystem;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

public class GroundItemSpawner : MonoBehaviour
{
    public string selectedItemName = "";
    public Vector3 spawnOffset = Vector3.up;
    public Transform spawnPoint;
    public float raycastHeight = 5f;
    public float maxRaycastDistance = 20f;
    public LayerMask groundLayer = ~0;

    [ContextMenu("Spawn Selected Item")]
    public void SpawnItem()
    {
        if (string.IsNullOrEmpty(selectedItemName))
        {
            Debug.LogWarning("❌ No item selected.");
            return;
        }

        GameObject prefab;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // Load prefab manually in edit mode
            string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
            if (guids.Length == 0)
            {
                Debug.LogWarning("❌ No ItemDatabase found.");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);

            if (db == null)
            {
                Debug.LogWarning("❌ Failed to load ItemDatabase.");
                return;
            }

            Item item = db.items.Find(i => i.Name == selectedItemName);
            prefab = item?.Prefab;

            if (prefab == null)
            {
                Debug.LogWarning($"❌ Prefab not found for item '{selectedItemName}' in ItemDatabase.");
                return;
            }
        }
        else
#endif
        {
            // In play mode, use InventoryManager (it is safe here)
            prefab = InventoryManager.GetPrefab(selectedItemName);

            if (prefab == null)
            {
                Debug.LogWarning($"❌ Could not find prefab for item '{selectedItemName}' via InventoryManager.");
                return;
            }
        }

        // Positioning logic (same in both modes)
        Vector3 basePosition = (spawnPoint != null ? spawnPoint.position : transform.position) + spawnOffset;
        Vector3 rayOrigin = basePosition + Vector3.up * raycastHeight;
        Vector3 spawnPos = basePosition;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxRaycastDistance, groundLayer))
        {
            spawnPos = hit.point;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject spawned = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            spawned.transform.position = spawnPos;
            Undo.RegisterCreatedObjectUndo(spawned, "Spawn Ground Item (Edit Mode)");
            Debug.Log($"✅ Spawned '{selectedItemName}' in Edit Mode at {spawnPos}", spawned);
        }
        else
#endif
        {
            GameObject spawned = InventoryManager.Instantiate(prefab, spawnPos, Quaternion.identity);
            spawned.name = selectedItemName;
            Debug.Log($"✅ Spawned '{selectedItemName}' in Play Mode at {spawnPos}", spawned);
        }
    }

}

#if UNITY_EDITOR
[CustomEditor(typeof(GroundItemSpawner))]
public class GroundItemSpawnerEditor : Editor
{
    private string[] itemNames;
    private SerializedProperty selectedItemName;

    private void OnEnable()
    {
        selectedItemName = serializedObject.FindProperty("selectedItemName");
        itemNames = LoadItemNamesDirectly();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        GroundItemSpawner spawner = (GroundItemSpawner)target;

        if (itemNames == null || itemNames.Length == 0)
        {
            EditorGUILayout.HelpBox("⚠️ No items found. Check if ItemDatabase exists.", MessageType.Warning);
        }
        else
        {
            int index = Mathf.Max(0, System.Array.IndexOf(itemNames, selectedItemName.stringValue));
            int newIndex = EditorGUILayout.Popup("Item To Spawn", index, itemNames);
            selectedItemName.stringValue = itemNames[newIndex];
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnPoint"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("raycastHeight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxRaycastDistance"));
        LayerMaskField(serializedObject.FindProperty("groundLayer"));

        if (GUILayout.Button("Spawn Now"))
        {
            spawner.SpawnItem();
        }

        EditorGUILayout.HelpBox("Right-click → 'Spawn Selected Item' also works.", MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    private string[] LoadItemNamesDirectly()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
        if (guids.Length == 0)
            return new string[0];

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);

        if (db == null || db.items == null)
            return new string[0];

        List<string> names = new List<string>();
        foreach (Item item in db.items)
        {
            if (!string.IsNullOrEmpty(item.Name) && item.Prefab != null)
                names.Add(item.Name);
        }

        return names.ToArray();
    }

    private void LayerMaskField(SerializedProperty property)
    {
        var layers = InternalEditorUtility.layers;
        property.intValue = EditorGUILayout.MaskField("Ground Layer", property.intValue, layers);
    }
}
#endif
