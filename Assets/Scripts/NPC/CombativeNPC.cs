// Assets/Scripts/NPC/CombativeNPC.cs
using UnityEngine;

public class CombativeNPC : BaseNPC
{
    private float lastAttackTime;
    private Transform player;

    protected override void Start()
    {
        base.Start();
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
            player = go.transform;
    }

    protected override void CombativeTick()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist < data.detectionRadius)
        {
            Vector3 dir = (player.position - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            cc.Move(dir * data.moveSpeed * Time.deltaTime);

            if (Time.time > lastAttackTime + data.attackCooldown)
            {
                Attack();
                lastAttackTime = Time.time;
            }
        }
        // apply gravity
        if (!cc.isGrounded)
            cc.Move(Vector3.down * Mathf.Abs(Physics.gravity.y) * Time.deltaTime);
    }

    protected override void NonCombativeTick()
    {
        // never called for combative NPCs
    }

    private void Attack()
    {
        Debug.Log($"{data.displayName} attacks for {data.attackDamage}!");
        // TODO: damage the player here, then:
        FactionManager.Instance.NotifyAttack(state.faction, Faction.Player);
    }
}