using UnityEngine;
using UnityEngine.UI;
using System;

[Serializable]
public class WinCounterSlot
{
    public GameObject rootObject;
    public GameObject activeIconObject;
}

public class WinCounterUIManager : MonoBehaviour
{
    public WinCounterSlot[] leftWinSlots;
    public WinCounterSlot[] rightWinSlots;

    public void InitializeCounters(int requiredWins)
    {
        SetSlotsState(leftWinSlots, requiredWins);
        SetSlotsState(rightWinSlots, requiredWins);
    }

    public void UpdateCounters(int leftWins, int rightWins)
    {
        UpdateActiveIcons(leftWinSlots, leftWins);
        UpdateActiveIcons(rightWinSlots, rightWins);
    }

    public void HideAllCounters()
    {
        HideSlots(leftWinSlots);
        HideSlots(rightWinSlots);
    }

    private void HideSlots(WinCounterSlot[] slots)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].rootObject != null)
            {
                slots[i].rootObject.SetActive(false);
            }
        }
    }

    private void SetSlotsState(WinCounterSlot[] slots, int requiredWins)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            bool isSlotRequired = i < requiredWins;
            
            if (slots[i].rootObject != null)
            {
                slots[i].rootObject.SetActive(isSlotRequired);
            }

            if (slots[i].activeIconObject != null)
            {
                slots[i].activeIconObject.SetActive(false);
            }
        }
    }

    private void UpdateActiveIcons(WinCounterSlot[] slots, int currentWins)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].activeIconObject != null)
            {
                bool isIconActive = i < currentWins;
                
                if (slots[i].rootObject != null && slots[i].rootObject.activeSelf)
                {
                    slots[i].activeIconObject.SetActive(isIconActive);
                }
            }
        }
    }
}