using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
//using UnityEngine.SceneManagement;      // Unity에서 제공하는 Scene 생성, 로드, 언로드 및 전환을 관리하는 nameSpace - Scene 전환 전용 매니저를 만들었기에, 더 이상 사용하지않음
using UnityEngine.UI;

public class LobbyPanelUI : MonoBehaviour
{
    // SaveSlotPanelUI가 담당하기 때문에 더 이상 사용하지 않음
    /*
    [Serializable]
    private sealed class SlotView
    {
        public Button button;
        public TMP_Text slotNameText;
        public TMP_Text summaryText;
    }
    */

    //[Header("GameScene")]
    //[SerializeField] private string gameSceneName = "GameScene";

    [Header("Main Menu")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button quitButton;

    [Header("Panels")]
    [SerializeField] private GameObject panelLayer;
    [SerializeField] private GameObject loadRoot;
    [SerializeField] private GameObject confirmationRoot;

    [Header("Loading Lists")]
    [SerializeField] private TMP_Text loadTitleText;
    [SerializeField] private Button loadBackButton;
    //[SerializeField] private SlotView[] slots = new SlotView[3];

    [Header("Common PopUps")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button denyButton;
    [SerializeField] private Button confirmButton;

    [Header("Save Slot UI")]
    [SerializeField] private SaveSlotPanelUI saveSlots;

    private bool transitioning;
    private Action pendingApplyAction;

    private GameObject popupPreviousSelection;

    private void Awake()
    {
        // 내부 버튼은 코드로 연결.
        // Inspector의 On Click에 중복 연결 x.
        newGameButton.onClick.AddListener(StartNewGame);
        loadButton.onClick.AddListener(OpenLoadPanel);
        quitButton.onClick.AddListener(RequestQuit);

        loadBackButton.onClick.AddListener(CloseLoadPanel);

        applyButton.onClick.AddListener(AcceptConfirmation);
        denyButton.onClick.AddListener(ClosePopup);
        confirmButton.onClick.AddListener(ClosePopup);

        // 부모가 비활성 상태여도 자식 상태를 먼저 정리할 수 있음
        loadRoot.SetActive(false);
        confirmationRoot.SetActive(false);
        panelLayer.SetActive(false);

        //PreparePlaceholderSlots();
        RefreshInteraction();
    }

    /// <summary>
    /// 로비 초기 선택을 설정하고, 불러오기 실패 안내가 있으면 표시함.
    /// </summary>
    private void Start()
    {
        Select(newGameButton.gameObject);

        // GameScene에서 복원에 실패하고 돌아온 경우에만 내용이 있음.
        string message = SceneTransitionManager.TakeLobbyMessage();

        if (!string.IsNullOrEmpty(message))
            ShowMessage(message);
    }

    private bool CanUseMainMenu()
    {
        return !transitioning && !loadRoot.activeSelf && !confirmationRoot.activeSelf;
    }

    /// <summary>
    /// 새 게임 버튼에서 호출됨.
    /// 시작 요청 정리와 Scene 이동은 전환 관리자가 담당.
    /// </summary>
    private void StartNewGame()
    {
        if (!CanUseMainMenu())
            return;

        if (SceneTransitionManager.Instance == null)
        {
            ShowMessage("씬 전환 관리자가 없습니다.");
            return;
        }

        // 버튼을 연속 클릭하지 못하게 막음.
        transitioning = true;
        RefreshInteraction();

        if (!SceneTransitionManager.Instance.TryStartNewGame(out string error))
        {
            // 이동에 실패했으면 로비 버튼을 다시 사용할 수 있게 함.
            transitioning = false;
            ShowMessage(error);
        }
    }

    // SaveSlotPanelUI가 담당하기 때문에 더 이상 사용하지 않음
    /*
    private void PreparePlaceholderSlots()
    {
        loadTitleText.text = "불러오기";

        for (int i = 0; i < slots.Length; i++)
        {
            SlotView slot = slots[i];

            // 저장 시스템 연결 전의 임시로 표시함
            // 실제 파일 존재 여부를 조회한 결과가 아님
            slot.slotNameText.text = $"세이브 파일 {i + 1}";
            slot.summaryText.text = "비어있음";
            slot.button.interactable = false;
        }
    }
    */

    /// <summary>
    /// 불러오기 목록을 열고 저장 파일 상태 표시.
    /// </summary>
    private void OpenLoadPanel()
    {
        if (!CanUseMainMenu())
            return;

        // 부모가 비활성 상태이면 자식 패널만 켜도 보이지 않음.
        panelLayer.SetActive(true);
        panelLayer.transform.SetAsLastSibling();

        loadRoot.SetActive(true);
        confirmationRoot.SetActive(false);

        // 파일 조회
        saveSlots.OpenLoad();

        RefreshInteraction();
        Select(loadBackButton.gameObject);
    }

    private void CloseLoadPanel()
    {
        if (transitioning || confirmationRoot.activeSelf)
            return;

        loadRoot.SetActive(false);
        panelLayer.SetActive(false);

        RefreshInteraction();
        Select(loadButton.gameObject);
    }

    private void RequestQuit()
    {
        if (!CanUseMainMenu())
            return;

        ShowConfirmation("게임을 종료하시겠습니까?", QuitGame);
    }

    /// <summary>
    /// 확인만 필요한 안내를 표시 
    /// </summary>
    /// <param name="message"></param>
    public void ShowMessage(string message)
    {
        PreparePopup(message);

        applyButton.gameObject.SetActive(false);
        denyButton.gameObject.SetActive(false);
        confirmButton.gameObject.SetActive(true);

        Select(confirmButton.gameObject);
    }

    /// <summary>
    /// applyButton을 눌렀을 때 실행할 작업을 전달받음 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="onApply"></param>
    public void ShowConfirmation(string message, Action onApply)
    {
        PreparePopup(message);

        pendingApplyAction = onApply;

        applyButton.gameObject.SetActive(true);
        denyButton.gameObject.SetActive(true);
        confirmButton.gameObject.SetActive(false);

        // 실수로 종료하거나 불러오지 않도록 denyButton을 기본 선택.
        Select(denyButton.gameObject);
    }

    private void PreparePopup(string message)
    {
        // 이미 열린 팝업의 문구만 바꿀 때는 복귀 대상을 유지
        if (!confirmationRoot.activeSelf)
        {
            popupPreviousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        }

        pendingApplyAction = null;
        messageText.text = message;

        panelLayer.SetActive(true);
        panelLayer.transform.SetAsLastSibling();

        confirmationRoot.SetActive(true);
        confirmationRoot.transform.SetAsLastSibling();

        RefreshInteraction();
    }

    private void AcceptConfirmation()
    {
        if (transitioning || !confirmationRoot.activeSelf)
            return;

        // 먼저 팝업을 닫아야 후속 작업에서 새 팝업을 열 수 있음.
        Action action = pendingApplyAction;

        ClosePopup();
        action?.Invoke();
    }

    private void ClosePopup()
    {
        if (transitioning || !confirmationRoot.activeSelf)
            return;

        pendingApplyAction = null;
        confirmationRoot.SetActive(false);

        // 불러오기 목록이 열려 있다면 부모 패널을 유지
        panelLayer.SetActive(loadRoot.activeSelf);

        RefreshInteraction();

        GameObject target = popupPreviousSelection;

        if (target == null || !target.activeInHierarchy)
        {
            target = loadRoot.activeSelf ? loadBackButton.gameObject : newGameButton.gameObject;
        }

        Select(target);
    }

    /// <summary>
    /// 현재 열린 화면에 맞춰 버튼 입력을 허용하거나 차단함.
    /// 슬롯별 파일 상태는 SaveSlotPanelUI가 판단함.
    /// </summary>
    private void RefreshInteraction()
    {
        bool popupOpen = confirmationRoot.activeSelf;

        bool mainEnabled = !transitioning && !loadRoot.activeSelf && !popupOpen;

        // 슬롯 목록이나 팝업이 열려 있으면 메인 메뉴 입력을 막음.
        newGameButton.interactable = mainEnabled;
        loadButton.interactable = mainEnabled;
        quitButton.interactable = mainEnabled;

        // 팝업이 열렸을 때 맨 뒤 뒤로 가기 버튼 인터랙션 차단
        loadBackButton.interactable = !transitioning && loadRoot.activeSelf && !popupOpen;

        // 슬롯을 무조건 끄지 않고 팝업, 전환에 의한 차단 상태만 전달함
        saveSlots.SetBlocked(transitioning || popupOpen);

        applyButton.interactable = !transitioning;
        denyButton.interactable = !transitioning;
        confirmButton.interactable = !transitioning;
    }

    private void QuitGame()
    {
        if (transitioning)
            return;

        transitioning = true;
        RefreshInteraction();

        // 로비 종료 과정에서 저장 파일을 생성하거나 삭제하지 않음
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #else
                Application.Quit();
        #endif
    }

    private static void Select(GameObject target)
    {
        if (EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(target);
    }

    private void OnDestroy()
    {
        // 이 컴포넌트가 추가한 이벤트만 해제
        pendingApplyAction = null;

        if (newGameButton != null)
            newGameButton.onClick.RemoveListener(StartNewGame);

        if (loadButton != null)
            loadButton.onClick.RemoveListener(OpenLoadPanel);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(RequestQuit);

        if (loadBackButton != null)
            loadBackButton.onClick.RemoveListener(CloseLoadPanel);

        if (applyButton != null)
            applyButton.onClick.RemoveListener(AcceptConfirmation);

        if (denyButton != null)
            denyButton.onClick.RemoveListener(ClosePopup);

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ClosePopup);
    }
}
