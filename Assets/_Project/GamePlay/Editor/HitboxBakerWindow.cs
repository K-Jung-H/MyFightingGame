using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System.Linq;

public class HitboxBakerWindow : EditorWindow
{
    private GameObject targetCharacter;
    private Transform targetRootBone;
    private ActionDataSO targetActionData;
    private PlayerConfigSO playerConfig;
    private int currentPreviewFrame;
    
    private Vector2 mainScrollPos;
    
    private Dictionary<int, bool> markerFoldoutStates = new Dictionary<int, bool>();
    private Dictionary<int, bool> vfxMarkerFoldoutStates = new Dictionary<int, bool>();

    private bool showHitboxMarkers = true;
    private bool showVfxMarkers = true;

    private BoxBoundsHandle boxBoundsHandle = new BoxBoundsHandle();

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
        bool characterChanged = EditorGUI.EndChangeCheck();

        targetRootBone = (Transform)EditorGUILayout.ObjectField("Target Root Bone", targetRootBone, typeof(Transform), true);
        playerConfig = (PlayerConfigSO)EditorGUILayout.ObjectField("Player Config", playerConfig, typeof(PlayerConfigSO), false);
        
        EditorGUI.BeginChangeCheck();
        targetActionData = (ActionDataSO)EditorGUILayout.ObjectField("Target Action Data", targetActionData, typeof(ActionDataSO), false);
        bool actionDataChanged = EditorGUI.EndChangeCheck();
        
        if (actionDataChanged && targetActionData != null)
        {
            if (targetActionData.frameData == null)
            {
                targetActionData.frameData = new AnimationFrameData();
            }

            if (targetActionData.animationClip != null && targetActionData.frameData.logicData.totalFrames <= 0)
            {
                InitializeLogicData();
            }
        }

        if (characterChanged || actionDataChanged)
        {
            SyncMarkersWithActionData();
        }

        if (targetCharacter != null && targetActionData != null && targetActionData.animationClip != null)
        {
            mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);

            DrawLogicDataEditor();
            DrawHurtboxDataEditor();
            DrawHitboxMarkerEditor();
            DrawVfxMarkerEditor();
            DrawTimelineAndPreview();

            EditorGUILayout.Space();
            if (GUILayout.Button("Bake All Data (Hitbox, VFX, RootMotion)", GUILayout.Height(40)))
            {
                ExecuteBakeProcess();
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void ExecuteBakeProcess()
    {
        BakeRootMotionData();
        BakeHitboxData();
        BakeVfxData();

        EditorUtility.SetDirty(targetActionData);
        AssetDatabase.SaveAssets();

        Debug.Log("Successfully baked Root Motion, Hitbox, and VFX events.");
    }

    private void InitializeLogicData()
    {
        if (targetActionData == null || targetActionData.animationClip == null) return;

        float clipLength = targetActionData.animationClip.length;
        float frameRate = targetActionData.animationClip.frameRate;
        int calculatedTotal = Mathf.RoundToInt(clipLength * frameRate);
        
        int baseSplit = calculatedTotal / 3;
        int remainder = calculatedTotal % 3;

        targetActionData.frameData.logicData.totalFrames = calculatedTotal;
        targetActionData.frameData.logicData.startupFrames = baseSplit;
        targetActionData.frameData.logicData.recoveryFrames = baseSplit + remainder;
        targetActionData.frameData.logicData.cancelWindowStartFrame = baseSplit * 2;
        targetActionData.frameData.logicData.isHoming = false;
        
        EditorUtility.SetDirty(targetActionData);
    }

    private void DrawLogicDataEditor()
    {
        EditorGUILayout.Space();
        
        GUILayout.BeginHorizontal();
        GUILayout.Label("Logic Data (Editable)", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Auto Calc from Hitboxes", GUILayout.Width(160)))
        {
            Undo.RecordObject(targetActionData, "Auto Calculate Logic");
            AutoCalculateFromHitboxes();
            GUI.FocusControl(null); 
        }

        if (GUILayout.Button("Reset", GUILayout.Width(60)))
        {
            if (targetActionData != null && targetActionData.animationClip != null)
            {
                Undo.RecordObject(targetActionData, "Reset Logic Data");
                InitializeLogicData();
                GUI.FocusControl(null); 
            }
        }
        GUILayout.EndHorizontal();
        
        EditorGUI.BeginChangeCheck();
        var logic = targetActionData.frameData.logicData;
        
        logic.totalFrames = EditorGUILayout.IntField("Total Frames", logic.totalFrames);
        logic.startupFrames = EditorGUILayout.IntField("Startup Frames", logic.startupFrames);
        logic.recoveryFrames = EditorGUILayout.IntField("Recovery Frames", logic.recoveryFrames);
        logic.cancelWindowStartFrame = EditorGUILayout.IntField("Cancel Window Start", logic.cancelWindowStartFrame);
        logic.isHoming = EditorGUILayout.Toggle("Is Homing Attack", logic.isHoming);
        logic.useRootMotion = EditorGUILayout.Toggle("Use Root Position", logic.useRootMotion);

        if (EditorGUI.EndChangeCheck())
        {
            targetActionData.frameData.logicData = logic;
            EditorUtility.SetDirty(targetActionData);
        }
    }

    private void AutoCalculateFromHitboxes()
    {
        if (targetCharacter == null || targetActionData == null) return;

        HitboxMarker[] markers = targetCharacter.GetComponentsInChildren<HitboxMarker>();
        int firstHitStart = int.MaxValue;
        int lastHitEnd = -1;

        foreach (var marker in markers)
        {
            if (!marker.isIncludeInBake) continue;
            if (marker.recordStartFrame < firstHitStart) firstHitStart = marker.recordStartFrame;
            if (marker.recordEndFrame > lastHitEnd) lastHitEnd = marker.recordEndFrame;
        }

        var logic = targetActionData.frameData.logicData;

        if (lastHitEnd == -1) return;

        lastHitEnd = Mathf.Min(lastHitEnd, logic.totalFrames - 1);
        logic.startupFrames = firstHitStart;
        logic.recoveryFrames = Mathf.Max(0, logic.totalFrames - (lastHitEnd + 1));

        int halfRecovery = logic.recoveryFrames / 2;
        logic.cancelWindowStartFrame = (lastHitEnd + 1) + halfRecovery;

        targetActionData.frameData.logicData = logic;
        EditorUtility.SetDirty(targetActionData);
    }

    private void SyncMarkersWithActionData()
    {
        if (targetCharacter == null) return;

        HitboxMarker[] hbMarkers = targetCharacter.GetComponentsInChildren<HitboxMarker>(true);
        VfxMarker[] vfxMarkers = targetCharacter.GetComponentsInChildren<VfxMarker>(true);

        foreach (var m in hbMarkers)
        {
            m.isIncludeInBake = false;
            m.recordStartFrame = 0;
            m.recordEndFrame = 0;
            m.hitGroupID = 0;
            m.damage = 0;
            m.hitstunFrames = 0;
            m.blockStunFrames = 0;
            m.localPushbackVector = Vector3.zero;
            m.isHardKnockdown = false;
            m.boxExtents = new Vector3(0.1f, 0.1f, 0.1f);
            EditorUtility.SetDirty(m);
        }

        foreach (var m in vfxMarkers)
        {
            m.isIncludeInBake = false;
            m.recordStartFrame = 0;
            m.recordEndFrame = 0;
            m.intervalFrames = 0;
            m.isAttached = false;
            m.effectType = EffectType.None;
            m.targetBone = HumanBodyBones.Hips;
            EditorUtility.SetDirty(m);
        }

        if (targetActionData == null || targetActionData.frameData == null) return;

        if (targetActionData.frameData.hitboxEvents != null)
        {
            for (int i = 0; i < targetActionData.frameData.hitboxEvents.Length; i++)
            {
                var ev = targetActionData.frameData.hitboxEvents[i];
                HitboxMarker targetMarker = null;

                if (!string.IsNullOrEmpty(ev.markerName))
                {
                    targetMarker = hbMarkers.FirstOrDefault(m => m.gameObject.name == ev.markerName);
                }

                if (targetMarker == null && i < hbMarkers.Length && string.IsNullOrEmpty(ev.markerName))
                {
                    targetMarker = hbMarkers[i];
                }

                if (targetMarker == null)
                {
                    string newName = string.IsNullOrEmpty(ev.markerName) ? $"HitboxMarker_{i}" : ev.markerName;
                    GameObject go = new GameObject(newName);
                    go.transform.SetParent(targetCharacter.transform);
                    go.transform.localPosition = Vector3.zero;
                    targetMarker = go.AddComponent<HitboxMarker>();
                    ArrayUtility.Add(ref hbMarkers, targetMarker);
                }

                targetMarker.isIncludeInBake = true;
                targetMarker.hitGroupID = ev.hitGroupID;
                targetMarker.attackHeight = ev.attackHeight;
                targetMarker.attackType = ev.attackType;
                targetMarker.targetHurtState = ev.targetHurtState;
                targetMarker.damage = ev.damage;
                targetMarker.hitstunFrames = ev.hitstunFrames;
                targetMarker.blockStunFrames = ev.blockStunFrames;
                targetMarker.localPushbackVector = ev.localPushbackVector;
                targetMarker.isHardKnockdown = ev.isHardKnockdown;
                
                targetMarker.recordStartFrame = ev.activeStartFrame; 
                targetMarker.recordEndFrame = (ev.boxPath != null && ev.boxPath.Length > 0) ? ev.activeStartFrame + ev.boxPath.Length - 1 : ev.activeStartFrame;

                if (ev.boxPath != null && ev.boxPath.Length > 0)
                {
                    targetMarker.boxExtents = ev.boxPath[0].extents;
                }
                EditorUtility.SetDirty(targetMarker);
            }
        }

        if (targetActionData.frameData.vfxEvents != null)
        {
            for (int i = 0; i < targetActionData.frameData.vfxEvents.Length; i++)
            {
                var ev = targetActionData.frameData.vfxEvents[i];
                VfxMarker targetMarker = null;

                if (!string.IsNullOrEmpty(ev.markerName))
                {
                    targetMarker = vfxMarkers.FirstOrDefault(m => m.gameObject.name == ev.markerName);
                }

                if (targetMarker == null && i < vfxMarkers.Length && string.IsNullOrEmpty(ev.markerName))
                {
                    targetMarker = vfxMarkers[i];
                }

                if (targetMarker == null)
                {
                    string newName = string.IsNullOrEmpty(ev.markerName) ? $"VfxMarker_{i}" : ev.markerName;
                    GameObject go = new GameObject(newName);
                    go.transform.SetParent(targetCharacter.transform);
                    go.transform.localPosition = Vector3.zero;
                    targetMarker = go.AddComponent<VfxMarker>();
                    ArrayUtility.Add(ref vfxMarkers, targetMarker);
                }

                targetMarker.isIncludeInBake = true;
                targetMarker.recordStartFrame = ev.startFrame;
                targetMarker.recordEndFrame = ev.endFrame;
                targetMarker.intervalFrames = ev.intervalFrames;
                targetMarker.effectType = ev.effectType;
                targetMarker.targetBone = ev.targetBone;
                targetMarker.isAttached = ev.isAttached;
                
                targetMarker.transform.localPosition = ev.localPositionOffset;
                targetMarker.transform.localRotation = ev.localRotationOffset;
                
                EditorUtility.SetDirty(targetMarker);
            }
        }
        
        SceneView.RepaintAll();
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
            hurtboxList.Add(new HurtboxEvent { startFrame = 0, endFrame = targetActionData.frameData.logicData.totalFrames, hurtboxType = Hurtbox_Type.Standing });
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
        showHitboxMarkers = EditorGUILayout.Foldout(showHitboxMarkers, "Hitbox Markers (In Character)", true, EditorStyles.foldoutHeader);

        if (showHitboxMarkers)
        {
            int maxFrames = Mathf.Max(1, targetActionData.frameData.logicData.totalFrames);

            foreach (var marker in markers)
            {
                int markerId = marker.GetInstanceID();
                if (!markerFoldoutStates.ContainsKey(markerId)) markerFoldoutStates[markerId] = false;

                GUILayout.BeginVertical("box");

                GUILayout.BeginHorizontal();
                markerFoldoutStates[markerId] = EditorGUILayout.Foldout(markerFoldoutStates[markerId], marker.gameObject.name, true);
                if (GUILayout.Button("Select & Resize in Scene", GUILayout.Width(180)))
                {
                    Selection.activeGameObject = marker.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
                GUILayout.EndHorizontal();

                if (markerFoldoutStates[markerId])
                {
                    EditorGUI.BeginChangeCheck();
                    
                    GUILayout.Space(5);
                    bool isIncluded = EditorGUILayout.Toggle("Include In Bake", marker.isIncludeInBake);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Frames", GUILayout.Width(60));
                    float minF = marker.recordStartFrame;
                    float maxF = marker.recordEndFrame;
                    minF = EditorGUILayout.IntField(Mathf.RoundToInt(minF), GUILayout.Width(40));
                    EditorGUILayout.MinMaxSlider(ref minF, ref maxF, 0, maxFrames);
                    maxF = EditorGUILayout.IntField(Mathf.RoundToInt(maxF), GUILayout.Width(40));
                    GUILayout.EndHorizontal();

                    int groupID = EditorGUILayout.IntField("Hit Group ID", marker.hitGroupID);
                    Attack_Height attackHeight = (Attack_Height)EditorGUILayout.EnumPopup("Attack Height", marker.attackHeight);
                    Attack_Type attackType = (Attack_Type)EditorGUILayout.EnumPopup("Attack Type", marker.attackType);
                    HurtState_Type targetState = (HurtState_Type)EditorGUILayout.EnumPopup("Target Hurt State", marker.targetHurtState);
                    int damage = EditorGUILayout.IntField("Damage", marker.damage);
                    int hitstun = EditorGUILayout.IntField("Hitstun Frames", marker.hitstunFrames);
                    int blockstun = EditorGUILayout.IntField("Blockstun Frames", marker.blockStunFrames);
                    Vector3 pushback = EditorGUILayout.Vector3Field("Local Pushback Vector", marker.localPushbackVector);
                    bool isKnockdown = EditorGUILayout.Toggle("Is Hard Knockdown", marker.isHardKnockdown);
                    Vector3 extents = EditorGUILayout.Vector3Field("Box Extents", marker.boxExtents);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(marker, "Modify Hitbox Marker");
                        marker.isIncludeInBake = isIncluded;
                        marker.recordStartFrame = Mathf.Max(0, Mathf.RoundToInt(minF));
                        marker.recordEndFrame = Mathf.Max(marker.recordStartFrame, Mathf.RoundToInt(maxF));
                        marker.hitGroupID = groupID;
                        marker.attackHeight = attackHeight;
                        marker.attackType = attackType;
                        marker.targetHurtState = targetState;
                        marker.damage = damage;
                        marker.hitstunFrames = hitstun;
                        marker.blockStunFrames = blockstun;
                        marker.localPushbackVector = pushback;
                        marker.isHardKnockdown = isKnockdown;
                        marker.boxExtents = extents;
                        EditorUtility.SetDirty(marker);
                    }
                }
                GUILayout.EndVertical();
            }
        }
    }

    private void DrawVfxMarkerEditor()
    {
        if (targetCharacter == null) return;
        VfxMarker[] markers = targetCharacter.GetComponentsInChildren<VfxMarker>();
        if (markers.Length == 0) return;

        EditorGUILayout.Space();
        showVfxMarkers = EditorGUILayout.Foldout(showVfxMarkers, "VFX Markers (In Character)", true, EditorStyles.foldoutHeader);

        if (showVfxMarkers)
        {
            int maxFrames = Mathf.Max(1, targetActionData.frameData.logicData.totalFrames);

            foreach (var marker in markers)
            {
                int markerId = marker.GetInstanceID();
                if (!vfxMarkerFoldoutStates.ContainsKey(markerId)) vfxMarkerFoldoutStates[markerId] = false;

                GUILayout.BeginVertical("box");
                
                GUILayout.BeginHorizontal();
                vfxMarkerFoldoutStates[markerId] = EditorGUILayout.Foldout(vfxMarkerFoldoutStates[markerId], marker.gameObject.name, true);
                if (GUILayout.Button("Select in Scene", GUILayout.Width(130)))
                {
                    Selection.activeGameObject = marker.gameObject;
                }
                GUILayout.EndHorizontal();

                if (vfxMarkerFoldoutStates[markerId])
                {
                    EditorGUI.BeginChangeCheck();
                    
                    GUILayout.Space(5);
                    bool isIncluded = EditorGUILayout.Toggle("Include In Bake", marker.isIncludeInBake);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Frames", GUILayout.Width(60));
                    float minF = marker.recordStartFrame;
                    float maxF = marker.recordEndFrame;
                    minF = EditorGUILayout.IntField(Mathf.RoundToInt(minF), GUILayout.Width(40));
                    EditorGUILayout.MinMaxSlider(ref minF, ref maxF, 0, maxFrames);
                    maxF = EditorGUILayout.IntField(Mathf.RoundToInt(maxF), GUILayout.Width(40));
                    GUILayout.EndHorizontal();
                    
                    int interval = EditorGUILayout.IntField("Interval Frames", marker.intervalFrames);
                    EffectType effect = (EffectType)EditorGUILayout.EnumPopup("Effect Type", marker.effectType);
                    HumanBodyBones bone = (HumanBodyBones)EditorGUILayout.EnumPopup("Target Bone", marker.targetBone);
                    bool attached = EditorGUILayout.Toggle("Is Attached", marker.isAttached);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(marker, "Modify VFX Marker");
                        marker.isIncludeInBake = isIncluded;
                        marker.recordStartFrame = Mathf.Max(0, Mathf.RoundToInt(minF));
                        marker.recordEndFrame = Mathf.Max(marker.recordStartFrame, Mathf.RoundToInt(maxF));
                        marker.intervalFrames = interval;
                        marker.effectType = effect;
                        marker.targetBone = bone;
                        marker.isAttached = attached;
                        EditorUtility.SetDirty(marker);
                    }
                }
                GUILayout.EndVertical();
            }
        }
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

        HitboxMarker[] hitboxMarkers = targetCharacter.GetComponentsInChildren<HitboxMarker>();
        foreach (var marker in hitboxMarkers) marker.currentPreviewFrame = currentPreviewFrame;

        VfxMarker[] vfxMarkers = targetCharacter.GetComponentsInChildren<VfxMarker>();
        foreach (var marker in vfxMarkers) marker.currentPreviewFrame = currentPreviewFrame;

        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (Selection.activeGameObject != null)
        {
            HitboxMarker activeMarker = Selection.activeGameObject.GetComponent<HitboxMarker>();
            if (activeMarker != null && activeMarker.isIncludeInBake)
            {
                Handles.matrix = activeMarker.transform.localToWorldMatrix;
                Handles.color = Color.green;

                boxBoundsHandle.center = Vector3.zero;
                boxBoundsHandle.size = activeMarker.boxExtents * 2f;

                EditorGUI.BeginChangeCheck();
                boxBoundsHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(activeMarker, "Resize Hitbox Extents");
                    activeMarker.boxExtents = boxBoundsHandle.size * 0.5f;
                    Repaint();
                }
            }
        }

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

    private void BakeRootMotionData()
    {
        if (targetActionData.animationClip == null || targetCharacter == null) return;
        
        if (!targetActionData.frameData.logicData.useRootMotion || targetRootBone == null)
        {
            targetActionData.frameData.rootMotionPath = new RootMotionData[0];
            return;
        }

        int totalFrames = targetActionData.frameData.logicData.totalFrames;
        targetActionData.frameData.rootMotionPath = new RootMotionData[totalFrames + 1];

        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
        }

        AnimationMode.SampleAnimationClip(targetCharacter, targetActionData.animationClip, 0f);
        Vector3 prevPos = targetCharacter.transform.InverseTransformPoint(targetRootBone.position);

        for (int frame = 0; frame <= totalFrames; frame++)
        {
            float currentTime = frame * Time.fixedDeltaTime;
            AnimationMode.SampleAnimationClip(targetCharacter, targetActionData.animationClip, currentTime);

            Vector3 currPos = targetCharacter.transform.InverseTransformPoint(targetRootBone.position);
            Quaternion currRot = Quaternion.Inverse(targetCharacter.transform.rotation) * targetRootBone.rotation;

            targetActionData.frameData.rootMotionPath[frame] = new RootMotionData
            {
                deltaPosition = currPos - prevPos,
            };

            prevPos = currPos;
        }
    }

    private void BakeHitboxData()
    {
        if (targetActionData.animationClip == null || targetCharacter == null) return;

        int totalFrames = targetActionData.frameData.logicData.totalFrames;
        HitboxMarker[] markers = targetCharacter.GetComponentsInChildren<HitboxMarker>();
        List<HitboxEvent> bakedEvents = new List<HitboxEvent>();

        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();

        Vector3[] accumulatedRootPos = new Vector3[totalFrames + 1];
        if (targetActionData.frameData.logicData.useRootMotion && targetRootBone != null)
        {
            AnimationMode.SampleAnimationClip(targetCharacter, targetActionData.animationClip, 0f);
            Vector3 startRootPos = targetCharacter.transform.InverseTransformPoint(targetRootBone.position);

            for (int frame = 0; frame <= totalFrames; frame++)
            {
                AnimationMode.SampleAnimationClip(targetCharacter, targetActionData.animationClip, frame * Time.fixedDeltaTime);
                Vector3 currentRootPos = targetCharacter.transform.InverseTransformPoint(targetRootBone.position);
                accumulatedRootPos[frame] = currentRootPos - startRootPos;
            }
        }
        else
        {
            for (int frame = 0; frame <= totalFrames; frame++) accumulatedRootPos[frame] = Vector3.zero;
        }

        foreach (var marker in markers)
        {
            if (!marker.isIncludeInBake) continue;
            if (marker.recordStartFrame < 0 || marker.recordEndFrame >= totalFrames || marker.recordStartFrame > marker.recordEndFrame) continue;

            HitboxEvent newEvent = new HitboxEvent
            {
                markerName = marker.gameObject.name,
                activeStartFrame = marker.recordStartFrame,
                hitGroupID = marker.hitGroupID,
                attackHeight = marker.attackHeight,
                attackType = marker.attackType,
                targetHurtState = marker.targetHurtState,
                damage = marker.damage,
                hitstunFrames = marker.hitstunFrames,
                blockStunFrames = marker.blockStunFrames,
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
                    localPosition = targetCharacter.transform.InverseTransformPoint(marker.transform.position) - accumulatedRootPos[frame],
                    extents = marker.boxExtents
                };

                newEvent.boxPath[frame - marker.recordStartFrame] = box;
            }

            bakedEvents.Add(newEvent);
        }

        targetActionData.frameData.hitboxEvents = bakedEvents.ToArray();
    }

    private void BakeVfxData()
    {
        if (targetActionData.animationClip == null || targetCharacter == null) return;

        int totalFrames = targetActionData.frameData.logicData.totalFrames;
        VfxMarker[] markers = targetCharacter.GetComponentsInChildren<VfxMarker>();
        System.Collections.Generic.List<VfxEvent> bakedEvents = new System.Collections.Generic.List<VfxEvent>();

        foreach (var marker in markers)
        {
            if (!marker.isIncludeInBake) continue;
            if (marker.recordStartFrame < 0 || marker.recordEndFrame >= totalFrames || marker.recordStartFrame > marker.recordEndFrame) continue;

            VfxEvent newEvent = new VfxEvent
            {
                markerName = marker.gameObject.name,
                startFrame = marker.recordStartFrame,
                endFrame = marker.recordEndFrame,
                intervalFrames = marker.intervalFrames,
                effectType = marker.effectType,
                targetBone = marker.targetBone,
                isAttached = marker.isAttached,
                localPositionOffset = marker.transform.localPosition,
                localRotationOffset = marker.transform.localRotation
            };

            bakedEvents.Add(newEvent);
        }

        targetActionData.frameData.vfxEvents = bakedEvents.ToArray();
    }
}