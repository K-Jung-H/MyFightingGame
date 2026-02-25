using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[System.Serializable]
public class CommandStep
{
    public InputFlags requiredFlags;
    public bool isExactMatchRequired;
}

[System.Serializable]
public class CommandDefinition
{
    public string commandName;
    public int priority;
    public List<CommandStep> sequence;
    public int timeWindowFrames = 15;
    public PlayerState targetState;
    public List<PlayerState> validStates;

    public AnimationClip animationClip;
    public string animationStateName;
    public AnimationFrameData frameData;
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


    private void OnValidate()
    {
        if (commands != null)
        {
            foreach (var cmd in commands)
            {
                if (cmd.animationClip != null)
                {
                    int calculatedTotalFrames = Mathf.RoundToInt(cmd.animationClip.length / Time.fixedDeltaTime);

                    if (cmd.frameData.totalFrames != calculatedTotalFrames)
                    {
                        cmd.frameData.totalFrames = calculatedTotalFrames;
                        
                        int baseSplit = calculatedTotalFrames / 3;
                        int remainderFrames = calculatedTotalFrames % 3;

                        cmd.frameData.startupFrames = baseSplit;
                        cmd.frameData.activeFrames = baseSplit;
                        cmd.frameData.recoveryFrames = baseSplit + remainderFrames;
                        cmd.frameData.cancelWindowStartFrame = cmd.frameData.startupFrames + cmd.frameData.activeFrames;
                    }

                    if (string.IsNullOrEmpty(cmd.animationStateName))
                    {
                        cmd.animationStateName = cmd.animationClip.name;
                    }
                }
            }
        }
    }
}