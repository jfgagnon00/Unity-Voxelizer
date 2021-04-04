# Voxelizer

This project is an implementation of [Octree-Based Sparse Voxelization Using the GPU Hardware Rasterizer](https://www.seas.upenn.edu/~pcozzi/OpenGLInsights/OpenGLInsights-SparseVoxelization.pdf) inside Unity.

Thin surface voxelisation is used to create an octree structure. It is done entirely on the gpu using rasterization and compute shaders at scene creation time. Results are encapsulated in a new kind of primitive so rendering can reuse it.

## Instructions

### Scene edition

1. Setup scene as usual
2. Create a VoxelsPrimitive scriptable object
3. Add a VoxelsPrimitiveFilter component to a game object
4. Assign desired VoxelsPrimitive
5. Adjust VoxelsPrimitive volume size and resolution property
6. Position the game object in the scene
7. Clicking on *Generate* button to generate VoxelsPrimitive content

### Limitations

* Limited to static mesh primitives
* Meshes must have a collider.
* Limited to opaque material.

### Rendering

TO BE COMPLETED

# Credits

* Sponza model was taken from https://github.com/TheRealMJP/DeferredTexturing
* Standford Bunny was taken from https://github.com/leon196/OhoShaders
* Happy Buddha was taken from https://www.lugher3d.com/free-3d-models/happy-buddha-fbx