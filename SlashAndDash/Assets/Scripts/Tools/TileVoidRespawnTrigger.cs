using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TileVoidRespawnTrigger : MonoBehaviour
{
    [SerializeField] float respawnDamage = -1f;

    public void SetRespawnDamage(float damage)
    {
        respawnDamage = damage;
    }

    void Reset()
    {
        EnsureTriggerCollider();
    }

    void Awake()
    {
        EnsureTriggerCollider();
    }

    void OnTriggerEnter(Collider other)
    {
        CarController player = GetPlayerFromCollider(other);
        if (player == null)
            return;

        player.RespawnFromVoid(respawnDamage);
    }

    void EnsureTriggerCollider()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    static CarController GetPlayerFromCollider(Collider entrant)
    {
        if (entrant == null)
            return null;

        if (entrant.attachedRigidbody != null)
        {
            CarController attachedPlayer = entrant.attachedRigidbody.GetComponentInParent<CarController>();
            if (attachedPlayer != null)
                return attachedPlayer;
        }

        return entrant.GetComponentInParent<CarController>();
    }
}
