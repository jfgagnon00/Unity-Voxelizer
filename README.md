# Voxelizer

This project tries to implement a thin surface voxelizer inside Unity using the gpu (rasterization and compute shader). Visualization is done by generating an indirect draw instance buffer with the rasterized voxels.

## Instructions

### Scene edition

1. Open VoxelizationScene
2. Look for the game object Voxels
3. Press play
4. Fiddle with Voxelization component properties.
5. Assign a new mesh to be voxelized.

### Limitations

* Limited to static mesh
* Voxelization outputs limited to mesh vertex color.
* Voxelization still has some minor issues
* Visualization is based on Unity Standard shader
* LOD not implemented

# Credits

* Standford Bunny was taken from https://github.com/leon196/OhoShaders
* Happy Buddha was taken from https://www.lugher3d.com/free-3d-models/happy-buddha-fbx