namespace FMODHelpers
{
    public class FMODBankRef : FMODRef
    {
        #region Public Fields
        public string Name;
        public string StudioPath;
        #endregion

        #region Public Properties
        public override object ID => StudioPath;
        #endregion
    }
}
