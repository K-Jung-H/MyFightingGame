using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct CharacterAnimationMap
{
    public StateAnimationMapSO stateMap;
    public CommandListSO commandList;
    public ComboTreeSO comboTree;
}

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Character/Character Data")]
public class CharacterDataSO : ScriptableObject
{
    public GameObject characterPrefab;
    public PlayerConfigSO config;
    public CharacterAnimationMap animationMap;
    public EffectTableSO effectTable;

    public List<ActionDataSO> GetAllRegisteredActions()
    {
        HashSet<ActionDataSO> uniqueActions = new HashSet<ActionDataSO>();

        CollectActionsFromCommandList(uniqueActions);
        CollectActionsFromComboTree(uniqueActions);

        return new List<ActionDataSO>(uniqueActions);
    }

    private void CollectActionsFromCommandList(HashSet<ActionDataSO> actionSet)
    {
        bool hasCommandList = animationMap.commandList != null && animationMap.commandList.commands != null;
        if (!hasCommandList) return;

        foreach (var command in animationMap.commandList.commands)
        {
            bool isActionValid = command.actionData != null;
            if (isActionValid)
            {
                actionSet.Add(command.actionData);
            }
        }
    }

    private void CollectActionsFromComboTree(HashSet<ActionDataSO> actionSet)
    {
        bool hasComboTree = animationMap.comboTree != null && animationMap.comboTree.startingAttacks != null;
        if (!hasComboTree) return;

        TraverseComboNode(animationMap.comboTree.startingAttacks, actionSet);
    }

    private void TraverseComboNode(List<ComboNode> nodes, HashSet<ActionDataSO> actionSet)
    {
        bool isNodesValid = nodes != null;
        if (!isNodesValid) return;

        foreach (var node in nodes)
        {
            bool isNodeActionValid = node != null && node.actionData != null;
            if (isNodeActionValid)
            {
                actionSet.Add(node.actionData);
            }

            TraverseComboNode(node.nextAttacks, actionSet);
        }
    }
}