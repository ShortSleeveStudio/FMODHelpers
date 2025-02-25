using System;
using System.Threading;
using FMOD;
using FMOD.Studio;

namespace FMODHelpers
{
    public class FMODUserData
    {
        #region Public State
        // Event
        public FMODEventRef EventRef;

        // Dialogue Parameters
        public Sound FmodSound;
        public SOUND_INFO FmodSoundInfo;

        #endregion

        #region Private State
        CancellationToken _callbackCancellationToken;
        Action<FMODUserData> _releaseUserDataAction;
        Action<FMODCallbackData> _userDataHandler;
        #endregion

        #region Constructor
        public FMODUserData(
            Action<FMODUserData> releaseUserDataAction,
            CancellationToken callbackCancellationToken
        )
        {
            _releaseUserDataAction = releaseUserDataAction;
            _callbackCancellationToken = callbackCancellationToken;
        }
        #endregion

        #region Public Properties
        public CancellationToken CallbackCancellationToken => _callbackCancellationToken;
        public Action<FMODCallbackData> UserCallbackHandler
        {
            get { return _userDataHandler; }
            set
            {
                if (_userDataHandler != null)
                    throw new Exception("Tried to set user callback handler twice");
                _userDataHandler = value;
            }
        }
        #endregion

        #region Public API
        public void Release() => _releaseUserDataAction(this);

        public void Clear()
        {
            EventRef = null;
            _userDataHandler = null;
        }
        #endregion
    }
}
