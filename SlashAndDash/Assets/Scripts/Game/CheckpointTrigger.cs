using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Game/Checkpoint Trigger")]
[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("The arrow points to the first valid checkpoint in this list after the player reaches this checkpoint. Leave empty to hide the arrow.")]
    [SerializeField] private List<CheckpointTrigger> nextCheckpoints = new List<CheckpointTrigger>();

    private static readonly Color CheckpointGizmoColor = new Color(1f, 0.85f, 0.1f, 0.45f);
    private static readonly Color SelectedCheckpointGizmoColor = new Color(1f, 0.9f, 0.2f, 0.9f);
    private static readonly Color LinkGizmoColor = new Color(1f, 0.72f, 0.08f, 0.55f);
    private const float FallbackGizmoRadius = 1.1f;

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

        ApplyForPlayer(player);
    }

    public void ApplyForPlayer(CarController player)
    {
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

    private void OnDrawGizmos()
    {
        DrawCheckpointGizmos(selected: false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawCheckpointGizmos(selected: true);
    }

    private void DrawCheckpointGizmos(bool selected)
    {
        Gizmos.color = selected ? SelectedCheckpointGizmoColor : CheckpointGizmoColor;
        DrawTriggerBoundsGizmo();
        Gizmos.DrawSphere(transform.position, selected ? 0.28f : 0.18f);

        if (nextCheckpoints == null)
            return;

        Gizmos.color = selected ? SelectedCheckpointGizmoColor : LinkGizmoColor;
        for (int i = 0; i < nextCheckpoints.Count; i++)
        {
            CheckpointTrigger nextCheckpoint = nextCheckpoints[i];
            if (nextCheckpoint != null)
                DrawLinkGizmo(nextCheckpoint.transform.position);
        }
    }

    private void DrawTriggerBoundsGizmo()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider is SphereCollider sphere)
        {
            Vector3 center = transform.TransformPoint(sphere.center);
            Vector3 scale = transform.lossyScale;
            float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Gizmos.DrawWireSphere(center, Mathf.Max(0.05f, radius));
            return;
        }

        if (triggerCollider is BoxCollider box)
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(box.center), transform.rotation, Vector3.Scale(transform.lossyScale, box.size));
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = previousMatrix;
            return;
        }

        Gizmos.DrawWireSphere(transform.position, FallbackGizmoRadius);
    }

    private void DrawLinkGizmo(Vector3 targetPosition)
    {
        Vector3 start = transform.position;
        Vector3 end = targetPosition;
        Gizmos.DrawLine(start, end);

        Vector3 direction = end - start;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 forward = direction.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.Cross(Vector3.forward, forward);

        right.Normalize();
        Vector3 arrowBase = end - forward * 0.55f;
        Gizmos.DrawLine(end, arrowBase + right * 0.22f);
        Gizmos.DrawLine(end, arrowBase - right * 0.22f);
    }
}
