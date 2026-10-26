
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Localization.Settings;
using UnityEngine.InputSystem;
using System.Collections;

public class MainMenu : MonoBehaviour
{
    [Header("Navigation")]
    public GameObject mainButtonsPanel;
    public GameObject topButtonsPanel;

    [Header("Écrans principaux")]
    public GameObject heroPanel;
    public GameObject villagePanel;
    public GameObject missionPanel;
    public GameObject challengePanel;
    public GameObject arenaPanel;
    public GameObject clanPanel;

    [Header("Sous-menus")]
    public GameObject cardsPanel;
    public GameObject settingsPanel;
    public GameObject aboutPanel;

    [Header("Boutons et confirmations")]
    public GameObject backButtonAbout;
    public GameObject quitConfirmPanel;

    [Header("Navigation par swipe")]
    [SerializeField] private float minSwipeDistance = 100f;

    private Vector2 swipeStartPosition;
    private bool swipeInProgress;
    private RectTransform draggedPanel;
    private RectTransform adjacentPanel;

    private Vector2 draggedPanelStart;
    private Vector2 adjacentPanelStart;

    private int dragTargetIndex = -1;
    private float dragOffset;
    [Header("Animation des menus")]
    [SerializeField] private float transitionDuration = 0.25f;

    private bool isTransitioning;
    private void Start()
    {
        ControlSettings.LoadSavedMode();

        // Héros est l'écran d'accueil.
        BackToHero();

        quitConfirmPanel.SetActive(false);
    }

    private void Update()
    {
        HandleSwipe();
        if (Keyboard.current == null ||
            !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (quitConfirmPanel.activeSelf)
        {
            CancelQuit();
        }
        else if (aboutPanel.activeSelf)
        {
            BackToSettings();
        }
        else if (cardsPanel.activeSelf)
        {
            BackToHero();
        }
        else if (settingsPanel.activeSelf)
        {
            BackToHero();
        }
        else if (missionPanel.activeSelf ||
         villagePanel.activeSelf ||
         challengePanel.activeSelf ||
         arenaPanel.activeSelf ||
         clanPanel.activeSelf)
        {
            NavigateToPanel(0);
        }
        else
        {
            // Depuis l'accueil Héros : demander confirmation.
            ShowQuitConfirmation();
        }
    }

    // --------------------------------------------------
    // NAVIGATION PRINCIPALE
    // --------------------------------------------------
    private bool TryGetPointerPosition(
    out Vector2 position,
    out bool pressed,
    out bool released,
    out bool held)
    {
        position = Vector2.zero;
        pressed = false;
        released = false;
        held = false;

        // Priorité au tactile sur Android.
        if (Touchscreen.current != null &&
        (Touchscreen.current.primaryTouch.press.isPressed ||
        Touchscreen.current.primaryTouch.press.wasReleasedThisFrame))
        {
            var touch = Touchscreen.current.primaryTouch;

            position = touch.position.ReadValue();
            pressed = touch.press.wasPressedThisFrame;
            released = touch.press.wasReleasedThisFrame;
            held = touch.press.isPressed;
            return true;
        }

        // Souris dans l'éditeur Unity.
        if (Mouse.current != null)
        {
            var mouse = Mouse.current;

            position = mouse.position.ReadValue();
            pressed = mouse.leftButton.wasPressedThisFrame;
            released = mouse.leftButton.wasReleasedThisFrame;
            held = mouse.leftButton.isPressed;
            return true;
        }

        return false;
    }
    private void BeginPanelDrag(int targetIndex)
    {
        GameObject[] panels = GetMainPanels();
        int currentIndex = GetActivePanelIndex();

        if (isTransitioning ||
            currentIndex < 0 ||
            targetIndex < 0 ||
            targetIndex >= panels.Length ||
            targetIndex == currentIndex)
            return;

        draggedPanel =
            panels[currentIndex].GetComponent<RectTransform>();

        adjacentPanel =
            panels[targetIndex].GetComponent<RectTransform>();

        draggedPanelStart = draggedPanel.anchoredPosition;
        adjacentPanelStart = adjacentPanel.anchoredPosition;

        dragTargetIndex = targetIndex;
        dragOffset = 0f;

        int direction = targetIndex > currentIndex ? 1 : -1;
        float width = ((RectTransform)draggedPanel.parent).rect.width;

        adjacentPanel.anchoredPosition =
            adjacentPanelStart + Vector2.right * direction * width;

        adjacentPanel.gameObject.SetActive(true);
        if (targetIndex == 0)
            topButtonsPanel.SetActive(true);
    }
    private void UpdateDraggedPanels(float offset)
    {
        if (draggedPanel == null || adjacentPanel == null)
            return;

        float width = ((RectTransform)draggedPanel.parent).rect.width;

        dragOffset = Mathf.Clamp(offset, -width, width);

        int direction = dragOffset < 0f ? 1 : -1;

        draggedPanel.anchoredPosition =
            draggedPanelStart + Vector2.right * dragOffset;

        adjacentPanel.anchoredPosition =
            adjacentPanelStart +
            Vector2.right * (dragOffset + direction * width);
    }
    private IEnumerator FinishPanelDrag(bool complete)
    {
        if (draggedPanel == null || adjacentPanel == null)
            yield break;

        isTransitioning = true;

        float width = ((RectTransform)draggedPanel.parent).rect.width;
        int direction = dragOffset < 0f ? 1 : -1;

        Vector2 outgoingFrom = draggedPanel.anchoredPosition;
        Vector2 incomingFrom = adjacentPanel.anchoredPosition;

        Vector2 outgoingTo = draggedPanelStart;
        Vector2 incomingTo = adjacentPanelStart +
            Vector2.right * direction * width;

        if (complete)
        {
            outgoingTo -= Vector2.right * direction * width;
            incomingTo = adjacentPanelStart;
        }

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / transitionDuration);
            t = t * t * (3f - 2f * t);

            draggedPanel.anchoredPosition =
                Vector2.Lerp(outgoingFrom, outgoingTo, t);

            adjacentPanel.anchoredPosition =
                Vector2.Lerp(incomingFrom, incomingTo, t);

            yield return null;
        }

        draggedPanel.anchoredPosition = draggedPanelStart;
        adjacentPanel.anchoredPosition = adjacentPanelStart;

        if (complete)
            draggedPanel.gameObject.SetActive(false);
        else
            adjacentPanel.gameObject.SetActive(false);
        if (complete)
        {
            topButtonsPanel.SetActive(
                dragTargetIndex == 0
            );
        }
        draggedPanel = null;
        adjacentPanel = null;
        dragTargetIndex = -1;
        dragOffset = 0f;

        isTransitioning = false;
    }
    private void HandleSwipe()
    {
        if (isTransitioning ||
            cardsPanel.activeSelf ||
            settingsPanel.activeSelf ||
            aboutPanel.activeSelf ||
            quitConfirmPanel.activeSelf)
            return;

        if (!TryGetPointerPosition(
            out Vector2 position,
            out bool pressed,
            out bool released,
            out bool held))
            return;

        if (pressed)
        {
            swipeStartPosition = position;
            swipeInProgress = true;
        }

        if (!swipeInProgress)
            return;

        Vector2 delta = position - swipeStartPosition;

        if (held && draggedPanel == null)
        {
            if (Mathf.Abs(delta.x) < 15f ||
                Mathf.Abs(delta.x) <= Mathf.Abs(delta.y))
                return;

            int currentIndex = GetActivePanelIndex();
            if (currentIndex < 0)
                return;

            int targetIndex = currentIndex + (delta.x < 0f ? 1 : -1);

            if (targetIndex < 0 || targetIndex >= 6)
                return;

            BeginPanelDrag(targetIndex);
        }

        if (held && draggedPanel != null)
        {
            UpdateDraggedPanels(delta.x);
        }

        if (released)
        {
            swipeInProgress = false;

            if (draggedPanel != null)
            {
                bool complete =
                    Mathf.Abs(dragOffset) >= minSwipeDistance;

                StartCoroutine(FinishPanelDrag(complete));
            }
        }
    }
    private GameObject[] GetMainPanels()
    {
        return new GameObject[]
        {
        heroPanel,
        villagePanel,
        missionPanel,
        challengePanel,
        arenaPanel,
        clanPanel
        };
    }

    private int GetActivePanelIndex()
    {
        GameObject[] panels = GetMainPanels();

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null && panels[i].activeSelf)
                return i;
        }

        return -1;
    }
    private void NavigateToPanel(int targetIndex)
    {
        if (isTransitioning)
            return;

        GameObject[] panels = GetMainPanels();

        int currentIndex = GetActivePanelIndex();

        if (currentIndex < 0 ||
            targetIndex < 0 ||
            targetIndex >= panels.Length ||
            currentIndex == targetIndex)
            return;

        int direction = targetIndex > currentIndex ? 1 : -1;

        RectTransform outgoing =
            panels[currentIndex].GetComponent<RectTransform>();

        RectTransform incoming =
            panels[targetIndex].GetComponent<RectTransform>();

        StartCoroutine(
            AnimatePanels(outgoing, incoming, direction)
        );
    }
    private IEnumerator AnimatePanels(
    RectTransform outgoing,
    RectTransform incoming,
    int direction)
    {
        isTransitioning = true;
        if (incoming.gameObject == heroPanel)
            topButtonsPanel.SetActive(true);

        float width = ((RectTransform)outgoing.parent).rect.width;

        Vector2 outgoingStart = outgoing.anchoredPosition;
        Vector2 incomingEnd = incoming.anchoredPosition;
        Vector2 incomingStart = incomingEnd + Vector2.right * direction * width;

        incoming.anchoredPosition = incomingStart;
        incoming.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / transitionDuration);
            t = t * t * (3f - 2f * t);

            outgoing.anchoredPosition =
                Vector2.Lerp(outgoingStart,
                    outgoingStart - Vector2.right * direction * width, t);

            incoming.anchoredPosition =
                Vector2.Lerp(incomingStart, incomingEnd, t);

            yield return null;
        }

        outgoing.anchoredPosition = outgoingStart;
        incoming.anchoredPosition = incomingEnd;
        outgoing.gameObject.SetActive(false);
        topButtonsPanel.SetActive(incoming.gameObject == heroPanel);
        isTransitioning = false;
    }
    public void OpenHero()
    {
        NavigateToPanel(0);
    }
    public void OpenVillage()
    {
        NavigateToPanel(1);
    }
    private void OpenPlaceholder(GameObject selectedPanel)
    {
        heroPanel.SetActive(false);
        villagePanel.SetActive(false);
        missionPanel.SetActive(false);
        challengePanel.SetActive(false);
        arenaPanel.SetActive(false);
        clanPanel.SetActive(false);

        cardsPanel.SetActive(false);
        settingsPanel.SetActive(false);
        aboutPanel.SetActive(false);
        backButtonAbout.SetActive(false);

        selectedPanel.SetActive(true);

        mainButtonsPanel.SetActive(true);
        topButtonsPanel.SetActive(false);
    }

    public void OpenChallenge()
    {
        NavigateToPanel(3);
    }

    public void OpenArena()
    {
        NavigateToPanel(4);
    }

    public void OpenClan()
    {
        NavigateToPanel(5);
    }
    public void OpenMissions()
    {
        NavigateToPanel(2);
    }

    public void BackToHero()
    {
        heroPanel.SetActive(true);
        villagePanel.SetActive(false);
        challengePanel.SetActive(false);
        arenaPanel.SetActive(false);
        clanPanel.SetActive(false);

        missionPanel.SetActive(false);
        cardsPanel.SetActive(false);
        settingsPanel.SetActive(false);
        aboutPanel.SetActive(false);
        backButtonAbout.SetActive(false);

        mainButtonsPanel.SetActive(true);
        topButtonsPanel.SetActive(true);
    }

    // Compatibilité avec les boutons déjà configurés.
    public void BackToMainMenu()
    {
        BackToHero();
    }

    // --------------------------------------------------
    // CARTES
    // --------------------------------------------------

    public void OpenCards()
    {
        heroPanel.SetActive(false);
        missionPanel.SetActive(false);
        cardsPanel.SetActive(true);

        mainButtonsPanel.SetActive(false);
        topButtonsPanel.SetActive(false);

        DeckManager deckManager = FindAnyObjectByType<DeckManager>();

        if (deckManager != null)
        {
            deckManager.LoadDeck();
        }
    }

    // --------------------------------------------------
    // RÉGLAGES ET À PROPOS
    // --------------------------------------------------

    public void OpenSettings()
    {
        heroPanel.SetActive(false);
        missionPanel.SetActive(false);
        cardsPanel.SetActive(false);
        aboutPanel.SetActive(false);

        mainButtonsPanel.SetActive(false);
        topButtonsPanel.SetActive(false);

        settingsPanel.SetActive(true);
    }

    public void OpenAbout()
    {
        settingsPanel.SetActive(false);
        aboutPanel.SetActive(true);
        backButtonAbout.SetActive(true);

        mainButtonsPanel.SetActive(false);
        topButtonsPanel.SetActive(false);
    }

    public void BackToSettings()
    {
        aboutPanel.SetActive(false);
        backButtonAbout.SetActive(false);

        settingsPanel.SetActive(true);

        mainButtonsPanel.SetActive(false);
        topButtonsPanel.SetActive(false);
    }

    // --------------------------------------------------
    // CONFIRMATION DE SORTIE
    // --------------------------------------------------

    public void ShowQuitConfirmation()
    {
        quitConfirmPanel.SetActive(true);
    }

    public void CancelQuit()
    {
        quitConfirmPanel.SetActive(false);
    }

    public void ConfirmQuit()
    {
        Application.Quit();
    }

    // --------------------------------------------------
    // MISSIONS
    // --------------------------------------------------

    public void StartNormalMission()
    {
        GameSession.SelectedDifficulty = 1;
        SceneManager.LoadScene("GameScene");
    }

    public void StartHardMission()
    {
        GameSession.SelectedDifficulty = 2;
        SceneManager.LoadScene("GameScene");
    }

    // --------------------------------------------------
    // CONTRÔLES
    // --------------------------------------------------

    public void UseTapToMove()
    {
        ControlSettings.SetMode(ControlMode.TapToMove);
    }

    public void UseVirtualJoystick()
    {
        ControlSettings.SetMode(ControlMode.VirtualJoystick);
    }

    // --------------------------------------------------
    // GITHUB
    // --------------------------------------------------

    public void OpenGitHub()
    {
        Application.OpenURL(
            "https://github.com/LuisHRL/First-Summoner-Reborn"
        );
    }

    // --------------------------------------------------
    // LANGUES
    // --------------------------------------------------

    public void ChangeLanguage(int languageIndex)
    {
        StartCoroutine(SetLanguage(languageIndex));
    }

    private System.Collections.IEnumerator SetLanguage(int languageIndex)
    {
        yield return LocalizationSettings.InitializationOperation;

        if (languageIndex >= 0 &&
            languageIndex < LocalizationSettings.AvailableLocales.Locales.Count)
        {
            LocalizationSettings.SelectedLocale =
                LocalizationSettings.AvailableLocales.Locales[languageIndex];
        }
    }
}
