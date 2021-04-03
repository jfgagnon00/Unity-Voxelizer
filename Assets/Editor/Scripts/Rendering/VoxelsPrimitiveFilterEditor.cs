using UnityEditor;
using UnityEngine;
using Voxelizer.Rendering;

namespace Voxelizer.Editor.Rendering
{
    /// <summary>
    /// Extends VoxelsPrimitiveFilter default editor by also exposing
    /// some tools to edit the contained VoxelsPrimitive.
    /// </summary>
    [CustomEditor(typeof(VoxelsPrimitiveFilter))]
    public class VoxelsPrimitiveFilterEditor : UnityEditor.Editor
    {
        private bool _primitiveGroupVisible = true;

        private VoxelsPrimitive Primitive
        {
            get
            {
                var primitiveProp = serializedObject.FindProperty("_voxelsPrimitive");
                if (primitiveProp.objectReferenceValue != null)
                    return (VoxelsPrimitive)primitiveProp.objectReferenceValue;
                else
                    return null;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            var primitive = Primitive;
            if (primitive != null) AddEditionUIElements(primitive);
            
            serializedObject.ApplyModifiedProperties();
        }

        // Add primitive default inspector and also add a 
        // few helpers: 
        //  - volume size gizmo visibility toggle
        //  - trigger generation of primitive content
        private void AddEditionUIElements(VoxelsPrimitive primitive)
        {
            if (primitive != null)
            {
                _primitiveGroupVisible = EditorGUILayout.BeginFoldoutHeaderGroup(_primitiveGroupVisible, "Primitive Helpers");
                EditorGUI.indentLevel++;

                UnityEditor.Editor.CreateEditor(primitive).DrawDefaultInspector();
                primitive.ShowVolumeSizeGizmo = GUILayout.Toggle(primitive.ShowVolumeSizeGizmo, 
                    "Toggle Volume Size Gizmo", 
                    new GUIStyle(GUI.skin.button));
                if (primitive.ShowVolumeSizeGizmo) SceneView.RepaintAll();

                if (GUILayout.Button("Generate Voxels Content"))
                {
                    // TODO: generate primitive content
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }

        private void OnSceneGUI()
        {
            // bail out if gizmo is not needed
            var primitive = Primitive;
            if (primitive == null || 
                !primitive.ShowVolumeSizeGizmo ||
                !Handles.ShouldRenderGizmos()) return;

            var filter = (VoxelsPrimitiveFilter) target;
            var transform = filter.transform;

            // draw gizmo handles
            EditorGUI.BeginChangeCheck();
            var newVolumeSize = Handles.RadiusHandle(transform.rotation,
                transform.position,
                primitive.VolumeSize * 0.5f,
                true) * 2.0f;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(primitive, "Change Volume Size");
                primitive.VolumeSize = newVolumeSize;
            }

            // draw volume
            Handles.color = Color.yellow;
            Handles.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Handles.DrawWireCube(Vector3.zero, new Vector3(newVolumeSize, newVolumeSize, newVolumeSize));
        }

        private void OnDisable()
        {
            // force primitive volume size gizmos to be hidden next time it is edited
            var primitive = Primitive;
            if (primitive != null) primitive.ShowVolumeSizeGizmo = false;
        }
    }
}