using TMPro;
using UnityEngine;

/// <summary>
/// 자산 관리소 패널 내 각 자산군 자산 내역 연결
/// </summary>
public class AssetNavigationPanel : MonoBehaviour
{
    /*
    Inspector Zone
    */

    [Header("Asset Balances")]
    [SerializeField] private TMP_Text bankBalanceText;
    [SerializeField] private TMP_Text stockBalanceText;
    [SerializeField] private TMP_Text stockInverseBalanceText;
    [SerializeField] private TMP_Text leverageBalanceText;
    [SerializeField] private TMP_Text leverageInverseBalanceText;

    /*
    function Zone
    */

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        AssetManager assets = AssetManager.Instance;

        if (assets == null)
            return;

        bankBalanceText.text = $"{assets.bankBalance:N0}원";

        stockBalanceText.text = $"{assets.stockBalance:N0}원";

        stockInverseBalanceText.text = $"{assets.stockInverseBalance:N0}원";

        leverageBalanceText.text = $"{assets.leverageBalance:N0}원";

        leverageInverseBalanceText.text = $"{assets.leverageInverseBalance:N0}원";
    }
}
