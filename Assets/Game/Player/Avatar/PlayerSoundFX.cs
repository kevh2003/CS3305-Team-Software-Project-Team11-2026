using UnityEngine;
using Unity.Netcode;

public class PlayerSoundFX : NetworkBehaviour
{

    private bool isDead = false;
    private bool deathSfxPlayedThisLife = false;

    [Header("Interact")]
    public AudioClip interactClip;
    public float interactVolume = 0f;

    [Header("Damage")]
    public AudioClip damageClip;
    public float damageVolume = 0.09f;

    [Header("Death")]
    public AudioClip deathClip;
    public float deathVolume = 0.4f;

    [Header("Impact")]
    public AudioClip jumpClip;
    public float jumpVolume = 0.1f;
    public AudioClip impactClip;
    public float impactVolume = 0.18f;

    [Header("Pickups")]
    public AudioClip keyPickupClip;
    public AudioClip torchPickupClip;
    [Range(0f, 1f)] public float pickupVolume = 0.15f;

    [Header("Utility")]
    public AudioClip torchToggleClip;
    [Range(0f, 1f)] public float torchToggleVolume = 0.25f;
    public AudioClip cctvUseClip;
    [Range(0f, 1f)] public float cctvUseVolume = 0.2f;
    public AudioClip duckPickupClip;
    [Range(0f, 1f)] public float duckPickupVolume = 0.075f;
    public AudioClip winClip;
    [Range(0f, 1f)] public float winVolume = 0.6f;

    [Header("Hold Loops")]
    public AudioClip assignmentTypingLoopClip;
    public AudioClip gradesChangeLoopClip;
    [Range(0f, 1f)] public float holdLoopVolume = 0.25f;

    [Header("Footsteps")]
    public AudioClip[] walkFootstepClips;
    public AudioClip[] runFootstepClips;
    [Range(0f, 1f)] public float walkFootstepVolume = 0.05f;
    [Range(0f, 1f)] public float runFootstepVolume = 0.25f;
    [Range(0f, 0.25f)] public float footstepPitchJitter = 0.06f;
  
    [Header("Sources")]
    private AudioSource actionSource;   //Player actions
    private AudioSource bodySource;     //Player damage, death, etc.
    private AudioSource holdLoopSource; //Looping hold-to-interact sounds
    private AudioSource footstepSource; //One-shot footsteps
    private PlayerHealth health;
    private float localSfxVolumeMultiplier = 1f;

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
        actionSource.PlayOneShot(interactClip, interactVolume * localSfxVolumeMultiplier);
    }

    // Plays when player takes damage
    public void PlayDamageSound()
    {   
        if (!IsOwner || isDead) return;
        if (bodySource == null || damageClip == null) return;
        bodySource.PlayOneShot(damageClip, damageVolume * localSfxVolumeMultiplier);
    }

    // Plays after death
    public void PlayDeathSound()
    {   
        if (!IsOwner || deathSfxPlayedThisLife) return;
        if (bodySource == null || deathClip == null) return;
        bodySource.PlayOneShot(deathClip, deathVolume * localSfxVolumeMultiplier);
        deathSfxPlayedThisLife = true;
    }

    // Plays when player jumps
    public void PlayJumpSound()
    {
        if (!IsOwner || isDead) return;
        if (bodySource == null || jumpClip == null) return;
        bodySource.PlayOneShot(jumpClip, jumpVolume * localSfxVolumeMultiplier);
    }

    // Plays after player falls vertically and hits the ground
    public void PlayImpactSound()
    {
        if (!IsOwner || isDead) return;
        if (bodySource == null || impactClip == null) return;

        bodySource.PlayOneShot(impactClip, impactVolume * localSfxVolumeMultiplier);
    }

    public void PlayPickupItemSound(int itemId, int keyItemId, int torchItemId)
    {
        if (!IsOwner) return;
        if (actionSource == null) return;

        AudioClip clip = null;
        if (itemId == keyItemId) clip = keyPickupClip;
        else if (itemId == torchItemId) clip = torchPickupClip;

        if (clip != null)
            actionSource.PlayOneShot(clip, pickupVolume * localSfxVolumeMultiplier);
    }

    public void PlayTorchToggleSound()
    {
        if (!IsOwner) return;
        if (actionSource == null || torchToggleClip == null) return;
        actionSource.PlayOneShot(torchToggleClip, torchToggleVolume * localSfxVolumeMultiplier);
    }

    public void PlayCctvUseSound()
    {
        if (!IsOwner) return;
        if (actionSource == null || cctvUseClip == null) return;
        actionSource.PlayOneShot(cctvUseClip, cctvUseVolume * localSfxVolumeMultiplier);
    }

    public void PlayDuckPickupSound()
    {
        if (!IsOwner) return;
        if (actionSource == null || duckPickupClip == null) return;
        actionSource.PlayOneShot(duckPickupClip, duckPickupVolume * localSfxVolumeMultiplier);
    }

    public void PlayWinSound()
    {
        if (!IsOwner) return;
        if (actionSource == null || winClip == null) return;
        actionSource.PlayOneShot(winClip, winVolume * localSfxVolumeMultiplier);
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
        holdLoopSource.volume = holdLoopVolume * localSfxVolumeMultiplier;
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
        footstepSource.PlayOneShot(clip, volume * localSfxVolumeMultiplier);
        footstepSource.pitch = 1f;
    }

    public void SetLocalSfxVolumeMultiplier(float multiplier)
    {
        if (!IsOwner) return;
        localSfxVolumeMultiplier = Mathf.Clamp01(multiplier);

        if (holdLoopSource != null && holdLoopSource.isPlaying)
            holdLoopSource.volume = holdLoopVolume * localSfxVolumeMultiplier;
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