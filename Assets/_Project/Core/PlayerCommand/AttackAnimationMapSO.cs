using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "AttackAnimationMap", menuName = "ScriptableObjects/AttackAnimationMap")]
public class AttackAnimationMapSO : ScriptableObject
{
    public List<ActionDataSO> basicAttacks;
    public List<ActionDataSO> commandAttacks;
    public List<ActionDataSO> aerialAttacks;

    public void CollectAllActions(HashSet<ActionDataSO> actionSet)
    {
        AddActionsToSet(basicAttacks, actionSet);
        AddActionsToSet(commandAttacks, actionSet);
        AddActionsToSet(aerialAttacks, actionSet);
    }

    private void AddActionsToSet(List<ActionDataSO> actions, HashSet<ActionDataSO> actionSet)
    {
        bool isListValid = actions != null;
        if (!isListValid) return;

        foreach (var action in actions)
        {
            bool isActionValid = action != null;
            if (isActionValid)
            {
                actionSet.Add(action);
            }
        }
    }
}