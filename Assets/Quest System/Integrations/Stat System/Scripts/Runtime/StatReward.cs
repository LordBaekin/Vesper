using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DevionGames.StatSystem;
using UnityEngine.UI;

namespace DevionGames.QuestSystem.Integrations.StatSystem
{
    public class StatReward : Reward
    {
        [SerializeField]
        private string m_StatsHandler = "Player Stats";
        [SerializeField]
        private string m_StatName = "Exp";
        [SerializeField]
        private float m_Value = 35f;
        [SerializeField]
        private StatRewardType m_RewardType = StatRewardType.CurrentValue;
        public override bool GiveReward()
        {
            Debug.Log($"[StatReward] → GiveReward called: Handler='{m_StatsHandler}', Stat='{m_StatName}', Value={m_Value}, Type={m_RewardType}");

            StatsHandler handler = StatsManager.GetStatsHandler(this.m_StatsHandler);
            if (handler is null)
            {
                Debug.LogError($"[StatReward]   ✗ No StatsHandler named '{m_StatsHandler}' found!");
                return false;
            }
            Stat stat = handler.GetStat(this.m_StatName);
            if (stat == null)
            {
                Debug.LogError($"[StatReward]   ✗ Handler '{m_StatsHandler}' has no stat '{m_StatName}'");
                return false;
            }

            switch (this.m_RewardType)
            {
                case StatRewardType.CurrentValue:
                    Debug.Log($"[StatReward]   Applying to CurrentValue: +{m_Value}");
                    handler.ApplyDamage(this.m_StatName, -this.m_Value);
                    break;
                case StatRewardType.Value:
                    Debug.Log($"[StatReward]   Applying to BaseValue: +{m_Value}");
                    stat.Add(this.m_Value);
                    break;
            }

            // Log post‐award state
            var attr = stat as DevionGames.StatSystem.Attribute;
            if (attr != null)
            {
                Debug.Log($"[StatReward] ← After GiveReward: Exp.CurrentValue={attr.CurrentValue} / Exp.Value={attr.Value}");
            }
            else
            {
                Debug.Log($"[StatReward] ← After GiveReward: Stat.Value={stat.Value}");
            }

            return true;
        }


        public override void DisplayReward(GameObject reward)
        {
            Text text = reward.GetComponent<Text>();

            text.text = (this.m_Value > 0 ? "+" : "-") + Mathf.Abs(this.m_Value).ToString() + " " + this.m_StatName;
        }

        public enum StatRewardType { 
            CurrentValue,
            Value
        }
    }
}