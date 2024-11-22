using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;



public class SPH : MonoBehaviour
{
    private static readonly int SizeProperty = Shader.PropertyToID("_size");
    private static readonly int ParticlesBufferProperty = Shader.PropertyToID("_particlesBuffer");
    private static readonly int DebugColorBufferProperty = Shader.PropertyToID("_debugColorBuffer");
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
    public GameObject playerObject;
    public List<GameObject> collisionObjs;
    public int maxCollisionObjects=10;
    public bool showSpheres = true;
    public Vector3 numToSpawn = new Vector3(16, 16, 16);

    //get total num of particles = cubic of the vector3
    public uint totalParticles {
        get 
        {
            return (uint)(numToSpawn.x*numToSpawn.y*numToSpawn.z); 
        } 
    }

    [Header("BoxSize")]
    [Range(5.0f, 15.0f)] public float x;
    [Range(5.0f, 15.0f)] public float y;
    [Range(5.0f, 15.0f)] public float z;


    [Range(0.01f,5.0f)]public float particleRadius = 0.1f;
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
    public float pressureMultiplier;
    public float timestep = 0.007f;


    [Header("Kernel variables")]
    public float neighborDistance = 1.0f;

    //private variables
    private ComputeBuffer _argsBuffer;
    public ComputeBuffer _particlesBuffer;
    public ComputeBuffer _spherePosBuffer;
    public ComputeBuffer _sphereRadiusBuffer;
    public ComputeBuffer _particleIndices;
    public ComputeBuffer _particleCellIndices;
    public ComputeBuffer _cellOffsets;
    public ComputeBuffer _debugColorBuffer;
    public ComputeBuffer _spatialIndicesBuffer;
    public ComputeBuffer _spatialOffsetsBuffer;
    public ComputeBuffer _densitiesBuffer;

    private int integrateKernel;
    private int computeForceKernel;
    private int densityPressureKernel;
    private int hashParticleKernel;
    private int bitonicSortKernel;
    private int cellOffsetKernel;
    private int updateSpatialHashKernel;

    private Vector3[] spherePosList;
    private float[] sphereRadiusList;

    
    public Vector3 boxSize = new Vector3(15, 15, 15);

    private Vector3 spawnCenter;

    GPUSort gpuSort;
    private void Awake()
    {
        
        //spawn particles
        SpawnParticlesInBox();

        //setup args for instanced particle rendering
        uint[] args = {
            particleMesh.GetIndexCount(0),
            totalParticles,
            particleMesh.GetIndexStart(0),
            particleMesh.GetBaseVertex(0),
            0
        };

        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(int), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(args);

        //set up particle buffer
        _particlesBuffer = new ComputeBuffer((int)totalParticles, 44);
        _particlesBuffer.SetData(particles);

        _particleIndices = new ComputeBuffer((int)totalParticles,4 );
        _particleCellIndices = new ComputeBuffer((int)totalParticles,4 );
        _cellOffsets = new ComputeBuffer((int)totalParticles,4 );

        int[] particleIndices = new int[totalParticles];
        for (int i = 0; i < particleIndices.Length; i++)
        {
            particleIndices[i] = i;
        }
        _particleIndices.SetData(particleIndices);


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

        _debugColorBuffer = new ComputeBuffer((int)totalParticles, 12);

        _spatialOffsetsBuffer = new ComputeBuffer((int)totalParticles, 4);
        _spatialIndicesBuffer = new ComputeBuffer((int)totalParticles,12);

        _densitiesBuffer = new ComputeBuffer((int)totalParticles, 8);
        //update compute buffers.
        SetupComputeBuffers();

    }
    int vertexCount;
    ComputeBuffer colorBuffer;
    //this function should be called when the value of the shader variable changes.
    private void SetupComputeBuffers()
    {
        integrateKernel = shader.FindKernel("Integrate");
        computeForceKernel = shader.FindKernel("ComputeForces");
        densityPressureKernel = shader.FindKernel("ComputeDensityPressure");
        hashParticleKernel = shader.FindKernel("HashParticles");
        bitonicSortKernel = shader.FindKernel("BitonicSort");
        cellOffsetKernel = shader.FindKernel("CalculateCellOffsets");

       
        shader.SetInt("particleLength", (int)totalParticles);
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
       
        shader.SetFloat("playerRadius", playerObject.transform.localScale.x * 0.5f);

        shader.SetFloat("neighborDist", neighborDistance);

        ComputeHelper.SetBuffer(shader, _particlesBuffer, "_particles",
            integrateKernel, computeForceKernel, densityPressureKernel, hashParticleKernel,
            updateSpatialHashKernel);

        ComputeHelper.SetBuffer(shader,_particleIndices,"_particleIndices",
            computeForceKernel,densityPressureKernel,hashParticleKernel,bitonicSortKernel,
            cellOffsetKernel);

        ComputeHelper.SetBuffer(shader, _particleCellIndices, "_particleCellIndices",
            computeForceKernel, densityPressureKernel, hashParticleKernel, bitonicSortKernel,
            cellOffsetKernel);

        ComputeHelper.SetBuffer(shader, _cellOffsets, "_cellOffsets",
            computeForceKernel, densityPressureKernel, hashParticleKernel,cellOffsetKernel);

        

        shader.SetBuffer(computeForceKernel, "_spherePosList", _spherePosBuffer);
        shader.SetBuffer(computeForceKernel, "_sphereRadiusList", _sphereRadiusBuffer);

        ComputeHelper.SetBuffer(shader, _debugColorBuffer, "_debugColorBuffer",
            densityPressureKernel, computeForceKernel,integrateKernel);

        ComputeHelper.SetBuffer(shader, _spatialIndicesBuffer, "_spatialIndices",
            updateSpatialHashKernel,computeForceKernel);
        ComputeHelper.SetBuffer(shader, _spatialOffsetsBuffer, "_spatialOffsets",
            updateSpatialHashKernel,computeForceKernel);

        ComputeHelper.SetBuffer(shader, _densitiesBuffer, "_densities",
            densityPressureKernel, computeForceKernel);

        gpuSort = new();
        gpuSort.SetBuffers(_spatialIndicesBuffer, _spatialOffsetsBuffer);

    }

    // Version 3
    float random(Vector2 p)
    {
        Vector2 K1 = new Vector2(
            (float)23.14069263277926, // e^pi (Gelfond's constant)
             (float)2.665144142690225 // 2^sqrt(2) (Gelfond–Schneider constant)
        );

        float value = Mathf.Cos(Vector2.Dot(p, K1) * (float)12345.6789);
        return (int)value-value;
    }


    private void SpawnParticlesInBox()
    {
        spawnCenter = this.transform.position;
          //  - new Vector3((float)boxSize.x/2,0,(float)boxSize.z/2);
        
        List<Particle> _particles = new List<Particle>();

        float halfX = boxSize.x / 2;
        float halfY = boxSize.y / 2;
        float halfZ = boxSize.z / 2;

        float xUnit = (halfX / numToSpawn.x) * particleRadius;
        float yUnit = (halfY / numToSpawn.y) * particleRadius;
        float zUnit = (halfZ / numToSpawn.z) * particleRadius;


        for (int x = 0; x < numToSpawn.x; x++)
        {
            for (int y = 0; y <numToSpawn.y; y++)
            {
                for (int z = 0; z < numToSpawn.z; z++)
                {
                    Vector3 spawnPos = spawnCenter + new Vector3(xUnit *x, yUnit*y,zUnit*z);

                    Particle p = new Particle
                    {
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
        Gizmos.DrawWireCube(this.transform.position, boxSize);

        if(!Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(this.transform.position, 0.1f);
        }
    }

    
    private void SortParticles()
    {
        for(var dim=2; dim <= totalParticles; dim <<=1)
        {
            shader.SetInt("dim", dim);
            for(var block = dim >> 1; block >0; block >>=1)
            {
                shader.SetInt("block", block);
                shader.Dispatch(bitonicSortKernel, (int)totalParticles / 256, 1, 1);
            }
        }
    }


 
    // update variables where physical timing is important
    private void FixedUpdate()
    {
      
        
        
        spherePosList = new Vector3[collisionObjs.Count];
        sphereRadiusList = new float[collisionObjs.Count];

        
        shader.SetFloat("radius", particleRadius);
        shader.SetFloat("radius2", particleRadius * particleRadius);
        shader.SetFloat("radius3", particleRadius * particleRadius * particleRadius);
        shader.SetFloat("radius4", particleRadius * particleRadius * particleRadius * particleRadius);
        shader.SetFloat("radius5", particleRadius * particleRadius * particleRadius * particleRadius * particleRadius);


        shader.SetVector("boxSize", boxSize);
        shader.SetFloat("timestep", timestep);
        shader.SetFloat("gasConstant", gasConstant);
        shader.SetFloat("restDensity", restingDensity);
        
        shader.SetFloat("particleMass", particleMass);
        shader.SetFloat("mass2",particleMass * particleMass);
        shader.SetFloat("pressureMultiplier", pressureMultiplier);
        shader.SetFloat("viscosity", viscosity);
        shader.SetFloat("boundDamping", boundDamping);
        shader.SetVector("spawnCenter", spawnCenter);
        
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

        Vector3 p0 = playerObject.transform.position
            + new Vector3(0, 1, 0)
            * playerObject.transform.localScale.y;

        Vector3 p1 = playerObject.transform.position
            + new Vector3(0, -1, 0)
            * playerObject.transform.localScale.y;

        shader.SetVector("endPoint1",p0);
        shader.SetVector("endPoint2",p1);
        shader.SetFloat("playerRadius", playerObject.transform.localScale.x * 0.5f);
        
    

        int threadGroupX =(int)totalParticles /256;

        shader.Dispatch(hashParticleKernel, threadGroupX, 1, 1);


        for (var dim = 2; dim <= totalParticles; dim <<= 1)
        {
            shader.SetInt("dim", dim);
            for (var block = dim >> 1; block > 0; block >>= 1)
            {

                shader.SetInt("block", block);
                //shader.Dispatch(bitonicSortKernel, threadGroupX, 1, 1);
            }
        }


        ComputeHelper.Dispatch(shader, _particlesBuffer.count, kernelIndex: updateSpatialHashKernel);

        gpuSort.SortAndCalculateOffsets();
        //shader.Dispatch(cellOffsetKernel, threadGroupX, 1, 1);

        ComputeHelper.Dispatch(shader, _particlesBuffer.count, kernelIndex: densityPressureKernel);
        ComputeHelper.Dispatch(shader, _particlesBuffer.count, kernelIndex: computeForceKernel);
        ComputeHelper.Dispatch(shader, _particlesBuffer.count, kernelIndex: integrateKernel);

        

    }


    void Update()
    {
        //update size and particles buffer for every frame
        //render the particles
        material.SetFloat(SizeProperty, particleRenderSize);
        material.SetBuffer(ParticlesBufferProperty,_particlesBuffer);
        material.SetBuffer(DebugColorBufferProperty, _debugColorBuffer);

        if(showSpheres)
        {
            Graphics.DrawMeshInstancedIndirect(
                particleMesh,
                0,
                material,
                new Bounds(Vector3.zero, boxSize/2),
                _argsBuffer,
                castShadows: UnityEngine.Rendering.ShadowCastingMode.Off
            );
        }
    }
}

