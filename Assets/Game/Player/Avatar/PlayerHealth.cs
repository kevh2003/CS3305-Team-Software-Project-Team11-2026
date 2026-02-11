using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxhealth = 3f;
    public float currentHealth = 0f;
    void Start()
    {
        currentHealth = maxhealth;
    }

    public void TakeDamage(float damageAmount)
    {
        currentHealth -= damageAmount;
        Debug.Log("Player health: " + currentHealth);
        
        if (currentHealth <= 0)
        {
            Debug.Log("Player has died!");
        }
    }

}
