using System.Collections;
using UnityEngine;

[AddComponentMenu("Game/Stone Gate Closer")]
public class StoneGateCloser : MonoBehaviour
{
    [SerializeField] private Transform movingGate;
    [SerializeField] private float openLocalY = 4.46f;
    [SerializeField] private float closedLocalY = 2f;
    [SerializeField] private float defaultCloseDuration = 1.5f;
    [SerializeField] private float autoDetectTolerance = 0.25f;

    private Coroutine closeRoutine;

    private void Awake()
    {
        ResolveMovingGate();
    }

    private void OnValidate()
    {
        defaultCloseDuration = Mathf.Max(0f, defaultCloseDuration);
        autoDetectTolerance = Mathf.Max(0.01f, autoDetectTolerance);
    }

    public void Close()
    {
        Close(defaultCloseDuration);
    }

    public void Close(float duration)
    {
        Transform gate = ResolveMovingGate();
        if (gate == null)
        {
            Debug.LogWarning("[StoneGateCloser] Could not find a moving gate part to close.", this);
            return;
        }

        if (closeRoutine != null)
            StopCoroutine(closeRoutine);

        duration = duration >= 0f ? duration : defaultCloseDuration;
        float targetLocalY = Mathf.Min(gate.localPosition.y, closedLocalY);
        if (duration <= 0f)
        {
            SetLocalY(gate, targetLocalY);
            closeRoutine = null;
            return;
        }

        closeRoutine = StartCoroutine(CloseRoutine(gate, targetLocalY, duration));
    }

    private IEnumerator CloseRoutine(Transform gate, float targetLocalY, float duration)
    {
        float startY = gate.localPosition.y;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);
            SetLocalY(gate, Mathf.Lerp(startY, targetLocalY, easedT));

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetLocalY(gate, targetLocalY);
        closeRoutine = null;
    }

    private Transform ResolveMovingGate()
    {
        if (movingGate != null)
            return movingGate;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        Transform nameMatch = null;
        Transform closestOpenHeightMatch = null;
        float closestOpenHeightDistance = float.PositiveInfinity;

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform)
                continue;

            float openHeightDistance = Mathf.Abs(child.localPosition.y - openLocalY);
            if (openHeightDistance < closestOpenHeightDistance)
            {
                closestOpenHeightDistance = openHeightDistance;
                closestOpenHeightMatch = child;
            }

            if (nameMatch == null && child.name.IndexOf("gate", System.StringComparison.OrdinalIgnoreCase) >= 0)
                nameMatch = child;
        }

        if (closestOpenHeightMatch != null && closestOpenHeightDistance <= autoDetectTolerance)
        {
            movingGate = closestOpenHeightMatch;
            return movingGate;
        }

        movingGate = nameMatch;
        return movingGate;
    }

    private static void SetLocalY(Transform target, float localY)
    {
        Vector3 localPosition = target.localPosition;
        localPosition.y = localY;
        target.localPosition = localPosition;
    }
}
