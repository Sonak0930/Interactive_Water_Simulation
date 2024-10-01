using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;



public class SPH : MonoBehaviour
{
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential, Size = 44)]
    public struct Particle
    {
        public float pressure;
        public float density;
        public Vector3 currentForce;
        public Vector3 velocity;
        public Vector3 position;

    }

    [Header("General")]
    public List<GameObject> collisionObjs;
    public int maxCollisionObjects=10;
    public bool showSpheres = true;
    public Vector3Int numToSpawn = new Vector3Int(10, 10, 10);

    //get total num of particles = cubic of the vector3
    public int totalParticles {
        get 
        { 
            return numToSpawn.x*numToSpawn.y*numToSpawn.z; 
        } 
    }

    public Vector3 boxSize = new Vector3(4, 10, 3);
    public Vector3 spawnCenter;
    public float particleRadius = 0.1f;
    //add randomness to the spawn pos
    public float spawnJitter = 0.2f;

    [Header("Particle Rendering")]
    public Mesh particleMesh;
    public float particleRenderSize = 8f;
    public Material material;

    [Header("Compute")]
    public ComputeShader shader;
    public Particle[] particles;

    [Header("Fluid Constants")]
    public float boundDamping = -0.3f;
    public float viscosity = -0.003f;
    public float particleMass = 1f;
    public float gasConstant = 2f;
    public float restingDensity = 1f;
    public float timestep = 0.007f;

    //private variables
    private ComputeBuffer _argsBuffer;
    public ComputeBuffer _particlesBuffer;
    public ComputeBuffer _spherePosBuffer;
    public ComputeBuffer _sphereRadiusBuffer;
    private int integrateKernel;
    private int computeForceKernel;
    private int densityPressureKernel;
    private Vector3[] spherePosList;
    private float[] sphereRadiusList;
    private void Awake()
    {
        //spawn particles
        SpawnParticlesInBox();

        //setup args for instanced particle rendering
        uint[] args = {
            particleMesh.GetIndexCount(0),
            (uint)totalParticles,
            particleMesh.GetIndexStart(0),
            particleMesh.GetBaseVertex(0),
            0
        };

        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(args);

        //set up particle buffer
        _particlesBuffer = new ComputeBuffer(totalParticles, 44);
        _particlesBuffer.SetData(particles);



        spherePosList = new Vector3[collisionObjs.Count];
        sphereRadiusList = new float[collisionObjs.Count];
       
        for (int i = 0; i < collisionObjs.Count; i++)
        {
            spherePosList[i] = new Vector3(
                collisionObjs[i].transform.position.x,
                collisionObjs[i].transform.position.y,
                collisionObjs[i].transform.position.z);


            sphereRadiusList[i] = collisionObjs[i].transform.localScale.x * 0.5f;

        }




        _spherePosBuffer = new ComputeBuffer(maxCollisionObjects, 12);
        _spherePosBuffer.SetData(spherePosList);

        _sphereRadiusBuffer = new ComputeBuffer(maxCollisionObjects, 4);
        _sphereRadiusBuffer.SetData(sphereRadiusList);
        //update compute buffers.
        SetupComputeBuffers();

    }

    //this function should be called when the value of the shader variable changes.
    private void SetupComputeBuffers()
    {
        integrateKernel = shader.FindKernel("Integrate");
        computeForceKernel = shader.FindKernel("ComputeForces");
        densityPressureKernel = shader.FindKernel("ComputeDensityPressure");
        shader.SetInt("particleLength", totalParticles);
        shader.SetFloat("particleMass", particleMass);
        shader.SetFloat("viscosity", viscosity);
        shader.SetFloat("gasConstant",gasConstant);
        shader.SetFloat("restDensity", restingDensity);
        shader.SetFloat("boundDamping",boundDamping);
        shader.SetFloat("pi", Mathf.PI);
        shader.SetVector("boxSize",boxSize);

        shader.SetFloat("radius", particleRadius);
        shader.SetFloat("radius2", particleRadius * particleRadius);
        shader.SetFloat("radius3", particleRadius * particleRadius * particleRadius);
        shader.SetFloat("radius4", particleRadius * particleRadius * particleRadius * particleRadius);
        shader.SetFloat("radius5", particleRadius * particleRadius * particleRadius * particleRadius * particleRadius);

        shader.SetBuffer(integrateKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(computeForceKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(densityPressureKernel, "_particles", _particlesBuffer);

        shader.SetBuffer(computeForceKernel, "_spherePosList", _spherePosBuffer);
        shader.SetBuffer(computeForceKernel, "_sphereRadiusList", _sphereRadiusBuffer);

    }


    private void SpawnParticlesInBox()
    {
        Vector3 spawnPoint = spawnCenter;
        List<Particle> _particles = new List<Particle>();

        for(int x=0; x< numToSpawn.x; x++)
        {
            for(int y=0; y<numToSpawn.y; y++)
            {
                for(int z=0; z< numToSpawn.z; z++)
                {
                    Vector3 spawnPos = spawnPoint + new Vector3(x * particleRadius * 2, y * particleRadius * 2, z * particleRadius * 2);
                    spawnPos += Random.onUnitSphere * particleRadius * spawnJitter;
                    Particle p = new Particle {
                        position = spawnPos 
                    };

                    _particles.Add(p);
                }
            }
        }

        particles = _particles.ToArray();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(Vector3.zero, boxSize);

        if(!Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnCenter, 0.1f);
        }
    }

    private static readonly int SizeProperty = Shader.PropertyToID("_size");
    private static readonly int ParticlesBufferProperty = Shader.PropertyToID("_particlesBuffer");

    // update variables where physical timing is important
    private void FixedUpdate()
    {

        spherePosList = new Vector3[collisionObjs.Count];
        sphereRadiusList = new float[collisionObjs.Count];
        shader.SetVector("boxSize", boxSize);
        shader.SetFloat("timestep", timestep);

        for (int i = 0; i < collisionObjs.Count; i++)
        {
            spherePosList[i] = new Vector3(
                collisionObjs[i].transform.position.x,
                collisionObjs[i].transform.position.y,
                collisionObjs[i].transform.position.z);

            
            sphereRadiusList[i] = collisionObjs[i].transform.localScale.x*0.5f;

        }

        _spherePosBuffer.SetData(spherePosList);
        _sphereRadiusBuffer.SetData(sphereRadiusList);
        shader.SetInt("collisionMax", collisionObjs.Count);
        shader.SetBuffer(computeForceKernel, "_spherePosList", _spherePosBuffer);
        shader.SetBuffer(computeForceKernel, "_sphereRadiusList", _sphereRadiusBuffer);
        //100 threads for 100 particles. and 1 thread for 1 particle.
        // the num of particles % 100 should be 0 !!!!
        //run each kernel independently.

        shader.Dispatch(densityPressureKernel, totalParticles / 100, 1, 1);
        shader.Dispatch(computeForceKernel, totalParticles / 100, 1, 1);
        shader.Dispatch(integrateKernel, totalParticles / 100, 1, 1);      
    }
    void Update()
    {
        //update size and particles buffer for every frame
        //render the particles
        material.SetFloat(SizeProperty, particleRenderSize);
        material.SetBuffer(ParticlesBufferProperty,_particlesBuffer);
        
        if(showSpheres)
        {
            Graphics.DrawMeshInstancedIndirect(
                particleMesh,
                0,
                material,
                new Bounds(Vector3.zero, boxSize),
                _argsBuffer,
                castShadows: UnityEngine.Rendering.ShadowCastingMode.Off
            );
        }
    }
}

