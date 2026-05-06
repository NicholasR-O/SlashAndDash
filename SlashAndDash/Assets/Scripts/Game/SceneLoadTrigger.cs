using UnityEngine;
using UnityEngine.SceneManagement;

[AddComponentMenu("Game/Scene Load Trigger")]
[RequireComponent(typeof(Collider))]
public class SceneLoadTrigger : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string sceneName;
    [SerializeField] private int buildIndex = -1;
    [SerializeField] private LoadSceneMode loadMode = LoadSceneMode.Single;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool requireTag = true;
    [SerializeField] private bool disableAfterTrigger = true;

    private bool triggered;

#if UNITY_EDITOR
    [Header("Editor")]
    [SerializeField] private UnityEditor.SceneAsset sceneAsset;
#endif

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        if (requireTag && !string.IsNullOrWhiteSpace(playerTag) && !other.CompareTag(playerTag))
            return;

        triggered = true;
        if (disableAfterTrigger)
            gameObject.SetActive(false);

        LoadTargetScene();
    }

    private void LoadTargetScene()
    {
        if (buildIndex >= 0)
        {
            SceneManager.LoadScene(buildIndex, loadMode);
            return;
        }

        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            SceneManager.LoadScene(sceneName, loadMode);
            return;
        }

        Debug.LogWarning("[SceneLoadTrigger] No scene assigned. Set buildIndex or sceneName.", this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (sceneAsset != null)
            sceneName = sceneAsset.name;
    }
#endif
}
