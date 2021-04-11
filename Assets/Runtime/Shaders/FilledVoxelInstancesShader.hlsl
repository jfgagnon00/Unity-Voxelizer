#include "VoxelInstances.hlsl"

float4 _VolumeSize;
float4x4 _VolumeLocalToWorld;

void setupVoxeInstance()
{
    #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
        float3 offset = _FilledVoxelInstances[unity_InstanceID].position;
        float scale = _VolumeSize.w;
        float4x4 voxelLocalToVolume =
        {
            scale, 0, 0, offset.x,
            0, scale, 0, offset.y,
            0, 0, scale, offset.z,
            0, 0, 0, 1,
        };

        unity_ObjectToWorld = mul(_VolumeLocalToWorld, voxelLocalToVolume);
    #endif
}
