using UnityEngine;

// Every sound the player makes, kept in one place. Footsteps and the suck loop run themselves off
// how the player is actually moving, so nothing has to remember to call them; the one-off sounds
// are played by whatever causes them.
public class PlayerAudio : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip swing;
    [SerializeField] private AudioClip hurt;
    [Tooltip("One is picked at random, so dying doesn't always sound the same")]
    [SerializeField] private AudioClip[] die;
    [SerializeField] private AudioClip footstep;
    [Tooltip("Loops for as long as the lamp is pulling")]
    [SerializeField] private AudioClip suckLoop;

    [Header("Levels")]
    [SerializeField, Range(0f, 1f)] private float swingVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float hurtVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float dieVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float suckVolume = 0.5f;

    [Header("Footsteps")]
    [Tooltip("How far you walk between footfalls, in units")]
    [SerializeField] private float stepDistance = 2.2f;
    [Tooltip("How much the pitch wanders, so repeats don't sound identical")]
    [SerializeField, Range(0f, 0.4f)] private float pitchJitter = 0.12f;

    private AudioSource oneShots;
    private AudioSource suckSource;
    private PlayerMovement movement;
    private LampSuck lamp;
    private Vector3 lastPosition;
    private float walkedSinceStep;

    // Test scenes don't have this on the player, so callers can just ask for it
    public static PlayerAudio Ensure(GameObject player)
    {
        if (player == null) return null;
        PlayerAudio existing = player.GetComponent<PlayerAudio>();
        return existing != null ? existing : player.AddComponent<PlayerAudio>();
    }

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        lamp = GetComponent<LampSuck>();
        lastPosition = transform.position;

        // Flat 2D sound: the player is always where you're looking, so it shouldn't fade with the
        // camera's distance, which sits well above and behind
        oneShots = gameObject.AddComponent<AudioSource>();
        oneShots.playOnAwake = false;
        oneShots.spatialBlend = 0f;

        suckSource = gameObject.AddComponent<AudioSource>();
        suckSource.playOnAwake = false;
        suckSource.loop = true;
        suckSource.spatialBlend = 0f;
        suckSource.clip = suckLoop;
        suckSource.volume = 0f;
    }

    void Update()
    {
        Footsteps();
        SuckLoop();
    }

    // Paced by ground covered rather than by a timer, so it stays in step whatever your speed is
    private void Footsteps()
    {
        Vector3 step = transform.position - lastPosition;
        lastPosition = transform.position;
        step.y = 0f;

        // A roll isn't footsteps, and it would fire several at once at that speed
        if (movement != null && movement.IsDodging) return;
        if (footstep == null || DeathSequence.IsDead) return;

        walkedSinceStep += step.magnitude;
        if (walkedSinceStep < stepDistance) return;

        walkedSinceStep = 0f;
        PlayOneShot(footstep, footstepVolume);
    }

    private void SuckLoop()
    {
        if (suckSource.clip == null) return;

        bool pulling = lamp != null && (lamp.IsSucking || lamp.IsBursting) && !UpgradeChooser.IsChoosing;
        if (pulling && !suckSource.isPlaying) suckSource.Play();

        // Fade rather than cut, or letting go of the suck clicks
        suckSource.volume = Mathf.MoveTowards(suckSource.volume, pulling ? suckVolume : 0f, Time.deltaTime * 4f);
        if (!pulling && suckSource.volume <= 0f && suckSource.isPlaying) suckSource.Stop();
    }

    public void PlaySwing() => PlayOneShot(swing, swingVolume);

    public void PlayHurt() => PlayOneShot(hurt, hurtVolume);

    public void PlayDie()
    {
        if (die == null || die.Length == 0) return;
        PlayOneShot(die[Random.Range(0, die.Length)], dieVolume);
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null || oneShots == null) return;
        oneShots.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        oneShots.PlayOneShot(clip, volume);
    }
}
