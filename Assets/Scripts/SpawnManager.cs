using System.Collections;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [Header("Prefabs & Settings")]
    public GameObject[] npcPrefabs;      // drag in your capsule or model prefabs
    public int initialCount = 5;
    public float spawnRadius = 8f;
    public float respawnDelay = 10f;

    [Header("Physics Settings")]
    public float rayStartHeight = 20f;   // how high above the spawn XZ you cast the ray
    public CollisionDetectionMode collisionMode = CollisionDetectionMode.Continuous;

    void Start()
    {
        for (int i = 0; i < initialCount; i++)
            SpawnOne();
    }

    void SpawnOne()
    {
        if (npcPrefabs == null || npcPrefabs.Length == 0)
        {
            Debug.LogError("❌ SpawnManager: npcPrefabs is empty or null. Assign prefabs in the Inspector.");
            return;
        }

        // pick a random prefab
        GameObject prefab = npcPrefabs[Random.Range(0, npcPrefabs.Length)];

        // random point in XZ plane
        Vector2 rnd = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnXZ = new Vector3(transform.position.x + rnd.x,
                                      0f,
                                      transform.position.z + rnd.y);

        // cast down from above to find ground
        Vector3 rayOrigin = spawnXZ + Vector3.up * rayStartHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, Mathf.Infinity))
        {
            // compute spawn Y so bottom of collider sits on hit.point
            float yOffset = 0.5f;
            Collider prefabCol = prefab.GetComponent<Collider>();
            if (prefabCol != null)
                yOffset = prefabCol.bounds.extents.y;

            Vector3 spawnPos = hit.point + Vector3.up * yOffset;

            // instantiate
            GameObject npc = Instantiate(prefab, spawnPos, Quaternion.identity);

            // ensure it has a collider
            if (npc.GetComponent<Collider>() == null)
                npc.AddComponent<CapsuleCollider>();

            // ensure it has a Rigidbody
            Rigidbody rb = npc.GetComponent<Rigidbody>();
            if (rb == null)
                rb = npc.AddComponent<Rigidbody>();

            rb.useGravity = true;
            rb.collisionDetectionMode = collisionMode;
            rb.constraints = RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationZ;

            // start respawn watcher
            StartCoroutine(HandleRespawn(npc, prefab));
        }
        else
        {
            Debug.LogWarning("⚠️ SpawnManager: No ground found under spawn point.");
        }
    }


    IEnumerator HandleRespawn(GameObject instance, GameObject prefab)
    {
        // wait until destroyed
        while (instance != null)
            yield return null;

        // wait extra time, then spawn a fresh one
        yield return new WaitForSeconds(respawnDelay);
        SpawnOne();
    }
}
