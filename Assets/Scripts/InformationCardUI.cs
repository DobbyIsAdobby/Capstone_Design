using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 한 등급의 카드 표시를 담당. 
/// 구매 상태는 RumorManager에서 보관함.
/// </summary>
public class InformationCardUI : MonoBehaviour
{
    [SerializeField] private InformationGrade grade;
    [SerializeField] private InformationPanel owner;

    [Header("Text")]
    [SerializeField] private TMP_Text gradeText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text lockedHintText;

    [Header("Lock")]
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private Button purchaseButton;

    public void Refresh()
    {
        DataManager data = DataManager.Instance;
        RumorManager rumors = RumorManager.Instance;

        if (data == null || rumors == null || !data.TryGetInformation(grade, out InformationItemData item))
        {
            bodyText.gameObject.SetActive(false);
            lockedRoot.SetActive(true);
            purchaseButton.interactable = false;
            lockedHintText.text = "정보 데이터를 확인하세요.";
            return;
        }

        gradeText.text = item.Name;

        bool revealed = rumors.TryGetReveal(grade, out InformationReveal reveal);

        // 블러 배경 뒤로 실제 정보가 보이지 않게 본문 자체를 숨김 - 추후 모자이크 이미지 모델 추가 시 아래 코드 주석화 가능(선택)
        bodyText.gameObject.SetActive(revealed);
        lockedRoot.SetActive(!revealed);

        if (revealed)
        {
            bodyText.text = reveal.Body;

            priceText.text = reveal.IsFree ? "휴대폰 보유 혜택 : 무료 열람" : $"{item.Price:N0}원 · 구매 완료";

            return;
        }

        bodyText.text = "";
        priceText.text = $"필요 금액: {item.Price:N0}원";

        bool canPurchase = rumors.CanPurchase(grade, out string reason);

        purchaseButton.interactable = canPurchase;
        lockedHintText.text = canPurchase ? "구매 후 열람할 수 있습니다." : reason;
    }

    /// <summary>
    /// 카드의 구매 버튼 OnClick에 연결. 
    /// 바로 결제하지 않고 확인창 열람.
    /// </summary>
    public void RequestPurchase()
    {
        owner.RequestPurchase(grade);
    }
}
