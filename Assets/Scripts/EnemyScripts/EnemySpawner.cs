using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyTarget;

    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float minRange;
    [SerializeField] private float maxRange;

    [Header("Paced by the lantern")]
    [Tooltip("Spawns per second when this enemy type first shows up")]
    [SerializeField] private float spawnRate;
    [Tooltip("Light stage this type starts showing up at. 1 = from the start of the game")]
    [SerializeField] private int startStage = 1;
    [Tooltip("Extra spawns per second for every light stage gained after that")]
    [SerializeField] private float ratePerStage = 0.1f;
    [Tooltip("Spawns per second this type never goes above")]
    [SerializeField] private float maxRate = 1f;
    [Tooltip("Lantern that sets the pace. Found on the player automatically if left empty")]
    [SerializeField] private LanternController lantern;

    private float spawnAccumulator = 0;
    public bool isActive = false;

    // Spawns per second right now
    public float CurrentRate { get; private set; }

    void Start()
    {
        // The spawners live under the player, so the lantern is usually right above them
        if (lantern == null) lantern = GetComponentInParent<LanternController>();
        if (lantern == null) lantern = FindAnyObjectByType<LanternController>();
        if (lantern == null) Debug.LogWarning("EnemySpawner: no lantern found, spawning at the base rate", this);
    }

    void Update()
    {
        CurrentRate = FindSpawnRate();
        spawnAccumulator += CurrentRate * Time.deltaTime;

        while (spawnAccumulator > 1)
        {
            SpawnEnemy(SelectSpawnLocation());
            spawnAccumulator--;
        }
    }

    // The brighter the lantern has grown, the more this type comes for it
    private float FindSpawnRate()
    {
        if (lantern == null) return spawnRate;
        if (lantern.LightStage < startStage) return 0f;

        // Light banked since this type appeared, counted in whole light stages
        float stagesIn = Mathf.Max(0f, lantern.LightProgress - (startStage - 1));
        return Mathf.Min(spawnRate + stagesIn * ratePerStage, maxRate);
    }

    private Vector3 SelectSpawnLocation()
    {
        float angle = Random.Range(0f, 360f);
        float distance = Random.Range(minRange, maxRange);

        Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
        Vector3 offset = rotation * Vector3.forward * distance;

        return transform.position + offset;
    }

    private void SpawnEnemy(Vector3 position)
{
    if (isActive){
        GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
        IHasTarget[] targetScripts = enemy.GetComponents<IHasTarget>();
        foreach (IHasTarget script in targetScripts)
        {
            script.SetTarget(enemyTarget);
        }
    }
}
}
