using UnityEngine;
using UnityEngine.UI;
using System;

public class BaseLobbyCanvasController : MonoBehaviour
{
    public Button openCreateButton;
    public Button openJoinSearchButton;
    public Button randomMatchButton;
    public event Action OnOpenCreateClicked;
    public event Action OnOpenJoinSearchClicked;
    public event Action OnRandomMatchClicked;

    private void Start()
    {
        openCreateButton.onClick.AddListener(() => OnOpenCreateClicked?.Invoke());
        openJoinSearchButton.onClick.AddListener(() => OnOpenJoinSearchClicked?.Invoke());
        randomMatchButton.onClick.AddListener(() => OnRandomMatchClicked?.Invoke());
    }
}