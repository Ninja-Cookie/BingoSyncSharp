using System;

namespace BingoSyncAPI
{
    internal static class DebugModeOutput
    {
        public static void WriteLine(string message)
        {
            if (BingoSync.DebugMode)
                Console.WriteLine(message);
        }
    }
}
