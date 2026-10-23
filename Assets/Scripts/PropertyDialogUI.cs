using System;
using TMPro;
using UnityEngine;

public class PropertyDialogUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text messageText;

    [SerializeField] private GameObject yesButton;
    [SerializeField] private GameObject noButton;
    [SerializeField] private GameObject closeButton;

    private Action accepted;

    public bool IsConfigured =>
        root != null &&
        messageText != null &&
        yesButton != null &&
        noButton != null &&
        closeButton != null;

    private void Awake()
    {
        if (root != null)
            root.SetActive(false);
    }

    /// <summary>
    /// confirmation이 true면 예/아니오, false면 확인 버튼만 표시.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="onAccepted"></param>
    /// <param name="confirmation"></param>
    public void Show(string message, Action onAccepted, bool confirmation)
    {
        accepted = onAccepted;
        messageText.text = message;

        yesButton.SetActive(confirmation);
        noButton.SetActive(confirmation);
        closeButton.SetActive(!confirmation);

        root.SetActive(true);
        root.transform.SetAsLastSibling();
    }

    public void Accept()
    {
        if (!root.activeSelf)
            return;

        // 실행 전에 콜백을 비워 중복 실행을 방지.
        Action callback = accepted;
        accepted = null;

        root.SetActive(false);
        callback?.Invoke();
    }

    public void Cancel()
    {
        accepted = null;
        root.SetActive(false);
    }
}
