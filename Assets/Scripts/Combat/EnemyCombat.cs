// Assets/Scripts/Combat/EnemyCombat.cs
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    public float attackRange;
    public float attackDamage;
    public float attackCooldown;

    private Transform player;
    private float lastAttackTime;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void Update()
    {
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            var ph = player.GetComponent<Health>();
            if (ph != null)
                ph.ApplyDamage(attackDamage, GetComponent<NPCState>().faction);
            lastAttackTime = Time.time;
        }
    }
}
