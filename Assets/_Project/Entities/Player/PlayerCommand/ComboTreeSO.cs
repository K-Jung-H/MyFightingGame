using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ComboNode
{
    public InputFlags requiredInput;
    public string animationTrigger;
    public int attackFrameLimit = 30;
    public List<ComboNode> nextAttacks;
}

[CreateAssetMenu(fileName = "ComboTree", menuName = "ScriptableObjects/ComboTree")]
public class ComboTreeSO : ScriptableObject
{
    public List<ComboNode> startingAttacks;

    public ComboNode GetNodeFromSequence(List<InputFlags> sequence)
    {
        if (sequence == null || sequence.Count == 0) return null;

        ComboNode currentNode = null;
        List<ComboNode> currentList = startingAttacks;

        for (int i = 0; i < sequence.Count; i++)
        {
            InputFlags input = sequence[i];
            bool found = false;

            if (currentList != null)
            {
                foreach (var node in currentList)
                {
                    if (node.requiredInput == input)
                    {
                        currentNode = node;
                        currentList = node.nextAttacks;
                        found = true;
                        break;
                    }
                }
            }

            if (!found) return null;
        }

        return currentNode;
    }
}