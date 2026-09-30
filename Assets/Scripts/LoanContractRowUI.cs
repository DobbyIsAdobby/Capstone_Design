using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoanContractRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private Button repayButton;

    private int contractId;     // 대출 계약 ID
    private Action<int> onRepay;

    private void Awake()
    {
        repayButton.onClick.AddListener(RequestRepayment);
    }

    private void OnDestroy()
    {
        repayButton.onClick.RemoveListener(RequestRepayment);
    }

    /// <summary>
    /// Unity UI 툴킷 데이터 바인딩 사용 -> 이름으로 UI 오브젝트를 찾아와 자동 할당 가능.
    /// </summary>
    /// <param name="contract"></param>
    /// <param name="currentTurn"></param>
    /// <param name="callback"></param>
    public void Bind(LoanContract contract, int currentTurn, Action<int> callback)
    {
        contractId = contract.Id;
        onRepay = callback;

        long interest = contract.GetInterestDue(currentTurn);
        long payment = contract.Principal + interest;

        detailText.text =
            $"대출 #{contract.Id}\n" +
            $"원금 {contract.Principal:N0}원\n" +
            $"월 이자 {contract.MonthlyInterest:N0}원\n" +
            $"만기 {contract.MaturityTurn}턴 마감\n" +
            $"지금 상환할 금액 {payment:N0}원";

        // 현금 부족 사유는 버튼을 눌렀을 때 패널에서 안내함
        repayButton.interactable =
            !contract.IsClosed &&
            GameManager.Instance.CanAct;
    }

    private void RequestRepayment()
    {
        onRepay?.Invoke(contractId);
    }
}
