using TMPro;
using UnityEngine;

public class FloatingMoneyText : MonoBehaviour
{
    [SerializeField] private TMP_Text amountText;

    [Header("Animation")]
    [SerializeField, Min(0.01f)]
    private float duration = 0.8f;

    [SerializeField, Min(0f)]
    private float riseDistance = 100f;

    private RectTransform rectTransform;
    private Vector2 startPosition;
    private Color originalColor;

    private float elapsed;
    private bool playing;

    /// <summary>
    /// 생성 위치를 설정한 다음 호출.
    /// </summary>
    /// <param name="amount"></param>
    public void Play(long amount)
    {
        rectTransform = (RectTransform)transform;

        amountText.text = $"+{amount:N0}원";
        amountText.raycastTarget = false;

        startPosition = rectTransform.anchoredPosition;
        originalColor = amountText.color;

        elapsed = 0f;
        playing = true;
    }

    private void Update()
    {
        if (!playing)
            return;

        elapsed += Time.deltaTime;

        float progress = Mathf.Clamp01(
            elapsed / Mathf.Max(0.01f, duration));

        // 시작 위치에서 위로 이동.
        rectTransform.anchoredPosition = startPosition + Vector2.up * riseDistance * progress;

        // 색은 유지하고 알파만 줄어들게
        Color color = originalColor;
        color.a = originalColor.a * (1f - progress);
        amountText.color = color;

        // 재생이 끝난 텍스트만 제거.
        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
