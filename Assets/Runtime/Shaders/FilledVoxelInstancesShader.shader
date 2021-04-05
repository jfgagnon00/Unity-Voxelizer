Shader "Voxelizer/FilledVoxelInstances"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "ForceNoShadowCasting"="True" }
        
        Pass
        {
            CGPROGRAM

            #pragma enable_d3d11_debug_symbols

            #pragma vertex vsMain
            #pragma fragment psMain

            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "UnityCG.cginc"
            #include "VoxelInstances.hlsl"

            struct VsInput
            {
                float3 position : POSITION;
                float4 color : COLOR0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct PsInput
            {
                float4 position : SV_POSITION;
                float4 color : COLOR0;
            };

            float4 _VolumeSize;
            float4x4 _VolumeLocalToWorld;

            void setup()
            {
                unity_ObjectToWorld = _VolumeLocalToWorld;
            }

            PsInput vsMain(VsInput vsIn)
            {
                UNITY_SETUP_INSTANCE_ID(vsIn);

                float3 position = vsIn.position;
                float4 color = vsIn.color;

                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    FilledVoxelInstance instance = _FilledVoxelInstances[unity_InstanceID];
                    
                    position *= _VolumeSize.w;
                    position += instance.position;
                    color.rgb *= instance.color;
                #endif

                PsInput output;
                output.position = UnityObjectToClipPos(position);
                output.color = color;

                return output;
            }

            // ===========================================

            fixed4 psMain(PsInput psIn) : SV_Target
            {
                return psIn.color;
            }

            ENDCG
        }
    }
}
