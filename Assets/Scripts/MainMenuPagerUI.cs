using UnityEngine;
using UnityEngine.UI;

public class MainMenuPagerUI : MonoBehaviour
{
    [Header("Menu Buttons")]
    [SerializeField] private RectTransform[] menuButtons;

    [Header("Slot Location")]
    [SerializeField] private RectTransform[] slots;

    [Header("Move Buttons")]
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    // 0이면 정보, 상점, 자산을 표시.
    // 1이면 상점, 자산, 부동산을 표시.
    private int firstIndex;

    private bool configured;

    private void Start()
    {
        configured = ValidateReferences();

        if (!configured)
        {
            Debug.LogError("메뉴 버튼 4개와 배치 위치 3개, 이동 버튼을 연결하세요.", this);

            if (leftButton != null)
                leftButton.interactable = false;

            if (rightButton != null)
                rightButton.interactable = false;

            return;
        }

        firstIndex = 0;
        Refresh();
    }

    private bool ValidateReferences()
    {
        if (menuButtons == null || menuButtons.Length != 4)
            return false;

        if (slots == null || slots.Length != 3)
            return false;

        if (leftButton == null || rightButton == null)
            return false;

        foreach (RectTransform menu in menuButtons)
        {
            if (menu == null)
                return false;
        }

        foreach (RectTransform slot in slots)
        {
            if (slot == null)
                return false;
        }

        // 같은 부모를 사용해야 anchoredPosition을 그대로 복사할 수 있음.
        Transform parent = slots[0].parent;

        foreach (RectTransform slot in slots)
        {
            if (slot.parent != parent)
                return false;
        }

        foreach (RectTransform menu in menuButtons)
        {
            if (menu.parent != parent)
                return false;
        }

        return true;
    }

    public void MoveLeft()
    {
        if (!configured)
            return;

        firstIndex = 0;
        Refresh();
    }

    public void MoveRight()
    {
        if (!configured)
            return;

        firstIndex = 1;
        Refresh();
    }

    private void Refresh()
    {
        for (int index = 0; index < menuButtons.Length; index++)
        {
            RectTransform menu = menuButtons[index];

            // 현재 표시 범위에 들어가는 버튼인지 확인
            bool visible = index >= firstIndex && index < firstIndex + slots.Length;

            if (!visible)
            {
                menu.gameObject.SetActive(false);
                continue;
            }

            // 정보, 상점, 자산이면 각각 0, 1, 2번 자리
            // 상점, 자산, 부동산이어도 각각 0, 1, 2번 자리
            int slotIndex = index - firstIndex;
            RectTransform slot = slots[slotIndex];

            // Slot에 적용.
            menu.anchorMin = slot.anchorMin;
            menu.anchorMax = slot.anchorMax;
            menu.pivot = slot.pivot;
            menu.anchoredPosition3D = slot.anchoredPosition3D;
            menu.sizeDelta = slot.sizeDelta;

            menu.gameObject.SetActive(true);
        }

        // 더 이동할 수 없는 방향은 비활성화.
        leftButton.interactable = firstIndex > 0;
        rightButton.interactable = firstIndex + slots.Length < menuButtons.Length;
    }
}
