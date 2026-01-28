using FMODUnity;
using UnityEditor;
using UnityEngine;

namespace FMODHelpers.Editor
{
    [CustomPropertyDrawer(typeof(FMODParameterizedEvent))]
    public class FMODParameterizedEventDrawer : PropertyDrawer
    {
        #region Unity Lifecycle
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            // Validate Event
            SerializedProperty eventReferenceProperty = property.FindPropertyRelative(nameof(FMODParameterizedEvent.EventRef));
            SerializedProperty defaultLocalParametersProperty = property.FindPropertyRelative(nameof(FMODParameterizedEvent.DefaultLocalParameters));

            // Extract GUID from EventReference struct
            FMOD.GUID guid = FMODEditorUtilities.ExtractGuidFromEventReference(eventReferenceProperty);
            if (!FMODEditorUtilities.IsGuidNull(guid) && EventManager.IsInitialized)
            {
                // Check if Event Exists
                EditorEventRef eventRef = EventManager.EventFromGUID(guid);
                if (eventRef != null)
                {
                    // Ensure Correct List Size
                    if (eventRef.LocalParameters.Count != defaultLocalParametersProperty.arraySize)
                    {
                        defaultLocalParametersProperty.ClearArray();
                        for (int i = 0; i < eventRef.LocalParameters.Count; i++)
                        {
                            defaultLocalParametersProperty.InsertArrayElementAtIndex(i);
                        }
                    }
                    else
                    {
                        // Size Is Correct, Ensure All Parameters Exist
                        int j = 0;
                        foreach (EditorParamRef param in eventRef.LocalParameters)
                        {
                            SerializedProperty paramProperty = defaultLocalParametersProperty.GetArrayElementAtIndex(j++);
                            SerializedProperty nameProperty = paramProperty.FindPropertyRelative(nameof(FMODParameterLocal.name));
                            // Reset non-matching list items
                            if (param.Name != nameProperty.stringValue)
                            {
                                nameProperty.stringValue = param.Name;
                                SerializedProperty valueProperty = paramProperty.FindPropertyRelative(nameof(FMODParameterLocal.value));
                                valueProperty.floatValue = param.Default;
                                SerializedProperty skipSeekProperty = paramProperty.FindPropertyRelative(nameof(FMODParameterLocal.skipSeek));
                                skipSeekProperty.boolValue = false;
                            }
                        }
                    }
                }
            }
            else
            {
                // Reset Parameters
                defaultLocalParametersProperty.ClearArray();
            }

            // Draw Default
            EditorGUI.PropertyField(position, property, label, true);
            EditorGUI.EndProperty();
        }
        #endregion

    }
}
