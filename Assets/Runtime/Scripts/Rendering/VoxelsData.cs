using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Voxelizer.Rendering
{
    /// <summary>
    /// Represents data unique to a particular voxelization.
    /// </summary>
    public class VoxelsData : IDisposable
    {
        public RenderTexture Voxels { get; private set; }
        public RenderTargetIdentifier VoxelsRTId { get; private set; }

        public ComputeBuffer FilledVoxelInstances { get; private set; }

        public float VoxelSize { get; private set; }

        public Vector3 VolumeSize => new Vector3(Voxels.width, Voxels.height, Voxels.volumeDepth) * VoxelSize;

        public Vector2Int LargestDimenstion2D
        {
            get
            {
                int[] dimensions = { Voxels.width, Voxels.height, Voxels.volumeDepth };
                Array.Sort(dimensions);
                return new Vector2Int(dimensions[2], dimensions[1]);
            }
        }

        public VoxelsData(RenderTexture voxels, 
            ComputeBuffer filledVoxelInstances, 
            float voxelSize)
        {
            Voxels = voxels;
            VoxelsRTId = new RenderTargetIdentifier(Voxels);
            VoxelSize = voxelSize;
            FilledVoxelInstances = filledVoxelInstances;
        }

        public void Dispose()
        {
            if (Voxels != null) Voxels.Release();
            Voxels = null;

            if (FilledVoxelInstances != null) FilledVoxelInstances.Release();
            FilledVoxelInstances = null;
        }
    }
}