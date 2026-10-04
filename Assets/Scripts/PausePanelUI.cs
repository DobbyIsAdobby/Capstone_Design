using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;               // Inspector에서 직접 eventListener를 연결하기 위한 nameSpace
using UnityEngine.EventSystems;         // 사용자 인터랙션을 감지하고 UI에 이벤트를 전달하기 위한 nameSpace
//using UnityEngine.SceneManagement;      // 씬 전환을 위한 nameSpace - Scene 전환 전용 매니저를 만들었기에, 더 이상 사용하지않음
using UnityEngine.UI;

public class PausePanelUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject pauseRoot;
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject saveSlotPanel;

    [Header("Menu Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button lobbyButton;
    [SerializeField] private Button quitButton;

    [Header("Slot Panel")]
    [SerializeField] private Button slotBackButton;

    [Header("Common PopUps")]
    [SerializeField] private GameObject confirmationRoot;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button denyButton;
    [SerializeField] private Button confirmButton;

    //[Header("Scene")]
    //[SerializeField] private string lobbySceneName = "LobbyScene";

    [Header("Save Sys")]
    // 실제 슬롯 관리 스크립트가 준비되면 Inspector에서 연결합니다.
    [SerializeField] private UnityEvent onOpenSaveSlots = new UnityEvent();
    [SerializeField] private UnityEvent onOpenLoadSlots = new UnityEvent();

    [Header("Save Slot UI")]
    [SerializeField] private SaveSlotPanelUI saveSlots;

    private Action pendingApplyAction;

    private GameObject previousSelection;
    private GameObject popupPreviousSelection;

    private void Awake()
    {
        // 내부 버튼은 코드로 연결함.
        // Inspector내 On Click은 비움. - 연결하면 중복이라 안됨.
        continueButton.onClick.AddListener(ContinueGame);
        saveButton.onClick.AddListener(OpenSaveSlots);
        loadButton.onClick.AddListener(OpenLoadSlots);
        lobbyButton.onClick.AddListener(MoveToLobby);
        quitButton.onClick.AddListener(QuitWithoutSaving);

        slotBackButton.onClick.AddListener(ShowMenu);

        applyButton.onClick.AddListener(AcceptConfirmation);
        denyButton.onClick.AddListener(ClosePopup);
        confirmButton.onClick.AddListener(ClosePopup);

        // 화면만 숨기고 관리 컴포넌트들은 활성 상태로 유지
        confirmationRoot.SetActive(false);
        saveSlotPanel.SetActive(false);
        pauseMenu.SetActive(true);
        pauseRoot.SetActive(false);
    }

    public void Open()
    {
        // 이미 열린 메뉴를 중복 초기화하지 않음.
        if (pauseRoot.activeSelf || PauseController.IsPaused)
            return;

        PauseController controller = PauseController.Instance;

        if (controller == null || !controller.TryPause())
            return;

        previousSelection = GetSelection();

        pauseRoot.SetActive(true);

        // 다른 패널보다 앞에 표시.
        pauseRoot.transform.SetAsLastSibling();

        ShowMenu();
    }

    public void ContinueGame()
    {
        if (!pauseRoot.activeSelf)
            return;

        pendingApplyAction = null;

        confirmationRoot.SetActive(false);
        saveSlotPanel.SetActive(false);
        pauseRoot.SetActive(false);

        if (PauseController.Instance != null)
            PauseController.Instance.Resume();

        // 일시정지 전에 선택했던 UI로 돌아감
        Select(previousSelection);
    }

    public void ShowMenu()
    {
        if (!pauseRoot.activeSelf)
            return;

        pendingApplyAction = null;

        confirmationRoot.SetActive(false);
        saveSlotPanel.SetActive(false);
        pauseMenu.SetActive(true);

        GameManager game = GameManager.Instance;

        // 저장 시스템이 연결되기 전에는 버튼을 비활성화
        saveButton.interactable =
            onOpenSaveSlots.GetPersistentEventCount() > 0 &&
            game != null &&
            game.CanSaveCurrentState;

        loadButton.interactable =
            onOpenLoadSlots.GetPersistentEventCount() > 0;

        Select(continueButton.gameObject);
    }

    private void OpenSaveSlots()
    {
        if (!PauseController.IsPaused)
            return;

        GameManager game = GameManager.Instance;

        // 버튼 상태 + 실행도 검사.
        if (game == null || !game.CanSaveCurrentState)
        {
            ShowMessage("현재는 저장할 수 없습니다.");
            return;
        }

        if (onOpenSaveSlots.GetPersistentEventCount() == 0)
            return;

        pauseMenu.SetActive(false);
        saveSlotPanel.SetActive(true);

        // slotManager가 저장 모드로 목록 표시
        onOpenSaveSlots.Invoke();
    }

    private void OpenLoadSlots()
    {
        if (!PauseController.IsPaused)
            return;

        if (onOpenLoadSlots.GetPersistentEventCount() == 0)
            return;

        pauseMenu.SetActive(false);
        saveSlotPanel.SetActive(true);

        // slotManager가 불러오기 모드로 목록 표시.
        onOpenLoadSlots.Invoke();
    }

    /// <summary>
    /// 저장 완료나 오류처럼 확인만 필요한 안내를 표시
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
    /// applyButton을 선택한 경우에만 전달받은 작업을 실행
    /// </summary>
    /// <param name="message"></param>
    /// <param name="onYes"></param>
    public void ShowConfirmation(string message, Action onYes)
    {
        PreparePopup(message);

        pendingApplyAction = onYes;

        applyButton.gameObject.SetActive(true);
        denyButton.gameObject.SetActive(true);
        confirmButton.gameObject.SetActive(false);

        // 실수로 덮어쓰거나 불러오지 않도록 아니오를 기본 선택
        Select(denyButton.gameObject);
    }

    /// <summary>
    /// 공용 팝업을 표시하기 전에 기본 상태를 준비.
    /// </summary>
    /// <param name="message"></param>
    private void PreparePopup(string message)
    {
        // 팝업을 닫았을 때 돌아갈 선택 대상을 기억
        if (!confirmationRoot.activeSelf)
            popupPreviousSelection = GetSelection();

        // 이전 확인창의 작업이 남지 않도록 제거
        pendingApplyAction = null;
        messageText.text = message;

        confirmationRoot.SetActive(true);
        confirmationRoot.transform.SetAsLastSibling();

        // 팝업 뒤에 있는 슬롯을 누르지 못하게 함 - 인터랙션 금지
        saveSlots.SetBlocked(true);
    }

    private void AcceptConfirmation()
    {
        // 팝업을 닫아야 작업 완료 후 새 안내를 표시할 수 있음
        Action action = pendingApplyAction;

        ClosePopup();
        action?.Invoke();
    }

    /// <summary>
    /// 공용 팝업을 닫고 슬롯 입력을 복구.
    /// </summary>
    public void ClosePopup()
    {
        bool wasOpen = confirmationRoot.activeSelf;

        // 취소하거나 닫은 작업이 나중에 실행되지 않게 제거.
        pendingApplyAction = null;
        confirmationRoot.SetActive(false);

        saveSlots.SetBlocked(false);

        if (wasOpen)
            Select(popupPreviousSelection);
    }

    /// <summary>
    /// 로비 이동 버튼 클릭 시 확인창부터 표시함
    /// </summary>
    private void MoveToLobby()
    {
        if (!PauseController.IsPaused || confirmationRoot.activeSelf)
            return;

        ShowConfirmation("지금 나갈 시 현재 진행 상황이 저장되지 않습니다.\n" + "로비로 나가시겠습니까?", ExecuteMoveToLobby);
    }

    /// <summary>
    /// 로비 이동 확인창에서 ApplyButton을 선택한 경우에만 실행
    /// </summary>
    private void ExecuteMoveToLobby()
    {
        SceneTransitionManager transition = SceneTransitionManager.Instance;

        if (transition == null)
        {
            ShowMessage("씬 전환 관리자가 없습니다.");
            return;
        }

        // 자동 저장 없이 로비로 이동.
        // 일시정지 해제는 Scene 전환 관리자가 처리
        if (!transition.TryMoveToLobby(out string error))
        {
            // 이동 실패 시 현재 화면에서 오류를 안내
            ShowMessage(error);
        }
    }

    /// <summary>
    /// 저장 없이 종료 버튼을 누르면 확인창부터 표시함
    /// </summary>
    private void QuitWithoutSaving()
    {
        if (!PauseController.IsPaused || confirmationRoot.activeSelf)
            return;

        ShowConfirmation("지금 나갈 시 현재 진행 상황이 저장되지 않습니다.\n" + "게임을 종료하시겠습니까?", ExecuteQuitWithoutSaving);
    }

    /// <summary>
    /// 종료 확인창에서 ApplyButton을 선택한 경우에만 실행.
    /// 기존 저장 파일은 유지하며 현재 진행 상황만 저장하지 않음.
    /// </summary>
    private void ExecuteQuitWithoutSaving()
    {
        // 일시정지 상태와 메뉴를 정리
        ContinueGame();

        #if UNITY_EDITOR
            // Unity 에디터에서는 플레이 모드를 종료
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            // 빌드된 게임에서는 애플리케이션을 종료
            Application.Quit();
        #endif
    }

    private static GameObject GetSelection()
    {
        return EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
    }

    private static void Select(GameObject target)
    {
        if (EventSystem.current == null)
            return;

        // 비활성 오브젝트는 선택 대상으로 사용하지 않음
        if (target != null && !target.activeInHierarchy)
            target = null;

        EventSystem.current.SetSelectedGameObject(target);
    }

    private void OnDisable()
    {
        // 관리 컴포넌트가 꺼지면 화면과 일시정지 상태도 함께 해제
        pendingApplyAction = null;

        if (pauseRoot != null)
            pauseRoot.SetActive(false);

        if (PauseController.Instance != null)
            PauseController.Instance.Resume();
    }

    private void OnDestroy()
    {
        // 이 컴포넌트가 등록한 콜백만 해제
        if (continueButton != null)
            continueButton.onClick.RemoveListener(ContinueGame);

        if (saveButton != null)
            saveButton.onClick.RemoveListener(OpenSaveSlots);

        if (loadButton != null)
            loadButton.onClick.RemoveListener(OpenLoadSlots);

        if (lobbyButton != null)
            lobbyButton.onClick.RemoveListener(MoveToLobby);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitWithoutSaving);

        if (slotBackButton != null)
            slotBackButton.onClick.RemoveListener(ShowMenu);

        if (applyButton != null)
            applyButton.onClick.RemoveListener(AcceptConfirmation);

        if (denyButton != null)
            denyButton.onClick.RemoveListener(ClosePopup);

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ClosePopup);
    }
}
