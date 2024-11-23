namespace FMODHelpers
{
    public class FMODEventRef : FMODRef
    {
        #region Public Fields
        public FMOD.GUID Guid;
        public string Path;
        public FMODBankRef[] Banks;
        #endregion

        #region Public Properties
        public override object ID => Guid;
        #endregion
    }
}
