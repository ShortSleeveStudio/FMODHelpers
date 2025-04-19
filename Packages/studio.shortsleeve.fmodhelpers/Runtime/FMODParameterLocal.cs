using System;

namespace FMODHelpers
{
    [Serializable]
    public class FMODParameterLocal
        : IEquatable<FMODParameterLocal>,
            IComparable<FMODParameterLocal>
    {
        [ReadOnly]
        public string name;
        public float value;
        public bool skipSeek;

        public static FMODParameterLocal Create(string name)
        {
            FMODParameterLocal parameter = new();
            parameter.name = name;
            parameter.value = 0f;
            parameter.skipSeek = false;
            return parameter;
        }

        public int CompareTo(FMODParameterLocal other) =>
            GetComparerString().CompareTo(other.GetComparerString());

        public override int GetHashCode() => GetComparerString().GetHashCode();

        public bool Equals(FMODParameterLocal other) =>
            GetComparerString().Equals(other.GetComparerString());

        private string GetComparerString()
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            else
                return name;
        }
    }
}
