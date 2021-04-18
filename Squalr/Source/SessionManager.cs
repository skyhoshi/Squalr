namespace Squalr.Source
{
    using Squalr.Engine;
    using Squalr.Engine.Projects;

    public static class SessionManager
    {
        private static Session session;

        public static Session Session
        {
            get
            {
                return SessionManager.session;
            }

            set
            {
                SessionManager.session = value;
                SessionManager.OnSessionChangedEvent.Invoke(value);
            }
        }

        public static Project Project { get; set; }


        public delegate void OnSessionChanged(Session session);

        public static event OnSessionChanged OnSessionChangedEvent;
    }
    //// End class
}
//// End namespace
