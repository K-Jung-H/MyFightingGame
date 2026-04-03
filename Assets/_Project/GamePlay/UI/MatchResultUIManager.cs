using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public enum MatchEndActionType
{
    ReturnToMenu,
    ReturnToCharacterSelect,
    Rematch
}

public class MatchResultUIManager : MonoBehaviour
{
    public GameObject resultPanel;
    public TextMeshProUGUI matchResultText;
    public Button btnGoToMenu;
    public Button btnGoToCharacterSelect;
    public Button btnRematch;

    public GameObject imgLeftRematchReady;
    public GameObject imgRightRematchReady;

    public event Action<MatchEndActionType> OnActionRequested;

    private void Awake()
    {
        resultPanel.SetActive(false);

        btnGoToMenu.onClick.AddListener(OnMenuButtonClicked);
        btnGoToCharacterSelect.onClick.AddListener(OnCharacterSelectButtonClicked);
        btnRematch.onClick.AddListener(OnRematchButtonClicked);
    }

    public void InitializeUI()
    {
        if (imgLeftRematchReady != null) imgLeftRematchReady.SetActive(false);
        if (imgRightRematchReady != null) imgRightRematchReady.SetActive(false);
    }

    public void ShowResult(int p1Wins, int p2Wins, int requiredWins, int localSlot)
    {
        resultPanel.SetActive(true);
        InitializeUI();

        bool isP1Winner = p1Wins >= requiredWins;
        bool isP2Winner = p2Wins >= requiredWins;

        if (isP1Winner && isP2Winner)
        {
            matchResultText.text = "DRAW";
            matchResultText.color = Color.yellow;
        }
        else if ((isP1Winner && localSlot == 0) || (isP2Winner && localSlot == 1))
        {
            matchResultText.text = "YOU WIN";
            matchResultText.color = Color.green;
        }
        else if ((isP2Winner && localSlot == 0) || (isP1Winner && localSlot == 1))
        {
            matchResultText.text = "YOU LOSE";
            matchResultText.color = Color.red;
        }
        else
        {
            matchResultText.text = "MATCH OVER";
            matchResultText.color = Color.white;
        }

        btnRematch.interactable = true;
        btnGoToCharacterSelect.interactable = true;
        btnGoToMenu.interactable = true;
    }

    public void UpdateRematchSync(bool isP1Ready, bool isP2Ready, bool isFlipped)
    {
        if (imgLeftRematchReady != null) 
        {
            imgLeftRematchReady.SetActive(isFlipped ? isP2Ready : isP1Ready);
        }
        
        if (imgRightRematchReady != null) 
        {
            imgRightRematchReady.SetActive(isFlipped ? isP1Ready : isP2Ready);
        }
    }

    private void OnMenuButtonClicked()
    {
        DisableAllButtons();
        OnActionRequested?.Invoke(MatchEndActionType.ReturnToMenu);
    }

    private void OnCharacterSelectButtonClicked()
    {
        DisableAllButtons();
        OnActionRequested?.Invoke(MatchEndActionType.ReturnToCharacterSelect);
    }

    private void OnRematchButtonClicked()
    {
        btnRematch.interactable = false;
        OnActionRequested?.Invoke(MatchEndActionType.Rematch);
    }

    public void DisableAllButtons()
    {
        btnRematch.interactable = false;
        btnGoToCharacterSelect.interactable = false;
        btnGoToMenu.interactable = false;
    }
}