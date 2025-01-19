using log4net;
using WvsBeta.Database;

namespace WvsBeta.Center.DBAccessor
{
    public partial class CharacterDBAccessor
    {
        private static ILog _log = LogManager.GetLogger(typeof(CharacterDBAccessor));
        private static MySQL_Connection _characterDatabaseConnection;

        public static void InitializeDB(MySQL_Connection characterDatabaseConnection)
        {
            _characterDatabaseConnection = characterDatabaseConnection;
        }
    }
}
