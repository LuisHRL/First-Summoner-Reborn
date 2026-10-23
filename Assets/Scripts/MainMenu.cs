
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
    public GameObject missionPanel;

    [Header("Sous-menus")]
    public GameObject cardsPanel;
    public GameObject settingsPanel;
    public GameObject aboutPanel;

    [Header("Boutons et confirmations")]
    public GameObject backButtonAbout;
    public GameObject quitConfirmPanel;

    private void Start()
    {
        ControlSettings.LoadSavedMode();

        // Héros est l'écran d'accueil.
        BackToHero();

        quitConfirmPanel.SetActive(false);
    }

    private void Update()
    {
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

    public void OpenHero()
    {
        BackToHero();
    }

    public void OpenMissions()
    {
        heroPanel.SetActive(false);
        missionPanel.SetActive(true);

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
