using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class Movement : MonoBehaviour
{
    public int entityNumber = 0;
    public int[] entityNumberArray = new int[36];

    public int amountPerLineX = 3;
    public int amountPerLineY = 1;
    public int amountPerLineZ = 3;
    public float howMuchLowerOverall = 1.0f;
    public float speed = 2.0f;
    public float maxSpeed = 15.0f;

    private EntityManager manager;
    // Start is called before the first frame update
    void Start()
    {
        manager = World.Active.EntityManager;
        //entityNumberArray = new int[amountPerLineX * amountPerLineY * amountPerLineZ];
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 inputVector = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        if(maxSpeed > this.GetComponent<Rigidbody>().velocity.magnitude)
        {
            this.GetComponent<Rigidbody>().AddForce(inputVector * speed, ForceMode.Force);// * Time.deltaTime;
        }

        Vector3 velocitySlowed = this.GetComponent<Rigidbody>().velocity;
        velocitySlowed.x = velocitySlowed.x * 0.95f * Time.deltaTime;
        velocitySlowed.z = velocitySlowed.z * 0.95f * Time.deltaTime;

        this.GetComponent<Rigidbody>().velocity = velocitySlowed;

        NativeArray<Entity> entities = new NativeArray<Entity>(50, Allocator.Temp);
        entities = manager.GetAllEntities(Unity.Collections.Allocator.Temp);

        //manager.GetComponentData<Transform>();

        //position.y = this.transform.position.y - (1.0f * ((int)(amountPerLineY/2)))
        int count = 0;

        //manager.Instantiate(sphParticlePrefab, entities);

        float positionY = this.transform.position.y - howMuchLowerOverall - (1.0f * (((float)amountPerLineY / 2.0f)));
        for (int i = 0; i < amountPerLineY; i++)
        {
            float positionZ = this.transform.position.z - (1.0f * (((float)amountPerLineZ / 2.0f)));
            //position.z = this.transform.position.z - (1.0f * ((int)(amountPerLineZ/2)))
            for (int j = 0; j < amountPerLineZ; j++)
            {
                float positionX = this.transform.position.x - (1.0f * (((float)amountPerLineX / 2.0f)));
                //position.x = this.transform.position.x - (1.0f * ((int)(amountPerLineX/2)))
                for (int k = 0; k < amountPerLineX; k++)
                {
                    positionX += 1.0f;
                    Vector3 newPosition = new Vector3(positionX, positionY, positionZ);
                    Vector3 position = newPosition + ((-this.transform.up * this.transform.localScale.y));

                    Translation translationEntity = new Translation();

                    translationEntity.Value.x = position.x;
                    translationEntity.Value.y = position.y;
                    translationEntity.Value.z = position.z;

                    LocalToWorld transformOfEntity = new LocalToWorld();

                    transformOfEntity.Value.c3.x = position.x;
                    transformOfEntity.Value.c3.y = position.y;
                    transformOfEntity.Value.c3.z = position.z;
                    transformOfEntity.Value.c3.w = 1;

                    manager.SetComponentData(entities[entityNumberArray[count]], transformOfEntity);
                    manager.SetComponentData<Translation>(entities[entityNumberArray[count]], translationEntity);

                    SPHVelocity velocityData = manager.GetComponentData<SPHVelocity>(entities[entityNumberArray[count]]);
                    velocityData.Value.x = 0.0f; velocityData.Value.y = 0.0f; velocityData.Value.z = 0.0f;
                    manager.SetComponentData(entities[entityNumberArray[count]], velocityData);

                    count++;
                }
                positionZ += 1.0f;
            }
            positionY += 1.0f;
        }

        entities.Dispose();


    }
}
