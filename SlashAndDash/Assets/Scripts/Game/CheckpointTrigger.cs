using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Game/Checkpoint Trigger")]
[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("The arrow points to the first valid checkpoint in this list after the player reaches this checkpoint. Leave empty to hide the arrow.")]
    [SerializeField] private List<CheckpointTrigger> nextCheckpoints = new List<CheckpointTrigger>();

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        CarController player = GetPlayerFromCollider(other);
        if (player == null)
            return;

        player.SetCheckpointRespawnPose(transform.position, transform.rotation);

        CheckpointArrowIndicator arrow = GetArrowIndicator(player);
        if (arrow == null)
            return;

        arrow.SetPlayer(player.transform);
        arrow.SetTarget(GetNextCheckpointTarget());
    }

    private Transform GetNextCheckpointTarget()
    {
        if (nextCheckpoints == null)
            return null;

        for (int i = 0; i < nextCheckpoints.Count; i++)
        {
            CheckpointTrigger nextCheckpoint = nextCheckpoints[i];
            if (nextCheckpoint != null)
                return nextCheckpoint.transform;
        }

        return null;
    }

    private static CheckpointArrowIndicator GetArrowIndicator(CarController player)
    {
        if (player == null)
            return null;

        CheckpointArrowIndicator arrow = player.GetComponentInChildren<CheckpointArrowIndicator>();
        if (arrow == null)
            arrow = player.gameObject.AddComponent<CheckpointArrowIndicator>();

        return arrow;
    }

    private static CarController GetPlayerFromCollider(Collider entrant)
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

    private void EnsureTriggerCollider()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, 1.1f);

        if (nextCheckpoints == null)
            return;

        for (int i = 0; i < nextCheckpoints.Count; i++)
        {
            CheckpointTrigger nextCheckpoint = nextCheckpoints[i];
            if (nextCheckpoint != null)
                Gizmos.DrawLine(transform.position, nextCheckpoint.transform.position);
        }
    }
}
