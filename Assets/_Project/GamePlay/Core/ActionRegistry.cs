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
                int id = action.name.GetHashCode();
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
        
        return action.name.GetHashCode();
    }
}