using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class FluidSimulation : MonoBehaviour
{
    public struct Particle
    {
        public float pressure;
        public float density;
        public Vector3 currentForce;
        public Vector3 velocity;
        public Vector3 position;
        

    }



    public GameObject samplePoint;
    public Vector3Int numToSpawn;
    public float particleRadius;
    public GameObject sphere;
    public float smoothingRadius;
    float PI = 3.141592f;
    
    private Particle[] _particles;
    private GameObject[] _particleObjs;
 
    float SmoothingKernel(float radius, float dst)
    {
        float volume = PI * Mathf.Pow(radius, 8) / 4;
        float value = Mathf.Max(0, radius * radius - dst * dst);

        return value * value * value / volume;
    }

    float CalculateDensity(Vector3 sample)
    {
        float mass = 1;
        float density = 0;
        for (int i= 0; i < _particles.Length; i++)
        {
            float dst = (_particles[i].position -sample).magnitude;
            float influence = SmoothingKernel(smoothingRadius, dst);

            density += mass * influence;
        }

        return density;
    }
    private void SpawnParticlesInBox()
    {
        _particles = new Particle[numToSpawn.x * numToSpawn.y*numToSpawn.z];
        _particleObjs = new GameObject[numToSpawn.x * numToSpawn.y*numToSpawn.z];

        int i = 0;
        for (int x = 0; x < numToSpawn.x; x++)
        {
            for (int y = 0; y < numToSpawn.y; y++)
            {
                for (int z = 0; z < numToSpawn.z; z++)
                {
                    Vector3 spawnPos = this.transform.position + new Vector3(x * particleRadius * 2, y * particleRadius * 2, z * particleRadius * 2);
                    Particle p = new Particle
                    {
                        position = spawnPos
                    };
                    _particleObjs[i] = GameObject.Instantiate(sphere, spawnPos, Quaternion.identity);
                    _particles[i] = p;
                    _particles[i].position = _particleObjs[i].transform.position;
                    i++;
                }
            }
        }
    }
    private void SpawnParticlesInBox2D()
    {
        _particles = new Particle[numToSpawn.x * numToSpawn.z ];
        _particleObjs = new GameObject[numToSpawn.x * numToSpawn.z];
        int i = 0;
        for (int x = 0; x < numToSpawn.x; x++)
        {
            for (int z = 0; z < numToSpawn.z; z++)
            {

                Vector3 spawnPos = new Vector3(0,0) + new Vector3(x * particleRadius * 2,0, z * particleRadius * 2);
                Particle p = new Particle
                {
                    position = spawnPos
                };
                _particleObjs[i]=GameObject.Instantiate(sphere, spawnPos, Quaternion.identity);
                _particles[i] = p;
                _particles[i].position = _particleObjs[i].transform.position;
                i++;

            }
        }


    }

    private void DensityPyramid()
    {
        densities = new float[_particles.Length];

        for (int i = 0; i < _particles.Length; i++)
        {
            densities[i] = CalculateDensity(_particles[i].position);
        }
        for (int i = 0; i < _particles.Length; i++)
        {
            Vector3 p = _particleObjs[i].transform.position;
            _particleObjs[i].transform.position = new Vector3(p.x, densities[i], p.z);
        }
    }
    float[] densities;
    void Start()
    { 
        SpawnParticlesInBox();
        DensityPyramid();
    }


    private void FixedUpdate()
    {
    }
    // Update is called once per frame
    void Update()
    {



        
    }
}
