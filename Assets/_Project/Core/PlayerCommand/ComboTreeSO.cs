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

    public ComboNode FindBestMatchNode(List<ComboNode> nodes, InputFlags input)
    {
        if (nodes == null || nodes.Count == 0) return null;

        InputFlags attackMask = InputFlags.LightAttack | InputFlags.HeavyAttack;
        InputFlags inputAttack = input & attackMask;

        ComboNode bestNode = null;
        int bestDirCount = -1;

        foreach (var node in nodes)
        {
            InputFlags nodeAttack = node.requiredInput & attackMask;
            if (nodeAttack != inputAttack) continue;

            if ((input & node.requiredInput) == node.requiredInput)
            {
                InputFlags nodeDir = node.requiredInput & ~attackMask;
                int dirCount = CountSetBits((int)nodeDir);

                if (dirCount > bestDirCount)
                {
                    bestNode = node;
                    bestDirCount = dirCount;
                }
            }
        }

        return bestNode;
    }

    private int CountSetBits(int n)
    {
        int count = 0;
        while (n > 0)
        {
            count += n & 1;
            n >>= 1;
        }
        return count;
    }
}