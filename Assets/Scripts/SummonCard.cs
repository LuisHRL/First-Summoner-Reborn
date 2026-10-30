using UnityEngine;

public class SummonCard : MonoBehaviour
{
    [Header("Définition de la carte")]
    public SummonCardDefinition cardDefinition;
    public ManaSystem manaSystem;
    public GameObject minionPrefab;
    public Transform player;
    public int manaCost = 25;
    [Header("Double invocation")]
    public bool spawnBehindPlayer = false;
    public float spawnDistance = 2f;
    public float unitSpacing = 1.5f;
    public float checkRadius = 0.6f;

    public void PlayCard()
    {
        if (manaSystem == null || minionPrefab == null || player == null)
            return;

        // Une seule dépense de mana pour les deux unités.
        if (!manaSystem.SpendMana(manaCost))
        {
            Debug.Log("Pas assez de mana");
            return;
        }

        // Placement fixe selon le haut/bas de l'écran,
        // indépendamment de l'orientation du héros.
        Vector3 direction = spawnBehindPlayer
            ? Vector3.back
            : Vector3.forward;

        Vector3 center = player.position + direction * spawnDistance;

        int unitCount = cardDefinition != null
    ? Mathf.Max(1, cardDefinition.numberOfUnits)
    : 2;
        int spawnedCount = 0;

        for (int unit = 0; unit < unitCount; unit++)
        {
            float side = unit - (unitCount - 1) * 0.5f;

            Vector3 desiredPosition =
                center + Vector3.right * side * unitSpacing;

            if (TryFindFreePosition(
                desiredPosition,
                direction,
                out Vector3 spawnPosition))
            {
                GameObject summonedUnit = Instantiate(
    minionPrefab,
    spawnPosition,
    Quaternion.identity
);

                // Appliquer les statistiques de la carte à l'invocation.
                MinionAI minionAI = summonedUnit.GetComponent<MinionAI>();

                if (minionAI != null && cardDefinition != null)
                {
                    minionAI.Initialize(cardDefinition);
                }

                spawnedCount++;


                if (cardDefinition != null &&
                    cardDefinition.summonSound != null &&
                    GameAudioManager.Instance != null)
                {
                    GameAudioManager.Instance.PlaySFX(
                        cardDefinition.summonSound,
                        spawnPosition
                    );
                }

            }
        }

        if (spawnedCount < unitCount)
        {
            Debug.LogWarning(
                "Invocation : emplacement libre introuvable pour une unité."
            );
        }
    }

    private bool TryFindFreePosition(
        Vector3 desiredPosition,
        Vector3 direction,
        out Vector3 freePosition)
    {
        // Vérifier d'abord la position prévue.
        if (IsPositionFree(desiredPosition))
        {
            freePosition = desiredPosition;
            return true;
        }

        // Chercher autour, sans partir trop loin.
        for (int ring = 1; ring <= 3; ring++)
        {
            float radius = ring * unitSpacing;

            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f;

                Vector3 offset =
                    Quaternion.Euler(0f, angle, 0f)
                    * direction * radius;

                Vector3 candidate = desiredPosition + offset;

                // Rester du bon côté du héros,
                // même si celui-ci tourne.
                float directionalOffset = Vector3.Dot(
                    candidate - player.position,
                    direction
                );

                if (directionalOffset <= 0f)
                    continue;

                if (IsPositionFree(candidate))
                {
                    freePosition = candidate;
                    return true;
                }
            }
        }

        freePosition = desiredPosition;
        return false;
    }

    private bool IsPositionFree(Vector3 position)
    {
        return !Physics.CheckSphere(
            position + Vector3.up,
            checkRadius
        );
    }
}