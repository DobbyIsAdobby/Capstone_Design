using TMPro;
using UnityEngine;

public class ReceiptRowUI : MonoBehaviour
{
    /*
    Inspector Zone
    */

    [SerializeField] private TMP_Text nameText;     // 금액명
    [SerializeField] private TMP_Text amountText;   // 금액

    [Header("Text Colors")]
    [SerializeField] private Color fixedExpenseColor = Color.black;     // 고정지출 폰트 색 : 검정
    [SerializeField] private Color positiveColor = new Color(0.12f, 0.55f, 0.25f);  // 수익 폰트 색 : 초록
    [SerializeField] private Color negativeColor = new Color(0.85f, 0.15f, 0.15f);  // 지출 폰트 색 : 빨강

    // 읽기 전용 프로퍼티
    /// <summary>
    /// bool 타입을 반환하는 IsConfigured get 전용 프로퍼티 선언. get할 시, nameText와 amountText가 null이 아니여야만 True를 반환하게 설정.
    /// </summary>
    public bool IsConfigured => nameText != null && amountText != null;

    /*
    function Zone
    */


    /// <summary>
    /// Unity UI 툴킷 데이터 바인딩 사용 -> 이름으로 UI 오브젝트를 찾아와 자동 할당 가능.
    /// </summary>
    /// <param name="line"></param>
    public void Bind(ReceiptLine line)
    {
        nameText.text = line.Name;

        // C# 커스텀 숫자 서식 사용. 양수/음수/0
        // #,0 : 숫자 자리 표시자 + 1,000단위 구분 기호(,)
        amountText.text = line.Amount.ToString("+#,0;-#,0;0") + "원";  // +#,0(양수); ,-#,0(음수); 0(0일 때)

        Color color;

        // 고정 지출일 경우
        if (line.Type == ReceiptLineType.FixedExpense)
        {
            color = fixedExpenseColor;
        }
        // 수익(양수)일 경우
        else if (line.Amount > 0)
        {
            color = positiveColor;
        }
        // 지출(음수)일 경우
        else if (line.Amount < 0)
        {
            color = negativeColor;
        }
        // 0일 경우
        else
        {
            color = fixedExpenseColor;
        }

        nameText.color = color;
        amountText.color = color;
    }
}
