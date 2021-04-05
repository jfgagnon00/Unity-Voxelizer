#ifndef VOXEL_INSTANCES_HLSL
#define VOXEL_INSTANCES_HLSL

struct FilledVoxelInstance
{
    float3 position;
    float3 color;
};

#ifdef VOXEL_INSTANCES_RW_ACCESS
    RWStructuredBuffer<FilledVoxelInstance> _FilledVoxelInstances;
#else
    StructuredBuffer<FilledVoxelInstance> _FilledVoxelInstances;
#endif

#endif