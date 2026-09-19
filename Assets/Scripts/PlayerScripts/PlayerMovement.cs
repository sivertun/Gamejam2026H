using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    private Rigidbody rb;
    [SerializeField] private float moveSpeed;

    [Header("Input Actions")]
    public InputActionReference Move;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        Vector3 m_Input = new Vector3(Move.action.ReadValue<Vector2>().x, 0, Move.action.ReadValue<Vector2>().y);
        rb.MovePosition(transform.position + m_Input * Time.fixedDeltaTime * moveSpeed);
    }
}
