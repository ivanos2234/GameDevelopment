using UnityEngine;

/// <summary>Health and a simple world-space sprite health bar for a prototype enemy.</summary>
public sealed class EnemyHealth2D : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private Transform healthBarFill;

    private float currentHealth;
    private Vector3 fillScale;
    private float fillCenterX;

    private void Awake()
    {
        currentHealth = maxHealth;
        if (healthBarFill != null)
        {
            fillScale = healthBarFill.localScale;
            fillCenterX = healthBarFill.localPosition.x;
        }
        RefreshHealthBar();
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || currentHealth <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        RefreshHealthBar();

        if (currentHealth <= 0f)
            Destroy(gameObject);
    }

    private void RefreshHealthBar()
    {
        if (healthBarFill == null)
            return;

        float fraction = maxHealth > 0f ? currentHealth / maxHealth : 0f;
        healthBarFill.localScale = new Vector3(fillScale.x * fraction, fillScale.y, fillScale.z);

        // Keep the left edge fixed as the bar shrinks.
        healthBarFill.localPosition = new Vector3(
            fillCenterX - fillScale.x * (1f - fraction) * 0.5f,
            healthBarFill.localPosition.y,
            healthBarFill.localPosition.z);
    }
}
