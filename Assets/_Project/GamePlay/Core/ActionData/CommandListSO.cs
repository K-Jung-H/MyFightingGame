using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum InputExecuteType
{
    Tap,
    Hold
}

[System.Serializable]
public class CommandStep
{
    public InputFlags requiredFlags;
    public InputExecuteType executeType;
    public int requiredHoldFrames;
    public bool isExactMatchRequired;
}

[System.Serializable]
public class CommandDefinition
{
    public string commandName;
    public int priority;
    public List<CommandStep> sequence;
    public int timeWindowFrames = 15;
    public PlayerState_Type targetState;
    public PlayerState_Type validStates;
    public ActionDataSO actionData;
}

[CreateAssetMenu(fileName = "CommandList", menuName = "ScriptableObjects/CommandList")]
public class CommandListSO : ScriptableObject
{
    public List<CommandDefinition> commands;

    public void SortCommands()
    {
        if (commands != null)
        {
            commands = commands.OrderByDescending(c => c.priority).ThenByDescending(c => c.sequence.Count).ToList();
        }
    }
}