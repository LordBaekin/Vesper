// Assets/Scripts/Combat/PlayerCombat.cs
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PlayerCombat : MonoBehaviour
{
    public float attackRange = 3f;
    public float attackDamage = 25f;
    public LayerMask hittableLayers;

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))  // left‐click
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, attackRange, hittableLayers))
            {
                var health = hit.collider.GetComponent<Health>();
                if (health != null)
                    health.ApplyDamage(attackDamage, Faction.Player);
            }
        }
    }
}
