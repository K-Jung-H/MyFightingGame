using UnityEngine;
using System;

[Serializable]
public class WinCounterSlot
{
    public GameObject rootObject;
    public GameObject activeIconObject;
}

public class WinCounterUIManager : MonoBehaviour
{
    public WinCounterSlot[] p1WinSlots;
    public WinCounterSlot[] p2WinSlots;

    /*
     * 지정된 목표 승수에 맞춰 필요한 개수의 승리 슬롯만 활성화하고, 내부 승리 아이콘은 모두 비활성화로 초기화합니다.
     */
    public void InitializeCounters(int requiredWins)
    {
        SetSlotsState(p1WinSlots, requiredWins);
        SetSlotsState(p2WinSlots, requiredWins);
    }

    /*
     * 각 플레이어의 현재 승수를 확인하여 획득한 승수만큼 슬롯 내부의 승리 아이콘을 활성화합니다.
     */
    public void UpdateCounters(int p1Wins, int p2Wins)
    {
        UpdateActiveIcons(p1WinSlots, p1Wins);
        UpdateActiveIcons(p2WinSlots, p2Wins);
    }

    /*
     * 슬롯 배열을 순회하며 목표 승수 인덱스 이내의 부모 객체만 활성화하고, 모든 자식 아이콘 객체를 비활성화합니다.
     */
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

    /*
     * 현재 획득한 승수만큼 슬롯 배열을 순회하며 자식 아이콘 객체를 활성화합니다.
     */
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