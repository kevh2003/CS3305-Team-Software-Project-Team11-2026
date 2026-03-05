
using Unity.Netcode;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

public class AnimationStateController : NetworkBehaviour
{
    private Animator animator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        animator = GetComponent<Animator>();
    }



    // Update is called once per frame
    void Update()
    {
        Debug.Log("Update called in AnimationStateController");
        // using unity's new input system is stinky 
        if (IsOwner)
        {
            bool walking = Keyboard.current.wKey.isPressed || Keyboard.current.aKey.isPressed || Keyboard.current.sKey.isPressed || Keyboard.current.dKey.isPressed;
            bool running = Keyboard.current.leftShiftKey.isPressed && walking;
            animator.SetBool("isWalking", walking);
            animator.SetBool("isRunning", running);
        }

      

    }
}
