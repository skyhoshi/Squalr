namespace Squalr
{
    using System;

    public static class SqualrSettings
    {
        public static Boolean AutomaticUpdates
        {
            get
            {
                return Properties.Settings.Default.AutomaticUpdates;
            }

            set
            {
                Properties.Settings.Default.AutomaticUpdates = value;
            }
        }
    }
    //// End class
}
//// End namespace