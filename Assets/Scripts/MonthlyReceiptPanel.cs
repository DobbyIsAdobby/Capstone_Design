using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MonthlyReceiptPanel : MonoBehaviour
{
    /*
    Inspector Zone
    */

    [Header("Details")]
    [SerializeField] private ReceiptRowUI rowPrefab;    //내역 한줄 프리팹
    [SerializeField] private RectTransform content;     //empty object
    [SerializeField] private ScrollRect detailsScroll;
    [SerializeField] private CanvasGroup detailsInput;

    [Header("Summary")]
    [SerializeField] private GameObject summaryRoot;
    [SerializeField] private TMP_Text assetChangeText;
    [SerializeField] private TMP_Text totalAssetText;
    [SerializeField] private TMP_Text totalAssetLabelText;  // 총자산 또는 미납금액 문구를 표시할 텍스트

    [Header("Continue")]
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueButtonText;

    [Header("Presentation")]
    [SerializeField, Min(0.05f)] private float lineDelay = 0.3f;    //다음 줄이 나오는 딜레이 시간
    [SerializeField] private Color positiveColor = new Color(0.12f, 0.55f, 0.25f);
    [SerializeField] private Color negativeColor = new Color(0.85f, 0.15f, 0.15f);

    private Coroutine revealRoutine;
    private Action onContinue;
    private bool readyToContinue;

    /// <summary>
    /// 각 인스펙터가 잘 연결됐는지 확인하는 get 전용 프로퍼티 선언
    /// </summary>
    public bool IsConfigured => 
        rowPrefab != null &&
        rowPrefab.IsConfigured &&
        content != null &&
        detailsScroll != null &&
        detailsInput != null &&
        summaryRoot != null &&
        assetChangeText != null &&
        totalAssetText != null &&
        totalAssetLabelText != null &&
        continueButton != null &&
        continueButtonText != null;

    /*
    function Zone
    */

    /// <summary>
    /// 내역이 하나씩 나오게 하는 함수
    /// </summary>
    /// <param name="data"></param>
    /// <param name="completed"></param>
    public void Show(MonthlyReceiptData data, Action completed)
    {
        gameObject.SetActive(true);

        if(revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }

        // Destroy는 프레임 끝에 실행되므로, 우선 비활성화
        foreach(Transform child in content)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        onContinue = completed;
        readyToContinue = false;

        summaryRoot.SetActive(false);
        // 버튼 인터렉션 비활성화
        continueButton.interactable = false;

        // 파산 시(True) : 파산 확인 Text, 아닐 시 최종 턴 돌입 여부 확인 및 결과에 따라 True/False Text 출력
        continueButtonText.text = data.IsBankrupt ? "파산 확인" : data.IsFinalMonth ? "최종 결과 확인" : "새로운 달 시작";

        detailsScroll.StopMovement();
        SetScrollInput(false);

        revealRoutine = StartCoroutine(Reveal(data));
    }

    private IEnumerator Reveal(MonthlyReceiptData data)
    {
        // 이전 행 제거와 레이아웃 갱신 대기
        yield return null;

        // 출력할 내역에 따라 ReceiptRowUI 프리팹을 Instantiate. -> 매 턴마다 바뀌고, 갯수도 통일적이지 않기 때문에 오브젝트 풀링이 오히려 최적화에 적합하지 않다고 판단함.
        foreach(ReceiptLine line in data.Lines)
        {
            ReceiptRowUI row = Instantiate(rowPrefab, content, false);

            row.Bind(line);     //Unity UI 툴킷 데이터 바인딩
            row.gameObject.SetActive(true);

            // 캔버스 레이아웃 리빌드 강제 실행 Static 메서드를 사용해 즉시 UI 업데이트가 진행하도록 함.
            Canvas.ForceUpdateCanvases();
            detailsScroll.verticalNormalizedPosition = 0f;

            yield return new WaitForSecondsRealtime(lineDelay);
        }

        assetChangeText.text = data.AssetChange.ToString("+#,0;-#,0;0") + "원";

        assetChangeText.color = data.AssetChange > 0 ? positiveColor : data.AssetChange < 0 ? negativeColor : Color.black;

        //totalAssetText.text = $"{data.TotalAsset:N0}원";
        if (data.IsBankrupt)
        {
            totalAssetLabelText.text = "미납금액";
            totalAssetText.text = $"{data.UnpaidAmount:N0}원";
            totalAssetText.color = negativeColor;
        }
        else
        {
            totalAssetLabelText.text = "총자산";
            totalAssetText.text = $"{data.TotalAsset:N0}원";
            totalAssetText.color = Color.black;
        }

        summaryRoot.SetActive(true);

        Canvas.ForceUpdateCanvases();
        detailsScroll.verticalNormalizedPosition = 0f;

        SetScrollInput(true);

        readyToContinue = true;
        continueButton.interactable = true;
        revealRoutine = null;
    }

    /// <summary>
    /// 스크롤 활성화 함수
    /// </summary>
    /// <param name="enabled"></param>
    private void SetScrollInput(bool enabled)
    {
        detailsScroll.enabled = enabled;
        detailsInput.interactable = enabled;
        detailsInput.blocksRaycasts = enabled;
    }

    /// <summary>
    /// 다음 턴 버튼을 누를 시 활성화 되는 함수
    /// </summary>
    public void OnClickContinue()
    {
        if(!readyToContinue) return;

        // 중복 클릭시에도 한번만 실행되도록.
        readyToContinue = false;
        continueButton.interactable = false;

        Action callback = onContinue;
        onContinue = null;

        // 영수증 창을 다시 비활성화
        gameObject.SetActive(false);
        // 콜백 함수가 비어 있지 않을 때만 안전하게 실행. => callback : 델리게이트 | ?. : null 조건 연산자 | Invoke() 저장된 함수 실행 메서드
        callback?.Invoke();
    }

    private void OnDisable()
    {
        if(revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }

        readyToContinue = false;
    }
}
