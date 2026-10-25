using UnityEngine;

public class SummonCard : MonoBehaviour
{
    public ManaSystem manaSystem;
    public GameObject minionPrefab;
    public Transform player;
    public int manaCost = 25;
    public AudioClip summonSound;

    public void PlayCard()
    {
        if (!manaSystem.SpendMana(manaCost))
        {
            Debug.Log("Pas assez de mana");
            return;
        }

        Vector3 spawnPosition = player.position + player.forward * 2f;

        float spacing = 1.5f;
        float checkRadius = 0.6f;

        for (int i = 0; i < 12; i++)
        {
            Vector3 candidate = player.position
                + Quaternion.Euler(0f, i * 30f, 0f)
                * player.forward * (2f + (i / 6) * spacing);

            if (!Physics.CheckSphere(candidate + Vector3.up, checkRadius))
            {
                spawnPosition = candidate;
                break;
            }
        }
        if (summonSound != null)
        {
            AudioSource.PlayClipAtPoint(summonSound, spawnPosition);
        }
        Instantiate(minionPrefab, spawnPosition, Quaternion.identity);
    }
}