using System;
using System.Linq;

namespace BingoSyncAPI
{
    #pragma warning disable 1591

    /// <summary>
    /// General types used by <see cref="BingoSync"/>.
    /// </summary>
    public class BingoSyncTypes
    {
        /// <summary>
        /// Linked IDs used in card generation.
        /// </summary>
        [Serializable]
        public class CardIDs
        {
            /// <summary>BingoSync's internal Game ID.</summary>
            public int GameID;
            /// <summary>BingoSync's internal Variant ID linked with this <see cref="GameID"/>.</summary>
            public int VariantID;

            public CardIDs (int GameID, int VariantID)
            {
                this.GameID     = GameID;
                this.VariantID  = VariantID;
            }
        }

        [Serializable]
        internal class Slot
        {
            public string name;
            public string slot;
            public string colors;
        }

        /// <summary>
        /// Info related to a slot on the board.
        /// </summary>
        [Serializable]
        public class SlotInfo
        {
            internal Slot slot;

            /// <summary>The description text shown on this slot.</summary>
            public string           Info    => slot.name;
            /// <summary>The ID of this slot.</summary>
            public int              ID      => int.Parse(slot.slot.Replace("slot", ""));
            /// <summary>The colors this slot has on them. If blank, will return an array only containing <see langword="null"/>.</summary>
            public PlayerColors?[]  Colors
            {
                get
                {
                    if (slot.colors.Equals("blank"))
                        return new PlayerColors?[1] { null };

                    string[] colorSplit = new string[1] { slot.colors };

                    if (slot.colors.Contains(' '))
                        colorSplit = slot.colors.Split(' ');

                    return colorSplit.Select(x => Enum.TryParse<PlayerColors>(x.Substring(0, 1).ToUpper() + x.Substring(1, x.Length - 1).ToLower(), out PlayerColors foundColor) ? (PlayerColors?)foundColor : null).Cast<PlayerColors?>().ToArray();
                }
            }

            internal SlotInfo(Slot slot)
            {
                this.slot = slot;
            }
        }

        /// <summary>
        /// Info related to the settings of the current room.
        /// </summary>
        [Serializable]
        public class RoomSettings
        {
            public bool     hide_card       { get; internal set; }
            public string   lockout_mode    { get; internal set; }
            public string   game            { get; internal set; }
            public int      game_id         { get; internal set; }
            public string   variant         { get; internal set; }
            public int      variant_id      { get; internal set; }
            public int      seed            { get; internal set; }
        }

        /// <summary>
        /// Varying info sent from the socket, where <see cref="type"/> should always return the type of socket message.
        /// </summary>
        /// 
        /// <remarks>
        /// <b>Example:</b> If the type is "goal", then info such as <see cref="player"/> (Info of who called this) and <see cref="square"/> (Info of which slot was effected) will be filled.
        /// </remarks>
        [Serializable]
        public class SocketMessage
        {
            public string       originalMsg { get; internal set; }
            public string       game        { get; internal set; }
            public bool         hide_card   { get; internal set; }
            public bool         is_current  { get; internal set; }
            public string       seed        { get; internal set; }
            public string       type        { get; internal set; }
            public string       event_type  { get; internal set; }
            public SocketPlayer player      { get; internal set; }
            public SocketSlot   square      { get; internal set; }
            public string       player_color{ get; internal set; }
            public string       color       { get; internal set; }
            public bool         remove      { get; internal set; }
            public double       timestamp   { get; internal set; }
            public string       room        { get; internal set; }
            public string       text        { get; internal set; }
            public string       socket_key  { get; internal set; }
        }

        /// <summary>
        /// Info about who called a socket message.
        /// </summary>
        [Serializable]
        public class SocketPlayer
        {
            public string   uuid            { get; internal set; }
            public string   name            { get; internal set; }
            public string   color           { get; internal set; }
            public bool     is_spectator    { get; internal set; }
        }

        /// <summary>
        /// Info about an affected slot from a socket message.
        /// </summary>
        [Serializable]
        public class SocketSlot
        {
            public string name              { get; internal set; }
            public string slot              { get; internal set; }
            public string colors            { get; internal set; }
        }

        /// <summary>
        /// Info about a rooms connection.
        /// </summary>
        [Serializable]
        public class RoomInfo
        {
            /// <summary>The rooms ID (values shown after /room/ in the URL)</summary>
            public string       RoomID          { get; private  set; }
            /// <summary>The rooms password.</summary>
            public string       RoomPassword    { get; private  set; }
            /// <summary>The name of the player for the instance of their connection.</summary>
            public string       PlayerName      { get; private  set; }
            /// <summary>The players color to be shown on the board or feed.</summary>
            public PlayerColors PlayerColor     { get; internal set; }
            /// <summary>If the instance of this player is a spectator or not.</summary>
            public bool         Spectator       { get; private  set; }

            public RoomInfo(string RoomID, string RoomPassword, string PlayerName, PlayerColors PlayerColor, bool Spectator)
            {
                this.RoomID         = RoomID;
                this.RoomPassword   = RoomPassword;
                this.PlayerName     = PlayerName;
                this.PlayerColor    = PlayerColor;
                this.Spectator      = Spectator;
            }
        }

        /// <summary>
        /// The player colors available on BingoSync.
        /// </summary>
        [Serializable]
        public enum PlayerColors
        {
            Orange,
            Red,
            Blue,
            Green,
            Purple,
            Navy,
            Teal,
            Brown,
            Pink,
            Yellow
        }
    }
}
