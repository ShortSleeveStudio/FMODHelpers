using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;

namespace FMODHelpers
{
    [Serializable]
    public class FMODParameterizedEvent
    {
        #region Public Fields
        public EventReference EventRef;
        public List<FMODParameterLocal> DefaultLocalParameters;
        #endregion

        #region Public API
        public void Reset()
        {
            EventRef = new();
            DefaultLocalParameters = new();
        }

        public bool IsValid() => !EventRef.IsNull;

        public void ApplyParameters(EventInstance instance)
        {
            foreach (FMODParameterLocal param in DefaultLocalParameters)
            {
                instance.SetParameter(param.name, param.value, param.skipSeek);
            }
        }
        #endregion
    }
}
