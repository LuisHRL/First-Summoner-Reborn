
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Localization.Settings;
using UnityEngine.InputSystem;

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
        else if (missionPanel.activeSelf)
        {
            BackToHero();
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
    private void HandleSwipe()
    {
        // Ne pas changer d'onglet dans les sous-menus.
        if (cardsPanel.activeSelf ||
            settingsPanel.activeSelf ||
            aboutPanel.activeSelf ||
            quitConfirmPanel.activeSelf)
            return;

        if (Touchscreen.current == null)
            return;

        var touch = Touchscreen.current.primaryTouch;

        if (touch.press.wasPressedThisFrame)
        {
            swipeStartPosition = touch.position.ReadValue();
            swipeInProgress = true;
        }

        if (touch.press.wasReleasedThisFrame && swipeInProgress)
        {
            swipeInProgress = false;

            Vector2 delta =
                touch.position.ReadValue() - swipeStartPosition;

            if (Mathf.Abs(delta.x) < minSwipeDistance ||
                Mathf.Abs(delta.x) <= Mathf.Abs(delta.y))
                return;

            int currentIndex = heroPanel.activeSelf ? 0 :
                villagePanel.activeSelf ? 1 :
                missionPanel.activeSelf ? 2 :
                challengePanel.activeSelf ? 3 :
                arenaPanel.activeSelf ? 4 :
                clanPanel.activeSelf ? 5 : -1;

            if (currentIndex < 0)
                return;

            int nextIndex = Mathf.Clamp(
                currentIndex + (delta.x < 0f ? 1 : -1),
                0,
                5
            );

            switch (nextIndex)
            {
                case 0: OpenHero(); break;
                case 1: OpenVillage(); break;
                case 2: OpenMissions(); break;
                case 3: OpenChallenge(); break;
                case 4: OpenArena(); break;
                case 5: OpenClan(); break;
            }
        }
    }
    public void OpenHero()
    {
        BackToHero();
    }
    public void OpenVillage()
    {
        heroPanel.SetActive(false);
        villagePanel.SetActive(true);
        missionPanel.SetActive(false);
        challengePanel.SetActive(false);
        arenaPanel.SetActive(false);
        clanPanel.SetActive(false);

        cardsPanel.SetActive(false);
        settingsPanel.SetActive(false);
        aboutPanel.SetActive(false);
        backButtonAbout.SetActive(false);

        mainButtonsPanel.SetActive(true);
        topButtonsPanel.SetActive(false);
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
        OpenPlaceholder(challengePanel);
    }

    public void OpenArena()
    {
        OpenPlaceholder(arenaPanel);
    }

    public void OpenClan()
    {
        OpenPlaceholder(clanPanel);
    }
    public void OpenMissions()
    {
        heroPanel.SetActive(false);
        missionPanel.SetActive(true);
        villagePanel.SetActive(false);
        challengePanel.SetActive(false);
        arenaPanel.SetActive(false);
        clanPanel.SetActive(false);
        cardsPanel.SetActive(false);
        settingsPanel.SetActive(false);
        aboutPanel.SetActive(false);
        backButtonAbout.SetActive(false);

        // Navigation inférieure permanente.
        mainButtonsPanel.SetActive(true);

        // Réglages affichés uniquement dans Héros.
        topButtonsPanel.SetActive(false);
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
