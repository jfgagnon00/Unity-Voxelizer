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

            return new VoxelsData(voxelsRt, filledVoxels, voxelSize, bounds.center);
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
            var vp = data.ViewportSize;
            commandBuffer.GetTemporaryRT(TEMP_RT, vp.x, vp.y, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat);
            commandBuffer.SetRenderTarget(TEMP_RT);
        }

        private static void End(CommandBuffer commandBuffer, VoxelizationResources resources, VoxelsData data, Mesh mesh)
        {
            commandBuffer.ReleaseTemporaryRT(TEMP_RT);
        }

        private static Matrix4x4 PermuteRows(Matrix4x4 m, int rowIndex0, int rowIndex1)
        {
            var row0 = m.GetRow(rowIndex0);
            var row1 = m.GetRow(rowIndex1);
            var result = m;
            result.SetRow(rowIndex0, row1);
            result.SetRow(rowIndex1, row0);
            return result;
        }

        private static Vector3 Permute(Vector3 v, int i0, int i1)
        {
            var result = v;
            result[i0] = v[i1];
            result[i1] = v[i0];
            return result;
        }

        private static Matrix4x4 GetProjection(VoxelsData data, int i0, int i1)
        {
            // goal is to find a matrix that maps volume local coordinates 
            // to voxel indices taking into account projection on a specific
            // face and face resolution

            // permute some values to deal with face projection
            var volumeCorner = Permute(data.VolumeBounds.min, i0, i1);
            var volumeSize = Permute(data.VolumeSize, i0, i1);

            var faceResolution = (Vector2) Permute(
                new Vector3(data.Voxels.width, data.Voxels.height, data.Voxels.volumeDepth), 
                i0, i1);

            // permute volume coordinates for face projection
            var permuteAxis = PermuteRows(Matrix4x4.identity, i0, i1);

            // actual projection
            var viewProjection =
                // map XY to [-1, 1]
                // map Z to [0, 1]
                Matrix4x4.Translate(new Vector3(-1, -1, 0)) *
                Matrix4x4.Scale(new Vector3(2, 2, 1)) *

                // map to normalize volume coordinates [0, 1] in all axis
                Matrix4x4.Scale(new Vector3(1.0f / volumeSize.x, 1.0f / volumeSize.y, 1.0f / volumeSize.z)) *
                Matrix4x4.Translate(-volumeCorner);

            // scale to match desired face resolution
            var vp = (Vector2) data.ViewportSize;
            var scale = faceResolution / vp;
            var scalarScale = Mathf.Max(scale.x, scale.y);

            // scale to compensate for face aspect ratio
            // resolution of a face != resolution of viewport
            Vector3 aspectRatioCompensation;
            var aspectRatio = volumeSize.x / volumeSize.y;
            if (aspectRatio >= 1.0f)
                aspectRatioCompensation = new Vector3(1, -1.0f / aspectRatio, 1);
            else
                aspectRatioCompensation = new Vector3(aspectRatio, -1, 1);

            // translate to fit texture corners and not viewport center
            var vpCenter = vp * 0.5f;
            var faceCenter = faceResolution * 0.5f;
            var delta = faceCenter - vpCenter;
            // -2 in y since Unity uses opengl convention
            var normalizedDelta = delta * new Vector2(2, -2) / vp;
            var fitCornerTranslation = Matrix4x4.Translate(normalizedDelta);

            // composite everything together
            // Unity uses lhs, so compositing happens from last to first
            var finalProjection =
                fitCornerTranslation *
                Matrix4x4.Scale(aspectRatioCompensation * scalarScale) *
                viewProjection *
                permuteAxis;

            return finalProjection;
        }

        private static void FillVoxels(CommandBuffer commandBuffer, VoxelizationResources resources, VoxelsData data, Mesh mesh)
        {
            var projections = new Matrix4x4[3]
            {
                GetProjection(data, 0, 2), // project on X
                GetProjection(data, 1, 2), // project on Y
                GetProjection(data, 2, 2)  // project on Z
            };
            commandBuffer.SetGlobalMatrixArray(VoxelizationResources.PROJECTIONS, projections);

            // set shader parameters
            commandBuffer.SetGlobalVector(VoxelizationResources.VOLUME_SIZE,
                new Vector4(data.Voxels.width,
                            data.Voxels.height,
                            data.Voxels.volumeDepth,
                            0));

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

            var volumeBounds = data.VolumeBounds;

            // transform index space to mesh local space
            // lhs system; matrix must be read from 
            // last to first
            var indexToPosition =

                // finally mesh local space
                Matrix4x4.Translate(volumeBounds.center) *

                // [-0.5, 0.5] space [-0.5 * volumeSize, 0.5 * volumeSize]
                Matrix4x4.Scale(volumeBounds.size) *

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
            var numGroup = count / size;
            numGroup += count % size > 0 ? 1 : 0;
            return Math.Max(1, numGroup);
        }
    }
}