// Assets/Scripts/Combat/Health.cs
using UnityEngine;

[RequireComponent(typeof(NPCState))]
public class Health : MonoBehaviour
{
    public float maxHealth = 100f;      // default, will override from NPCData
    private float currentHealth;
    private NPCState state;

    void Awake()
    {
        state = GetComponent<NPCState>();
        // if NPCData exists, pull its maxHealth
        var baseNpc = GetComponent<BaseNPC>();
        if (baseNpc != null && baseNpc.data != null)
            maxHealth = baseNpc.data.maxHealth;
        currentHealth = maxHealth;
    }

    /// <summary>Damages this entity. If it hits zero, fires death.</summary>
    public void ApplyDamage(float amount, Faction attackerFaction)
    {
        currentHealth -= amount;
        if (currentHealth <= 0f)
            Die(attackerFaction);
    }

    private void Die(Faction attackerFaction)
    {
        // let FactionManager switch any allies/enemies on kill
        if (state != null)
            FactionManager.Instance.NotifyAttack(attackerFaction, state.faction);

        // destroy this NPC—SpawnManager will respawn it automatically
        Destroy(gameObject);
    }
}
