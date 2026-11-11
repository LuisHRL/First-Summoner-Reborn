using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Persistent loading overlay for mission entry and retry. Progress is driven by AsyncOperation.</summary>
public sealed class MissionLoadingScreen : MonoBehaviour
{
    private static MissionLoadingScreen active;
    private Image progressFill;
    private TMP_Text percentage;

    public static void Load(string sceneName, Sprite emblem)
    {
        if (active != null || string.IsNullOrEmpty(sceneName) ||
            !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("MissionLoadingScreen: invalid scene or loading already in progress: " + sceneName);
            return;
        }
        var root = new GameObject("MissionLoadingScreen", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(MissionLoadingScreen));
        DontDestroyOnLoad(root);
        active = root.GetComponent<MissionLoadingScreen>();
        active.Build(emblem);
        active.StartCoroutine(active.LoadRoutine(sceneName));
    }

    private void Build(Sprite emblem)
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var background = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(transform, false);
        var bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0.035f, 0.04f, 0.065f, 1f);

        var symbol = new GameObject("LoadingEmblem", typeof(RectTransform), typeof(Image));
        symbol.transform.SetParent(background.transform, false);
        var symbolRect = symbol.GetComponent<RectTransform>();
        symbolRect.anchorMin = symbolRect.anchorMax = new Vector2(0.5f, 0.57f);
        symbolRect.sizeDelta = new Vector2(260f, 260f);
        var symbolImage = symbol.GetComponent<Image>();
        symbolImage.sprite = emblem;
        symbolImage.preserveAspect = true;
        symbolImage.color = emblem == null ? Color.clear : Color.white;
        symbolImage.raycastTarget = false;

        var bar = new GameObject("ProgressTrack", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(background.transform, false);
        var barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.33f);
        barRect.sizeDelta = new Vector2(620f, 24f);
        bar.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.24f, 1f);

        var fill = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bar.transform, false);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        progressFill = fill.GetComponent<Image>();
        progressFill.color = new Color(0.85f, 0.52f, 0.13f, 1f);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillAmount = 0f;

        var label = new GameObject("ProgressText", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(background.transform, false);
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.27f);
        labelRect.sizeDelta = new Vector2(420f, 90f);
        percentage = label.GetComponent<TextMeshProUGUI>();
        percentage.alignment = TextAlignmentOptions.Center;
        percentage.fontSize = 36f;
        percentage.color = Color.white;
        percentage.raycastTarget = false;
        percentage.text = "Loading... 0%";
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        yield return null; // Display overlay before starting the scene load.
        var operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError("MissionLoadingScreen: failed to begin scene loading.");
            active = null;
            Destroy(gameObject);
            yield break;
        }
        operation.allowSceneActivation = false;
        while (operation.progress < 0.9f)
        {
            SetProgress(Mathf.Clamp01(operation.progress / 0.9f));
            yield return null;
        }
        SetProgress(1f);
        yield return null;
        operation.allowSceneActivation = true;
        while (!operation.isDone) yield return null;
        active = null;
        Destroy(gameObject);
    }

    private void SetProgress(float value)
    {
        progressFill.fillAmount = value;
        percentage.text = "Loading... " + Mathf.RoundToInt(value * 100f) + "%";
    }
}
