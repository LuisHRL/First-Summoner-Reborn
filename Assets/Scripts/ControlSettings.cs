using UnityEngine;

public enum ControlMode
{
    TapToMove,
    VirtualJoystick
}

public static class ControlSettings
{
    private const string SaveKey = "ControlMode";

    // Valeur par défaut si le joueur n'a jamais choisi : TapToMove.
    private static ControlMode currentMode = ControlMode.TapToMove;

    public static ControlMode CurrentMode => currentMode;

    // À appeler une fois au lancement du jeu (par exemple depuis MainMenu)
    // pour recharger le dernier choix du joueur.
    public static void LoadSavedMode()
    {
        int saved = PlayerPrefs.GetInt(SaveKey, (int)ControlMode.TapToMove);
        currentMode = (ControlMode)saved;
    }

    public static void SetMode(ControlMode mode)
    {
        currentMode = mode;
        PlayerPrefs.SetInt(SaveKey, (int)mode);
        PlayerPrefs.Save();
    }
}