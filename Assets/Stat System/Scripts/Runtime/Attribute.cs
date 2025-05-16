using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DevionGames.StatSystem
{
    public class Attribute : Stat
    {
        public System.Action onCurrentValueChange;

        [SerializeField]
        [Range(0f, 1f)]
        protected float m_StartCurrentValue = 1f;

        protected float m_CurrentValue;
        public float CurrentValue
        {
            get => this.m_CurrentValue;
            set
            {
                float oldValue = this.m_CurrentValue;
                float newValue = Mathf.Clamp(value, 0, Value);
                if (oldValue != newValue)
                {
                    Debug.Log($"[Attribute] '{Name}' CurrentValue changing: {oldValue} → {newValue} (Threshold={Value})");
                    this.m_CurrentValue = newValue;
                    onCurrentValueChange?.Invoke();
                    Debug.Log($"[Attribute] '{Name}' CurrentValue now: {this.m_CurrentValue}");
                }
            }
        }


        public override void Initialize(StatsHandler handler, StatOverride statOverride)
        {
            base.Initialize(handler, statOverride);
            onValueChange += () =>
            {
                CurrentValue = Mathf.Clamp(CurrentValue, 0f, Value);
            };
        }

        public override void ApplyStartValues()
        {
            base.ApplyStartValues();
            this.m_CurrentValue = this.m_Value * this.m_StartCurrentValue;
        }

        public override string ToString()
        {
            return this.m_StatName + ": " + this.CurrentValue.ToString() + "/" + this.Value.ToString();
        }

        public override void GetObjectData(Dictionary<string, object> data)
        {
            base.GetObjectData(data);
            data.Add("CurrentValue", this.m_CurrentValue);
        }

        public override void SetObjectData(Dictionary<string, object> data)
        {
            base.SetObjectData(data);
            this.m_CurrentValue = System.Convert.ToSingle(data["CurrentValue"]);
            CalculateValue(false);
        }
    }
}