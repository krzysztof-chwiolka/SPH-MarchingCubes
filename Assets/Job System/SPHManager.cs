using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class SPHManager : MonoBehaviour
{

    // Import
    [Header("Import")]
    [SerializeField] private GameObject sphParticlePrefab = null;
    [SerializeField] private GameObject sphColliderPrefab = null;
    [SerializeField] private GameObject sphSphereColliderPrefab = null;
    private EntityManager manager;

    // Properties
    [Header("Properties")]
    [SerializeField] private int amount = 5000;

    public int amountOfColliders = 0;
    private int amountOfEntities = 0;
    public int amountOfOtherEntites = 0;

    [Header("Add Particles")]
    [SerializeField] private bool shouldAddParticles = false;
    [SerializeField] private int addParticlesAmount = 0;

    [Header("Add Particles Over Time")]
    [SerializeField] private bool shouldAddParticlesOverTime = false;

    [SerializeField] private GameObject whereParticle;
    [SerializeField] private float timeBetweenParticle = 1.0f;
    private float timer = 0;

    [SerializeField] private int amountOfParticlesPerLine = 2;
    [SerializeField] private int amountOfParticleLines = 2;
    [SerializeField] private float distanceInBetween = 1.0f;

    [Header("Change Collider Position")]
    [SerializeField] private bool changeColliderPos = false;
    private GameObject[] colliders;
    private GameObject[] sphereColliders;

    [Header("Add Sphere Collider Particle")]
    [SerializeField] private bool addSphereColliderParticle = false;
    [SerializeField] private GameObject sphereColliderObject;

    private void Start()
    {
        // Imoprt
        //manager = World.Active.GetOrCreateSystem<EntityManager>();
        manager = World.Active.EntityManager;

        // Setup
        AddColliders();
        AddParticles(amount);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (addSphereColliderParticle)
        {
            AddSphereCollider();
        }

        if (shouldAddParticles)
        {
            AddParticles(addParticlesAmount);
            addParticlesAmount = 0;
            shouldAddParticles = false;
        }

        if (shouldAddParticlesOverTime)
        {
            AddParticlesOverTime();
        }

        if (changeColliderPos)
        {
            MoveCollider();
        }

        UpdateColliders();
    }

    private void AddSphereCollider()
    {
        // Find all colliders
        //sphereColliders = GameObject.FindGameObjectsWithTag("SPHSphereCollider");

        //amountOfColliders = colliders.Length + sphereColliders.Length;

        // Turn them into entities
        sphereColliderObject.active = true;
        Movement movementScript = sphereColliderObject.GetComponent<Movement>();
        int amountToSpawn = movementScript.amountPerLineX * movementScript.amountPerLineZ * movementScript.amountPerLineY;

        movementScript.entityNumberArray = new int[amountToSpawn];

        for (int i = 0; i < amountToSpawn; i++)
        {
            NativeArray<Entity> entities = new NativeArray<Entity>(1, Allocator.Persistent);
            manager.Instantiate(sphSphereColliderPrefab, entities);

            // Set data
            float x_pos = whereParticle.transform.position.x;
            float y_pos = whereParticle.transform.position.y;
            float z_pos = whereParticle.transform.position.z;

            Translation particleTranslation = new Translation { Value = new float3(x_pos, y_pos, z_pos) };

            movementScript.entityNumberArray[i] = amountOfEntities;
            manager.SetComponentData(entities[0], particleTranslation);

            amountOfEntities++;
            amountOfOtherEntites++;

            entities.Dispose();
        }

        addSphereColliderParticle = false;
        // Done
    }

    private void AddParticles(int _amount)
    {
        NativeArray<Entity> entities = new NativeArray<Entity>(_amount, Allocator.Temp);
        manager.Instantiate(sphParticlePrefab, entities);

        for (int i = 0; i < _amount; i++)
        {
            manager.SetComponentData(entities[i], new Translation { Value = new float3(i % 16 + UnityEngine.Random.Range(-0.1f, 0.1f), 2 + (i / 16 / 16) * 1f, (i / 16) % 16) + UnityEngine.Random.Range(-0.1f, 0.1f) });
        }

        amountOfEntities += _amount;

        entities.Dispose();
    }

    private void UpdateColliders()
    {
        NativeArray<Entity> entities = new NativeArray<Entity>(50, Allocator.Temp);
        entities = manager.GetAllEntities(Unity.Collections.Allocator.Temp);

        for (int i = 0; i < colliders.Length; i++)
        {
            SPHCollider colliderData = manager.GetComponentData<SPHCollider>(entities[i]);

            colliderData.position = colliders[i].transform.position;
            colliderData.right = colliders[i].transform.right;
            colliderData.up = colliders[i].transform.up;
            colliderData.scale = new float2(colliders[i].transform.localScale.x / 2f, colliders[i].transform.localScale.y / 2f);

            manager.SetComponentData(entities[i], colliderData);
        }

        entities.Dispose();
    }

    private void MoveCollider()
    {
        NativeArray<Entity> entities = new NativeArray<Entity>(50, Allocator.Temp);
        entities = manager.GetAllEntities(Unity.Collections.Allocator.Temp);

        SPHCollider colliderData = manager.GetComponentData<SPHCollider>(entities[2]);

        colliderData.position.x = 10.0f;

        manager.SetComponentData(entities[2], colliderData);

        entities.Dispose();
    }

    private void AddParticlesOverTime()
    {
        if(timer > timeBetweenParticle)
        {
            int overallAmountOfParticles = amountOfParticlesPerLine * amountOfParticleLines;

            NativeArray<Entity> entities = new NativeArray<Entity>(overallAmountOfParticles, Allocator.Temp);
            manager.Instantiate(sphSphereColliderPrefab, entities);

            int particleNumber = 0;
            for (int i = 0; i < amountOfParticleLines; i++)
            {
                for (int j = 0; j < amountOfParticlesPerLine; j++)
                {
                    float x_pos = whereParticle.transform.position.x - ((distanceInBetween * amountOfParticlesPerLine) / 2) + (distanceInBetween * j);
                    float y_pos = whereParticle.transform.position.y + (distanceInBetween * i);
                    float z_pos = whereParticle.transform.position.z;
                    //Translation particleTranslation = new Translation { Value = new float3(j % 16 + UnityEngine.Random.Range(-0.1f, 0.1f), 2 + (j / 16 / 16) * 1f, (j / 16) % 16) + UnityEngine.Random.Range(-0.1f, 0.1f) };
                    Translation particleTranslation = new Translation { Value = new float3(x_pos,y_pos,z_pos) };

                    if(particleNumber < entities.Length)
                    {
                        manager.SetComponentData(entities[particleNumber], particleTranslation);
                    }
                    particleNumber++;
                }
            }

            amountOfEntities += overallAmountOfParticles;

            entities.Dispose();

            timer = 0;
        }
    }



    private void AddColliders()
    {
        // Find all colliders
        colliders = GameObject.FindGameObjectsWithTag("SPHCollider");

        amountOfColliders = colliders.Length;

        // Turn them into entities
        NativeArray<Entity> entities = new NativeArray<Entity>(colliders.Length, Allocator.Temp);
        manager.Instantiate(sphColliderPrefab, entities);

        // Set data
        for (int i = 0; i < colliders.Length; i++)
        {
            manager.SetComponentData(entities[i], new SPHCollider
            {
                position = colliders[i].transform.position,
                right = colliders[i].transform.right,
                up = colliders[i].transform.up,
                scale = new float2(colliders[i].transform.localScale.x / 2f, colliders[i].transform.localScale.y / 2f)
            });
        }

        amountOfEntities += amountOfColliders;

        // Done
        entities.Dispose();
    }
}
