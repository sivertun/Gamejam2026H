using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class GameController : MonoBehaviour
{
    [SerializeField] private CameraMovement cameraMovement;
    [SerializeField] private GameObject menuUI;
    [SerializeField] private PlayerMovement player;

    [Header("Menu UI slide-up")]
    [SerializeField] private float uiSlideDuration = 3f;
    [SerializeField] private float uiSlideDistance = 10f; // how far up the sprite moves, in world units
    [SerializeField] private EnemySpawner basicSpawn;
    [SerializeField] private EnemySpawner runSpawn;
    [SerializeField] private EnemySpawner forSpawn;

    bool started = false;

    void Start()
    {
        player.enabled = false;
        menuUI.SetActive(true);
    }

    void Update()
    {
        if (!started && AnyInputPressed())
        {
            started = true;
            StartCoroutine(SlideMenuUpThenStart());
            cameraMovement.BeginTransition();
        }
    }

    IEnumerator SlideMenuUpThenStart()
    {
        Vector3 startPos = menuUI.transform.position;
        Vector3 endPos = startPos + Vector3.up * uiSlideDistance;
        Debug.Log("Slide starting");
        float elapsed = 0f;
        while (elapsed < uiSlideDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / uiSlideDuration;
            menuUI.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        Debug.Log("Slide stop");
        menuUI.transform.position = endPos;
        menuUI.SetActive(false);
        basicSpawn.isActive = true;
        forSpawn.isActive = true;
        if(runSpawn)runSpawn.isActive=true;
    }

    // Called by CameraMovement once it finishes easing into the player
    public void OnCameraTransitionComplete()
    {
        player.enabled = true;
    }

    bool AnyInputPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
            return true;

        return false;
    }

    public void dead(){
        if(basicSpawn)basicSpawn.isActive = false;
        if(forSpawn)forSpawn.isActive = false;
        if(runSpawn)runSpawn.isActive=false;
    }
}