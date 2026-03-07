using UnityEngine;
using Unity.Netcode;

public class PlayerSoundFX : NetworkBehaviour
{

    private bool isDead = false;
    private bool deathSfxPlayedThisLife = false;

    [Header("Interact")]
    public AudioClip interactClip;  //https://opengameart.org/content/click
    public float interactVolume = 1f;

    [Header("Damage")]
    public AudioClip damageClip;    //https://opengameart.org/content/player-hit-damage
    public float damageVolume = 1f;

    [Header("Death")]
    public AudioClip deathClip;     //https://opengameart.org/content/8bit-death-whirl
    public float deathVolume = 1f;

    [Header("Impact")]
    public AudioClip impactClip; //https://opengameart.org/content/jump-landing-sound
    public float impactVolume = 1f;
  
    [Header("Sources")]
    private AudioSource actionSource;   //Player actions
    private AudioSource bodySource;     //Player damage, death, etc.
    private PlayerHealth health;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        actionSource = CreateAudioSource("ActionSource", false);
        bodySource = CreateAudioSource("BodySource", false);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            actionSource.enabled = false;
            bodySource.enabled = false;
            return;
        }

        if (health != null)
        {
            isDead = health.IsDead.Value;
            deathSfxPlayedThisLife = isDead;
            health.CurrentHealth.OnValueChanged += OnHealthChanged;
            health.IsDead.OnValueChanged += OnDeadChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (health != null)
        {
            health.CurrentHealth.OnValueChanged -= OnHealthChanged;
            health.IsDead.OnValueChanged -= OnDeadChanged;
        }
    }

    private void OnHealthChanged(int oldValue, int newValue)
    {
        // Owner-local feedback for both host and client-owned players.
        if (newValue < oldValue)
            PlayDamageSound();
    }

    private void OnDeadChanged(bool oldValue, bool newValue)
    {
        isDead = newValue;

        if (!newValue)
        {
            deathSfxPlayedThisLife = false;
            return;
        }

        if (!oldValue)
            PlayDeathSound();
    }

    // Plays when player presses Interact button
    public void PlayInteractSound()
    {   
        if (!IsOwner) return;
        
        actionSource.PlayOneShot(interactClip, interactVolume);
    }

    // Plays when player takes damage
    public void PlayDamageSound()
    {   
        if (!IsOwner || isDead) return;
        bodySource.PlayOneShot(damageClip, damageVolume);
    }

    // Plays after death
    public void PlayDeathSound()
    {   
        if (!IsOwner || deathSfxPlayedThisLife) return;
        if (bodySource == null || deathClip == null) return;
        bodySource.PlayOneShot(deathClip, deathVolume);
        deathSfxPlayedThisLife = true;
    }

    // Plays after player falls vertically and hits the ground
    public void PlayImpactSound()
    {
        if (!IsOwner || isDead) return;

        bodySource.PlayOneShot(impactClip, impactVolume);
    }

    private AudioSource CreateAudioSource(string name, bool loop)
    {
        GameObject sourceObject = new GameObject(name);
        sourceObject.transform.SetParent(transform);
        AudioSource audioSource = sourceObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        return audioSource;
    }

}