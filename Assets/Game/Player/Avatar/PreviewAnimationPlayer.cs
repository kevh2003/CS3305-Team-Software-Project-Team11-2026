using UnityEngine;


// Used for showing player previews in the Lobby scene for selectable players/characters
[DisallowMultipleComponent]
public sealed class PreviewAnimationPlayer : MonoBehaviour
{
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private AnimationClip clip;
    [SerializeField] private GameObject sampleRoot;
    [SerializeField] private GameObject secondarySampleRoot;

    private float _sampleTime;

    public void Configure(
        AnimationClip animationClip,
        Animator animatorOverride = null,
        GameObject sampleRootOverride = null,
        GameObject secondarySampleRootOverride = null)
    {
        clip = animationClip;
        if (animatorOverride != null)
            targetAnimator = animatorOverride;
        if (sampleRootOverride != null)
            sampleRoot = sampleRootOverride;
        secondarySampleRoot = secondarySampleRootOverride;
        _sampleTime = 0f;
    }

    private void OnEnable()
    {
        _sampleTime = 0f;
    }

    private void Update()
    {
        if (clip == null)
            return;

        if (sampleRoot == null)
        {
            if (targetAnimator != null)
                sampleRoot = targetAnimator.gameObject;
            else
                sampleRoot = gameObject;
        }

        if (sampleRoot == null)
            return;

        _sampleTime += Time.unscaledDeltaTime;
        float clipLength = clip.length;
        float t = clipLength > 0f ? Mathf.Repeat(_sampleTime, clipLength) : 0f;
        clip.SampleAnimation(sampleRoot, t);

        if (secondarySampleRoot != null && secondarySampleRoot != sampleRoot)
            clip.SampleAnimation(secondarySampleRoot, t);
    }
}