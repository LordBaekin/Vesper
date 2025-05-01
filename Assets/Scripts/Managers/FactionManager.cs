// Assets/Scripts/Managers/FactionManager.cs
using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
public class FactionManager : MonoBehaviour
{
    public static FactionManager Instance { get; private set; }

    [Header("Relationships")]
    [Tooltip("For each faction, which other factions it considers hostile.")]
    public List<FactionRelationship> relationships = new();

    [Header("Player Reputation")]
    [Tooltip("Numeric reputation for the Player with each faction.")]
    public List<FactionReputation> initialReputations = new();
    private Dictionary<Faction, int> playerReputation;

    [System.Serializable]
    public struct FactionRelationship
    {
        public Faction faction;
        public List<Faction> hostileTo;
    }

    [System.Serializable]
    public struct FactionReputation
    {
        public Faction faction;
        public int startingValue;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // initialize reputation dictionary
        playerReputation = new Dictionary<Faction, int>();
        foreach (var rep in initialReputations)
            playerReputation[rep.faction] = rep.startingValue;
    }

    /// <summary>
    /// Returns true if 'source' considers 'target' hostile.
    /// </summary>
    public bool IsHostile(Faction source, Faction target)
    {
        var rel = relationships.Find(r => r.faction == source);
        return (rel.hostileTo != null && rel.hostileTo.Contains(target));
    }

    /// <summary>
    /// Notify all NPCs of targetFaction that they've been attacked by attackerFaction.
    /// </summary>
    public void NotifyAttack(Faction attackerFaction, Faction targetFaction)
    {
        var all = Object.FindObjectsByType<NPCState>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        foreach (var npc in all)
        {
            if (npc.faction == targetFaction)
                npc.OnAttackedBy(attackerFaction);
        }
    }

    /// <summary>
    /// Adjusts player reputation and updates NPC allegiances.
    /// </summary>
    public void ModifyReputation(Faction faction, int delta)
    {
        if (!playerReputation.ContainsKey(faction))
            playerReputation[faction] = 0;

        playerReputation[faction] =
            Mathf.Clamp(playerReputation[faction] + delta, -100, 100);

        // broadcast to NPCs of that faction
        var all = Object.FindObjectsByType<NPCState>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        foreach (var npc in all)
        {
            if (npc.faction == faction)
                npc.OnFactionChanged(Faction.Player);
        }
    }

    /// <summary>
    /// Gets current player reputation with a faction (0 if none).
    /// </summary>
    public int GetReputation(Faction faction)
    {
        return playerReputation.TryGetValue(faction, out var v) ? v : 0;
    }
}