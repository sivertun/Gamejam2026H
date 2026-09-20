using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour, IHasVelocity
{
    private Rigidbody rb;
    private Camera mainCamera;

    [SerializeField] private float maxSpeed;
    [SerializeField] private float smoothTime;
    public Vector3 velocity;
    private Vector3 movementDerivative;

    public bool enable;

    [Header("Input Actions")]
    public InputActionReference moveAction;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        mainCamera = Camera.main;
    }

    void FixedUpdate()
    {
        Vector3 moveInput = new Vector3(moveAction.action.ReadValue<Vector2>().x, 0, moveAction.action.ReadValue<Vector2>().y);
        Vector3 targetVelocity = maxSpeed * moveInput.normalized;
        velocity = Vector3.SmoothDamp(velocity, targetVelocity, ref movementDerivative, smoothTime);
        rb.MovePosition(transform.position + velocity * Time.fixedDeltaTime);

        rb.MoveRotation(FindLookRotation());
    }

    private Quaternion FindLookRotation()
    {
        if (Pointer.current != null)
        {
            Vector2 pointerScreenPos = Pointer.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(pointerScreenPos);

            Plane mouseInteractionPlane = new Plane(Vector3.up, transform.position);

            if (mouseInteractionPlane.Raycast(ray, out float planeDistance))
            {
                Vector3 pointerWorldPos = ray.GetPoint(planeDistance);
                Vector3 direction = pointerWorldPos - transform.position;
                direction.y = 0;

                return Quaternion.LookRotation(direction);
            }
        }

        return Quaternion.identity;
    }

    public void SetVelocity(Vector3 velocity)
    {
        this.velocity = velocity;
    }
}