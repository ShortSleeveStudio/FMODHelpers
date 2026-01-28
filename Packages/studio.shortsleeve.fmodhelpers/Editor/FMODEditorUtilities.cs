using UnityEditor;

namespace FMODHelpers.Editor
{
    public static class FMODEditorUtilities
    {
        public static FMOD.GUID ExtractGuidFromEventReference(SerializedProperty eventRefProperty)
        {
            SerializedProperty guidProperty = eventRefProperty.FindPropertyRelative("Guid");
            if (guidProperty == null)
                return new();

            FMOD.GUID guid = new();
            SerializedProperty data1 = guidProperty.FindPropertyRelative("Data1");
            SerializedProperty data2 = guidProperty.FindPropertyRelative("Data2");
            SerializedProperty data3 = guidProperty.FindPropertyRelative("Data3");
            SerializedProperty data4 = guidProperty.FindPropertyRelative("Data4");

            if (data1 != null) guid.Data1 = data1.intValue;
            if (data2 != null) guid.Data2 = data2.intValue;
            if (data3 != null) guid.Data3 = data3.intValue;
            if (data4 != null) guid.Data4 = data4.intValue;

            return guid;
        }

        public static bool IsGuidNull(FMOD.GUID guid)
        {
            return guid.Data1 == 0 && guid.Data2 == 0 && guid.Data3 == 0 && guid.Data4 == 0;
        }
    }
}
