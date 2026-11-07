
using UnityEngine;

public class LevelProgressGauge : MonoBehaviour
{
    [Header("Progression du niveau")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform levelStart;
    [SerializeField] private Transform levelEnd;

    [Header("Interface")]
    [SerializeField] private RectTransform playerProgressMarker;
    [SerializeField] private RectTransform progressGauge;

    private void Update()
    {
        if (player == null || levelStart == null ||
            levelEnd == null || playerProgressMarker == null ||
            progressGauge == null)
            return;

        // Progression sur l'axe du chemin (début -> fin).
        Vector3 direction = levelEnd.position - levelStart.position;
        float lengthSquared = direction.sqrMagnitude;

        if (lengthSquared < 0.001f)
            return;

        float progress = Vector3.Dot(
            player.position - levelStart.position,
            direction
        ) / lengthSquared;

        progress = Mathf.Clamp01(progress);

        // Le triangle est ancré en bas de l'échelle.
        Vector2 position = playerProgressMarker.anchoredPosition;
        position.y = progress * progressGauge.rect.height;

        playerProgressMarker.anchoredPosition = position;
    }
}
