using System.Collections.Generic;

public class ActionRegistry
{
    private Dictionary<int, ActionDataSO> actionMap;

    public void Initialize(List<ActionDataSO> actionList)
    {
        actionMap = new Dictionary<int, ActionDataSO>();

        for (int i = 0; i < actionList.Count; i++)
        {
            ActionDataSO action = actionList[i];
            bool isActionValid = action != null;
            
            if (isActionValid)
            {
                int id = GetDeterministicHash(action.name);
                bool isNotRegistered = !actionMap.ContainsKey(id);
                
                if (isNotRegistered)
                {
                    actionMap.Add(id, action);
                }
            }
        }
    }

    public ActionDataSO GetAction(int id)
    {
        actionMap.TryGetValue(id, out ActionDataSO action);
        return action;
    }

    public int GetActionID(ActionDataSO action)
    {
        bool isActionInvalid = action == null;
        if (isActionInvalid) return 0;
        
        return GetDeterministicHash(action.name);
    }

    private int GetDeterministicHash(string str)
    {
        if (string.IsNullOrEmpty(str)) return 0;

        unchecked
        {
            int hash = (int)2166136261;
            for (int i = 0; i < str.Length; i++)
            {
                hash ^= str[i];
                hash *= 16777619;
            }
            return hash;
        }
    }
}