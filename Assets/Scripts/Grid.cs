using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;
using Unity.Jobs;
using System.Threading;

struct GPUEdgeValues {
    public float edge0Val, edge1Val, edge2Val, edge3Val, edge4Val, edge5Val, edge6Val, edge7Val;
}

struct GPUPositions {
    public Vector3 centerPos;
    public Vector3 edge0Pos, edge1Pos, edge2Pos, edge3Pos, edge4Pos, edge5Pos, edge6Pos, edge7Pos;
}

struct GPUBall {
    public float factor;
    public Vector3 position;
}

struct GPUEdgeVertices {
    public int index;
    public Vector3 edge0, edge1, edge2, edge3, edge4, edge5, edge6, edge7, edge8, edge9, edge10, edge11;
};

public class Grid {
    public List<Vector3> vertices;
    public List<Vector3> vertices2;
    public int width, height, depth;

    private Chunk container;

    private ComputeShader shader;
    private int shaderKernel;

    private ComputeBuffer positionsBuffer;
    private ComputeBuffer valuesBuffer;
    private ComputeBuffer metaballsBuffer;
    private ComputeBuffer edgeMapBuffer;
    private ComputeBuffer verticesBuffer;
    private ComputeBuffer verticesRecalculatedBuffer;
    private ComputeBuffer TriangleConnectionTableBuffer;


    private GPUPositions[] precomputedPositions;

    private bool initized;

    public SPHManager sphManager;
    public GameObject sphParticlePrefab = null;

    private GPUEdgeVertices[] output;// = new GPUEdgeVertices[verticesBuffer.count];


    //
    // Constructor
    //

    public Grid(Chunk container, ComputeShader shader) {
        this.container = container;
        this.shader = shader;

        this.width = Mathf.RoundToInt(container.transform.localScale.x / this.container.resolution);
        this.height = Mathf.RoundToInt(container.transform.localScale.y / this.container.resolution);
        this.depth = Mathf.RoundToInt(container.transform.localScale.z / this.container.resolution);

        this.vertices = new List<Vector3>();
        this.vertices2 = new List<Vector3>();

        this.initized = false;
    }

    //
    // Public methods
    //

    public NativeArray<Entity> entities;// = new NativeArray<Entity>(50, Allocator.Temp);
    public int entitiesRetrieved = 0;

    int countO = 0;

    public void evaluateAll()//MetaBall[] metaballs) 
    {
        if(!this.initized) {
            this.init();
        }

        this.vertices.Clear();
        this.vertices2.Clear();

        EntityManager manager = World.Active.EntityManager;

        //NativeArray<Entity> entities = new NativeArray<Entity>(50, Allocator.Temp);
        if(entitiesRetrieved > -1)
        {
            entities = manager.GetAllEntities(Unity.Collections.Allocator.Temp);
            entitiesRetrieved++;
        }

        // write info about metaballs in format readable by compute shaders
        GPUBall[] gpuBalls = new GPUBall[entities.Length - sphManager.amountOfColliders];

        for (int i = 0; i < entities.Length - sphManager.amountOfColliders - sphManager.amountOfOtherEntites; i++) {
            
            //Debug.Log(manager.GetComponentData<LocalToWorld>(entities[i]).Value.c3);

            float4 transformOfEntity = manager.GetComponentData<LocalToWorld>(entities[i + sphManager.amountOfColliders]).Value.c3;

            Vector3 worldPos = new Vector3(transformOfEntity.x,
                               transformOfEntity.y,
                               transformOfEntity.z);


            //bool negativeBall = false;
            //float factor = (negativeBall ? -1 : 1) * 0.5f * 0.5f;// uniqueTypes[i].radius * uniqueTypes[i].radius;
            float factor = 0.2f * 0.5f;// uniqueTypes[i].radius * uniqueTypes[i].radius;

            if (!float.IsNaN(worldPos.x) && !float.IsNaN(worldPos.y) && !float.IsNaN(worldPos.z))
            {
                container.transformObj.transform.position = worldPos;
            }


            gpuBalls[i].position = container.transformObj.transform.localPosition;// worldPos;
            gpuBalls[i].factor = factor;
        }
        
        // magic happens here
        GPUEdgeVertices[] edgeVertices = this.runComputeShader(gpuBalls);

        //Add verticies
        //this.vertices = edgeVerticesRecalculated;


        //int updateCount = 0;
        // perform rest of the marching cubes algorithm

        //Job number of runs = width*height*depth
        /*
        int numberOfRuns = width * height * depth;

        var vertsToAdd = new NativeArray<Vector3>(numberOfRuns*3, Allocator.Persistent);
        var vertsToAddList = new NativeList<Vector3>(0, Allocator.Persistent);
        var triTableToSet = new NativeArray<int>(triTable.Length * triTable[0].Length, Allocator.Persistent);

        var edgeVerts = new NativeArray<GPUEdgeVertices>(edgeVertices.Length, Allocator.Persistent);
        var offsetInt = new NativeArray<int>(2, Allocator.Persistent);
        var offsetInts = new NativeArray<int>(2, Allocator.Persistent);
        var width_counter = new NativeArray<int>(2, Allocator.Persistent);
        var height_counter = new NativeArray<int>(2, Allocator.Persistent);
        var depth_counter = new NativeArray<int>(2, Allocator.Persistent);


        int size = triTable.Length * triTable[0].Length;
        int[] result = new int[size];

        // copy 2D array elements into a 1D array.
        int write = 0;
        for (int i = 0; i < triTable.Length; i++)
        {
            for (int z = 0; z < triTable[0].Length; z++)
            {
                result[write++] = triTable[i][z];
            }
        }

        edgeVerts.CopyFrom(edgeVertices);

        //Debug.Log(triTable.Length * triTable[0].Length);
        //int[] arrayToTest = new int[triTable.Length];

        triTableToSet.CopyFrom(result);

        // Initialize the job data
        ComputeAllVerticies job = new ComputeAllVerticies()
        {
            verts = edgeVerts,
            triTable = triTableToSet,
            offset = offsetInt,
            verticiesToAdd = vertsToAdd,
            //vertsToAddList,
            width = width,
            height = height,
            depth = depth,
            width_counter = width_counter,
            height_counter = height_counter,
            depth_counter = depth_counter,
            sizeOf2D = triTable[0].Length,
            indexSet = offsetInts
        };

        JobHandle jobHandle = job.Schedule(numberOfRuns, 64);
        jobHandle.Complete();

        Debug.Log(job.indexSet[0]);

        Debug.Log(job.verticiesToAdd[0]);


        //Debug.Log(job.verticiesToAdd[countO]);
        countO++;

        offsetInt.Dispose();
        offsetInts.Dispose();
        width_counter.Dispose();
        height_counter.Dispose();
        depth_counter.Dispose();


        //Vector3[] verticiesToAdd = new Vector3[4096 * 5];

        //job.verticiesToAdd;//.CopyTo(vertices.ToArray());

        //vertices.AddRange(job.verticiesToAdd);

        //vertices = verticiesToAdd;
        // Native arrays must be disposed manually.
        */

        //List<Thread> threads = new List<Thread>();

        //Thread[] threads = new Thread[width * height * depth];


        for (int x = 0; x < this.width; x++) 
        {
            for (int y = 0; y < this.height; y++) 
            {
                for (int z = 0; z < this.depth; z++)
                {
                    this.updateVertices2(edgeVertices[x + this.width * (y + this.height * z)]);
                }
            }
        }
        //Debug.Log(updateCount);
        entities.Dispose();

        //triTableToSet.Dispose();
        //edgeVerts.Dispose();
        //vertsToAdd.Dispose();
    }

    public int[] getTriangles() {
        int num = this.vertices.Count;

        if(this.triangleBuffer == null) {
            // nothing in buffer, create it
            this.triangleBuffer = new List<int>();
            for(int i = 0; i < num; i++) {
                this.triangleBuffer.Add(i);
            }

            return this.triangleBuffer.ToArray();
        } else if(this.triangleBuffer.Count < num) {
            // missing elements in buffer, add them
            for(int i = this.triangleBuffer.Count; i < num; i++) {
                this.triangleBuffer.Add(i);
            }

            return this.triangleBuffer.ToArray();
        } else if(this.triangleBuffer.Count == num) {
            // buffer is of perfect size, just return it

            return this.triangleBuffer.ToArray();
        } else {
            // buffer is too long, return slice

            return this.triangleBuffer.GetRange(0, num).ToArray();
        }
    }

    public void destroy() {
        this.positionsBuffer.Release();
        this.valuesBuffer.Release();
        this.metaballsBuffer.Release();
        this.edgeMapBuffer.Release();
        this.verticesBuffer.Release();
        this.verticesRecalculatedBuffer.Release();
        this.TriangleConnectionTableBuffer.Release();
        this.triangleBuffer = null;
    }

    //
    // Setup
    //

    private void init() {
        this.instantiateEdgeMap();
        this.instantiatePositionMap();
        this.instantiateGPUPositions();
        this.instantiateComputeShader();
        output = new GPUEdgeVertices[verticesBuffer.count];

        this.initized = true;
    }

    private void instantiateEdgeMap() {
        this.edgeMap = new Vector3[] {
            new Vector3(-1, -1, -1),
            new Vector3(1, -1, -1),
            new Vector3(1, 1, -1),
            new Vector3(-1, 1, -1),
            new Vector3(-1, -1, 1),
            new Vector3(1, -1, 1),
            new Vector3(1, 1, 1),
            new Vector3(-1, 1, 1)
        };

        // scale edge map
        for(int i = 0; i < 8; i++) {
            this.edgeMap[i] /= 2;
            this.edgeMap[i] = new Vector3(this.edgeMap[i].x / ((float) this.width),
                this.edgeMap[i].y / ((float) this.height),
                this.edgeMap[i].z / ((float) this.depth));
        }
    }

    private void instantiatePositionMap() {
        this.positionMap = new Vector3[this.width, this.height, this.depth];

        for(int x = 0; x < this.width; x++) {
            for(int y = 0; y < this.height; y++) {
                for(int z = 0; z < this.depth; z++) {

                    float xCoord = (((float) x) / ((float) this.width)) - 0.5f;
                    float yCoord = (((float) y) / ((float) this.height)) - 0.5f;
                    float zCoord = (((float) z) / ((float) this.depth)) - 0.5f;

                    this.positionMap[x, y, z] = new Vector3(xCoord, yCoord, zCoord);
                }
            }
        }
    }

    private void instantiateGPUPositions() {
        this.precomputedPositions = new GPUPositions[this.width*this.height*this.depth];

        for(int x = 0; x < this.width; x++) {
            for(int y = 0; y < this.height; y++) {
                for(int z = 0; z < this.depth; z++) {
                    Vector3 centerPoint = this.positionMap[x, y, z];
                    this.precomputedPositions[x + this.width * (y + this.height * z)].centerPos = centerPoint;

                    this.precomputedPositions[x + this.width * (y + this.height * z)].edge0Pos = centerPoint + this.edgeMap[0];
                    this.precomputedPositions[x + this.width * (y + this.height * z)].edge1Pos = centerPoint + this.edgeMap[1];
                    this.precomputedPositions[x + this.width * (y + this.height * z)].edge2Pos = centerPoint + this.edgeMap[2];
                    this.precomputedPositions[x + this.width * (y + this.height * z)].edge3Pos = centerPoint + this.edgeMap[3];
                    this.precomputedPositions[x + this.width * (y + this.height * z)].edge4Pos = centerPoint + this.edgeMap[4];
                    this.precomputedPositions[x + this.width * (y + this.height * z)].edge5Pos = centerPoint + this.edgeMap[5];
                    this.precomputedPositions[x + this.width * (y + this.height * z)].edge6Pos = centerPoint + this.edgeMap[6];
                    this.precomputedPositions[x + this.width * (y + this.height * z)].edge7Pos = centerPoint + this.edgeMap[7];
                }
            }
        }
    }

    private void instantiateComputeShader() {
        // setup buffers
        this.positionsBuffer = new ComputeBuffer(this.precomputedPositions.Length, 108); //this
        this.positionsBuffer.SetData(this.precomputedPositions);

        this.edgeMapBuffer = new ComputeBuffer(8, 12); //this
        this.edgeMapBuffer.SetData(this.edgeMap);

        
        this.verticesBuffer = new ComputeBuffer(this.precomputedPositions.Length, 148); //this
        this.verticesRecalculatedBuffer = new ComputeBuffer(this.precomputedPositions.Length*3, 148*3); //this
        this.TriangleConnectionTableBuffer = new ComputeBuffer(256 * 16, sizeof(int)); //this
        this.metaballsBuffer = new ComputeBuffer(this.precomputedPositions.Length, 16); //this

        this.TriangleConnectionTableBuffer.SetData(TriangleConnectionTable);


        // and assign them to compute shader buffer
        this.shaderKernel = this.shader.FindKernel("Calculate");

        this.shader.SetBuffer(this.shaderKernel, "positions", this.positionsBuffer);
        this.shader.SetBuffer(this.shaderKernel, "metaballs", this.metaballsBuffer);
        this.shader.SetBuffer(this.shaderKernel, "edgeMap", this.edgeMapBuffer);
        this.shader.SetBuffer(this.shaderKernel, "edgeVertices", this.verticesBuffer);
        this.shader.SetBuffer(this.shaderKernel, "edgeVerticesRecalculated", this.verticesRecalculatedBuffer);
        this.shader.SetBuffer(this.shaderKernel, "TriangleConnectionTable", this.TriangleConnectionTableBuffer);
    }

    //
    // GPU metaball falloff function summator & part of marching cubes algorithm
    //

    private GPUEdgeVertices[] runComputeShader(GPUBall[] gpuBalls) {
        // pass data to the compute shader
        this.metaballsBuffer.SetData(gpuBalls);
        this.shader.SetInt("numMetaballs", gpuBalls.Length);
        this.shader.SetInt("width", this.width);
        this.shader.SetInt("height", this.height);
        this.shader.SetInt("depth", this.depth);
        this.shader.SetFloat("threshold", this.container.threshold);

        // Run
        this.shader.Dispatch(this.shaderKernel, this.width / 8, this.height / 8, this.depth / 8);

        // parse returned vertex data and return it
        //GPUEdgeVertices[] output = new GPUEdgeVertices[this.verticesBuffer.count];
        this.verticesBuffer.GetData(output);
        return output;
    }

    //
    // Rest of marching cubes algorithm (on CPU)
    //

    private void updateVertices2(GPUEdgeVertices vert) {
        int cubeIndex = vert.index;

        //int countLog = 0;
        for(int k = 0; triTable[cubeIndex][k] != -1; k += 3) {
            this.vertices.Add(this.findVertex(vert, this.triTable[cubeIndex][k]));
            this.vertices.Add(this.findVertex(vert, this.triTable[cubeIndex][k + 2]));
            this.vertices.Add(this.findVertex(vert, this.triTable[cubeIndex][k + 1]));
            //countLog += 3;
        }
        //Debug.Log(countLog);
    }

    private List<Vector3> updateVertices3(GPUEdgeVertices vert)
    {
        int cubeIndex = vert.index;

        List<Vector3> vertices3 = new List<Vector3>();

        //int countLog = 0;
        for (int k = 0; triTable[cubeIndex][k] != -1; k += 3)
        {
            vertices3.Add(this.findVertex(vert, this.triTable[cubeIndex][k]));
            vertices3.Add(this.findVertex(vert, this.triTable[cubeIndex][k + 2]));
            vertices3.Add(this.findVertex(vert, this.triTable[cubeIndex][k + 1]));
            //countLog += 3;
        }
        //Debug.Log(countLog);

        return vertices3;
    }

    private List<Vector3> updateVertices2Threaded(GPUEdgeVertices vert)
    {
        int cubeIndex = vert.index;

        List<Vector3> verticiesToAdd = new List<Vector3>();

        //int countLog = 0;
        for (int k = 0; triTable[cubeIndex][k] != -1; k += 3)
        {
            verticiesToAdd.Add(this.findVertex(vert, this.triTable[cubeIndex][k]));
            verticiesToAdd.Add(this.findVertex(vert, this.triTable[cubeIndex][k + 2]));
            verticiesToAdd.Add(this.findVertex(vert, this.triTable[cubeIndex][k + 1]));
            //countLog += 3;
        }

        return verticiesToAdd;
        //Debug.Log(countLog);
    }

    [BurstCompile]
    struct ComputeVerticies : IJobParallelFor
    {
        #pragma warning disable 0649
        //[ReadOnly] public NativeArray<GPUEdgeVertices> particlesPressure;
        [ReadOnly] public GPUEdgeVertices vert;
        [ReadOnly] public NativeArray<int> triTable;

        public NativeArray<Vector3> verticiesToAdd;
        public int offset;

        #pragma warning restore 0649

        public void Execute(int index)
        {
            //int cubeIndex = vert.index;
            //int countLog = 0;
            for (int k = 0; triTable[k] != -1; k += 3)
            {
                int i = this.triTable[k];
                int newIndex = index + offset;

                if (i == 0) { verticiesToAdd[newIndex] = vert.edge0;} 
                else if(i == 1) { verticiesToAdd[newIndex] = vert.edge1;} 
                else if(i == 2) { verticiesToAdd[newIndex] = vert.edge2;} 
                else if(i == 3) { verticiesToAdd[newIndex] = vert.edge3;} 
                else if(i == 4) { verticiesToAdd[newIndex] = vert.edge4;} 
                else if(i == 5) { verticiesToAdd[newIndex] = vert.edge5;} 
                else if(i == 6) { verticiesToAdd[newIndex] = vert.edge6;} 
                else if(i == 7) { verticiesToAdd[newIndex] = vert.edge7;} 
                else if(i == 8) { verticiesToAdd[newIndex] = vert.edge8;} 
                else if(i == 9) { verticiesToAdd[newIndex] = vert.edge9;} 
                else if(i == 10) { verticiesToAdd[newIndex] = vert.edge10;} 
                else { verticiesToAdd[newIndex] = vert.edge11;}

                i = this.triTable[k + 2];
                newIndex = index + 1 + offset;

                if (i == 0) { verticiesToAdd[newIndex] = vert.edge0; }
                else if (i == 1) { verticiesToAdd[newIndex] = vert.edge1; }
                else if (i == 2) { verticiesToAdd[newIndex] = vert.edge2; }
                else if (i == 3) { verticiesToAdd[newIndex] = vert.edge3; }
                else if (i == 4) { verticiesToAdd[newIndex] = vert.edge4; }
                else if (i == 5) { verticiesToAdd[newIndex] = vert.edge5; }
                else if (i == 6) { verticiesToAdd[newIndex] = vert.edge6; }
                else if (i == 7) { verticiesToAdd[newIndex] = vert.edge7; }
                else if (i == 8) { verticiesToAdd[newIndex] = vert.edge8; }
                else if (i == 9) { verticiesToAdd[newIndex] = vert.edge9; }
                else if (i == 10) { verticiesToAdd[newIndex] = vert.edge10; }
                else { verticiesToAdd[newIndex] = vert.edge11; }

                i = this.triTable[k + 1];
                newIndex = index + 2 + offset;

                if (i == 0) { verticiesToAdd[newIndex] = vert.edge0; }
                else if (i == 1) { verticiesToAdd[newIndex] = vert.edge1; }
                else if (i == 2) { verticiesToAdd[newIndex] = vert.edge2; }
                else if (i == 3) { verticiesToAdd[newIndex] = vert.edge3; }
                else if (i == 4) { verticiesToAdd[newIndex] = vert.edge4; }
                else if (i == 5) { verticiesToAdd[newIndex] = vert.edge5; }
                else if (i == 6) { verticiesToAdd[newIndex] = vert.edge6; }
                else if (i == 7) { verticiesToAdd[newIndex] = vert.edge7; }
                else if (i == 8) { verticiesToAdd[newIndex] = vert.edge8; }
                else if (i == 9) { verticiesToAdd[newIndex] = vert.edge9; }
                else if (i == 10) { verticiesToAdd[newIndex] = vert.edge10; }
                else { verticiesToAdd[newIndex] = vert.edge11; }

                offset += 3;

                //verticiesToAdd.Add(this.findVertex(vert, this.triTable[k]));
                //verticiesToAdd.Add(this.findVertex(vert, this.triTable[k + 2]));
                //verticiesToAdd.Add(this.findVertex(vert, this.triTable[k + 1]));
                //countLog += 3;
            }

            //return verticiesToAdd;
        }
    }

    //[BurstCompile]
    struct ComputeAllVerticies : IJobParallelFor
    {
        #pragma warning disable 0649
        //[ReadOnly] public NativeArray<GPUEdgeVertices> particlesPressure;
        [ReadOnly] public NativeArray<GPUEdgeVertices> verts;
        [ReadOnly] public NativeArray<int> triTable;

        [ReadOnly] public int width;
        [ReadOnly] public int height;
        [ReadOnly] public int depth;

        public NativeArray<int> width_counter; //x
        public NativeArray<int> height_counter; //y
        public NativeArray<int> depth_counter; //z

        public NativeArray<Vector3> verticiesToAdd;
        //public NativeList<Vector3> verticiesToAddList;
        public NativeArray<int> offset;
        public int sizeOf2D;
        public NativeArray<int> indexSet;

        #pragma warning restore 0649

        public void Execute(int index)
        {
            //int cubeIndex = vert.index;
            //int countLog = 0;

            //offset2 = 50;
            
            if (depth_counter[0] >= depth)
            {
                depth_counter[0] = 0;
                height_counter[0] = height_counter[0] + 1;
            }

            if (height_counter[0] >= height)
            {
                depth_counter[0] = 0;
                height_counter[0] = 0;
                width_counter[0] = width_counter[0] + 1;
            }

            //tritable how to calulate
            //triTable[cubeIndex][k]
            //cubeIndex*size+k
            //cubeIndex*sizeOf2D+k

            //int cubeIndex = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index;
            indexSet[0] = 0;

            //indexSet[0] = (verts[width_counter[0] + 33 * (height_counter[0] + 33 * depth_counter[0])].index * 16) + indexSet[0];

            while (triTable[(verts[width_counter[0] + 33 * (height_counter[0] + 33 * depth_counter[0])].index * 16) + indexSet[0]] != -1)
            {
                indexSet[0] = (verts[width_counter[0] + 33 * (height_counter[0] + 33 * depth_counter[0])].index * 16) + indexSet[0];

                verticiesToAdd[0] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge0;

                if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0]] == 0) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge0; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0]] == 1) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge1; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0]] == 2) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge2; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0]] == 3) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge3; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0]] == 4) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge4; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0]] == 5) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge5; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0]] == 6) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge6; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0]] == 7) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge7; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0]] == 8) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge8; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0]] == 9) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge9; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0]] == 10) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge10; }
                else { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge11; }

                //i = this.triTable[(cubeIndex * sizeOf2D) + k + 2];//[k + 2];
                //newIndex = 1 + offset[0];

                if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 2] == 0) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge0; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 2] == 1) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge1; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 2] == 2) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge2; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 2] == 3) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge3; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 2] == 4) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge4; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 2] == 5) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge5; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 2] == 6) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge6; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 2] == 7) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge7; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 2] == 8) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge8; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 2] == 9) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge9; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 2] == 10) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge10; }
                else { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge11; }

                //i = this.triTable[(cubeIndex * sizeOf2D) + k + 1];//[k + 1];
                //newIndex = 2 + offset[0];

                if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 1] == 0) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge0; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 1] == 1) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge1; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 1] == 2) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge2; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 1] == 3) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge3; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 1] == 4) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge4; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 1] == 5) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge5; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 1] == 6) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge6; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 1] == 7) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge7; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 1] == 8) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge8; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 1] == 9) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge9; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * 16) + indexSet[0] + 1] == 10) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge10; }
                else { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge11; }

                //offset[0] += 3;

                //indexSet[0] = indexSet[0] + 1;
            }
            /*
            for (indexSet[0] = 0; triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0]] != -1; indexSet[0] += 3)
            {
                //int i = triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + k];//this.triTable[k];
                //int newIndex = offset[0];

                if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0]] == 0) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge0; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0]] == 1) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge1; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0]] == 2) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge2; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0]] == 3) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge3; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0]] == 4) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge4; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0]] == 5) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge5; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0]] == 6) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge6; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0]] == 7) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge7; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0]] == 8) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge8; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0]] == 9) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge9; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0]] == 10) { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge10; }
                else { verticiesToAdd[offset[0]] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge11; }

                //i = this.triTable[(cubeIndex * sizeOf2D) + k + 2];//[k + 2];
                //newIndex = 1 + offset[0];

                if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 2] == 0) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge0; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 2] == 1) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge1; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 2] == 2) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge2; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 2] == 3) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge3; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 2] == 4) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge4; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 2] == 5) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge5; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 2] == 6) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge6; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 2] == 7) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge7; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 2] == 8) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge8; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 2] == 9) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge9; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 2] == 10) { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge10; }
                else { verticiesToAdd[offset[0] + 1] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge11; }

                //i = this.triTable[(cubeIndex * sizeOf2D) + k + 1];//[k + 1];
                //newIndex = 2 + offset[0];

                if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 1] == 0) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge0; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 1] == 1) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge1; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 1] == 2) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge2; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 1] == 3) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge3; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 1] == 4) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge4; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 1] == 5) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge5; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 1] == 6) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge6; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 1] == 7) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge7; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 1] == 8) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge8; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 1] == 9) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge9; }
                else if (triTable[(verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].index * sizeOf2D) + indexSet[0] + 1] == 10) { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge10; }
                else { verticiesToAdd[offset[0] + 2] = verts[width_counter[0] + width * (height_counter[0] + height * depth_counter[0])].edge11; }

                offset[0] += 3;

                //verticiesToAdd.Add(this.findVertex(vert, this.triTable[k]));
                //verticiesToAdd.Add(this.findVertex(vert, this.triTable[k + 2]));
                //verticiesToAdd.Add(this.findVertex(vert, this.triTable[k + 1]));
                //countLog += 3;

                //offset2[0] = i;

            }
            */

            /*
            if (i == 0) { verticiesToAddList.Add(verts[width_counter + width * (height_counter + height * depth_counter)].edge0); }
            else if (i == 1) { verticiesToAddList.Add(verts[width_counter + width * (height_counter + height * depth_counter)].edge1); }
            else if (i == 2) { verticiesToAddList.Add(verts[width_counter + width * (height_counter + height * depth_counter)].edge2); }
            else if (i == 3) { verticiesToAddList.Add(verts[width_counter + width * (height_counter + height * depth_counter)].edge3); }
            else if (i == 4) { verticiesToAddList.Add(verts[width_counter + width * (height_counter + height * depth_counter)].edge4); }
            else if (i == 5) { verticiesToAddList.Add(verts[width_counter + width * (height_counter + height * depth_counter)].edge5); }
            else if (i == 6) { verticiesToAddList.Add(verts[width_counter + width * (height_counter + height * depth_counter)].edge6); }
            else if (i == 7) { verticiesToAddList.Add(verts[width_counter + width * (height_counter + height * depth_counter)].edge7); }
            else if (i == 8) { verticiesToAddList.Add(verts[width_counter + width * (height_counter + height * depth_counter)].edge8); }
            else if (i == 9) { verticiesToAddList.Add(verts[width_counter + width * (height_counter + height * depth_counter)].edge9); }
            else if (i == 10) { verticiesToAddList.Add(verts[width_counter + width * (height_counter + height * depth_counter)].edge10); }
            else { verticiesToAddList.Add(verts[width_counter + width * (height_counter + height * depth_counter)].edge11); }
            */

            //offset = 5000;


            depth_counter[0] = depth_counter[0] + 1;
            //return verticiesToAdd;
        }
    }

    private Vector3 findVertex(GPUEdgeVertices vert, int i) {
        if(i == 0) {
            return vert.edge0;
        } else if(i == 1) {
            return vert.edge1;
        } else if(i == 2) {
            return vert.edge2;
        } else if(i == 3) {
            return vert.edge3;
        } else if(i == 4) {
            return vert.edge4;
        } else if(i == 5) {
            return vert.edge5;
        } else if(i == 6) {
            return vert.edge6;
        } else if(i == 7) {
            return vert.edge7;
        } else if(i == 8) {
            return vert.edge8;
        } else if(i == 9) {
            return vert.edge9;
        } else if(i == 10) {
            return vert.edge10;
        } else {
            return vert.edge11;
        }
    }

    //
    // LOOKUP TABLES
    //

    private List<int> triangleBuffer;

    private Vector3[,,] positionMap;

    private Vector3[] edgeMap;
    
    private int[][] triTable = {
        new int[] {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
        new int[] {2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1},
        new int[] {8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
        new int[] {4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1},
        new int[] {3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1},
        new int[] {4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1},
        new int[] {4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
        new int[] {5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1},
        new int[] {2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1},
        new int[] {9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
        new int[] {2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1},
        new int[] {10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1},
        new int[] {4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1},
        new int[] {5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1},
        new int[] {5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1},
        new int[] {10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1},
        new int[] {8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1},
        new int[] {2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1},
        new int[] {7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1},
        new int[] {2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1},
        new int[] {11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1},
        new int[] {5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1},
        new int[] {11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1},
        new int[] {11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1},
        new int[] {5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1},
        new int[] {2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
        new int[] {5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1},
        new int[] {6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1},
        new int[] {3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1},
        new int[] {6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1},
        new int[] {5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
        new int[] {10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1},
        new int[] {6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1},
        new int[] {8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1},
        new int[] {7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1},
        new int[] {3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
        new int[] {5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1},
        new int[] {0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1},
        new int[] {9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1},
        new int[] {8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1},
        new int[] {5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1},
        new int[] {0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1},
        new int[] {6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1},
        new int[] {10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1},
        new int[] {10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1},
        new int[] {8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1},
        new int[] {1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1},
        new int[] {0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1},
        new int[] {10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1},
        new int[] {3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1},
        new int[] {6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1},
        new int[] {9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1},
        new int[] {8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1},
        new int[] {3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1},
        new int[] {6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1},
        new int[] {10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1},
        new int[] {10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1},
        new int[] {2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1},
        new int[] {7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1},
        new int[] {7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1},
        new int[] {2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1},
        new int[] {1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1},
        new int[] {11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1},
        new int[] {8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1},
        new int[] {0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1},
        new int[] {7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
        new int[] {10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
        new int[] {2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
        new int[] {6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1},
        new int[] {7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1},
        new int[] {2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1},
        new int[] {10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1},
        new int[] {10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1},
        new int[] {0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1},
        new int[] {7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1},
        new int[] {6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1},
        new int[] {8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1},
        new int[] {6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1},
        new int[] {4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1},
        new int[] {10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1},
        new int[] {8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1},
        new int[] {1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1},
        new int[] {8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1},
        new int[] {10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1},
        new int[] {4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1},
        new int[] {10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
        new int[] {5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
        new int[] {11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1},
        new int[] {9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
        new int[] {6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1},
        new int[] {7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1},
        new int[] {3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1},
        new int[] {7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1},
        new int[] {3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1},
        new int[] {6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1},
        new int[] {9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1},
        new int[] {1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1},
        new int[] {4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1},
        new int[] {7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1},
        new int[] {6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1},
        new int[] {0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1},
        new int[] {6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1},
        new int[] {0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1},
        new int[] {11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1},
        new int[] {6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1},
        new int[] {5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1},
        new int[] {9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1},
        new int[] {1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1},
        new int[] {10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1},
        new int[] {0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1},
        new int[] {5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1},
        new int[] {10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1},
        new int[] {11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1},
        new int[] {9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1},
        new int[] {7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1},
        new int[] {2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1},
        new int[] {8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1},
        new int[] {9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1},
        new int[] {9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1},
        new int[] {1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1},
        new int[] {5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1},
        new int[] {0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1},
        new int[] {10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1},
        new int[] {2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1},
        new int[] {0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1},
        new int[] {0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1},
        new int[] {9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1},
        new int[] {5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1},
        new int[] {5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1},
        new int[] {8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1},
        new int[] {9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1},
        new int[] {1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1},
        new int[] {3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1},
        new int[] {4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1},
        new int[] {9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1},
        new int[] {11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1},
        new int[] {11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1},
        new int[] {2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1},
        new int[] {9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1},
        new int[] {3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1},
        new int[] {1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1},
        new int[] {4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1},
        new int[] {4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1},
        new int[] {0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1},
        new int[] {9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1},
        new int[] {1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        new int[] {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}
    };

    public readonly static int[,] TriangleConnectionTable = new int[,]
        {
        {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1},
        {3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1},
        {3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1},
        {3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1},
        {9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
        {2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1},
        {8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
        {4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1},
        {3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1},
        {1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1},
        {4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1},
        {4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
        {5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1},
        {2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1},
        {9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
        {0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
        {2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1},
        {10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1},
        {5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1},
        {5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1},
        {9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1},
        {1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1},
        {10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1},
        {8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1},
        {2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1},
        {7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1},
        {2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1},
        {11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1},
        {5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1},
        {11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1},
        {11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1},
        {9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1},
        {2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1},
        {6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1},
        {3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1},
        {6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
        {10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1},
        {6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1},
        {8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1},
        {7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1},
        {3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1},
        {0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1},
        {9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1},
        {8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1},
        {5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1},
        {0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1},
        {6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1},
        {10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1},
        {10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1},
        {8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1},
        {1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1},
        {0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1},
        {10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1},
        {3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1},
        {6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1},
        {9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1},
        {8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1},
        {3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1},
        {6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1},
        {0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1},
        {10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1},
        {10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1},
        {2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1},
        {7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1},
        {7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1},
        {2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1},
        {1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1},
        {11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1},
        {8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1},
        {0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1},
        {7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
        {10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
        {2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
        {6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1},
        {7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1},
        {2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1},
        {10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1},
        {10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1},
        {0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1},
        {7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1},
        {6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1},
        {8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1},
        {9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1},
        {6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1},
        {4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1},
        {10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1},
        {8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1},
        {0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1},
        {1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1},
        {8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1},
        {10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1},
        {4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1},
        {10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
        {11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1},
        {9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
        {6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1},
        {7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1},
        {3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1},
        {7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1},
        {3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1},
        {6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1},
        {9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1},
        {1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1},
        {4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1},
        {7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1},
        {6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1},
        {3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1},
        {0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1},
        {6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1},
        {0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1},
        {11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1},
        {6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1},
        {5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1},
        {9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1},
        {1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1},
        {1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1},
        {10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1},
        {0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1},
        {5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1},
        {10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1},
        {11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1},
        {9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1},
        {7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1},
        {2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1},
        {8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1},
        {9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1},
        {9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1},
        {1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1},
        {9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1},
        {5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1},
        {0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1},
        {10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1},
        {2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1},
        {0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1},
        {0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1},
        {9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1},
        {5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1},
        {3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1},
        {5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1},
        {8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1},
        {0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1},
        {9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1},
        {1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1},
        {3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1},
        {4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1},
        {9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1},
        {11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1},
        {11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1},
        {2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1},
        {9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1},
        {3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1},
        {1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1},
        {4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1},
        {3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1},
        {0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1},
        {1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}
        };
}
