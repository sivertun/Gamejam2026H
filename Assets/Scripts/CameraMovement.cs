using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Vector3 offset;
    [SerializeField] private float smoothTime = 1.5f; // increased from 0.25f — slower, more cinematic pull-in
    [SerializeField] private float rotationSpeed = 2f; // higher = faster rotation
    [SerializeField] private float arrivalThreshold = 0.05f;
    [SerializeField] private GameController gameController;

    [Tooltip("How tightly the camera follows the player once the menu transition is done")]
    [SerializeField] private float followSmoothTime = 0.25f;

    private Vector3 velocity = Vector3.zero;
    private bool transitioning = false;
    private bool transitionComplete = false;

    void Start()
    {
        // Scenes without a main menu (test scenes) skip the transition and follow right away
        if (gameController == null)
        {
            transitioning = true;
            transitionComplete = true;
        }
    }

    public void BeginTransition()
    {
        transitioning = true;
    }


    void FixedUpdate()
    {
        if (!transitioning || cameraTarget == null) return;

        Vector3 targetPosition = cameraTarget.position + offset;

        if (transitionComplete)
        {
            // Normal gameplay follow
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, followSmoothTime);
            transform.LookAt(cameraTarget);
            return;
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);

        Quaternion targetRotation = Quaternion.LookRotation(cameraTarget.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < arrivalThreshold)
        {
            transitionComplete = true;
            gameController.OnCameraTransitionComplete();
        }
    }
}
