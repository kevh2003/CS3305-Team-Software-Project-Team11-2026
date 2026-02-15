using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 3f;
    public float currentHealth = 0f;

    private PlayerMovement playerMovement;
    private Renderer[] playerVisuals; // multiple renderers
    private bool isDead = false;

    public bool IsDead => isDead; // public read-only property for other scripts

    void Awake()
    {
        currentHealth = maxHealth;
        playerMovement = GetComponent<PlayerMovement>();
        playerVisuals = GetComponentsInChildren<Renderer>();
    }

    public void TakeDamage(float damageAmount)
    {
        if (isDead) return;

        currentHealth -= damageAmount;
        Debug.Log("Player health: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("Player has died!");

        // Disable movement
        if (playerMovement != null)
            playerMovement.enabled = false;

        // Hide all visuals
        foreach (Renderer render in playerVisuals)
        {
            render.enabled = false;
        }
    }
}
