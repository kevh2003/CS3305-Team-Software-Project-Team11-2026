using UnityEngine;
using UnityEngine.UI;

public class HealthDisplay : MonoBehaviour
{

    public float health;
    public float maxHealth;

    public Sprite emptyHeart;
    public Sprite fullHeart;
    public Image[] hearts;

    private PlayerHealth playerHealth;

    void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();
    }

    void Update()
    {

        health = playerHealth.currentHealth;
        maxHealth = playerHealth.maxhealth;

        for (int i=0;i<hearts.Length;i++)
        {

            if (i < health)
            {
                hearts[i].sprite = fullHeart;
            } else
            {
                hearts[i].sprite = emptyHeart;    
            }

            if (i < maxHealth)
            {
                hearts[i].enabled = true;
            }
        }
    }
}
