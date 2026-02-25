using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ComboNode
{
    public InputFlags requiredInput;
    public AnimationClip animationClip;
    public string animationStateName;
    public AnimationFrameData frameData;
    public List<ComboNode> nextAttacks = new List<ComboNode>();
}

[CreateAssetMenu(fileName = "ComboTree", menuName = "ScriptableObjects/ComboTree")]
public class ComboTreeSO : ScriptableObject
{
    public List<ComboNode> startingAttacks = new List<ComboNode>();

    public ComboNode GetNodeFromSequence(List<InputFlags> sequence)
    {
        if (sequence == null || sequence.Count == 0) return null;

        ComboNode currentNode = null;
        List<ComboNode> currentList = startingAttacks;

        for (int i = 0; i < sequence.Count; i++)
        {
            InputFlags input = sequence[i];
            bool isFound = false;

            if (currentList != null)
            {
                foreach (var node in currentList)
                {
                    if (node.requiredInput == input)
                    {
                        currentNode = node;
                        currentList = node.nextAttacks;
                        isFound = true;
                        break;
                    }
                }
            }

            if (!isFound) return null;
        }

        return currentNode;
    }

    private void OnValidate()
    {
        if (startingAttacks != null)
        {
            foreach (var node in startingAttacks)
            {
                CalculateFrameDataRecursive(node);
            }
        }
    }

    private void CalculateFrameDataRecursive(ComboNode node)
    {
        if (node == null) return;

        if (node.animationClip != null)
        {
            int calculatedTotalFrames = Mathf.RoundToInt(node.animationClip.length / Time.fixedDeltaTime);

            if (node.frameData.totalFrames != calculatedTotalFrames)
            {
                node.frameData.totalFrames = calculatedTotalFrames;
                
                int baseSplit = calculatedTotalFrames / 3;
                int remainderFrames = calculatedTotalFrames % 3;

                node.frameData.startupFrames = baseSplit;
                node.frameData.activeFrames = baseSplit;
                node.frameData.recoveryFrames = baseSplit + remainderFrames;
                node.frameData.cancelWindowStartFrame = node.frameData.startupFrames + node.frameData.activeFrames;

                if (string.IsNullOrEmpty(node.animationStateName))
                {
                    node.animationStateName = node.animationClip.name;
                }
            }
        }

        if (node.nextAttacks != null)
        {
            foreach (var nextNode in node.nextAttacks)
            {
                CalculateFrameDataRecursive(nextNode);
            }
        }
    }
}