using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TutorialTrigger))]
public class TutorialTriggerEditor : Editor
{
    static readonly Color SelectedWallColor = new Color(1f, 0.7f, 0.15f, 1f);
    static readonly Color UnselectedWallColor = new Color(0.95f, 0.95f, 0.95f, 0.95f);
    static readonly Color SelectedPreviewColor = new Color(1f, 0.78f, 0.3f, 0.95f);
    static readonly Color UnselectedPreviewColor = new Color(0.5f, 0.8f, 1f, 0.5f);
    static readonly Color SelectedEnemyColor = new Color(0.25f, 1f, 0.4f, 1f);
    static readonly Color UnselectedEnemyColor = new Color(0.6f, 0.9f, 0.7f, 0.85f);

    const float PlaceInFrontDistance = 10f;

    int selectedWallIndex = -1;
    int selectedEnemySpawnIndex = -1;

    void OnEnable()
    {
        TutorialTrigger trigger = target as TutorialTrigger;
        EnsureWallData(trigger);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TutorialTrigger trigger = (TutorialTrigger)target;
        EnsureWallData(trigger);
        EnsureSelectedIndexIsValid(trigger);

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Wall Layout Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Click wall markers in Scene view, then use Move/Rotate handles to place each wall.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Wall"))
            {
                Undo.RecordObject(trigger, "Add Tutorial Wall");
                trigger.AddWallPlacement();
                selectedWallIndex = trigger.WallCount - 1;
                MarkDirty(trigger);
            }

            using (new EditorGUI.DisabledScope(!trigger.IsWallIndexValid(selectedWallIndex)))
            {
                if (GUILayout.Button("Duplicate Selected"))
                {
                    Undo.RecordObject(trigger, "Duplicate Tutorial Wall");
                    int sourceIndex = selectedWallIndex;
                    trigger.DuplicateWallPlacement(sourceIndex);
                    selectedWallIndex = Mathf.Clamp(sourceIndex + 1, 0, trigger.WallCount - 1);
                    MarkDirty(trigger);
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!trigger.IsWallIndexValid(selectedWallIndex)))
            {
                if (GUILayout.Button("Remove Selected"))
                {
                    Undo.RecordObject(trigger, "Remove Tutorial Wall");
                    trigger.RemoveWallPlacement(selectedWallIndex);
                    selectedWallIndex = Mathf.Clamp(selectedWallIndex, 0, trigger.WallCount - 1);
                    MarkDirty(trigger);
                }

                if (GUILayout.Button("Place Selected In Front"))
                {
                    Vector3 position = trigger.transform.position + trigger.transform.forward * PlaceInFrontDistance;
                    Quaternion rotation = Quaternion.LookRotation(trigger.transform.forward, Vector3.up);

                    Undo.RecordObject(trigger, "Place Tutorial Wall In Front");
                    trigger.SetWallPlacementWorld(selectedWallIndex, position, rotation);
                    MarkDirty(trigger);
                }

                if (GUILayout.Button("Snap Selected To Ground"))
                {
                    SnapSelectedWallToGround(trigger);
                }
            }
        }

        DrawWallSelectionList(trigger);
        DrawEnemySpawnLayoutTool(trigger);
    }

    void DrawWallSelectionList(TutorialTrigger trigger)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Walls", EditorStyles.boldLabel);

        if (trigger.WallCount <= 0)
        {
            EditorGUILayout.HelpBox("No walls are currently defined.", MessageType.Warning);
            return;
        }

        for (int i = 0; i < trigger.WallCount; i++)
        {
            bool isSelected = i == selectedWallIndex;
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal
            };

            if (GUILayout.Button("Wall " + (i + 1), buttonStyle))
            {
                selectedWallIndex = i;
                SceneView.RepaintAll();
            }
        }
    }

    void DrawEnemySpawnLayoutTool(TutorialTrigger trigger)
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Enemy Spawn Layout Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Click enemy spawn markers in Scene view, then use Move/Rotate handles to place each spawn point.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Spawn"))
            {
                Undo.RecordObject(trigger, "Add Enemy Spawn");
                trigger.AddEnemySpawnPlacement();
                selectedEnemySpawnIndex = trigger.EnemySpawnCount - 1;
                MarkDirty(trigger);
            }

            using (new EditorGUI.DisabledScope(!trigger.IsEnemySpawnIndexValid(selectedEnemySpawnIndex)))
            {
                if (GUILayout.Button("Duplicate Selected"))
                {
                    Undo.RecordObject(trigger, "Duplicate Enemy Spawn");
                    int sourceIndex = selectedEnemySpawnIndex;
                    trigger.DuplicateEnemySpawnPlacement(sourceIndex);
                    selectedEnemySpawnIndex = Mathf.Clamp(sourceIndex + 1, 0, trigger.EnemySpawnCount - 1);
                    MarkDirty(trigger);
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!trigger.IsEnemySpawnIndexValid(selectedEnemySpawnIndex)))
            {
                if (GUILayout.Button("Remove Selected"))
                {
                    Undo.RecordObject(trigger, "Remove Enemy Spawn");
                    trigger.RemoveEnemySpawnPlacement(selectedEnemySpawnIndex);
                    selectedEnemySpawnIndex = Mathf.Clamp(selectedEnemySpawnIndex, 0, trigger.EnemySpawnCount - 1);
                    MarkDirty(trigger);
                }

                if (GUILayout.Button("Place Selected In Front"))
                {
                    Vector3 position = trigger.transform.position + trigger.transform.forward * PlaceInFrontDistance;
                    Quaternion rotation = Quaternion.LookRotation(trigger.transform.forward, Vector3.up);

                    Undo.RecordObject(trigger, "Place Enemy Spawn In Front");
                    trigger.SetEnemySpawnPlacementWorld(selectedEnemySpawnIndex, position, rotation);
                    MarkDirty(trigger);
                }

                if (GUILayout.Button("Snap Selected To Ground"))
                {
                    SnapSelectedEnemySpawnToGround(trigger);
                }
            }
        }

        DrawEnemySpawnSelectionList(trigger);
    }

    void DrawEnemySpawnSelectionList(TutorialTrigger trigger)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Enemy Spawns", EditorStyles.boldLabel);

        if (trigger.EnemySpawnCount <= 0)
        {
            EditorGUILayout.HelpBox("No enemy spawns are currently defined.", MessageType.Info);
            return;
        }

        for (int i = 0; i < trigger.EnemySpawnCount; i++)
        {
            bool isSelected = i == selectedEnemySpawnIndex;
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal
            };

            if (GUILayout.Button("Spawn " + (i + 1), buttonStyle))
            {
                selectedEnemySpawnIndex = i;
                SceneView.RepaintAll();
            }
        }
    }

    void SnapSelectedEnemySpawnToGround(TutorialTrigger trigger)
    {
        if (!trigger.IsEnemySpawnIndexValid(selectedEnemySpawnIndex))
            return;

        if (!trigger.TryGetEnemySpawnPlacementWorld(selectedEnemySpawnIndex, out Vector3 position, out Quaternion rotation))
            return;

        Vector3 castStart = position + Vector3.up * 200f;
        if (!Physics.Raycast(castStart, Vector3.down, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning("[TutorialTriggerEditor] Could not find ground below selected enemy spawn.");
            return;
        }

        Vector3 projectedForward = Vector3.ProjectOnPlane(rotation * Vector3.forward, hit.normal);
        if (projectedForward.sqrMagnitude > 0.001f)
            rotation = Quaternion.LookRotation(projectedForward.normalized, hit.normal);

        Undo.RecordObject(trigger, "Snap Enemy Spawn To Ground");
        trigger.SetEnemySpawnPlacementWorld(selectedEnemySpawnIndex, hit.point, rotation);
        MarkDirty(trigger);
    }

    void SnapSelectedWallToGround(TutorialTrigger trigger)
    {
        if (!trigger.IsWallIndexValid(selectedWallIndex))
            return;

        if (!trigger.TryGetWallPlacementWorld(selectedWallIndex, out Vector3 position, out Quaternion rotation))
            return;

        Vector3 castStart = position + Vector3.up * 200f;
        if (!Physics.Raycast(castStart, Vector3.down, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning("[TutorialTriggerEditor] Could not find ground below selected wall placement.");
            return;
        }

        Vector3 projectedForward = Vector3.ProjectOnPlane(rotation * Vector3.forward, hit.normal);
        if (projectedForward.sqrMagnitude > 0.001f)
            rotation = Quaternion.LookRotation(projectedForward.normalized, hit.normal);

        Undo.RecordObject(trigger, "Snap Tutorial Wall To Ground");
        trigger.SetWallPlacementWorld(selectedWallIndex, hit.point, rotation);
        MarkDirty(trigger);
    }

    void OnSceneGUI()
    {
        TutorialTrigger trigger = (TutorialTrigger)target;
        EnsureWallData(trigger);
        EnsureSelectedIndexIsValid(trigger);
        if (trigger == null)
            return;

        if (trigger.WallCount > 0)
        {
            DrawWallSelectionHandles(trigger);
            DrawSelectedWallTransformHandles(trigger);
        }

        if (trigger.EnemySpawnCount > 0)
        {
            DrawEnemySpawnSelectionHandles(trigger);
            DrawSelectedEnemySpawnTransformHandles(trigger);
        }
    }

    void DrawWallSelectionHandles(TutorialTrigger trigger)
    {
        for (int i = 0; i < trigger.WallCount; i++)
        {
            if (!trigger.TryGetWallPlacementWorld(i, out Vector3 position, out Quaternion rotation))
                continue;

            float handleSize = HandleUtility.GetHandleSize(position) * 0.08f;
            bool isSelected = i == selectedWallIndex;

            Handles.color = isSelected ? SelectedWallColor : UnselectedWallColor;
            if (Handles.Button(position + Vector3.up * 0.5f, Quaternion.identity, handleSize, handleSize, Handles.SphereHandleCap))
            {
                selectedWallIndex = i;
                Repaint();
            }

            DrawWallPreviewHandle(position, rotation, trigger.WallPreviewSize, isSelected ? SelectedPreviewColor : UnselectedPreviewColor);
        }
    }

    void DrawSelectedWallTransformHandles(TutorialTrigger trigger)
    {
        if (!trigger.IsWallIndexValid(selectedWallIndex))
            return;
        if (!trigger.TryGetWallPlacementWorld(selectedWallIndex, out Vector3 position, out Quaternion rotation))
            return;

        Handles.color = SelectedWallColor;
        Handles.Label(position + Vector3.up * 1.8f, "Selected Wall " + (selectedWallIndex + 1));

        EditorGUI.BeginChangeCheck();
        Vector3 newPosition = Handles.PositionHandle(position, rotation);
        Quaternion newRotation = Handles.RotationHandle(rotation, newPosition);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(trigger, "Move Tutorial Wall");
            trigger.SetWallPlacementWorld(selectedWallIndex, newPosition, newRotation);
            MarkDirty(trigger);
        }
    }

    void DrawEnemySpawnSelectionHandles(TutorialTrigger trigger)
    {
        for (int i = 0; i < trigger.EnemySpawnCount; i++)
        {
            if (!trigger.TryGetEnemySpawnPlacementWorld(i, out Vector3 position, out Quaternion rotation))
                continue;

            float handleSize = HandleUtility.GetHandleSize(position) * 0.07f;
            bool isSelected = i == selectedEnemySpawnIndex;

            Handles.color = isSelected ? SelectedEnemyColor : UnselectedEnemyColor;
            if (Handles.Button(position + Vector3.up * 0.4f, Quaternion.identity, handleSize, handleSize, Handles.SphereHandleCap))
            {
                selectedEnemySpawnIndex = i;
                Repaint();
            }

            Handles.DrawLine(position, position + (rotation * Vector3.forward * 1.2f));
        }
    }

    void DrawSelectedEnemySpawnTransformHandles(TutorialTrigger trigger)
    {
        if (!trigger.IsEnemySpawnIndexValid(selectedEnemySpawnIndex))
            return;
        if (!trigger.TryGetEnemySpawnPlacementWorld(selectedEnemySpawnIndex, out Vector3 position, out Quaternion rotation))
            return;

        Handles.color = SelectedEnemyColor;
        Handles.Label(position + Vector3.up * 1.4f, "Selected Spawn " + (selectedEnemySpawnIndex + 1));

        EditorGUI.BeginChangeCheck();
        Vector3 newPosition = Handles.PositionHandle(position, rotation);
        Quaternion newRotation = Handles.RotationHandle(rotation, newPosition);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(trigger, "Move Enemy Spawn");
            trigger.SetEnemySpawnPlacementWorld(selectedEnemySpawnIndex, newPosition, newRotation);
            MarkDirty(trigger);
        }
    }

    static void DrawWallPreviewHandle(Vector3 position, Quaternion rotation, Vector3 size, Color color)
    {
        Matrix4x4 previousMatrix = Handles.matrix;
        Color previousColor = Handles.color;

        Handles.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
        Handles.color = color;
        Handles.DrawWireCube(Vector3.zero, size);

        Handles.matrix = previousMatrix;
        Handles.color = previousColor;
    }

    void EnsureSelectedIndexIsValid(TutorialTrigger trigger)
    {
        if (trigger == null)
        {
            selectedWallIndex = -1;
            selectedEnemySpawnIndex = -1;
            return;
        }

        if (trigger.WallCount <= 0)
        {
            selectedWallIndex = -1;
        }
        else if (selectedWallIndex < 0 || selectedWallIndex >= trigger.WallCount)
        {
            selectedWallIndex = 0;
        }

        if (trigger.EnemySpawnCount <= 0)
        {
            selectedEnemySpawnIndex = -1;
        }
        else if (selectedEnemySpawnIndex < 0 || selectedEnemySpawnIndex >= trigger.EnemySpawnCount)
        {
            selectedEnemySpawnIndex = 0;
        }
    }

    static void MarkDirty(TutorialTrigger trigger)
    {
        EditorUtility.SetDirty(trigger);
        SceneView.RepaintAll();
    }

    static void EnsureWallData(TutorialTrigger trigger)
    {
        if (trigger == null)
            return;

        bool wallChanged = trigger.EnsureWallDataForEditing();
        bool enemyChanged = trigger.EnsureEnemySpawnDataForEditing();
        bool stepChanged = trigger.EnsureStepDataForEditing();
        if (!wallChanged && !enemyChanged && !stepChanged)
            return;

        MarkDirty(trigger);
    }
}
