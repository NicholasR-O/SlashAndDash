using UnityEngine;

[AddComponentMenu("Game/Checkpoint Arrow Indicator")]
public class CheckpointArrowIndicator : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float hoverHeight = 3f;
    [SerializeField] private float arrowLength = 1.8f;
    [SerializeField] private float arrowWidth = 0.95f;
    [SerializeField] private float shaftWidth = 0.36f;
    [SerializeField] private float arrowThickness = 0.08f;
    [SerializeField] private Color arrowColor = new Color(1f, 0.82f, 0.12f, 1f);
    [SerializeField] private bool hideInArena = true;

    private Transform target;
    private Transform visualRoot;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material arrowMaterial;

    private void Awake()
    {
        ResolvePlayerIfNeeded();
        EnsureVisual();
    }

    private void LateUpdate()
    {
        ResolvePlayerIfNeeded();
        EnsureVisual();

        if (player == null || target == null || visualRoot == null || ShouldHideForArena())
        {
            SetVisualVisible(false);
            return;
        }

        Vector3 direction = target.position - player.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            SetVisualVisible(false);
            return;
        }

        SetVisualVisible(true);
        visualRoot.position = player.position + Vector3.up * hoverHeight;
        visualRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void OnDestroy()
    {
        if (arrowMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(arrowMaterial);
        else
            DestroyImmediate(arrowMaterial);
    }

    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    public void SetTarget(Transform targetTransform)
    {
        target = targetTransform;
        SetVisualVisible(target != null);
    }

    private void ResolvePlayerIfNeeded()
    {
        if (player != null)
            return;

        CarController car = GetComponentInParent<CarController>();
        player = car != null ? car.transform : transform;
    }

    private void EnsureVisual()
    {
        if (visualRoot != null)
            return;

        GameObject visual = new GameObject("CheckpointArrowVisual");
        visualRoot = visual.transform;
        visualRoot.SetParent(transform, worldPositionStays: false);
        visualRoot.localPosition = Vector3.up * hoverHeight;
        visualRoot.localRotation = Quaternion.identity;

        meshFilter = visual.AddComponent<MeshFilter>();
        meshRenderer = visual.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = CreateArrowMesh();
        meshRenderer.sharedMaterial = CreateArrowMaterial();
        SetVisualVisible(target != null);
    }

    private void SetVisualVisible(bool visible)
    {
        if (visualRoot != null && visualRoot.gameObject.activeSelf != visible)
            visualRoot.gameObject.SetActive(visible);
    }

    private bool ShouldHideForArena()
    {
        return hideInArena && ArenaTrigger.PlayerIsInArena;
    }

    private Mesh CreateArrowMesh()
    {
        float halfLength = Mathf.Max(0.05f, arrowLength) * 0.5f;
        float halfHeadWidth = Mathf.Max(0.05f, arrowWidth) * 0.5f;
        float halfShaftWidth = Mathf.Min(halfHeadWidth, Mathf.Max(0.02f, shaftWidth) * 0.5f);
        float halfThickness = Mathf.Max(0.01f, arrowThickness) * 0.5f;
        float headStart = Mathf.Lerp(-halfLength, halfLength, 0.42f);

        Vector3[] vertices =
        {
            new Vector3(-halfShaftWidth, halfThickness, -halfLength),
            new Vector3(halfShaftWidth, halfThickness, -halfLength),
            new Vector3(halfShaftWidth, halfThickness, headStart),
            new Vector3(halfHeadWidth, halfThickness, headStart),
            new Vector3(0f, halfThickness, halfLength),
            new Vector3(-halfHeadWidth, halfThickness, headStart),
            new Vector3(-halfShaftWidth, halfThickness, headStart),
            new Vector3(-halfShaftWidth, -halfThickness, -halfLength),
            new Vector3(halfShaftWidth, -halfThickness, -halfLength),
            new Vector3(halfShaftWidth, -halfThickness, headStart),
            new Vector3(halfHeadWidth, -halfThickness, headStart),
            new Vector3(0f, -halfThickness, halfLength),
            new Vector3(-halfHeadWidth, -halfThickness, headStart),
            new Vector3(-halfShaftWidth, -halfThickness, headStart)
        };

        int[] triangles =
        {
            0, 2, 1,
            0, 6, 2,
            5, 4, 3,
            7, 8, 9,
            7, 9, 13,
            12, 10, 11,
            0, 1, 8,
            0, 8, 7,
            1, 2, 9,
            1, 9, 8,
            6, 0, 7,
            6, 7, 13,
            3, 4, 11,
            3, 11, 10,
            4, 5, 12,
            4, 12, 11,
            5, 3, 10,
            5, 10, 12
        };

        Mesh mesh = new Mesh
        {
            name = "Checkpoint Arrow"
        };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Material CreateArrowMaterial()
    {
        if (arrowMaterial != null)
            return arrowMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogWarning("[CheckpointArrowIndicator] Could not find a supported shader for the checkpoint arrow.", this);
            return null;
        }

        arrowMaterial = new Material(shader)
        {
            name = "Checkpoint Arrow Material",
            color = arrowColor
        };

        if (arrowMaterial.HasProperty("_BaseColor"))
            arrowMaterial.SetColor("_BaseColor", arrowColor);
        if (arrowMaterial.HasProperty("_Color"))
            arrowMaterial.SetColor("_Color", arrowColor);
        if (arrowMaterial.HasProperty("_Cull"))
            arrowMaterial.SetFloat("_Cull", 0f);

        return arrowMaterial;
    }
}
