using System;

namespace FMODHelpers
{
    /// <summary>
    /// Represents a local FMOD parameter with name, value, and seek behavior.
    /// Equality and comparison are based ONLY on the parameter name - value and skipSeek are ignored.
    /// This allows parameters with the same name but different values to be treated as "the same parameter"
    /// for purposes of collections like HashSet or Dictionary.
    /// </summary>
    [Serializable]
    public class FMODParameterLocal
        : IEquatable<FMODParameterLocal>,
            IComparable<FMODParameterLocal>
    {
        /// <summary>
        /// Parameter name as defined in FMOD Studio (read-only in inspector).
        /// </summary>
        [ReadOnly]
        public string name;

        /// <summary>
        /// Current parameter value.
        /// </summary>
        public float value;

        /// <summary>
        /// If true, parameter changes happen instantly. If false, timeline seeks to match the parameter value.
        /// </summary>
        public bool skipSeek;

        public FMODParameterLocal() { }

        public FMODParameterLocal(string name)
        {
            this.name = name;
            this.value = 0f;
            this.skipSeek = false;
        }

        /// <summary>
        /// Compares based on name only.
        /// </summary>
        public int CompareTo(FMODParameterLocal other) =>
            GetComparerString().CompareTo(other.GetComparerString());

        /// <summary>
        /// Hash code based on name only.
        /// </summary>
        public override int GetHashCode() => GetComparerString().GetHashCode();

        /// <summary>
        /// Equality based on name only. Two parameters with the same name but different values are considered equal.
        /// </summary>
        public bool Equals(FMODParameterLocal other) =>
            GetComparerString().Equals(other.GetComparerString());

        private string GetComparerString() => name ?? string.Empty;
    }
}
