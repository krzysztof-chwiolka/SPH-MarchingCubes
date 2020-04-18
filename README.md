# Unity Based Ground Deformation System
Implementation of the SPH Algorithm alongside a marching cubes algorithm to provide a fully meshed ground deformation system.

Uses Unity 2019.2

### Settings:
**Marching cubes algorithm:**
- Resolution
The resolution can be changed in the marching cubes chunk script (in main scene)

**ECS, Job system:**
- Particle count
You can find the particle parameters* inside 'Assets/Job System/Prefabs' in the SPHSphereECS GameObject. (change both Particle componect and particle component additional)


* Particle parameters:
  - radius
  - smoothing radius
  - rest density
  - gravity multiplier
  - mass
  - viscosity
  - drag
