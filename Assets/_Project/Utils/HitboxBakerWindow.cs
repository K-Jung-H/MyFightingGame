using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class HitboxBakerWindow : EditorWindow
{
    private GameObject targetCharacter;
    private ActionDataSO targetActionData;
    private PlayerConfigSO playerConfig;
    private int currentPreviewFrame;
    
    private Vector2 markerScrollPos;
    private Dictionary<int, bool> markerFoldoutStates = new Dictionary<int, bool>();

    [MenuItem("Tools/Hitbox Baker")]
    public static void ShowWindow()
    {
        GetWindow<HitboxBakerWindow>("Hitbox Baker");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        GUILayout.Label("Hitbox Baker Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        targetCharacter = (GameObject)EditorGUILayout.ObjectField("Target Character", targetCharacter, typeof(GameObject), true);
        targetActionData = (ActionDataSO)EditorGUILayout.ObjectField("Target Action Data", targetActionData, typeof(ActionDataSO), false);
        playerConfig = (PlayerConfigSO)EditorGUILayout.ObjectField("Player Config", playerConfig, typeof(PlayerConfigSO), false);
        
        if (EditorGUI.EndChangeCheck() && targetActionData != null && targetActionData.animationClip != null)
        {
            InitializeLogicData();
        }

        if (targetCharacter != null && targetActionData != null && targetActionData.animationClip != null)
        {
            DrawLogicDataEditor();
            DrawHurtboxDataEditor();
            DrawHitboxMarkerEditor();
            DrawTimelineAndPreview();

            EditorGUILayout.Space();
            if (GUILayout.Button("Bake Hitbox Data", GUILayout.Height(40)))
            {
                BakeHitboxData();
            }
        }
    }

    private void InitializeLogicData()
    {
        int calculatedTotal = Mathf.RoundToInt(targetActionData.animationClip.length / Time.fixedDeltaTime);
        
        if (targetActionData.frameData.logicData.totalFrames != calculatedTotal)
        {
            int baseSplit = calculatedTotal / 3;
            int remainder = calculatedTotal % 3;

            targetActionData.frameData.logicData.totalFrames = calculatedTotal;
            targetActionData.frameData.logicData.startupFrames = baseSplit;
            targetActionData.frameData.logicData.recoveryFrames = baseSplit + remainder;
            targetActionData.frameData.logicData.cancelWindowStartFrame = baseSplit * 2;
            
            EditorUtility.SetDirty(targetActionData);
        }
    }

    private void DrawLogicDataEditor()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Logic Data (Editable)", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        var logic = targetActionData.frameData.logicData;
        
        logic.totalFrames = EditorGUILayout.IntField("Total Frames", logic.totalFrames);
        logic.startupFrames = EditorGUILayout.IntField("Startup Frames", logic.startupFrames);
        logic.recoveryFrames = EditorGUILayout.IntField("Recovery Frames", logic.recoveryFrames);
        logic.cancelWindowStartFrame = EditorGUILayout.IntField("Cancel Window Start", logic.cancelWindowStartFrame);

        if (EditorGUI.EndChangeCheck())
        {
            targetActionData.frameData.logicData = logic;
            EditorUtility.SetDirty(targetActionData);
        }
    }

    private void DrawHurtboxDataEditor()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Hurtbox Events (State-based)", EditorStyles.boldLabel);

        if (targetActionData.frameData.hurtboxEvents == null)
        {
            targetActionData.frameData.hurtboxEvents = new HurtboxEvent[0];
        }

        EditorGUI.BeginChangeCheck();

        List<HurtboxEvent> hurtboxList = new List<HurtboxEvent>(targetActionData.frameData.hurtboxEvents);

        for (int i = 0; i < hurtboxList.Count; i++)
        {
            GUILayout.BeginHorizontal();
            
            HurtboxEvent evt = hurtboxList[i];
            
            GUILayout.Label("Start", GUILayout.Width(35));
            evt.startFrame = EditorGUILayout.IntField(evt.startFrame, GUILayout.Width(40));
            
            GUILayout.Label("End", GUILayout.Width(30));
            evt.endFrame = EditorGUILayout.IntField(evt.endFrame, GUILayout.Width(40));
            
            GUILayout.Label("Type", GUILayout.Width(35));
            evt.hurtboxType = (Hurtbox_Type)EditorGUILayout.EnumPopup(evt.hurtboxType);

            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                hurtboxList.RemoveAt(i);
                i--;
            }
            else
            {
                hurtboxList[i] = evt;
            }

            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Add Hurtbox Event"))
        {
            hurtboxList.Add(new HurtboxEvent 
            { 
                startFrame = 0, 
                endFrame = targetActionData.frameData.logicData.totalFrames, 
                hurtboxType = Hurtbox_Type.Standing 
            });
        }

        if (EditorGUI.EndChangeCheck())
        {
            targetActionData.frameData.hurtboxEvents = hurtboxList.ToArray();
            EditorUtility.SetDirty(targetActionData);
        }
    }

    private void DrawHitboxMarkerEditor()
    {
        if (targetCharacter == null) return;

        HitboxMarker[] markers = targetCharacter.GetComponentsInChildren<HitboxMarker>();
        if (markers.Length == 0) return;

        EditorGUILayout.Space();
        GUILayout.Label("Hitbox Markers (In Character)", EditorStyles.boldLabel);

        markerScrollPos = EditorGUILayout.BeginScrollView(markerScrollPos, GUILayout.MinHeight(200), GUILayout.ExpandHeight(true));

        foreach (var marker in markers)
        {
            int markerId = marker.GetInstanceID();
            
            if (!markerFoldoutStates.ContainsKey(markerId))
            {
                markerFoldoutStates[markerId] = false;
            }

            GUILayout.BeginVertical("box");

            markerFoldoutStates[markerId] = EditorGUILayout.Foldout(markerFoldoutStates[markerId], marker.gameObject.name, true, EditorStyles.foldoutHeader);

            if (markerFoldoutStates[markerId])
            {
                EditorGUI.BeginChangeCheck();
                
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                int start = EditorGUILayout.IntField("Start Frame", marker.recordStartFrame);
                int end = EditorGUILayout.IntField("End Frame", marker.recordEndFrame);
                GUILayout.EndHorizontal();

                int groupID = EditorGUILayout.IntField("Hit Group ID", marker.hitGroupID);
                Attack_Type attackType = (Attack_Type)EditorGUILayout.EnumPopup("Attack Type", marker.attackType);
                HurtState_Type targetState = (HurtState_Type)EditorGUILayout.EnumPopup("Target Hurt State", marker.targetHurtState);
                
                int damage = EditorGUILayout.IntField("Damage", marker.damage);
                int hitstun = EditorGUILayout.IntField("Hitstun Frames", marker.hitstunFrames);
                Vector3 pushback = EditorGUILayout.Vector3Field("Local Pushback Vector", marker.localPushbackVector);
                bool isKnockdown = EditorGUILayout.Toggle("Is Hard Knockdown", marker.isHardKnockdown);
                
                Vector3 extents = EditorGUILayout.Vector3Field("Box Extents", marker.boxExtents);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(marker, "Modify Hitbox Marker");
                    marker.recordStartFrame = start;
                    marker.recordEndFrame = end;
                    marker.hitGroupID = groupID;
                    marker.attackType = attackType;
                    marker.targetHurtState = targetState;
                    marker.damage = damage;
                    marker.hitstunFrames = hitstun;
                    marker.localPushbackVector = pushback;
                    marker.isHardKnockdown = isKnockdown;
                    marker.boxExtents = extents;
                    EditorUtility.SetDirty(marker);
                }
            }
            
            GUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTimelineAndPreview()
    {
        EditorGUILayout.Space();
        var logic = targetActionData.frameData.logicData;
        
        string stateLabel = "Unknown";
        Color stateColor = Color.white;

        int activeStart = logic.startupFrames;
        int activeEnd = logic.totalFrames - logic.recoveryFrames;

        if (currentPreviewFrame < activeStart)
        {
            stateLabel = "STARTUP";
            stateColor = new Color(0.3f, 0.8f, 1f); 
        }
        else if (currentPreviewFrame < activeEnd)
        {
            stateLabel = "ACTIVE";
            stateColor = Color.green;
        }
        else
        {
            stateLabel = "RECOVERY";
            stateColor = Color.yellow;
        }

        bool isCancelable = currentPreviewFrame >= logic.cancelWindowStartFrame;
        string cancelText = isCancelable ? "[CANCELABLE]" : "[LOCKED]";

        Rect headerRect = GUILayoutUtility.GetRect(10, 25);
        GUI.Box(headerRect, "");
        
        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
        labelStyle.normal.textColor = stateColor;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(headerRect, $"{stateLabel} | Frame: {currentPreviewFrame} / {logic.totalFrames} {cancelText}", labelStyle);

        Rect logicSliderRect = GUILayoutUtility.GetRect(10, 20);
        DrawGradientTimeline(logicSliderRect, logic);
        
        Rect hurtboxTimelineRect = GUILayoutUtility.GetRect(10, 15);
        DrawHurtboxTimeline(hurtboxTimelineRect, logic);

        EditorGUI.BeginChangeCheck();
        currentPreviewFrame = Mathf.RoundToInt(GUI.HorizontalSlider(logicSliderRect, currentPreviewFrame, 0, logic.totalFrames, GUIStyle.none, GUI.skin.horizontalSliderThumb));
        
        if (EditorGUI.EndChangeCheck())
        {
            UpdateScenePreview();
        }
    }

    private void DrawGradientTimeline(Rect rect, ActionLogicData logic)
    {
        if (logic.totalFrames <= 0) return;

        float width = rect.width;
        float startupEndPos = (float)logic.startupFrames / logic.totalFrames * width;
        float recoveryStartPos = (float)(logic.totalFrames - logic.recoveryFrames) / logic.totalFrames * width;
        float cancelStartPos = (float)logic.cancelWindowStartFrame / logic.totalFrames * width;

        Rect startupRect = new Rect(rect.x, rect.y + 5, startupEndPos, 10);
        EditorGUI.DrawRect(startupRect, new Color(0.1f, 0.5f, 0.7f, 0.8f));

        Rect activeRect = new Rect(rect.x + startupEndPos, rect.y + 5, recoveryStartPos - startupEndPos, 10);
        EditorGUI.DrawRect(activeRect, new Color(0.1f, 0.7f, 0.1f, 0.8f));

        Rect recoveryRect = new Rect(rect.x + recoveryStartPos, rect.y + 5, width - recoveryStartPos, 10);
        EditorGUI.DrawRect(recoveryRect, new Color(0.7f, 0.7f, 0.1f, 0.8f));

        Rect cancelMark = new Rect(rect.x + cancelStartPos - 1, rect.y + 2, 2, 16);
        EditorGUI.DrawRect(cancelMark, Color.white);
    }

    private void DrawHurtboxTimeline(Rect rect, ActionLogicData logic)
    {
        if (logic.totalFrames <= 0 || targetActionData.frameData.hurtboxEvents == null) return;

        float width = rect.width;
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2, width, 8), new Color(0.2f, 0.2f, 0.2f, 1f));

        foreach (var evt in targetActionData.frameData.hurtboxEvents)
        {
            float startPos = (float)evt.startFrame / logic.totalFrames * width;
            float endPos = (float)evt.endFrame / logic.totalFrames * width;
            float eventWidth = Mathf.Max(endPos - startPos, 2f);
            
            Rect eventRect = new Rect(rect.x + startPos, rect.y + 2, eventWidth, 8);
            EditorGUI.DrawRect(eventRect, GetHurtboxColor(evt.hurtboxType));
        }
    }

    private Color GetHurtboxColor(Hurtbox_Type type)
    {
        switch (type)
        {
            case Hurtbox_Type.Standing: return new Color(0.2f, 0.6f, 0.8f, 0.8f);
            case Hurtbox_Type.Crouching: return new Color(0.8f, 0.5f, 0.2f, 0.8f);
            case Hurtbox_Type.Airborne: return new Color(0.6f, 0.2f, 0.8f, 0.8f);
            case Hurtbox_Type.Invincible: return new Color(0.9f, 0.9f, 0.9f, 0.8f);
            default: return new Color(0.5f, 0.5f, 0.5f, 0.8f);
        }
    }

    private void UpdateScenePreview()
    {
        if (targetActionData.animationClip == null || targetCharacter == null) return;

        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
        }

        float currentTime = currentPreviewFrame * Time.fixedDeltaTime;
        AnimationMode.SampleAnimationClip(targetCharacter, targetActionData.animationClip, currentTime);

        HitboxMarker[] markers = targetCharacter.GetComponentsInChildren<HitboxMarker>();
        foreach (var marker in markers)
        {
            marker.currentPreviewFrame = currentPreviewFrame;
        }

        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (targetCharacter == null || targetActionData == null || playerConfig == null) return;
        if (targetActionData.frameData.hurtboxEvents == null) return;

        bool isEventActive = false;
        HurtboxEvent activeEvent = new HurtboxEvent();

        foreach (var evt in targetActionData.frameData.hurtboxEvents)
        {
            if (currentPreviewFrame >= evt.startFrame && currentPreviewFrame <= evt.endFrame)
            {
                activeEvent = evt;
                isEventActive = true;
                break;
            }
        }

        if (isEventActive)
        {
            CollisionBox[] boxes = playerConfig.GetHurtboxBoxes(activeEvent.hurtboxType);
            if (boxes != null)
            {
                Handles.matrix = targetCharacter.transform.localToWorldMatrix;
                Color boxColor = GetHurtboxColor(activeEvent.hurtboxType);
                Handles.color = new Color(boxColor.r, boxColor.g, boxColor.b, 1f);

                foreach (var box in boxes)
                {
                    Handles.DrawWireCube(box.localPosition, box.extents * 2f);
                }
            }
        }
    }

    private void BakeHitboxData()
    {
        if (targetActionData.animationClip == null || targetCharacter == null) return;

        int totalFrames = targetActionData.frameData.logicData.totalFrames;
        HitboxMarker[] markers = targetCharacter.GetComponentsInChildren<HitboxMarker>();
        List<HitboxEvent> bakedEvents = new List<HitboxEvent>();

        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
        }

        foreach (var marker in markers)
        {
            if (marker.recordStartFrame < 0 || marker.recordEndFrame > totalFrames || marker.recordStartFrame > marker.recordEndFrame) continue;

            HitboxEvent newEvent = new HitboxEvent
            {
                activeStartFrame = marker.recordStartFrame,
                hitGroupID = marker.hitGroupID,
                attackType = marker.attackType,
                targetHurtState = marker.targetHurtState,
                damage = marker.damage,
                hitstunFrames = marker.hitstunFrames,
                localPushbackVector = marker.localPushbackVector,
                isHardKnockdown = marker.isHardKnockdown
            };

            int pathLength = marker.recordEndFrame - marker.recordStartFrame + 1;
            newEvent.boxPath = new CollisionBox[pathLength];

            for (int frame = marker.recordStartFrame; frame <= marker.recordEndFrame; frame++)
            {
                float currentTime = frame * Time.fixedDeltaTime;
                AnimationMode.SampleAnimationClip(targetCharacter, targetActionData.animationClip, currentTime);

                CollisionBox box = new CollisionBox
                {
                    localPosition = targetCharacter.transform.InverseTransformPoint(marker.transform.position),
                    extents = marker.boxExtents
                };

                newEvent.boxPath[frame - marker.recordStartFrame] = box;
            }

            bakedEvents.Add(newEvent);
        }

        targetActionData.frameData.hitboxEvents = bakedEvents.ToArray();

        AnimationMode.SampleAnimationClip(targetCharacter, targetActionData.animationClip, 0f);
        AnimationMode.StopAnimationMode();

        EditorUtility.SetDirty(targetActionData);
        AssetDatabase.SaveAssets();

        Debug.Log($"Baked {bakedEvents.Count} hitbox events successfully.");
    }
}