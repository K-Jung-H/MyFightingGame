using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SearchRoomCanvasController : MonoBehaviour
{
    public TMP_InputField titleInput;
    public Button titleSearchButton;
    public TMP_InputField codeInput;
    public Button codeSearchButton;
    public Button closeButton;

    public event Action<string> OnTitleSearchRequested;
    public event Action<string> OnCodeJoinRequested;
    public event Action OnCloseClicked;

    private void Start()
    {
        titleSearchButton.onClick.AddListener(() => OnTitleSearchRequested?.Invoke(titleInput.text));
        codeSearchButton.onClick.AddListener(() => OnCodeJoinRequested?.Invoke(codeInput.text.ToUpper()));
        closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());
    }
}