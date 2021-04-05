// Shader responsible to voxelize thin surface using rasterization
// Trick is to use geometry shader to find plane with largest projected
// area and let rasterization do the rest. Pixel shader then writes
// to a UAV.

// There are many problems at the moment as not all voxels are found.
// Area to investigate: conservative rendering, using voxel depth exent
// to ensure all touched voxels are found.
Shader "Voxelizer/Voxelization"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "ForceNoShadowCasting"="True" }
        
        Pass
        {
            Blend Off
            Cull Off
            ZTest Always

            ColorMask 0
            ZWrite Off

            CGPROGRAM

            #pragma require randomwrite compute geometry
            #pragma enable_d3d11_debug_symbols

            #pragma vertex vsMain
            #pragma geometry gsMain
            #pragma fragment psMain

            #include "UnityCG.cginc"

            #define VOXELS_RW_ACCESS
            #include "Voxels.hlsl"

            struct VsInput
            {
                float3 position : POSITION;
                float4 color : COLOR0;
            };

            struct GsPsInput
            {
                // gs: view space
                // ps: clip space
                float4 position: SV_POSITION;
                float4 color : COLOR0;
            };

            float4 _VolumeSize; // in pixels
            float4 _ViewportST;
          
            GsPsInput vsMain(VsInput vsIn)
            {
                GsPsInput output;

                // gs use view space: [-1, 1] in all dimensions
                output.position.xyz = UnityObjectToViewPos(vsIn.position);
                output.position.w = 1.0;
                output.color = vsIn.color;

                return output;
            }

            // ===========================================

            float4 PemuteXZ(float4 p)
            {
                return float4(p.z, p.y, p.x, 1.0);
            }

            float4 PemuteYZ(float4 p)
            {
                return float4(p.x, p.z, p.y, 1.0);
            }

            float3 GetTriangleNormal(GsPsInput points[3])
            {
                float3 e01 = points[1].position - points[0].position;
                float3 e12 = points[2].position - points[1].position;
                float3 n = normalize(cross(e01, e12));
                return n;
            }

            [maxvertexcount(3)]
            void gsMain(triangle GsPsInput gsIn[3], inout TriangleStream<GsPsInput> gsOut)
            {
                float3 n = GetTriangleNormal(gsIn);

                float4 p[3];
                float axis;

                // find plane giving largest area and project on it
                // and adjust triangle positions accordingly
                n = abs(n);
                if (n.x >= n.y && n.x >= n.z)
                {
                    // X has largest area
                    p[0] = PemuteXZ(gsIn[0].position);
                    p[1] = PemuteXZ(gsIn[1].position);
                    p[2] = PemuteXZ(gsIn[2].position);
                    axis = 0;
                }
                else if (n.y >= n.x && n.y >= n.z)
                {
                    // Y has largest area
                    p[0] = PemuteYZ(gsIn[0].position);
                    p[1] = PemuteYZ(gsIn[1].position);
                    p[2] = PemuteYZ(gsIn[2].position);
                    axis = 1;
                }
                else
                {
                    // Z has largest area: nothing to adjust
                    p[0] = gsIn[0].position;
                    p[1] = gsIn[1].position;
                    p[2] = gsIn[2].position;
                    axis = 2;
                }

                // send new triangle to rasterization
                for (int i = 0; i < 3; ++i)
                {
                    // carfull:
                    //     [-1, 1] range for XY 
                    //     [0, 1] range for Z 
                    gsIn[i].position = mul(UNITY_MATRIX_P, p[i]);
                    gsIn[i].color.a = axis;
                    gsOut.Append(gsIn[i]);
                }

                gsOut.RestartStrip();
            }

            // ===========================================

            fixed4 psMain(GsPsInput psIn) : SV_Target
            {
                float3 color = psIn.color.rgb;

                // x and y are pixel centers but z is still
                // in clip coordinate, [0, 1], at this point;
                // must  adjust psIn.position so all components
                // are in same space
                float4 position = psIn.position;
                position.xy = position.xy * _ViewportST.xy + _ViewportST.zw;

                // compensate for largest area plane projetion
                if (psIn.color.a == 0)
                    position = PemuteXZ(position);
                else if (psIn.color.a == 1)
                    position = PemuteYZ(position);

                // at this point XYZ are in [0, 1] range
                // convert to voxel indices
                position.xyz *= _VolumeSize.xyz;

                uint3 index = uint3(position.xyz);

                // store result
                float4 result = float4(color, 100.0);
                _Voxels[index] = result;

                return result;
            }

            ENDCG
        }
    }
}
