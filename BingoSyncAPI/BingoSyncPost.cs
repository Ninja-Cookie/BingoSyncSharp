using Json.Net;

namespace BingoSyncAPI
{
    internal static class BingoSyncPost
    {
        public static string JoinRoom(string room, string nickname, string password, bool is_spectator = false)
        {
            return JsonNet.Serialize(new
            {
                room,
                nickname,
                password,
                is_specator = is_spectator // This spelling is what BingoSync checks for.
            });
        }

        public static string SetColor(string room, string color)
        {
            return JsonNet.Serialize(new
            {
                room,
                color
            });
        }

        public static string SendChat(string room, string text)
        {
            return JsonNet.Serialize(new
            {
                room,
                text
            });
        }

        public static string RevealBoard(string room)
        {
            return JsonNet.Serialize(new
            {
                room
            });
        }

        public static string Select(string room, string slot, string color, bool remove_color)
        {
            return JsonNet.Serialize(new
            {
                room,
                slot,
                color,
                remove_color
            });
        }

        public static string NewCard(string room, string lockout_mode, bool hide_card, string seed, string custom_json, string game_type, string variant_type)
        {
            return JsonNet.Serialize(new
            {
                hide_card,
                game_type,
                variant_type,
                custom_json,
                lockout_mode,
                seed,
                room
            });
        }
    }
}
