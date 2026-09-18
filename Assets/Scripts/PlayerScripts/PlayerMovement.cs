using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody rb;
    [SerializeField] private float moveSpeed;

    [Header("Input Actions")]
    public InputActionReference Move;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void FixedUpdate()
    {
        Vector3 m_Input = new Vector3(Move.action.ReadValue<Vector2>().x, 0, Move.action.ReadValue<Vector2>().y);
        rb.MovePosition(transform.position + m_Input * Time.fixedDeltaTime * moveSpeed);
    }
}
