using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class ComboGraphNode : Node
{
    public string nodeGuid;
    public InputFlags requiredInput;
    public ActionDataSO actionData;

    public EnumFlagsField inputEnumField;
    public ObjectField actionObjectField;
}

public class ComboGraphWindow : EditorWindow
{
    private ComboGraphView comboGraphView;
    private ComboTreeSO targetComboTree;

    [MenuItem("Tools/Combo Graph Editor")]
    public static void OpenGraphWindow()
    {
        ComboGraphWindow window = GetWindow<ComboGraphWindow>();
        window.titleContent = new GUIContent("Combo Graph Editor");
        window.Show();
    }

    private void OnEnable()
    {
        InitializeGraphView();
        InitializeToolbar();
    }

    private void OnDisable()
    {
        bool isGraphViewValid = comboGraphView != null;
        if (isGraphViewValid)
        {
            rootVisualElement.Remove(comboGraphView);
        }
    }

    private void InitializeGraphView()
    {
        comboGraphView = new ComboGraphView();
        comboGraphView.StretchToParentSize();
        rootVisualElement.Add(comboGraphView);
    }

    private void InitializeToolbar()
    {
        Toolbar toolbar = new Toolbar();

        ObjectField treeAssetField = new ObjectField("Target Tree Asset");
        treeAssetField.objectType = typeof(ComboTreeSO);
        treeAssetField.RegisterValueChangedCallback(evt => 
        {
            targetComboTree = (ComboTreeSO)evt.newValue;
        });
        toolbar.Add(treeAssetField);

        Button loadButton = new Button(LoadGraphData);
        loadButton.text = "Load Data";
        toolbar.Add(loadButton);

        Button saveButton = new Button(SaveGraphData);
        saveButton.text = "Save Data";
        toolbar.Add(saveButton);

        rootVisualElement.Add(toolbar);
    }

    private void SaveGraphData()
    {
        bool isTreeValid = targetComboTree != null;
        if (isTreeValid)
        {
            GraphSaveUtility saveUtility = GraphSaveUtility.GetInstance(comboGraphView, targetComboTree);
            saveUtility.SaveGraph();
        }
        else
        {
            Debug.LogWarning("Target Tree Asset is not assigned!");
        }
    }

    private void LoadGraphData()
    {
        bool isTreeValid = targetComboTree != null;
        if (isTreeValid)
        {
            GraphSaveUtility saveUtility = GraphSaveUtility.GetInstance(comboGraphView, targetComboTree);
            saveUtility.LoadGraph();
        }
        else
        {
            Debug.LogWarning("Target Tree Asset is not assigned!");
        }
    }
}

public class ComboGraphView : GraphView
{
    public ComboGraphView()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        GridBackground gridBackground = new GridBackground();
        Insert(0, gridBackground);
        gridBackground.StretchToParentSize();
    }

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        base.BuildContextualMenu(evt);

        evt.menu.AppendAction("Create Node", action => 
        {
            Vector2 mousePosition = action.eventInfo.mousePosition;
            Vector2 localPosition = contentViewContainer.WorldToLocal(mousePosition);
            CreateNode("Empty Node", localPosition);
        });
    }

    public ComboGraphNode CreateNode(string nodeName, Vector2 position)
    {
        ComboGraphNode comboNode = new ComboGraphNode
        {
            title = nodeName,
            nodeGuid = Guid.NewGuid().ToString()
        };

        Port inputPort = GeneratePort(comboNode, Direction.Input, Port.Capacity.Multi);
        inputPort.portName = "Input";
        comboNode.inputContainer.Add(inputPort);

        Port outputPort = GeneratePort(comboNode, Direction.Output, Port.Capacity.Multi);
        outputPort.portName = "Next Attacks";
        comboNode.outputContainer.Add(outputPort);

        EnumFlagsField inputEnumField = new EnumFlagsField("Required Input", InputFlags.None);
        inputEnumField.RegisterValueChangedCallback(evt =>
        {
            comboNode.requiredInput = (InputFlags)evt.newValue;
        });
        comboNode.inputEnumField = inputEnumField;
        comboNode.mainContainer.Add(inputEnumField);

        ObjectField actionObjectField = new ObjectField("Action Data");
        actionObjectField.objectType = typeof(ActionDataSO);
        actionObjectField.RegisterValueChangedCallback(evt =>
        {
            comboNode.actionData = (ActionDataSO)evt.newValue;
            
            bool hasValidData = comboNode.actionData != null && !string.IsNullOrEmpty(comboNode.actionData.animationStateName);
            if (hasValidData)
            {
                comboNode.title = comboNode.actionData.animationStateName;
            }
            else
            {
                comboNode.title = "Empty Node";
            }
        });
        comboNode.actionObjectField = actionObjectField;
        comboNode.mainContainer.Add(actionObjectField);

        comboNode.RefreshExpandedState();
        comboNode.RefreshPorts();
        comboNode.SetPosition(new Rect(position, new Vector2(250, 150)));

        AddElement(comboNode);

        return comboNode;
    }

    private Port GeneratePort(Node node, Direction portDirection, Port.Capacity capacity)
    {
        return node.InstantiatePort(Orientation.Horizontal, portDirection, capacity, typeof(float));
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        List<Port> compatiblePorts = new List<Port>();
        
        ports.ForEach(port => 
        {
            bool isSameNode = startPort.node == port.node;
            bool isSameDirection = startPort.direction == port.direction;
            
            if (!isSameNode && !isSameDirection)
            {
                bool isCycle = false;
                
                bool isForwardConnection = startPort.direction == Direction.Output && port.direction == Direction.Input;
                if (isForwardConnection)
                {
                    isCycle = CheckForCycle(startPort.node, port.node);
                }
                else
                {
                    isCycle = CheckForCycle(port.node, startPort.node);
                }

                if (!isCycle)
                {
                    compatiblePorts.Add(port);
                }
            }
        });
        
        return compatiblePorts;
    }

    private bool CheckForCycle(Node parentNode, Node childNode)
    {
        Stack<Node> nodesToVisit = new Stack<Node>();
        HashSet<Node> visitedNodes = new HashSet<Node>();
        
        nodesToVisit.Push(childNode);

        while (nodesToVisit.Count > 0)
        {
            Node currentNode = nodesToVisit.Pop();
            
            bool isAlreadyVisited = visitedNodes.Contains(currentNode);
            if (isAlreadyVisited) continue;
            
            visitedNodes.Add(currentNode);

            bool isCycleDetected = currentNode == parentNode;
            if (isCycleDetected) return true;

            Port outputPort = currentNode.outputContainer[0] as Port;
            bool hasOutputPort = outputPort != null;
            
            if (hasOutputPort)
            {
                foreach (Edge edge in outputPort.connections)
                {
                    nodesToVisit.Push(edge.input.node);
                }
            }
        }

        return false;
    }
}

public class GraphSaveUtility
{
    private ComboGraphView targetGraphView;
    private ComboTreeSO targetTreeAsset;

    public static GraphSaveUtility GetInstance(ComboGraphView graphView, ComboTreeSO treeAsset)
    {
        return new GraphSaveUtility
        {
            targetGraphView = graphView,
            targetTreeAsset = treeAsset
        };
    }

    public void SaveGraph()
    {
        bool isInvalidTarget = targetTreeAsset == null;
        if (isInvalidTarget) return;

        List<ComboGraphNode> rootNodes = GetRootNodes();
        targetTreeAsset.startingAttacks.Clear();

        foreach (ComboGraphNode rootNode in rootNodes)
        {
            ComboNode newComboNode = CreateComboNode(rootNode);
            targetTreeAsset.startingAttacks.Add(newComboNode);
        }

        EditorUtility.SetDirty(targetTreeAsset);
        AssetDatabase.SaveAssets();
    }

    private List<ComboGraphNode> GetRootNodes()
    {
        List<ComboGraphNode> allNodes = targetGraphView.nodes.ToList().Cast<ComboGraphNode>().ToList();
        List<ComboGraphNode> rootNodes = new List<ComboGraphNode>();

        foreach (ComboGraphNode node in allNodes)
        {
            Port inputPort = node.inputContainer[0] as Port;
            bool isRoot = !inputPort.connected;
            if (isRoot)
            {
                rootNodes.Add(node);
            }
        }

        return rootNodes;
    }

    private ComboNode CreateComboNode(ComboGraphNode graphNode)
    {
        ComboNode nodeData = new ComboNode
        {
            requiredInput = graphNode.requiredInput,
            actionData = graphNode.actionData,
            nextAttacks = new List<ComboNode>()
        };

        Port outputPort = graphNode.outputContainer[0] as Port;
        foreach (Edge edge in outputPort.connections)
        {
            ComboGraphNode targetGraphNode = edge.input.node as ComboGraphNode;
            bool isTargetValid = targetGraphNode != null;
            if (isTargetValid)
            {
                ComboNode childNode = CreateComboNode(targetGraphNode);
                nodeData.nextAttacks.Add(childNode);
            }
        }

        return nodeData;
    }

    public void LoadGraph()
    {
        bool isInvalidTarget = targetTreeAsset == null;
        if (isInvalidTarget) return;

        ClearGraph();

        Dictionary<ComboNode, ComboGraphNode> nodeMap = new Dictionary<ComboNode, ComboGraphNode>();

        float currentXOffset = 100f;
        float currentYOffset = 100f;

        foreach (ComboNode rootCombo in targetTreeAsset.startingAttacks)
        {
            CreateGraphNodeRecursively(rootCombo, null, ref currentXOffset, ref currentYOffset, nodeMap);
            currentXOffset += 300f;
            currentYOffset = 100f;
        }
    }

    private void CreateGraphNodeRecursively(ComboNode comboData, ComboGraphNode parentGraphNode, ref float xOffset, ref float yOffset, Dictionary<ComboNode, ComboGraphNode> nodeMap)
    {
        bool isAlreadyCreated = nodeMap.ContainsKey(comboData);
        ComboGraphNode currentGraphNode;

        if (isAlreadyCreated)
        {
            currentGraphNode = nodeMap[comboData];
        }
        else
        {
            string nodeName = "Empty Node";
            bool hasValidAction = comboData.actionData != null && !string.IsNullOrEmpty(comboData.actionData.animationStateName);
            if (hasValidAction)
            {
                nodeName = comboData.actionData.animationStateName;
            }

            currentGraphNode = targetGraphView.CreateNode(nodeName, new Vector2(xOffset, yOffset));
            currentGraphNode.requiredInput = comboData.requiredInput;
            currentGraphNode.actionData = comboData.actionData;

            bool hasEnumField = currentGraphNode.inputEnumField != null;
            if (hasEnumField)
            {
                currentGraphNode.inputEnumField.SetValueWithoutNotify(comboData.requiredInput);
            }

            bool hasObjectField = currentGraphNode.actionObjectField != null;
            if (hasObjectField)
            {
                currentGraphNode.actionObjectField.SetValueWithoutNotify(comboData.actionData);
            }

            nodeMap.Add(comboData, currentGraphNode);
            yOffset += 250f;
        }

        bool hasParent = parentGraphNode != null;
        if (hasParent)
        {
            Port parentOutput = parentGraphNode.outputContainer[0] as Port;
            Port currentInput = currentGraphNode.inputContainer[0] as Port;

            Edge newEdge = parentOutput.ConnectTo(currentInput);
            targetGraphView.AddElement(newEdge);
        }

        foreach (ComboNode childCombo in comboData.nextAttacks)
        {
            CreateGraphNodeRecursively(childCombo, currentGraphNode, ref xOffset, ref yOffset, nodeMap);
        }
    }

    private void ClearGraph()
    {
        List<Edge> edges = targetGraphView.edges.ToList();
        foreach (Edge edge in edges)
        {
            targetGraphView.RemoveElement(edge);
        }

        List<ComboGraphNode> nodes = targetGraphView.nodes.ToList().Cast<ComboGraphNode>().ToList();
        foreach (ComboGraphNode node in nodes)
        {
            targetGraphView.RemoveElement(node);
        }
    }
}