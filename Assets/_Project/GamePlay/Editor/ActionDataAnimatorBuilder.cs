using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class ActionDataAnimatorBuilder : EditorWindow
{
    private AnimatorController targetController;

    [MenuItem("Tools/Action Data Animator Builder")]
    public static void ShowWindow()
    {
        GetWindow<ActionDataAnimatorBuilder>("Animator Builder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Action Data to Animator Setup", EditorStyles.boldLabel);

        targetController = (AnimatorController)EditorGUILayout.ObjectField("Target Animator", targetController, typeof(AnimatorController), false);

        if (targetController == null)
        {
            EditorGUILayout.HelpBox("애니메이터 컨트롤러를 먼저 할당해주세요.", MessageType.Info);
            return;
        }

        Rect dropArea = GUILayoutUtility.GetRect(0.0f, 100.0f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "\nActionDataSO 파일들을 여기에 드래그 앤 드롭 하세요", new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter });

        Event evt = Event.current;
        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition))
                    break;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (Object dragged_object in DragAndDrop.objectReferences)
                    {
                        ActionDataSO actionData = dragged_object as ActionDataSO;
                        if (actionData != null)
                        {
                            AddStateToAnimator(actionData);
                        }
                    }
                }
                Event.current.Use();
                break;
        }
    }

    private void AddStateToAnimator(ActionDataSO data)
    {
        if (data.animationClip == null)
        {
            Debug.LogWarning($"[Animator Builder] {data.name}에 AnimationClip이 할당되어 있지 않아 건너뜁니다.");
            return;
        }
        
        if (string.IsNullOrEmpty(data.animationStateName))
        {
            Debug.LogWarning($"[Animator Builder] {data.name}의 AnimationStateName이 비어있어 건너뜁니다.");
            return;
        }

        AnimatorStateMachine rootStateMachine = targetController.layers[0].stateMachine;

        foreach (var childState in rootStateMachine.states)
        {
            if (childState.state.name == data.animationStateName)
            {
                Debug.Log($"[Animator Builder] '{data.animationStateName}' 노드가 이미 존재하여 생성을 생략합니다.");
                return;
            }
        }


        int currentStateCount = rootStateMachine.states.Length;
        Vector3 safeSpawnPosition = new Vector3(300, currentStateCount * 50, 0);

        AnimatorState newState = rootStateMachine.AddState(data.animationStateName, safeSpawnPosition);
        newState.motion = data.animationClip;

        EditorUtility.SetDirty(targetController);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"[Animator Builder] '{data.animationStateName}' 노드 생성 완료!");
    }
}