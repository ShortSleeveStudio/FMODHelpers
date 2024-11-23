using UnityEngine;

namespace FMODHelpers
{
    public abstract class FMODRef : ScriptableObject
    {
        #region Public Properties
        public abstract object ID { get; }
        #endregion
    }
}
