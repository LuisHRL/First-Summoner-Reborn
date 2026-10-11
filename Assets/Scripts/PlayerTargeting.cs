using UnityEngine;

public class PlayerTargeting : MonoBehaviour
{
    public EnemyHealth CurrentTarget { get; private set; }

    private Renderer selectedRenderer;
    private Color originalColor;

    void Update()
    {
        if (PointerInput.TryGetPointerDown(out Vector2 screenPosition) &&
            !PointerInput.IsPointerOverUI())
        {
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                EnemyHealth enemy =
                    hit.collider.GetComponentInParent<EnemyHealth>();

                if (enemy != null)
                {
                    SelectTarget(enemy);
                }
            }
        }
    }

    void SelectTarget(EnemyHealth newTarget)
    {
        if (CurrentTarget == newTarget)
            return;

        ClearCurrentTarget();

        CurrentTarget = newTarget;
        selectedRenderer = CurrentTarget.GetComponentInChildren<Renderer>();

        if (selectedRenderer != null)
        {
            originalColor = selectedRenderer.material.color;
            selectedRenderer.material.color = Color.red;
        }
    }

    void ClearCurrentTarget()
    {
        if (selectedRenderer != null)
        {
            selectedRenderer.material.color = originalColor;
        }

        CurrentTarget = null;
        selectedRenderer = null;
    }
}