using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Speeds")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    public float jumpHeight = 2f;     // how high you jump

    private CharacterController cc;
    private Vector3 velocity;

    void Start()
    {
        cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        // ── Read input ──
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // ── Horizontal move ──
        Vector3 move = transform.right * x + transform.forward * z;
        cc.Move(move * moveSpeed * Time.deltaTime);

        // ── Ground check & stick ──
        if (cc.isGrounded && velocity.y < 0f)
            velocity.y = -2f;  // keep you glued to the ground

        // ── Jump ──
        if (cc.isGrounded && Input.GetButtonDown("Jump"))
        {
            // v = sqrt(2 * jumpHeight * -gravity)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // ── Gravity ──
        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }
}
