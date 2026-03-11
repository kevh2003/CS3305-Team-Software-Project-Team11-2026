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
    public AudioClip jumpClip;
    public float jumpVolume = 1f;
    public AudioClip impactClip; //https://opengameart.org/content/jump-landing-sound
    public float impactVolume = 1f;

    [Header("Pickups")]
    public AudioClip keyPickupClip;
    public AudioClip torchPickupClip;
    [Range(0f, 1f)] public float pickupVolume = 1f;

    [Header("Utility")]
    public AudioClip torchToggleClip;
    [Range(0f, 1f)] public float torchToggleVolume = 1f;
    public AudioClip cctvUseClip;
    [Range(0f, 1f)] public float cctvUseVolume = 1f;
    public AudioClip duckPickupClip;
    [Range(0f, 1f)] public float duckPickupVolume = 1f;
    public AudioClip winClip;
    [Range(0f, 1f)] public float winVolume = 1f;

    [Header("Hold Loops")]
    public AudioClip assignmentTypingLoopClip;
    public AudioClip gradesChangeLoopClip;
    [Range(0f, 1f)] public float holdLoopVolume = 1f;

    [Header("Footsteps")]
    public AudioClip[] walkFootstepClips;
    public AudioClip[] runFootstepClips;
    [Range(0f, 1f)] public float walkFootstepVolume = 0.8f;
    [Range(0f, 1f)] public float runFootstepVolume = 0.9f;
    [Range(0f, 0.25f)] public float footstepPitchJitter = 0.06f;
  
    [Header("Sources")]
    private AudioSource actionSource;   //Player actions
    private AudioSource bodySource;     //Player damage, death, etc.
    private AudioSource holdLoopSource; //Looping hold-to-interact sounds
    private AudioSource footstepSource; //One-shot footsteps
    private PlayerHealth health;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        actionSource = CreateAudioSource("ActionSource", false);
        bodySource = CreateAudioSource("BodySource", false);
        holdLoopSource = CreateAudioSource("HoldLoopSource", true);
        footstepSource = CreateAudioSource("FootstepSource", false);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            actionSource.enabled = false;
            bodySource.enabled = false;
            holdLoopSource.enabled = false;
            footstepSource.enabled = false;
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

        StopHoldLoopSound();
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
        if (actionSource == null || interactClip == null) return;
        actionSource.PlayOneShot(interactClip, interactVolume);
    }

    // Plays when player takes damage
    public void PlayDamageSound()
    {   
        if (!IsOwner || isDead) return;
        if (bodySource == null || damageClip == null) return;
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

    // Plays when player jumps
    public void PlayJumpSound()
    {
        if (!IsOwner || isDead) return;
        if (bodySource == null || jumpClip == null) return;
        bodySource.PlayOneShot(jumpClip, jumpVolume);
    }

    // Plays after player falls vertically and hits the ground
    public void PlayImpactSound()
    {
        if (!IsOwner || isDead) return;
        if (bodySource == null || impactClip == null) return;

        bodySource.PlayOneShot(impactClip, impactVolume);
    }

    public void PlayPickupItemSound(int itemId, int keyItemId, int torchItemId)
    {
        if (!IsOwner) return;
        if (actionSource == null) return;

        AudioClip clip = null;
        if (itemId == keyItemId) clip = keyPickupClip;
        else if (itemId == torchItemId) clip = torchPickupClip;

        if (clip != null)
            actionSource.PlayOneShot(clip, pickupVolume);
    }

    public void PlayTorchToggleSound()
    {
        if (!IsOwner) return;
        if (actionSource == null || torchToggleClip == null) return;
        actionSource.PlayOneShot(torchToggleClip, torchToggleVolume);
    }

    public void PlayCctvUseSound()
    {
        if (!IsOwner) return;
        if (actionSource == null || cctvUseClip == null) return;
        actionSource.PlayOneShot(cctvUseClip, cctvUseVolume);
    }

    public void PlayDuckPickupSound()
    {
        if (!IsOwner) return;
        if (actionSource == null || duckPickupClip == null) return;
        actionSource.PlayOneShot(duckPickupClip, duckPickupVolume);
    }

    public void PlayWinSound()
    {
        if (!IsOwner) return;
        if (actionSource == null || winClip == null) return;
        actionSource.PlayOneShot(winClip, winVolume);
    }

    public void StartAssignmentTypingLoop()
    {
        StartHoldLoop(assignmentTypingLoopClip);
    }

    public void StartGradesChangeLoop()
    {
        StartHoldLoop(gradesChangeLoopClip);
    }

    public void StopHoldLoopSound()
    {
        if (holdLoopSource == null) return;
        holdLoopSource.Stop();
        holdLoopSource.clip = null;
    }

    public void PlayWalkFootstepSound()
    {
        PlayFootstepFromSet(walkFootstepClips, walkFootstepVolume);
    }

    public void PlayRunFootstepSound()
    {
        PlayFootstepFromSet(runFootstepClips, runFootstepVolume);
    }

    private void StartHoldLoop(AudioClip clip)
    {
        if (!IsOwner) return;
        if (holdLoopSource == null) return;
        if (clip == null)
        {
            StopHoldLoopSound();
            return;
        }

        if (holdLoopSource.isPlaying && holdLoopSource.clip == clip)
            return;

        holdLoopSource.clip = clip;
        holdLoopSource.volume = holdLoopVolume;
        holdLoopSource.loop = true;
        holdLoopSource.Play();
    }

    private void PlayFootstepFromSet(AudioClip[] clips, float volume)
    {
        if (!IsOwner || isDead) return;
        if (footstepSource == null || clips == null || clips.Length == 0) return;

        int idx = Random.Range(0, clips.Length);
        AudioClip clip = clips[idx];
        if (clip == null) return;

        footstepSource.pitch = 1f + Random.Range(-footstepPitchJitter, footstepPitchJitter);
        footstepSource.PlayOneShot(clip, volume);
        footstepSource.pitch = 1f;
    }

    private AudioSource CreateAudioSource(string name, bool loop)
    {
        GameObject sourceObject = new GameObject(name);
        sourceObject.transform.SetParent(transform);
        AudioSource audioSource = sourceObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = loop;
        return audioSource;
    }

}