using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 공통 엔딩 화면 전환
/// </summary>
public class EndingSceneUI : MonoBehaviour
{
    [Header("EndingTitle & Text Data")]
    [SerializeField] private TMP_Text titleText;

    // DataManager가 사라져도 엔딩 대사를 읽을 수 있어야함.
    [SerializeField] private TextAsset storyJson;

    [Header("Ending Title Colors")]
    [SerializeField]
    private Color badTitleColor = new Color32(200, 0, 0, 255);

    [SerializeField]
    private Color normalTitleColor = Color.white;

    [SerializeField]
    private Color happyTitleColor = new Color32(0, 200, 0, 255);

    [Header("Ending Backgrounds")]
    [SerializeField] private Image backgroundImage;

    // 각 엔딩에 표시할 배경 이미지.
    [SerializeField] private Sprite badBackground;
    [SerializeField] private Sprite normalBackground;
    [SerializeField] private Sprite happyBackground;

    [Header("Videos")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoImage;
    [SerializeField] private GameObject placeholderImage;
    [SerializeField] private RenderTexture videoTexture;

    [SerializeField] private VideoClip badVideo;
    [SerializeField] private VideoClip normalVideo;
    [SerializeField] private VideoClip happyVideo;

    // 영상 준비가 끝나지 않을 경우 방지
    [SerializeField, Min(1f)]
    private float prepareTimeout = 10f;

    // 재생 시간이 진행되지 않는 상태의 허용 시간
    [SerializeField, Min(1f)]
    private float playbackStallTimeout = 15f;

    [Header("Dialogue")]
    [SerializeField] private ScrollRect dialogueScroll;
    [SerializeField] private RectTransform dialogueContent;
    [SerializeField] private TMP_Text lineTemplate;

    [SerializeField, Min(1f)]
    private float charactersPerSecond = 25f;

    // 문장 한 줄 출력한 뒤 다음 항목으로 넘어가기 전 대기 시간
    [SerializeField, Min(0f)]
    private float lineHoldSeconds = 1.2f;

    [SerializeField] private float currentFontSize = 46f;
    [SerializeField] private float previousFontSize = 36f;

    [SerializeField] private Color currentColor = Color.white;
    [SerializeField] private Color previousColor = new Color32(150, 150, 150, 255);

    // Content의 위쪽 경계와 첫 번째 문장 사이의 여백.
    [SerializeField, Min(0f)]
    private float dialogueTopPadding = 25f;

    // 각 문장 사이의 간격.
    [SerializeField, Min(0f)]
    private float lineSpacing = 25f;

    [Header("Button")]
    [SerializeField] private Button reportButton;

    // 영상과 대사가 모두 끝난 시점부터 기다리는 시간.
    [SerializeField, Min(0f)]
    private float reportDelay = 5f;

    [SerializeField] private UnityEvent onReportRequested = new UnityEvent();

    [Header("DEBUG : TEST ONLY IN EDITOR")]
    [SerializeField] private bool allowEditorPreview;
    [SerializeField] private EndingType previewEnding = EndingType.Normal;

    // 생성한 대사를 출력 순서대로 보관.
    private readonly List<TMP_Text> visibleLines = new List<TMP_Text>();

    private bool videoFinished;
    private bool dialogueFinished;
    private bool reportReady;

    public EndingType CurrentEnding { get; private set; }

    private IEnumerator Start()
    {
        if (reportButton != null)
        {
            reportButton.gameObject.SetActive(false);
        }

        if (!IsConfigured())
        {
            Debug.LogError("EndingSceneUI의 제목, JSON, 대사 영역, 버튼 연결을 확인하세요.", this);

            yield break;
        }

        // 씬 전환 시스템이 보관한 결과를 가져옴
        bool hasEnding = SceneTransitionManager.TryTakeEnding(out EndingType ending);

#if UNITY_EDITOR
        // 인게임용이 아님!!!. 에디터 내에서 엔딩 화면만 테스트하기 위한 기능.
        // 반드시 빌드하기 전 주석 or 제거할 것!!!!!!!!!
        if (!hasEnding && allowEditorPreview)
        {
            ending = previewEnding;
            hasEnding = true;
        }
#endif

        if (!hasEnding)
        {
            titleText.text = "Ending";

            Debug.LogError("전달된 엔딩 결과가 없습니다. GameScene에서 진입하세요.", this);

            yield break;
        }

        CurrentEnding = ending;
        titleText.text = GetTitle(ending);

        // 엔딩 결과를 받고 배경과 제목 색상 설정
        ApplyEndingAppearance(ending);

        // 확정된 엔딩 결과로 배경음을 선택.
        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.PlayEnding(ending);
        }

        // 멈췄던 시간을 다시 흘러가게끔
        // 이후 대기와 글자 출력에는 deltaTime을 사용.
        Time.timeScale = 1f;

        lineTemplate.gameObject.SetActive(false);

        reportButton.onClick.AddListener(OnClickReport);

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoEnded;
            videoPlayer.errorReceived += OnVideoError;
        }

        ShowPlaceholder();

        // Canvas에 존재하는 패널 폭 기준으로 대사 높이를 계산.
        Canvas.ForceUpdateCanvases();

        string[] lines = ReadEndingLines(ending);

        // 두 코루틴을 병행.
        // 영상이 짧으면 대사를 기다리고, 대사가 짧으면 영상을 기다림.
        StartCoroutine(PlayVideo(GetVideo(ending)));
        StartCoroutine(PlayDialogue(lines));

        while (!videoFinished || !dialogueFinished)
        {
            yield return null;
        }

        yield return new WaitForSeconds(reportDelay);

        reportReady = true;
        reportButton.interactable = true;
        reportButton.gameObject.SetActive(true);
    }

    /// <summary>
    /// UI와 데이터 검사.
    /// </summary>
    /// <returns></returns>
    private bool IsConfigured()
    {
        return titleText != null &&
               storyJson != null &&
               dialogueScroll != null &&
               dialogueContent != null &&
               lineTemplate != null &&
               reportButton != null;
    }

    /// <summary>
    /// 엔딩 종류에 맞춰 배경 이미지와 제목 색상을 적용.
    /// </summary>
    /// <param name="ending"></param>
    private void ApplyEndingAppearance(EndingType ending)
    {
        Sprite selectedBackground;

        switch (ending)
        {
            case EndingType.Bad:
                selectedBackground = badBackground;
                titleText.color = badTitleColor;
                break;

            case EndingType.Happy:
                selectedBackground = happyBackground;
                titleText.color = happyTitleColor;
                break;

            default:
                selectedBackground = normalBackground;
                titleText.color = normalTitleColor;
                break;
        }

        // 배경 참조 또는 해당 엔딩의 이미지가 없다면 씬에 배치된 배경 사용.
        if (backgroundImage == null || selectedBackground == null)
        {
            return;
        }

        backgroundImage.sprite = selectedBackground;

        // Image의 기존 색상이 Sprite에 곱해져 배경 색감이 달라지는 것을 방지.
        backgroundImage.color = Color.white;
    }

    /// <summary>
    /// story.json에 선언된 엔딩 배열을 읽어옴.
    /// </summary>
    /// <param name="ending"></param>
    /// <returns></returns>
    private string[] ReadEndingLines(EndingType ending)
    {
        try
        {
            TutorialStoryData data = JsonUtility.FromJson<TutorialStoryData>(storyJson.text);

            if (data == null)
            {
                throw new InvalidOperationException("story.json 해석 결과가 없습니다.");
            }

            string[] source;

            switch (ending)
            {
                case EndingType.Bad:
                    source = data.bad_ending;
                    break;

                case EndingType.Happy:
                    source = data.happy_ending;
                    break;

                default:
                    source = data.normal_ending;
                    break;
            }

            if (source == null || source.Length == 0)
            {
                throw new InvalidOperationException("해당 엔딩 대사가 없습니다.");
            }

            // 빈 항목은 출력하지 않음.
            var result = new List<string>();

            foreach (string line in source)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    result.Add(line);
                }
            }

            if (result.Count == 0)
            {
                throw new InvalidOperationException("엔딩 대사가 모두 비어 있습니다.");
            }

            return result.ToArray();
        }
        catch (Exception exception)
        {
            Debug.LogError($"엔딩 대사 읽기 실패: {exception.Message}", this);

            // 데이터 오류가 발생하면 예외처리 진행.
            return new[] { "도대체 어떻게 했길래 이런 엔딩이 나오셨습니까?" };
        }
    }

    /// <summary>
    /// 각 대사를 생성하고 글자 수를 늘려 한 글자씩 보여줌.
    /// </summary>
    /// <param name="lines"></param>
    /// <returns></returns>
    private IEnumerator PlayDialogue(string[] lines)
    {
        dialogueFinished = false;

        for (int i = 0; i < lines.Length; i++)
        {
            // 다음 대사가 시작되는 시점에만 이전 대사를 작게 변경.
            // 마지막 대사는 흰색 46pt를 유지.
            if (visibleLines.Count > 0)
            {
                TMP_Text previous = visibleLines[visibleLines.Count - 1];
                previous.fontSize = previousFontSize;
                previous.color = previousColor;
            }

            TMP_Text current = Instantiate(lineTemplate, dialogueContent);

            current.name = $"EndingLine_{i + 1}";
            current.gameObject.SetActive(true);
            current.enableAutoSizing = false;
            current.fontSize = currentFontSize;
            current.color = currentColor;

            // 대사에 ...를 UI 태그로 인식하면 안됨.
            current.richText = false;
            current.text = lines[i];

            // 전체 문장은 먼저 넣되, 아직 보이는 글자는 0개로 설정.
            current.maxVisibleCharacters = 0;

            visibleLines.Add(current);

            // 글자 크기가 바뀐 이전 대사까지 포함해 다시 배치.
            LayoutLines();

            current.ForceMeshUpdate();

            int totalCharacters = current.textInfo.characterCount;
            float revealedCharacters = 0f;

            while (current.maxVisibleCharacters < totalCharacters)
            {
                // deltaTime을 누적하므로 프레임 속도에 따라 전체 출력 속도가 크게 달라지지 않음.
                revealedCharacters += charactersPerSecond * Time.deltaTime;

                current.maxVisibleCharacters = Mathf.Min(totalCharacters, Mathf.FloorToInt(revealedCharacters));

                yield return null;
            }

            // 마지막 항목 뒤에는 별도의 항목 간 대기를 하지 않음.
            // 이후 공통 reportDelay가 적용됨.
            if (i < lines.Length - 1)
            {
                yield return new WaitForSeconds(lineHoldSeconds);
            }
        }

        dialogueFinished = true;
    }

    /// <summary>
    /// 대사 높이를 계산해 위에서 아래로 배치.
    /// 전체 문장의 높이를 미리 확보하니, 글자가 하나씩 나올 때마다 아래 UI가 흔들리지 않을거임.
    /// </summary>
    private void LayoutLines()
    {
        float width = Mathf.Max(1f, dialogueContent.rect.width);
        
        // 지정된 상단 여백만큼 아래에서 시작.
        float y = dialogueTopPadding;

        for (int i = 0; i < visibleLines.Count; i++)
        {
            TMP_Text text = visibleLines[i];
            RectTransform rect = text.rectTransform;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);

            // 부모 가로 폭을 사용
            rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);

            float height = Mathf.Ceil(text.GetPreferredValues(text.text, width, Mathf.Infinity).y);

            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, -y);

            y += height;

            if (i < visibleLines.Count - 1)
            {
                y += lineSpacing;
            }
        }

        dialogueContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);

        // 새 대사가 추가될 때만 최신 대사를 화면에 보이게 함.
        Canvas.ForceUpdateCanvases();
        dialogueScroll.StopMovement();
        dialogueScroll.verticalNormalizedPosition = 0f;
    }

    /// <summary>
    /// 영상이 없으면 바로 완료 처리.
    /// 영상이 있으면 준비 후 재생하고, 준비 및 재생 정지를 검사함.
    /// </summary>
    /// <param name="clip"></param>
    /// <returns></returns>
    private IEnumerator PlayVideo(VideoClip clip)
    {
        videoFinished = false;

        if (clip == null || videoPlayer == null || videoImage == null || videoTexture == null)
        {
            ShowPlaceholder();
            videoFinished = true;
            yield break;
        }

        videoPlayer.Stop();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.playbackSpeed = 1f;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = clip;

        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoTexture;
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside;

        // 오디오 연결은 추후 별도 작업에서 진행.
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

        videoImage.texture = videoTexture;

        videoPlayer.Prepare();

        float prepareElapsed = 0f;

        while (!videoPlayer.isPrepared && !videoFinished)
        {
            prepareElapsed += Time.deltaTime;

            if (prepareElapsed >= prepareTimeout)
            {
                FailVideo("영상 준비 시간이 초과되었습니다.");
                yield break;
            }

            yield return null;
        }

        // 준비 과정에서 errorReceived가 호출된 경우.
        if (videoFinished)
        {
            yield break;
        }

        videoImage.gameObject.SetActive(true);

        if (placeholderImage != null)
        {
            placeholderImage.SetActive(false);
        }

        videoPlayer.Play();

        double lastVideoTime = -1d;
        float stalledSeconds = 0f;

        while (!videoFinished)
        {
            // 정상 재생이면 영상의 time 값이 계속 증가.
            // 오류 이벤트 없이 멈추는 경우에도 무한 대기하지 않도록 해야함.
            double currentVideoTime = videoPlayer.time;

            if (Math.Abs(currentVideoTime - lastVideoTime) > 0.001d)
            {
                lastVideoTime = currentVideoTime;
                stalledSeconds = 0f;
            }
            else
            {
                stalledSeconds += Time.deltaTime;
            }

            if (stalledSeconds >= playbackStallTimeout)
            {
                FailVideo("영상 재생이 진행되지 않아 대체 화면으로 전환합니다.");
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// 영상이 정상 종료되면 마지막 화면을 유지하고 완료만 표시.
    /// </summary>
    /// <param name="source"></param>
    private void OnVideoEnded(VideoPlayer source)
    {
        videoFinished = true;
    }

    /// <summary>
    /// 디코딩 실패 등 VideoPlayer가 보고한 오류를 처리.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="message"></param>
    private void OnVideoError(VideoPlayer source, string message)
    {
        FailVideo(message);
    }

    private void FailVideo(string message)
    {
        // Stop 과정에서 추가 알림이 발생해도 중복 처리하면 안됨.
        if (videoFinished)
        {
            return;
        }

        videoFinished = true;

        Debug.LogWarning($"엔딩 영상 생략: {message}", this);

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        ShowPlaceholder();
    }

    private void ShowPlaceholder()
    {
        if (videoImage != null)
        {
            videoImage.gameObject.SetActive(false);
        }

        if (placeholderImage != null)
        {
            placeholderImage.SetActive(true);
        }
    }

    private void OnClickReport()
    {
        if (!reportReady)
        {
            return;
        }

        onReportRequested.Invoke();
    }

    private VideoClip GetVideo(EndingType ending)
    {
        switch (ending)
        {
            case EndingType.Bad:
                return badVideo;

            case EndingType.Happy:
                return happyVideo;

            default:
                return normalVideo;
        }
    }

    private string GetTitle(EndingType ending)
    {
        switch (ending)
        {
            case EndingType.Bad:
                return "Bad Ending";

            case EndingType.Happy:
                return "Happy Ending";

            default:
                return "Normal Ending";
        }
    }

    private void OnDestroy()
    {
        // 씬 이동 시 등록한 이벤트 해제.
        if (reportButton != null)
        {
            reportButton.onClick.RemoveListener(OnClickReport);
        }

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnded;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.Stop();
        }
    }

    /* -- 기존 프로토타이핑용 임시 엔딩 로직
    [SerializeField] private TMP_Text endingText;

    private string commonMessage = "엔딩 입니다.";

    private void Start()
    {
        if (endingText == null)
        {
            Debug.LogError("EndingText를 연결하세요.", this);
            return;
        }

        // GameScene에서 전달한 결과만 사용
        // 이 Scene에서는 자산이나 생존 여부를 다시 계산하지 않음.
        if (!SceneTransitionManager.TryTakeEnding(out EndingType ending))
        {
            endingText.text = "";

            Debug.LogError("전달된 엔딩 결과가 없습니다. GameScene을 통해 진입하세요.", this);

            return;
        }

        switch (ending)
        {
            case EndingType.Bad:
                endingText.text = $"배드" + commonMessage;
                endingText.color = Color.red;
                break;

            case EndingType.Normal:
                endingText.text = $"노말" + commonMessage;
                endingText.color = Color.black;
                break;

            case EndingType.Happy:
                endingText.text = $"해피" + commonMessage;
                endingText.color = Color.green;
                break;
        }
    }
    */
}
