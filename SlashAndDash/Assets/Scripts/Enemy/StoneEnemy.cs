using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody), typeof(NavMeshAgent))]
public class StoneEnemy : BasicEnemy
{
    private void Reset()
    {
        maxHealth = 120f;
        damage = 12f;
        size = 1.2f;
        canBeGrappled = false;
    }
}
