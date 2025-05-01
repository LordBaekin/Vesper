// Assets/Scripts/NPC/BaseNPC.cs
using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(NPCState))]
public abstract class BaseNPC : MonoBehaviour
{
    [Header("Core")]
    public NPCData data;             // assign your NPCData ScriptableObject

    protected NPCState state;
    protected CharacterController cc;

    protected virtual void Awake()
    {
        state = GetComponent<NPCState>();
        cc = GetComponent<CharacterController>();
    }

    protected virtual void Start()
    {
        if (data != null && data.modelPrefab != null)
        {
            var model = Instantiate(data.modelPrefab, transform);
            model.transform.localPosition = Vector3.zero;
        }
    }

    private void Update()
    {
        if (state.isCombative)
            CombativeTick();
        else
            NonCombativeTick();
    }

    /// <summary>Enemy‐style logic: chasing, attacking, etc.</summary>
    protected abstract void CombativeTick();

    /// <summary>Idle/patrol/dialogue logic.</summary>
    protected abstract void NonCombativeTick();
}