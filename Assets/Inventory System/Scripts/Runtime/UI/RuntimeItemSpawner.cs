using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DevionGames.InventorySystem;

public class RuntimeItemSpawnerUI : MonoBehaviour
{
    public Dropdown itemDropdown;
    public Button spawnButton;
    public Transform player;
    public GameObject panel; // 👈 Assign this in Inspector (root panel of the UI)
    public float spawnDistance = 2f;

    private List<string> itemNames = new List<string>();

    void Start()
    {
        if (InventoryManager.Database == null)
        {
            Debug.LogError("InventoryManager.Database is not assigned!");
            return;
        }

        itemNames.Clear();
        foreach (Item item in InventoryManager.Database.items)
        {
            if (InventoryManager.GetPrefab(item.Name) != null)
            {
                itemNames.Add(item.Name);
            }
        }

        itemDropdown.ClearOptions();
        itemDropdown.AddOptions(itemNames);

        spawnButton.onClick.AddListener(SpawnSelectedItem);
    }

    void Update()
    {
        // Toggle panel with CTRL + I
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.I))
        {
            panel.SetActive(!panel.activeSelf);
        }
    }

    void SpawnSelectedItem()
    {
        string selectedItem = itemDropdown.options[itemDropdown.value].text;
        GameObject prefab = InventoryManager.GetPrefab(selectedItem);

        if (prefab == null)
        {
            Debug.LogWarning($"❌ Could not find prefab for item '{selectedItem}'.");
            return;
        }

        Vector3 spawnPosition = player.position + player.forward * spawnDistance;
        GameObject spawned = InventoryManager.Instantiate(prefab, spawnPosition, Quaternion.identity);
        spawned.name = selectedItem;

        Debug.Log($"✅ Spawned {selectedItem} at {spawnPosition}");
    }
}
