using UnityEngine;
using Unity.Netcode;

public class PlayerSoundFX : NetworkBehaviour
{

    private bool isDead = false;

    [Header("Interact")]
    public AudioClip interactClip;
    public float interactVolume = 1f;

    [Header("Damage")]
    public AudioClip damageClip;
    public float damageVolume = 1f;

    [Header("Death")]
    public AudioClip deathClip;
    public float deathVolume = 1f;
  
    [Header("Sources")]
    private AudioSource actionSource;   //Player actions
    private AudioSource bodySource;     //Player damage, death, etc.

    private void Awake()
    {

        actionSource = CreateAudioSource("ActionSource", false);
        bodySource = CreateAudioSource("BodySource", false);

    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            actionSource.enabled = false;
            bodySource.enabled = false;
        }
    }

    public void PlayInteractSound()
    {   
        if (!IsOwner) return;
        
        actionSource.PlayOneShot(interactClip, interactVolume);
    }

    public void PlayDamageSound()
    {   
        if (!IsOwner || isDead) return;
        bodySource.PlayOneShot(damageClip, damageVolume);
    }

    public void PlayDeathSound()
    {   
        if (!IsOwner || isDead) return;

        isDead = true;

        bodySource.PlayOneShot(deathClip, deathVolume);
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
