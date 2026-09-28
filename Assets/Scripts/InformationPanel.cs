using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InformationPanel : MonoBehaviour
{
    [Header("Cards")]
    [SerializeField] private InformationCardUI[] cards;
    [SerializeField] private TMP_Text statusText;

    [Header("Purchase Confirmation")]
    [SerializeField] private GameObject confirmRoot;
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button positiveButton;

    private bool hasPendingPurchase;
    private InformationGrade pendingGrade;
    private int pendingTurn;

    /// <summary>
    /// 메인 INFO 버튼에 연결. 
    /// 로딩/정산/게임 종료 중에는 열지 않음.
    /// </summary>
    public void Open()
    {
        if (GameManager.Instance == null || !GameManager.Instance.CanAct)
        {
            return;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Close()
    {
        CancelPurchase();
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        CancelPurchase();
        Refresh();
    }

    private void OnDisable()
    {
        // 다시 열 때 이전 구매 확인 상태가 남지 않게 함
        CancelPurchase();
    }

    public void Refresh()
    {
        GameManager game = GameManager.Instance;

        // 턴이 바뀌면 이전 확인창은 닫음
        if (hasPendingPurchase && (game == null || game.currentMonth != pendingTurn || !game.CanAct))
        {
            CancelPurchase();
        }

        foreach (InformationCardUI card in cards)
        {
            if (card != null)
                card.Refresh();
        }

        if (DataManager.Instance == null || !DataManager.Instance.IsInformationLoaded)
        {
            statusText.text = "정보 데이터를 불러오지 못했습니다.";
        }
        else if (game != null && game.currentMonth >= game.maxMonth)
        {
            statusText.text = "마지막 턴에는 다음 턴 정보가 없습니다.";
        }
        else
        {
            statusText.text = "";
        }
    }

    public void RequestPurchase(InformationGrade grade)
    {
        RumorManager rumors = RumorManager.Instance;

        if (rumors == null)
        {
            statusText.text = "RumorManager 연결을 확인하세요.";
            return;
        }

        if (!rumors.CanPurchase(grade, out string reason))
        {
            statusText.text = reason;
            Refresh();
            return;
        }

        DataManager.Instance.TryGetInformation(grade, out InformationItemData item);

        pendingGrade = grade;
        pendingTurn = GameManager.Instance.currentMonth;
        hasPendingPurchase = true;

        questionText.text = $"{item.Name}를 구매하시겠습니까?";
        priceText.text = $"필요 금액: {item.Price:N0}원";

        positiveButton.interactable = true;
        confirmRoot.SetActive(true);
        confirmRoot.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 확인창의 '예' 버튼에 연결. 
    /// 실제 결제 검증은 RumorManager가 다시 수행함.
    /// </summary>
    public void ConfirmPurchase()
    {
        if (!hasPendingPurchase)
            return;

        // 연속 클릭에 대한 UI 차단
        // Manager에서도 이미 구매한 등급인지 별도로 검사함
        positiveButton.interactable = false;

        bool success = RumorManager.Instance.TryPurchase(pendingGrade, pendingTurn, out string message);

        if (success)
        {
            CancelPurchase();
            Refresh();
        }
    }

    public void CancelPurchase()
    {
        hasPendingPurchase = false;

        if (confirmRoot != null)
            confirmRoot.SetActive(false);
    }
}
