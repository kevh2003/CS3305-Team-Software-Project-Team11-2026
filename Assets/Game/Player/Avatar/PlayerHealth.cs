using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 3f;
    public float currentHealth = 0f;

    private PlayerMovement playerMovement;
    private Renderer[] playerVisuals;
    private Interactor interactor;
    private bool isDead = false;

    public bool IsDead => isDead; // public read-only property for other scripts

    void Awake()
    {
        currentHealth = maxHealth;
        playerMovement = GetComponent<PlayerMovement>();
        playerVisuals = GetComponentsInChildren<Renderer>();
        interactor = GetComponent<Interactor>();
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

        if (interactor != null)
        {
            interactor.enabled = false;
            Debug.Log("Player interactor disabled.");
        }

        // Hide all visuals
        foreach (Renderer render in playerVisuals)
        {
            render.enabled = false;
        }
    }
}
