using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class CommandListEditorWindow : EditorWindow
{
    private CommandListSO targetCommandList;
    private ListView commandListView;
    private ScrollView rightPane;
    private CommandDefinition currentSelection;

    [MenuItem("Tools/Command List Editor")]
    public static void ShowWindow()
    {
        CommandListEditorWindow window = GetWindow<CommandListEditorWindow>();
        window.titleContent = new GUIContent("Command List Editor");
        window.Show();
    }

    private void OnEnable()
    {
        InitializeLayout();
    }

    private void InitializeLayout()
    {
        Toolbar toolbar = new Toolbar();

        ObjectField listAssetField = new ObjectField("Target Command List");
        listAssetField.objectType = typeof(CommandListSO);
        listAssetField.RegisterValueChangedCallback(evt =>
        {
            targetCommandList = (CommandListSO)evt.newValue;
            RefreshCommandList();
        });
        toolbar.Add(listAssetField);

        Button addButton = new Button(AddNewCommand) { text = "Add Command" };
        toolbar.Add(addButton);

        Button sortButton = new Button(SortCommands) { text = "Sort By Priority" };
        toolbar.Add(sortButton);

        rootVisualElement.Add(toolbar);

        VisualElement separator = new VisualElement { style = { height = 2, backgroundColor = new Color(0.1f, 0.1f, 0.1f), marginBottom = 2, marginTop = 2 } };
        rootVisualElement.Add(separator);

        TwoPaneSplitView splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
        
        VisualElement leftPane = new VisualElement { style = { flexGrow = 1 } };
        Label leftHeader = new Label("Command List") { style = { unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 5, paddingTop = 5, paddingBottom = 5, backgroundColor = new Color(0.2f, 0.2f, 0.2f) } };
        leftPane.Add(leftHeader);

        commandListView = new ListView();
        commandListView.style.flexGrow = 1;
        commandListView.makeItem = () => 
        {
            Label label = new Label { style = { paddingLeft = 5, paddingTop = 2, paddingBottom = 2 } };
            label.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                CommandDefinition cmd = label.userData as CommandDefinition;
                bool isCmdValid = cmd != null;
                if (isCmdValid)
                {
                    evt.menu.AppendAction("Delete Command", action => DeleteCommand(cmd));
                }
            }));
            return label;
        };

        commandListView.bindItem = (element, i) =>
        {
            bool isListValid = targetCommandList != null && targetCommandList.commands != null;
            if (isListValid)
            {
                Label label = element as Label;
                label.text = targetCommandList.commands[i].commandName;
                label.userData = targetCommandList.commands[i];
            }
        };
        
        commandListView.selectionChanged += OnCommandSelected;
        
        commandListView.AddManipulator(new ContextualMenuManipulator(evt => 
        {
            evt.menu.AppendAction("Add Command", action => AddNewCommand());
        }));

        leftPane.Add(commandListView);
        splitView.Add(leftPane);

        VisualElement rightWrapper = new VisualElement { style = { flexGrow = 1 } };
        Label rightHeader = new Label("Properties") { style = { unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 5, paddingTop = 5, paddingBottom = 5, backgroundColor = new Color(0.2f, 0.2f, 0.2f) } };
        rightWrapper.Add(rightHeader);

        rightPane = new ScrollView { style = { paddingLeft = 10, paddingRight = 10, paddingTop = 10, flexGrow = 1 } };
        rightWrapper.Add(rightPane);
        
        splitView.Add(rightWrapper);

        rootVisualElement.Add(splitView);
    }

    private void AddNewCommand()
    {
        bool isListInvalid = targetCommandList == null;
        if (isListInvalid) return;

        bool isCommandsNull = targetCommandList.commands == null;
        if (isCommandsNull)
        {
            targetCommandList.commands = new List<CommandDefinition>();
        }

        CommandDefinition newCommand = new CommandDefinition
        {
            commandName = "New Command",
            priority = 0,
            timeWindowFrames = 15,
            sequence = new List<CommandStep>(),
            validStates = (PlayerState_Type)0 
        };

        targetCommandList.commands.Add(newCommand);
        MarkAssetDirty();
        RefreshCommandList();
    }

    private void DeleteCommand(CommandDefinition cmd)
    {
        bool isListValid = targetCommandList != null && targetCommandList.commands != null;
        if (isListValid)
        {
            targetCommandList.commands.Remove(cmd);
            MarkAssetDirty();
            RefreshCommandList();
        }
    }

    private void SortCommands()
    {
        bool isListValid = targetCommandList != null;
        if (isListValid)
        {
            targetCommandList.SortCommands();
            MarkAssetDirty();
            RefreshCommandList();
        }
    }

    private void RefreshCommandList()
    {
        bool isListValid = targetCommandList != null && targetCommandList.commands != null;
        if (isListValid)
        {
            commandListView.itemsSource = targetCommandList.commands;
            commandListView.Rebuild();
        }
        else
        {
            commandListView.itemsSource = null;
            commandListView.Rebuild();
        }
        
        rightPane.Clear();
        currentSelection = null;
    }

    private void OnCommandSelected(IEnumerable<object> selection)
    {
        rightPane.Clear();
        IEnumerator<object> enumerator = selection.GetEnumerator();
        
        bool hasSelection = enumerator.MoveNext();
        if (hasSelection)
        {
            currentSelection = enumerator.Current as CommandDefinition;
            DrawCommandDetails();
        }
    }

    private void DrawCommandDetails()
    {
        bool isSelectionInvalid = currentSelection == null;
        if (isSelectionInvalid) return;

        TextField nameField = new TextField("Command Name") { value = currentSelection.commandName };
        nameField.RegisterValueChangedCallback(evt => 
        { 
            currentSelection.commandName = evt.newValue; 
            commandListView.RefreshItems();
            MarkAssetDirty();
        });
        rightPane.Add(nameField);

        IntegerField priorityField = new IntegerField("Priority") { value = currentSelection.priority };
        priorityField.RegisterValueChangedCallback(evt => 
        { 
            currentSelection.priority = evt.newValue; 
            MarkAssetDirty();
        });
        rightPane.Add(priorityField);

        IntegerField timeWindowField = new IntegerField("Time Window (Frames)") { value = currentSelection.timeWindowFrames };
        timeWindowField.RegisterValueChangedCallback(evt => 
        { 
            currentSelection.timeWindowFrames = evt.newValue; 
            MarkAssetDirty();
        });
        rightPane.Add(timeWindowField);

        EnumField targetStateField = new EnumField("Target State", currentSelection.targetState);
        targetStateField.RegisterValueChangedCallback(evt => 
        { 
            currentSelection.targetState = (PlayerState_Type)evt.newValue; 
            MarkAssetDirty();
        });
        rightPane.Add(targetStateField);

        EnumFlagsField validStatesField = new EnumFlagsField("Valid States", currentSelection.validStates);
        validStatesField.RegisterValueChangedCallback(evt => 
        { 
            currentSelection.validStates = (PlayerState_Type)evt.newValue; 
            MarkAssetDirty();
        });
        rightPane.Add(validStatesField);

        VisualElement summaryContainer = new VisualElement();

        ObjectField actionDataField = new ObjectField("Action Data") 
        { 
            objectType = typeof(ActionDataSO), 
            value = currentSelection.actionData 
        };
        actionDataField.RegisterValueChangedCallback(evt => 
        { 
            currentSelection.actionData = (ActionDataSO)evt.newValue; 
            DrawActionSummary(currentSelection.actionData, summaryContainer);
            MarkAssetDirty();
        });
        rightPane.Add(actionDataField);
        rightPane.Add(summaryContainer);

        DrawActionSummary(currentSelection.actionData, summaryContainer);

        rightPane.Add(new Label("Command Sequence") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 15, marginBottom = 5 } });
        DrawSequenceList();
    }

    private void DrawActionSummary(ActionDataSO actionData, VisualElement container)
    {
        container.Clear();
        
        bool hasValidFrameData = actionData != null && actionData.frameData != null;

        if (hasValidFrameData)
        {
            VisualElement box = new VisualElement 
            { 
                style = 
                { 
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f), 
                    paddingBottom = 5, 
                    paddingTop = 5, 
                    paddingLeft = 5, 
                    marginTop = 5, 
                    marginBottom = 5, 
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3
                } 
            };
            
            ActionLogicData logic = actionData.frameData.logicData;
            
            box.Add(new Label("[Logic Summary]") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.8f, 0.6f, 1f) } });
            box.Add(new Label($"Total Frames: {logic.totalFrames} | Cancel Start: {logic.cancelWindowStartFrame}"));
            box.Add(new Label($"Use Root Motion: {logic.useRootMotion}"));
            box.Add(new Label($"Is Homing: {logic.isHoming}"));

            bool hasValidHitbox = actionData.frameData.hitboxEvents != null && actionData.frameData.hitboxEvents.Length > 0;

            if (hasValidHitbox)
            {
                HitboxEvent firstHit = actionData.frameData.hitboxEvents[0];
                
                box.Add(new Label("[Hitbox Summary]") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.7f, 0.7f, 1f), marginTop = 5 } });
                box.Add(new Label($"Attack Height: {firstHit.attackHeight}"));
                box.Add(new Label($"Damage: {firstHit.damage}"));
                box.Add(new Label($"Stun (Hit/Block): {firstHit.hitstunFrames} / {firstHit.blockStunFrames}"));
            }

            container.Add(box);
        }
    }

    private void DrawSequenceList()
    {
        bool isSequenceNull = currentSelection.sequence == null;
        if (isSequenceNull)
        {
            currentSelection.sequence = new List<CommandStep>();
        }

        for (int i = 0; i < currentSelection.sequence.Count; i++)
        {
            int index = i;
            CommandStep step = currentSelection.sequence[index];

            VisualElement stepRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };

            EnumFlagsField inputField = new EnumFlagsField(step.requiredFlags) { style = { flexGrow = 1 } };
            inputField.RegisterValueChangedCallback(evt => 
            {
                step.requiredFlags = (InputFlags)evt.newValue;
                MarkAssetDirty();
            });
            stepRow.Add(inputField);

            EnumField executeTypeField = new EnumField(step.executeType) { style = { width = 60 } };
            IntegerField holdFramesField = new IntegerField() { value = step.requiredHoldFrames, style = { width = 40 } };

            holdFramesField.RegisterValueChangedCallback(evt => 
            {
                step.requiredHoldFrames = evt.newValue;
                MarkAssetDirty();
            });

            holdFramesField.style.display = step.executeType == InputExecuteType.Hold ? DisplayStyle.Flex : DisplayStyle.None;

            executeTypeField.RegisterValueChangedCallback(evt => 
            {
                step.executeType = (InputExecuteType)evt.newValue;
                
                bool isHoldType = step.executeType == InputExecuteType.Hold;
                holdFramesField.style.display = isHoldType ? DisplayStyle.Flex : DisplayStyle.None;

                if (!isHoldType)
                {
                    step.requiredHoldFrames = 0;
                    holdFramesField.SetValueWithoutNotify(0);
                }

                MarkAssetDirty();
            });

            stepRow.Add(executeTypeField);
            stepRow.Add(holdFramesField);

            Toggle exactMatchToggle = new Toggle("Exact Match") { value = step.isExactMatchRequired };
            exactMatchToggle.RegisterValueChangedCallback(evt => 
            {
                step.isExactMatchRequired = evt.newValue;
                MarkAssetDirty();
            });
            stepRow.Add(exactMatchToggle);

            Button removeButton = new Button(() => 
            {
                currentSelection.sequence.RemoveAt(index);
                MarkAssetDirty();
                RefreshDetailsView();
            }) { text = "X", style = { width = 25 } };
            stepRow.Add(removeButton);

            rightPane.Add(stepRow);
        }

        Button addStepButton = new Button(() => 
        {
            currentSelection.sequence.Add(new CommandStep { requiredFlags = InputFlags.None, isExactMatchRequired = false, executeType = InputExecuteType.Tap, requiredHoldFrames = 0 });
            MarkAssetDirty();
            RefreshDetailsView();
        }) { text = "+ Add Step", style = { marginTop = 5 } };
        rightPane.Add(addStepButton);
    }

    private void RefreshDetailsView()
    {
        rightPane.Clear();
        DrawCommandDetails();
    }

    private void MarkAssetDirty()
    {
        bool isListValid = targetCommandList != null;
        if (isListValid)
        {
            EditorUtility.SetDirty(targetCommandList);
            AssetDatabase.SaveAssets();
        }
    }
}