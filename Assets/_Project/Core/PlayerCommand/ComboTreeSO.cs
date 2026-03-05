using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ComboNode
{
    public InputFlags requiredInput;
    public ActionDataSO actionData;
    
    [SerializeReference]
    public List<ComboNode> nextAttacks = new List<ComboNode>();
}

[CreateAssetMenu(fileName = "ComboTree", menuName = "ScriptableObjects/ComboTree")]
public class ComboTreeSO : ScriptableObject
{
    [SerializeReference]
    public List<ComboNode> startingAttacks = new List<ComboNode>();

    private void OnValidate()
    {
        HashSet<ComboNode> visitedNodes = new HashSet<ComboNode>();
        InitializeNullNodes(startingAttacks, visitedNodes);
    }

    private void InitializeNullNodes(List<ComboNode> nodes, HashSet<ComboNode> visitedNodes)
    {
        if (nodes == null) return;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == null)
            {
                nodes[i] = new ComboNode();
            }

            if (!visitedNodes.Contains(nodes[i]))
            {
                visitedNodes.Add(nodes[i]);
                InitializeNullNodes(nodes[i].nextAttacks, visitedNodes);
            }
        }
    }

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
        bool isNodesEmpty = nodes == null || nodes.Count == 0;
        if (isNodesEmpty) return null;

        InputFlags attackMask = InputFlags.LP | InputFlags.RP | InputFlags.LK | InputFlags.RK;
        InputFlags inputAttack = input & attackMask;

        ComboNode bestNode = null;
        int bestDirectionCount = -1;

        foreach (var node in nodes)
        {
            InputFlags nodeAttack = node.requiredInput & attackMask;
            bool isAttackMatching = nodeAttack == inputAttack;
            
            if (!isAttackMatching) continue;

            bool isInputSatisfied = (input & node.requiredInput) == node.requiredInput;
            
            if (isInputSatisfied)
            {
                InputFlags nodeDirection = node.requiredInput & ~attackMask;
                int directionCount = CountSetBits((int)nodeDirection);

                bool isBetterMatch = directionCount > bestDirectionCount;
                if (isBetterMatch)
                {
                    bestNode = node;
                    bestDirectionCount = directionCount;
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