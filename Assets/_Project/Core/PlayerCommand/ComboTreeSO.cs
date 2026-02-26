using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ComboNode
{
    public InputFlags requiredInput;
    public ActionDataSO actionData;
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
}