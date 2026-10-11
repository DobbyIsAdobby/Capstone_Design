using System.Collections;
using UnityEngine;

public class LobbyIntroUI : MonoBehaviour
{
    [Header("Logo")]
    [SerializeField] private CanvasGroup logoGroup;

    [Header("Buttons")]
    [Tooltip("표시할 순서대로 연결하세요.")]
    [SerializeField] private GameObject[] menuButtons = new GameObject[3];

    [Header("Timing")]
    [SerializeField, Min(0f)]
    private float initialDelay = 0.5f; // 로고가 나타나기 전 대기 시간
    [SerializeField, Min(0f)]
    private float logoFadeDuration = 1.2f;

    [SerializeField, Min(0f)]
    private float buttonInterval = 0.2f;

    private bool configured;

    private void Awake()
    {
        configured = logoGroup != null && menuButtons != null && menuButtons.Length == 3;

        if (configured)
        {
            foreach (GameObject button in menuButtons)
            {
                if (button == null)
                {
                    configured = false;
                    break;
                }
            }
        }

        if (!configured)
        {
            Debug.LogError("로고 CanvasGroup과 버튼 3개를 연결하세요.", this);

            enabled = false;
            return;
        }

        // 로고는 알파값이 0인 상태에서 시작
        logoGroup.alpha = 0f;
        logoGroup.interactable = false;
        logoGroup.blocksRaycasts = false;

        // 버튼은 SetActive(false)로 숨김.
        foreach (GameObject button in menuButtons)
        {
            button.SetActive(false);
        }
    }

    private IEnumerator Start()
    {
        // 로고와 버튼을 숨긴 상태로 먼저 대기함
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        // 로고는 선형 페이드로 표현
        float elapsed = 0f;

        while (elapsed < logoFadeDuration)
        {
            elapsed += Time.deltaTime;

            logoGroup.alpha = Mathf.Clamp01(
                elapsed / logoFadeDuration);

            yield return null;
        }

        // 선형 감쇠 공식 - 비버집에서 썼던 방식
        logoGroup.alpha = 1f;

        // 로고의 알파값이 1임이 확인되면 첫 버튼부터 활성화
        for (int i = 0; i < menuButtons.Length; i++)
        {
            menuButtons[i].SetActive(true);

            // 다음 버튼이 있다면 지정한 간격만큼 대기
            if (i < menuButtons.Length - 1 && buttonInterval > 0f)
            {
                yield return new WaitForSeconds(buttonInterval);
            }
        }
    }

    private void OnDisable()
    {
        if (!configured)
            return;

        // 연출 스크립트가 꺼져도 버튼이 숨겨진 채 남지 않게 함
        StopAllCoroutines();

        logoGroup.alpha = 1f;

        foreach (GameObject button in menuButtons)
        {
            button.SetActive(true);
        }
    }
}
