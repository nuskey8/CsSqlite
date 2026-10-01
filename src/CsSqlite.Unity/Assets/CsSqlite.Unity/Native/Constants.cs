namespace CsSqlite
{
    public static class Constants
    {
        public const int SQLITE_OK = 0;
        public const int SQLITE_ERROR = 1;
        public const int SQLITE_MISUSE = 21;
        public const int SQLITE_RANGE = 25;
        public const int SQLITE_ROW = 100;
        public const int SQLITE_DONE = 101;
        public const int SQLITE_INTEGER = 1;
        public const int SQLITE_FLOAT = 2;
        public const int SQLITE_TEXT = 3;
        public const int SQLITE_BLOB = 4;
        public const int SQLITE_NULL = 5;

        public unsafe static readonly delegate* unmanaged[Cdecl]<void*, void> SQLITE_STATIC = (delegate* unmanaged[Cdecl]<void*, void>)0;
        public unsafe static readonly delegate* unmanaged[Cdecl]<void*, void> SQLITE_TRANSIENT = (delegate* unmanaged[Cdecl]<void*, void>)-1;
    }
}