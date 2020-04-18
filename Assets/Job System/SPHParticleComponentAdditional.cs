using Unity.Entities;
using Unity.Mathematics;

[System.Serializable]
public struct SPHParticleAdditional : IComponentData
{
    public float3 translation;

    public float radius;
    public float smoothingRadius;
    public float smoothingRadiusSq;

    public float mass;

    public float restDensity;
    public float viscosity;
    public float gravityMult;

    public float drag;
}

public class SPHParticleComponentAdditional : ComponentDataProxy<SPHParticleAdditional> { }
