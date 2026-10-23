using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 버튼의 기능 잠금을 표시하고 클릭 실행을 검사함.
/// </summary>
[RequireComponent(typeof(Button))]
public class TutorialFeatureButton : MonoBehaviour
{
    [Header("Feature")]
    [SerializeField] private TutorialFeature feature;

    [Header("Lock Display")]
    [SerializeField] private GameObject lockRoot;
    [SerializeField] private TMP_Text lockText;

    [Header("Original Button Actions")]
    [SerializeField] private UnityEvent onUnlockedClick = new UnityEvent();

    private Button button;

    private bool hasDisplayState;
    private bool lastUnlocked;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        RefreshLock(true);
    }

    private void LateUpdate()
    {
        // 턴 변경이나 불러오기 이후에도 표시를 맞춰 놓음.
        RefreshLock(false);
    }

    private void RefreshLock(bool force)
    {
        bool unlocked = TutorialManager.IsUnlocked(feature);

        // 상태가 같으면 화면을 다시 변경하지 않음.
        if (!force && hasDisplayState && lastUnlocked == unlocked)
        {
            return;
        }

        hasDisplayState = true;
        lastUnlocked = unlocked;

        if (lockRoot != null)
        {
            lockRoot.SetActive(!unlocked);
        }

        if (lockText != null)
        {
            lockText.text = $"{TutorialManager.GetUnlockTurn(feature)}달차 해금";
        }

        // 기존 버튼 상태 관리와 충돌하지 않도록
        // interactable은 여기서 변경하지 않습니다.
    }

    /// <summary>
    /// Button의 On Click에 연결.
    /// 조건을 통과하면 기존 버튼 동작을 실행.
    /// </summary>
    public void OnClick()
    {
        if (button == null || !button.IsActive() || !button.IsInteractable())
        {
            return;
        }

        GameManager game = GameManager.Instance;

        // 스토리, 이벤트, 정산, 일시정지 중에는 실행하지 않음.
        if (game == null || !game.CanAct)
        {
            return;
        }

        if (!TutorialManager.Check(feature, out _))
        {
            return;
        }

        onUnlockedClick.Invoke();
    }
}
