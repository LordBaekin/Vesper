using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DevionGames.StatSystem
{
    public class SelectableUIStat : UIStat
    {
        protected override StatsHandler GetStatsHandler()
        {
            if (SelectableObject.Current != null)
            {
                return SelectableObject.Current.GetComponent<StatsHandler>();

            }
            return null;
        }
    }
}