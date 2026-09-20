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

    // 각 인스펙터가 잘 연결됐는지 확인하는 get 전용 프로퍼티 선언
    public bool IsConfigured => 
        rowPrefab != null &&
        rowPrefab.IsConfigured &&
        content != null &&
        detailsScroll != null &&
        detailsInput != null &&
        summaryRoot != null &&
        assetChangeText != null &&
        totalAssetText != null &&
        continueButton != null &&
        continueButtonText != null;

    /*
    function Zone
    */

    
}
