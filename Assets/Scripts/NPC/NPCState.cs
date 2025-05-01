// Assets/Scripts/NPC/NPCState.cs
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("NPC/State")]
public class NPCState : MonoBehaviour
{
    [Header("Identity")]
    public Faction faction = Faction.Neutral;
    public bool isCombative = false;
    public List<Faction> hostileTo;  // which factions this NPC auto‐attacks

    /// <summary>
    /// Called when this NPC is damaged or witnesses an attack.
    /// </summary>
    public void OnAttackedBy(Faction attackerFaction)
    {
        if (!isCombative &&
            (attackerFaction != faction || hostileTo.Contains(attackerFaction)))
        {
            isCombative = true;
        }
    }

    /// <summary>
    /// Called when this NPC’s own faction changes.
    /// </summary>
    public void OnFactionChanged(Faction newFaction)
    {
        faction = newFaction;
        isCombative = hostileTo.Contains(faction);
    }
}
