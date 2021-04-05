using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Voxelizer.Rendering
{
    /// <summary>
    /// Implementation of thin surface voxelization using rasterization. Also 
    /// generates a buffer of filled voxels.
    /// </summary>
    public static class VoxelizationUtils
    {
        private const string FILL_VOXELS = "FillVoxelsPass";
        private const string FIND_FILLED_VOXELS = "FindFilledVoxelsPass";
        private static int TEMP_RT = Shader.PropertyToID("_RenderTargetVoxl");

        /// <summary>
        /// Initialize VoxelsData
        /// </summary>
        /// <param name="bounds">Bounds of area to voxelize</param>
        /// <param name="resolution">Number of voxels in the largest bounds dimension</param>
        /// <param name="debugName">Name given to graphic resources to help debugging</param>
        /// <returns></returns>
        public static VoxelsData CreateVoxelData(Bounds bounds, int resolution, string debugName = "")
        {
            resolution = Math.Max(resolution, 1);

            // find voxel physical size
            var fullExtent = bounds.size;
            var largestDimension = Mathf.Max(Mathf.Max(fullExtent.x, fullExtent.y), fullExtent.z);
            var voxelSize = largestDimension / resolution;

            // find number of voxels that fit bounds
            var textureSizeVec3 = fullExtent / voxelSize;
            var textureSizeInt3 = Vector3Int.CeilToInt(textureSizeVec3);
            textureSizeInt3 = Vector3Int.Max(textureSizeInt3, Vector3Int.one);

            // allocate 3d texture
            var voxelsRt = new RenderTexture(textureSizeInt3.x, textureSizeInt3.y, 0, RenderTextureFormat.ARGBFloat);
            voxelsRt.enableRandomWrite = true;
            voxelsRt.volumeDepth = textureSizeInt3.z;
            voxelsRt.dimension = TextureDimension.Tex3D;
            voxelsRt.name = debugName + "_Voxels";
            voxelsRt.Create();

            // visualization needs to have data for non empty voxels
            // create buffer for those instances
            var filledVoxels = new ComputeBuffer(textureSizeInt3.x * textureSizeInt3.y * textureSizeInt3.z,
                Marshal.SizeOf<Vector3>() * 2, 
                ComputeBufferType.Counter | ComputeBufferType.Structured);
            filledVoxels.name = debugName + "_FilledVoxels";

            // adjust voxel size so that bounds has 0.5 voxel extra in all dimensions
            voxelSize += voxelSize / resolution;

            return new VoxelsData(voxelsRt, filledVoxels, voxelSize);
        }

        /// <summary>
        /// Voxelize the surface of a mesh. Only color is constructed at the moment.
        /// </summary>
        /// <param name="commandBuffer">Command buffer receiving gpu instuctions</param>
        /// <param name="data">Voxelization results</param>
        /// <param name="mesh">Mesh to voxelize</param>
        public static void VoxelizeSurface(CommandBuffer commandBuffer, VoxelizationResources resources, VoxelsData data, Mesh mesh)
        {
            Begin(commandBuffer, resources, data, mesh);
            FillVoxels(commandBuffer, resources, data, mesh);
            FindFilledVoxelInstances(commandBuffer, resources, data, mesh);

            // generate voxels mips
            // TODO

            End(commandBuffer, resources, data, mesh);
        }

        private static void Begin(CommandBuffer commandBuffer, VoxelizationResources resources, VoxelsData data, Mesh mesh)
        {
            // reset command buffer
            // must render states are setup in the shader so no need to set everything
            commandBuffer.Clear();
            commandBuffer.DisableScissorRect();

            // for debugging purposes, bind a render target to examine shader behaviour
            // to be removed
            // TODO: find out how to set null render target
            //       viewport is implicitly set
            var vp = data.LargestDimenstion2D;
            commandBuffer.GetTemporaryRT(TEMP_RT, vp.x, vp.y, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat);
            commandBuffer.SetRenderTarget(TEMP_RT);
        }

        private static void End(CommandBuffer commandBuffer, VoxelizationResources resources, VoxelsData data, Mesh mesh)
        {
            commandBuffer.ReleaseTemporaryRT(TEMP_RT);
        }

        private static void FillVoxels(CommandBuffer commandBuffer, VoxelizationResources resources, VoxelsData data, Mesh mesh)
        {
            // transform mesh space in voxelization volume space
            // [-1, 1] in XYZ
            var volumeSize = data.VolumeSize;
            var view = Matrix4x4.Scale(new Vector3(2.0f / volumeSize.x, 2.0f / volumeSize.y, 2.0f / volumeSize.z)) *
                       Matrix4x4.Translate(-mesh.bounds.center);

            // geometry shader reprojects each triangles
            // hence a simple orthogonal projection will do for all cases
            var proj = Matrix4x4.Ortho(-1, 1, -1, 1, -1, 1);
            commandBuffer.SetViewProjectionMatrices(view, proj);

            // set shader parameters
            commandBuffer.SetGlobalVector(VoxelizationResources.VOLUME_SIZE,
                new Vector4(data.Voxels.width,
                            data.Voxels.height,
                            data.Voxels.volumeDepth));

            var vp = data.LargestDimenstion2D;
            commandBuffer.SetGlobalVector(VoxelizationResources.VIEWPORT_ST,
                new Vector4(1.0f / vp.x,
                            1.0f / vp.y,
                            -0.5f / vp.x,
                            -0.5f / vp.y));

            // pixel shader actual output is the 3d texture
            commandBuffer.SetRandomWriteTarget(1, data.VoxelsRTId);

            // trigger rasterization to fill highest resolution voxels
            commandBuffer.BeginSample(FILL_VOXELS);
            commandBuffer.DrawMesh(mesh, Matrix4x4.identity, resources.VoxelizationMaterial);
            commandBuffer.EndSample(FILL_VOXELS);
        }

        private static void FindFilledVoxelInstances(CommandBuffer commandBuffer, VoxelizationResources resources, VoxelsData data, Mesh mesh)
        {
            // reset number of filled voxels
            data.FilledVoxelInstances.SetCounterValue(0);

            // set compute shader parameters
            var cs = resources.VoxelizationPostProcessShader;
            var kernel = resources.FindFilledVoxelsKernel;
            cs.SetTexture(kernel, VoxelizationResources.VOXELS, data.Voxels);
            cs.SetBuffer(kernel, VoxelizationResources.FILLED_VOXELS_INSTANCES, data.FilledVoxelInstances);
            cs.SetInts(VoxelizationResources.VOLUME_SIZE,
                new int[] { data.Voxels.width, data.Voxels.height, data.Voxels.volumeDepth });

            var volumeSize = data.VolumeSize;

            // transform index space to mesh local space
            // lhs system; matrix must be read from 
            // last to first
            var indexToPosition =

                // finally mesh local space
                Matrix4x4.Translate(mesh.bounds.center) *

                // [-0.5, 0.5] space [-0.5 * volumeSize, 0.5 * volumeSize]
                Matrix4x4.Scale(new Vector3(volumeSize.x,
                                            volumeSize.y,
                                            volumeSize.z)) *

                // [0, 1] space to [-0.5, 0.5] space
                Matrix4x4.Translate(new Vector3(-0.5f, -0.5f, -0.5f)) *

                // voxel space to [0, 1] space
                Matrix4x4.Scale(new Vector3(1.0f / data.Voxels.width,
                                            1.0f / data.Voxels.height,
                                            1.0f / data.Voxels.volumeDepth)) *

                // move by half a voxel
                Matrix4x4.Translate(new Vector3(0.5f, 0.5f, 0.5f));
            cs.SetMatrix(VoxelizationResources.INDEX_TO_POSITION, indexToPosition);

            (var tgX, var tgY, var tgZ) = resources.FindFilledVoxelsThreadGroupsSize;
            commandBuffer.BeginSample(FIND_FILLED_VOXELS);
            commandBuffer.ClearRandomWriteTargets();
            commandBuffer.DispatchCompute(cs,
                kernel,
                NumGroup(data.Voxels.width, tgX),
                NumGroup(data.Voxels.height, tgY),
                NumGroup(data.Voxels.volumeDepth, tgZ));
            commandBuffer.EndSample(FIND_FILLED_VOXELS);
        }

        private static int NumGroup(int count, int size)
        {
            return Math.Max(1, count / size);
        }
    }
}