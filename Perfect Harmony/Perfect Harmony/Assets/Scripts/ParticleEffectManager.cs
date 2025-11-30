using UnityEngine;

public class ParticleEffectManager : MonoBehaviour
{
    [Header("Particle Prefabs")]
    public GameObject perfectParticlePrefab;
    public GameObject goodParticlePrefab;
    public GameObject okayParticlePrefab;

    public static ParticleEffectManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SpawnHitParticles(TimingResult timingResult, Vector3 position)
    {
        GameObject prefabToSpawn = null;
        
        switch(timingResult)
        {
            case TimingResult.Perfect:
                prefabToSpawn = perfectParticlePrefab;
                break;
            case TimingResult.Good:
                prefabToSpawn = goodParticlePrefab;
                break;
            case TimingResult.Okay:
                prefabToSpawn = okayParticlePrefab;
                break;
            case TimingResult.Miss:
                // No particles for miss
                return;
        }
        
        if (prefabToSpawn != null)
        {
            Instantiate(prefabToSpawn, position, Quaternion.identity);
        }
    }
}