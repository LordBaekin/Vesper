using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DevionGames.StatSystem
{
    [System.Serializable]
    public class Level : Stat
    {
        [StatPicker]
        [SerializeField]
        protected Attribute m_Experience;

        public override void Initialize(StatsHandler handler, StatOverride statOverride)
        {
            base.Initialize(handler, statOverride);
            this.m_Experience = handler.GetStat(this.m_Experience.Name) as Attribute;

            // Log the subscription so you know it’s wired up
            Debug.Log($"[Level] Initialized. Listening for Exp changes on '{handler.HandlerName}'");

            this.m_Experience.onCurrentValueChange += () =>
            {
                // Log every time Exp changes
                Debug.Log($"[Level] Exp changed: {m_Experience.CurrentValue} / {m_Experience.Value}");

                if (m_Experience.CurrentValue >= m_Experience.Value)
                {
                    Debug.Log("[Level] Threshold reached → leveling up!");
                    // Reset Exp
                    m_Experience.CurrentValue = 0f;
                    // Bump level
                    Add(1f);
                    Debug.Log($"[Level] New Level = {Value}; Exp reset to {m_Experience.CurrentValue}");
                }
            };
        }
    }
}