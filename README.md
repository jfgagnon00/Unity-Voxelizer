# Voxelizer

This project tries to implement a thin surface voxelizer inside Unity using gpu (rasterization and compute shader). Visualization is done by generating an indirect draw instance buffer with rasterized voxels.

## Instructions

### Scene edition

1. Create GameObject
2. Add Voxelization component
3. Assign mesh to be voxelized. Use premade scriptable objects for resources where appropriate.

### Limitations

* Limited to static mesh
* Voxelization outputs limited to mesh vertex color.
* Voxelization still buggy
* Visualization is minimal
* LOD not implemented

# Credits

* Standford Bunny was taken from https://github.com/leon196/OhoShaders
* Happy Buddha was taken from https://www.lugher3d.com/free-3d-models/happy-buddha-fbx