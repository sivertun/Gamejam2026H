using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyTarget;

    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float minRange;
    [SerializeField] private float maxRange;
    [SerializeField] private float spawnRate;
    private float spawnAccumulator = 0;


    void Update()
    {
        spawnAccumulator += spawnRate * Time.deltaTime;

        while (spawnAccumulator > 1)
        {
            SpawnEnemy(SelectSpawnLocation());
            spawnAccumulator--;
        }
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
    GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
    IHasTarget[] targetScripts = enemy.GetComponents<IHasTarget>();
    foreach (IHasTarget script in targetScripts)
    {
        script.SetTarget(enemyTarget);
    }
}
}
