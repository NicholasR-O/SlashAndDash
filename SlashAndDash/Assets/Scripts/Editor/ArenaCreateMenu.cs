using UnityEditor;
using UnityEngine;

public static class ArenaCreateMenu
{
    [MenuItem("GameObject/Arena/Arena Trigger", false, 10)]
    static void CreateArenaTrigger(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("Arena Trigger");
        Undo.RegisterCreatedObjectUndo(go, "Create Arena Trigger");
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

        SphereCollider triggerCollider = go.AddComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = 18f;

        go.AddComponent<ArenaTrigger>();
        Selection.activeObject = go;
    }

    [MenuItem("GameObject/Arena/Arena Spawner", false, 11)]
    static void CreateArenaSpawner(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("Arena Spawner");
        Undo.RegisterCreatedObjectUndo(go, "Create Arena Spawner");
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

        go.AddComponent<ArenaSpawner>();
        Selection.activeObject = go;
    }

    [MenuItem("Assets/Create/Arena/Arena Spawner Prefab", false, 10)]
    static void CreateArenaSpawnerPrefab()
    {
        string folderPath = GetSelectedFolderPath();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/Arena Spawner.prefab");

        GameObject temp = new GameObject("Arena Spawner");
        temp.AddComponent<ArenaSpawner>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, assetPath);
        Object.DestroyImmediate(temp);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
    }

    static string GetSelectedFolderPath()
    {
        Object selected = Selection.activeObject;
        if (selected == null)
            return "Assets";

        string path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path))
            return "Assets";

        if (AssetDatabase.IsValidFolder(path))
            return path;

        string folder = System.IO.Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(folder) ? "Assets" : folder.Replace('\\', '/');
    }
}
