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

    public GameObject imgP1RematchReady;
    public GameObject imgP2RematchReady;

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
        if (imgP1RematchReady != null) imgP1RematchReady.SetActive(false);
        if (imgP2RematchReady != null) imgP2RematchReady.SetActive(false);
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

    public void UpdateRematchSync(bool isP1Ready, bool isP2Ready)
    {
        if (imgP1RematchReady != null) imgP1RematchReady.SetActive(isP1Ready);
        if (imgP2RematchReady != null) imgP2RematchReady.SetActive(isP2Ready);
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