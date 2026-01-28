using System;
using System.Reflection;
using FMODUnity;
using UnityEditor;
using UnityEngine;

namespace FMODHelpers.Editor
{
    [CustomPropertyDrawer(typeof(FMODParameter))]
    public class FMODParameterDrawer : PropertyDrawer
    {
        #region State
        private FieldInfo _eventRefField;
        #endregion

        #region Unity Lifecycle
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Cache Reflection Data
            CacheReflectionData();

            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.indentLevel = 0;

            // Create Foldout
            float currentHeight = 0f;
            float currentPosition = position.min.y;
            Rect rectFoldout = GetRect(position, EditorGUIUtility.singleLineHeight, ref currentPosition, ref currentHeight);
            property.isExpanded = EditorGUI.Foldout(rectFoldout, property.isExpanded, label);
            int lines = 1;
            if (property.isExpanded)
            {
                // Indent
                EditorGUI.indentLevel++;

                // Grab Event Path
                SerializedProperty eventRefProperty = property.FindPropertyRelative(nameof(FMODParameter.EventRef));
                Rect eventPathRect = GetRect(position, EditorGUI.GetPropertyHeight(eventRefProperty), ref currentPosition, ref currentHeight);
                EditorGUI.PropertyField(eventPathRect, eventRefProperty);

                // Grab Parameter
                SerializedProperty parameterProperty = property.FindPropertyRelative(nameof(FMODParameter.ParameterName));

                // Draw Dropdown
                FMOD.GUID guid = FMODEditorUtilities.ExtractGuidFromEventReference(eventRefProperty);
                if (!FMODEditorUtilities.IsGuidNull(guid) && EventManager.IsInitialized)
                {
                    // Check if Event Exists
                    EditorEventRef eventRef = EventManager.EventFromGUID(guid);
                    if (eventRef != null && eventRef.LocalParameters.Count > 0)
                    {
                        // Load Current Value
                        string currentParameterName = parameterProperty.stringValue;

                        // Create List
                        int selectedIndex = 0;
                        string[] parameterNames = new string[eventRef.LocalParameters.Count];
                        for (int i = 0; i < eventRef.LocalParameters.Count; i++)
                        {
                            string parameterName = eventRef.LocalParameters[i].Name;
                            if (parameterName == currentParameterName)
                                selectedIndex = i;
                            parameterNames[i] = eventRef.LocalParameters[i].Name;
                        }

                        // Parameter Dropdown
                        Rect rectDropdown = GetRect(position, EditorGUIUtility.singleLineHeight, ref currentPosition, ref currentHeight);
                        selectedIndex = EditorGUI.Popup(rectDropdown, "Parameter Name", selectedIndex, parameterNames);

                        // Update Serialized Parameter Name
                        parameterProperty.stringValue = parameterNames[selectedIndex];
                    }
                }
                else
                {
                    lines += 1;
                    parameterProperty.stringValue = string.Empty;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
        #endregion

        #region Private API
        void CacheReflectionData()
        {
            _eventRefField ??= GetField(typeof(FMODParameter), nameof(FMODParameter.EventRef), BindingFlags.Instance | BindingFlags.NonPublic);
        }

        Rect GetRect(Rect position, float height, ref float currentPosition, ref float currentHeight)
        {
            currentHeight = height;
            Rect rect = EditorGUI.IndentedRect(new Rect(position.min.x, currentPosition, position.size.x, currentHeight));
            currentPosition += currentHeight;
            return rect;
        }

        static FieldInfo GetField(Type @type, string name, BindingFlags flags)
        {
            if (@type == null)
                return null;

            FieldInfo fieldInfo = @type.GetField(name, flags);
            if (fieldInfo != null)
                return fieldInfo;

            return GetField(@type.BaseType, name, flags);
        }
        #endregion
    }
}
