using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float mvspeed = 5f;
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    private Rigidbody rb;
    private Vector2 moveInput;
    private bool isGrounded;
    private PlayerInput playerInput;

    void Start()
    {
       rb = GetComponent<Rigidbody>(); 
       playerInput = new PlayerInput();
    }
    void Update()
    {}

    void FixedUpdate(){
        MovePlayer();
    }

    public void OnMovement(InputValue value){

        moveInput = value.Get<Vector2>();
    }

    void MovePlayer(){
        Vector3 direction = transform.right * moveInput.x + transform.forward * moveInput.y;
        direction.Normalize();
        rb.linearVelocity = new Vector3(direction.x * mvspeed, rb.linearVelocity.y,direction.z * mvspeed);
    }
}
