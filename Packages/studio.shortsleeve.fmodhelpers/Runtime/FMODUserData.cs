using System;
using System.Runtime.InteropServices;
using System.Threading;
using FMOD.Studio;

namespace FMODHelpers
{
    public class FMODUserData
    {
        #region Static
        static int IDCounter = 0;
        #endregion

        #region Private State
        int _ID;
        GCHandle _handle; // this is expected to exist until the program closes so we never free them
        Action<FMODUserData> _releaseUserDataAction;
        internal CancellationToken _callbackCancellationToken;
        #endregion

        #region Internal State
        internal FMODEventRef EventRef;
        internal EventInstance CurrentInstance;
        internal IFMODStudioEventHandler StudioEventCallbackHandler;
        #endregion

        #region Internal Properties
        internal GCHandle Handle => _handle;
        internal CancellationToken Cancellation => _callbackCancellationToken;
        #endregion

        #region Public Properties
        public int ID => _ID;

        // Use this for whatever you like
        public object CustomStateObject { get; set; }

        // Use this for whatever you like
        public IntPtr CustomStatePointer { get; set; }
        #endregion

        #region Constructor / Factory
        FMODUserData() { }

        public static FMODUserData Create(
            Action<FMODUserData> releaseUserDataAction,
            CancellationToken callbackCancellationToken
        )
        {
            FMODUserData data = new FMODUserData();
            data._ID = IDCounter++;
            data._handle = GCHandle.Alloc(data);
            data._releaseUserDataAction = releaseUserDataAction;
            data._callbackCancellationToken = callbackCancellationToken;
            return data;
        }
        #endregion

        #region Public API
        public void Release() => _releaseUserDataAction(this);

        public void Clear()
        {
            // This insures repeated calls from FMOD to the callback
            // handler don't mess with this user data which it doesn't
            // own anymore.
            CurrentInstance.setUserData(IntPtr.Zero);
            CurrentInstance.clearHandle();
            EventRef = null;
            StudioEventCallbackHandler = null;
            CustomStateObject = null;
            CustomStatePointer = IntPtr.Zero;
        }
        #endregion
    }
}
