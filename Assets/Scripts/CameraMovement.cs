using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Vector3 offset;
    [SerializeField] private float smoothTime = 1.5f; // increased from 0.25f — slower, more cinematic pull-in
    [SerializeField] private float rotationSpeed = 2f; // higher = faster rotation
    [SerializeField] private float arrivalThreshold = 0.05f;
    [SerializeField] private GameController gameController;

    private Vector3 velocity = Vector3.zero;
    private bool transitioning = false;
    private bool transitionComplete = false;

    public void BeginTransition()
    {
        transitioning = true;
    }


    void FixedUpdate()
    {
        if (!transitioning) return;

        Vector3 targetPosition = cameraTarget.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);

        Quaternion targetRotation = Quaternion.LookRotation(cameraTarget.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

        if (!transitionComplete && Vector3.Distance(transform.position, targetPosition) < arrivalThreshold)
        {
            transitionComplete = true;
            gameController.OnCameraTransitionComplete();
        }
    }
}