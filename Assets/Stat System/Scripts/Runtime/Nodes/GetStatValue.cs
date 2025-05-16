using System;
using UnityEngine;

namespace DevionGames.Graphs
{
    [ComponentMenu("Stat System/Get Stat Value")]
    [Serializable]
    public class GetStatValue : StatNode
    {
        public override object OnRequestValue(Port port)
        {
            try
            {
                if (statValue == null)
                {
                    Debug.LogWarning($"[GetStatValue] stat “{stat}” not found on StatsHandler—returning 0.");
                    return 0f;
                }
                return statValue.Value;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GetStatValue] suppressed exception: {e.GetType().Name}: {e.Message}");
                return 0f;
            }
        }
    }
}
