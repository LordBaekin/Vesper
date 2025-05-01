using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Speeds")]
    public float mouseSensitivity = 100f;   // for free‐look
    public float rotationSpeed = 100f;   // for Q/E smooth turn

    [Header("References")]
    public Transform playerBody;           // assign your Player transform here

    private float xRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 1) Smooth Q/E turn
        float yaw = 0f;
        if (Input.GetKey(KeyCode.Q))
            yaw = -rotationSpeed * dt;
        else if (Input.GetKey(KeyCode.E))
            yaw = rotationSpeed * dt;

        // 2) Mouse‐look only when RMB held
        if (Input.GetMouseButton(1))
        {
            // horizontal look (yaw)
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * dt;
            yaw += mouseX;

            // vertical look (pitch)
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * dt;
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        // apply yaw to player body
        playerBody.Rotate(Vector3.up * yaw);
    }
}
