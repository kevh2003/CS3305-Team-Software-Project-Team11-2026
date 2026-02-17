using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 3f;
    public float currentHealth = 0f;
    private PlayerInput playerInput;

    private PlayerMovement playerMovement;
    private Renderer[] playerVisuals;
    private Interactor interactor;
    private bool isDead = false;

    public bool IsDead => isDead; // public read-only property for other scripts

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
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
        interactor.enabled = false;

    // Disable only the Interact action
    playerInput.actions["Interact"].Disable();

    //Removes player visuals
    foreach (Renderer render in playerVisuals)
        render.enabled = false;
}

}
