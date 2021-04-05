#ifndef VOXELS_HLSL
#define VOXELS_HLSL

#ifdef VOXELS_RW_ACCESS
    RWTexture3D<float4> _Voxels;
#else
    Texture3D<float4> _Voxels;
#endif

#endif