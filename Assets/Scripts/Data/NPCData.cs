// Assets/Scripts/Data/NPCData.cs
using UnityEngine;

[CreateAssetMenu(menuName = "NPC/NPC Data")]
public class NPCData : ScriptableObject
{
    public string displayName;
    public float maxHealth = 100f;      // new
    public float moveSpeed;
    public GameObject modelPrefab;
    // Combat settings
    public bool isCombative;
    public float detectionRadius;
    public float attackDamage;
    public float attackCooldown;
    // Dialogue / friendly settings
    public string[] dialogueLines;
}
