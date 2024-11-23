using System;
using System.Collections.Generic;
using FMOD.Studio;

namespace FMODHelpers
{
    [Serializable]
    public class FMODParameterizedEvent
    {
        #region Public Fields
        public FMODEventRef EventRef;
        public List<FMODParameterLocal> DefaultLocalParameters;
        #endregion

        #region Public API
        public void Reset()
        {
            EventRef = null;
            DefaultLocalParameters = new();
        }

        public bool IsValid() => EventRef != null;

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
