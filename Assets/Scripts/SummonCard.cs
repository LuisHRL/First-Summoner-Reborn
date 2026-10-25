using UnityEngine;

public class SummonCard : MonoBehaviour
{
    public ManaSystem manaSystem;
    public GameObject minionPrefab;
    public Transform player;
    public int manaCost = 25;
    public AudioClip summonSound;

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

        Vector3 direction = spawnBehindPlayer
            ? -player.forward
            : player.forward;

        Vector3 center = player.position + direction * spawnDistance;

        int spawnedCount = 0;

        for (int unit = 0; unit < 2; unit++)
        {
            // Une unité à gauche, l'autre à droite.
            float side = unit == 0 ? -0.5f : 0.5f;

            Vector3 desiredPosition =
                center + player.right * side * unitSpacing;

            if (TryFindFreePosition(desiredPosition, out Vector3 spawnPosition))
            {
                Instantiate(
                    minionPrefab,
                    spawnPosition,
                    Quaternion.identity
                );

                spawnedCount++;

                if (summonSound != null)
                {
                    AudioSource.PlayClipAtPoint(
                        summonSound,
                        spawnPosition
                    );
                }
            }
        }

        if (spawnedCount < 2)
        {
            Debug.LogWarning(
                "Invocation : emplacement libre introuvable pour une unité."
            );
        }
    }

    private bool TryFindFreePosition(
        Vector3 desiredPosition,
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
                    * player.forward * radius;

                Vector3 candidate = desiredPosition + offset;

                // Rester devant ou derrière selon la carte.
                float forwardOffset = Vector3.Dot(
                    candidate - player.position,
                    player.forward
                );

                if (spawnBehindPlayer && forwardOffset >= 0f)
                    continue;

                if (!spawnBehindPlayer && forwardOffset <= 0f)
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