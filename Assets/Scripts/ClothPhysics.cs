using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class ClothPhysics : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ParticleData
    {
        public float mass;
        public Vector3 position;
        public Vector3 deltaPosition;
        public Vector3 prevPosition;
        public Vector3 velocity;
        public int constraintCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SpringData
    {
        public int p1;
        public int p2;
        public float restLength;
        public float lambda;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Sphere
    {
        public Vector3 position;
        public float radius;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct Capsule
    {
        public Vector3 start;
        public Vector3 end;
        public float radius;
    }
    private List<Capsule> capsuleList;

    List<ClothParticle> particles;
    Vector3[] vertices;
    Vector3[] verticesOut;
    List<Spring> springs;
    Mesh mesh;

    public bool squareGrid = true;
    [Range(0.0f, 10f)]
    public float mass = 0.1f;
    [Range(0.0f, 1f)]
    public float compliance = 1f;
    [Range(1, 100)]
    public int iterations = 10;

    public ComputeShader shader;
    private ComputeBuffer particleBuffer;
    private ComputeBuffer springBuffer;
    private ComputeBuffer capsuleBuffer;
    private ParticleData[] particleArray;
    private SpringData[] springArray;
    int kernelXPBD0, kernelXPBD1, kernelXPBD2;
    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        vertices = new Vector3[mesh.vertexCount];
        verticesOut = new Vector3[mesh.vertexCount];
        particles = new List<ClothParticle>(mesh.vertexCount);
        transform.TransformPoints(mesh.vertices, vertices);
        // Create Particles
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            particles.Add(new ClothParticle(vertices[i], i));
        }

        int[] tris = mesh.triangles;
        Dictionary<string, Spring> Neighbours4 = new Dictionary<string, Spring>();

        // Find neighboring verticies 
        for (int i = 0; i < tris.Length; i += 3)
        {
            int v1 = tris[i];
            int v2 = tris[i + 1];
            int v3 = tris[i + 2];

            if (squareGrid)
            {
                Vector3 p1 = vertices[v1];
                Vector3 p2 = vertices[v2];
                Vector3 p3 = vertices[v3];

                float length1 = (p1 - p2).magnitude;
                float length2 = (p2 - p3).magnitude;
                float length3 = (p3 - p1).magnitude;

                if (length1 > length2 && length1 > length3)
                {
                    var temp = tris[i + 1];
                    tris[i + 1] = tris[i + 2];
                    tris[i + 2] = temp;

                    var temp2 = p2;
                    p2 = p3;
                    p3 = temp2;
                }
                else if (length2 > length1 && length2 > length3)
                {
                    var temp = tris[i + 1];
                    tris[i + 1] = tris[i];
                    tris[i] = temp;

                    var temp2 = p2;
                    p2 = p1;
                    p1 = temp2;
                }
            }

            v1 = tris[i];
            v2 = tris[i + 1];
            v3 = tris[i + 2];
            if (tris[i] > tris[i + 1])
            {
                var temp = v2;
                v2 = v1;
                v1 = temp;
            }
            Neighbours4.TryAdd(v1.ToString() + " " + v2.ToString(), new Spring(particles[v1], particles[v2]));

            v1 = tris[i];
            v2 = tris[i + 1];
            v3 = tris[i + 2];
            if (tris[i + 1] > tris[i + 2])
            {
                var temp = v2;
                v2 = v3;
                v3 = temp;
            }
            Neighbours4.TryAdd(v2.ToString() + " " + v3.ToString(), new Spring(particles[v2], particles[v3]));

            if (!squareGrid)
            {
                v1 = tris[i];
                v2 = tris[i + 1];
                v3 = tris[i + 2];

                if (tris[i] > tris[i + 2])
                {
                    var temp = v1;
                    v1 = v3;
                    v3 = temp;
                }
                Neighbours4.TryAdd(v1.ToString() + " " + v3.ToString(), new Spring(particles[v1], particles[v3]));
            }
        }

        // Create particle adjacency list
        List<List<int>> particleAdjacency = new List<List<int>>();
        for (int i = 0; i < particles.Count; i++)
        {
            particleAdjacency.Add(new List<int>());
        }
        foreach (var s in Neighbours4)
        {
            if(particleAdjacency[s.Value.p1.vertexIdx] == null)
            {
                particleAdjacency[s.Value.p1.vertexIdx] = new List<int>();
            }
            if(!particleAdjacency[s.Value.p1.vertexIdx].Contains(s.Value.p2.vertexIdx))
            {
                particleAdjacency[s.Value.p1.vertexIdx].Add(s.Value.p2.vertexIdx);
                particleAdjacency[s.Value.p2.vertexIdx].Add(s.Value.p1.vertexIdx);
            }
        }
        // Find and add 9-neighboring and 12-neighboring verticies
        for (int i = 0; i < particleAdjacency.Count; i++)
        {
            for (int j = 0; j < particleAdjacency[i].Count; j++)
            {
                int l, h;
                l = Math.Min(i, particleAdjacency[i][j]);
                h = Math.Max(i, particleAdjacency[i][j]);
                Neighbours4.TryAdd(l.ToString() + " " + h.ToString(), new Spring(particles[l], particles[h]));
                for (int k = 0; k < particleAdjacency[particleAdjacency[i][j]].Count; k++)
                {
                    l = Math.Min(i, particleAdjacency[particleAdjacency[i][j]][k]);
                    h = Math.Max(i, particleAdjacency[particleAdjacency[i][j]][k]);
                    if(l != h)
                        Neighbours4.TryAdd(l.ToString() + " " + h.ToString(), new Spring(particles[l], particles[h]));
                }
            }
        }

        // Create springs from neighbours list
        springs = new List<Spring>();
        foreach (var s in Neighbours4)
        {
            springs.Add(s.Value);
        }

        // Create array with particle data, which will be transfered to GPU
        particleArray = new ParticleData[mesh.vertexCount];
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            particleArray[i] = new ParticleData();
            particleArray[i].position = particles[i].position;
            particleArray[i].prevPosition = particles[i].position;
            particleArray[i].deltaPosition = Vector3.zero;
            particleArray[i].constraintCount = 1;
            particleArray[i].velocity = Vector3.zero;
            float Mass = 0;
            foreach(var p in particleAdjacency[i])
            {
                Mass += (particles[i].position - particles[p].position).magnitude / particleAdjacency[i].Count;
            }
            // We set a bigger mass to particles with smaller adjacent edges, which are
            // actually particles at the center of the mesh (around the waist)
            particleArray[i].mass = 1.0f / (Mass) * mass;
        }

        // Create array with spring data, which will be transfered to GPU
        springArray = new SpringData[springs.Count];
        for (int i = 0; i < springs.Count; i++)
        {
            springArray[i] = new SpringData();
            springArray[i].p1 = springs[i].p1.vertexIdx;
            springArray[i].p2 = springs[i].p2.vertexIdx;
            springArray[i].restLength = springs[i].restLength;
        }

        // Create compute shader kernels
        kernelXPBD0 = shader.FindKernel("preSolve");
        kernelXPBD1 = shader.FindKernel("solve");
        kernelXPBD2 = shader.FindKernel("postSolve");
        // Create and fill compute buffers
        int stride = Marshal.SizeOf(typeof(ParticleData));
        particleBuffer = new ComputeBuffer(particleArray.Length, stride);
        stride = Marshal.SizeOf(typeof(SpringData));
        springBuffer = new ComputeBuffer(springArray.Length, stride);
        particleBuffer.SetData(particleArray);
        springBuffer.SetData(springArray);
        // Extract all capsule colliders from the scene
        CapsuleUpdate();
        // Create and fill buffer with capsule colliders
        capsuleBuffer = new ComputeBuffer(capsuleList.Count, Marshal.SizeOf(typeof(Capsule)));
        capsuleBuffer.SetData(capsuleList.ToArray());
        // CPU to GPU data transfer 
        shader.SetBuffer(kernelXPBD0, "particles", particleBuffer);
        shader.SetBuffer(kernelXPBD1, "particles", particleBuffer);
        shader.SetBuffer(kernelXPBD2, "particles", particleBuffer);
        shader.SetBuffer(kernelXPBD0, "springs", springBuffer);
        shader.SetBuffer(kernelXPBD1, "springs", springBuffer);
        shader.SetBuffer(kernelXPBD2, "springs", springBuffer);
        shader.SetBuffer(kernelXPBD0, "capsules", capsuleBuffer);
        shader.SetBuffer(kernelXPBD1, "capsules", capsuleBuffer);
        shader.SetBuffer(kernelXPBD2, "capsules", capsuleBuffer);
        shader.SetInt("springs_n", springArray.Length);
        shader.SetInt("particles_n", particleArray.Length);
        shader.SetInt("capsules_n", capsuleList.Count);
    }

    // Extract all capsule colliders from the scene
    // (simple capsules and objects with BoneCollider class are considered)
    void CapsuleUpdate()
    {
        CapsuleCollider[] Capsules = FindObjectsByType(typeof(CapsuleCollider), FindObjectsSortMode.None) as CapsuleCollider[];
        BoneCollider[] Avatars = FindObjectsByType(typeof(BoneCollider), FindObjectsSortMode.None) as BoneCollider[];
        capsuleList = new List<Capsule>();
        foreach (var av in Avatars)
            foreach (var capsule in av.humanBodyBones)
                capsuleList.Add(capsule.Value);
        for (int i = 0; i < Capsules.Length; i++)
        {
            CapsuleCollider capsule = Capsules[i];
            Capsule newCapsule = new Capsule();
            Vector3 localP1 = capsule.center + (Vector3.up * (capsule.height / 2f - capsule.radius));
            Vector3 localP2 = capsule.center - (Vector3.up * (capsule.height / 2f - capsule.radius));
            Vector3 globalP1 = capsule.transform.TransformPoint(localP1);
            Vector3 globalP2 = capsule.transform.TransformPoint(localP2);
            newCapsule.start = globalP1;
            newCapsule.end = globalP2;
            newCapsule.radius = capsule.radius * capsule.transform.lossyScale.x;
            capsuleList.Add(newCapsule);
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (Time.time < 1)
            return;
        // Extract all capsule colliders from the scene
        CapsuleUpdate();
        // CPU to GPU data transfer 
        capsuleBuffer.SetData(capsuleList.ToArray());
        shader.SetBuffer(kernelXPBD0, "capsules", capsuleBuffer);
        shader.SetBuffer(kernelXPBD1, "capsules", capsuleBuffer);
        shader.SetBuffer(kernelXPBD2, "capsules", capsuleBuffer);
        shader.SetFloat("compliance", compliance);
        shader.SetFloat("mass", mass);
        shader.SetFloat("iterations", iterations);

        // Run kernels
        shader.Dispatch(kernelXPBD0, (springArray.Length + 63) / 64, 1, 1);
        shader.SetFloat("deltaTime", Time.fixedDeltaTime / iterations);
        for (int i = 0; i < iterations; i++)
        {
            shader.Dispatch(kernelXPBD1, (springArray.Length + 63) / 64, 1, 1);
            shader.Dispatch(kernelXPBD2, (particleArray.Length + 63) / 64, 1, 1);
        }
        // GPU to CPU data transfer 
        particleBuffer.GetData(particleArray);
    }

    private void LateUpdate()
    {
        // Update mesh
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            vertices[i] = particleArray[i].position;
        }
        transform.InverseTransformPoints(vertices, verticesOut);
        mesh.SetVertices(verticesOut);
        mesh.RecalculateNormals();
    }

    void OnDestroy()
    {
        if (particleBuffer != null) particleBuffer.Release();
        if (springBuffer != null) springBuffer.Release();        
        if (capsuleBuffer != null) capsuleBuffer.Release();
    }

}
