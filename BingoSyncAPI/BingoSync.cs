using BingoSyncAPI.NetworkHandler;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using static BingoSyncAPI.NetworkHandler.HttpWebHandler;
using static BingoSyncAPI.BingoSyncTypes;
using System.Text.RegularExpressions;

namespace BingoSyncAPI
{
    /// <summary>
    /// Class for communicating with BingoSync.
    /// </summary>
    public class BingoSync
    {
        const string URL_BingoSync              = "https://bingosync.com/";
        const string URL_BignoSyncBoard         = URL_BingoSync + "room/{0}/board";
        const string URL_BignoSyncFeed          = URL_BingoSync + "room/{0}/feed";
        const string URL_BignoSyncDisconnect    = URL_BingoSync + "room/{0}/disconnect";
        const string URL_BignoSyncSettings      = URL_BingoSync + "room/{0}/room-settings";

        const string URL_API_Select             = URL_BingoSync + "api/select";     // Select on board
        const string URL_API_Chat               = URL_BingoSync + "api/chat";       // Send chat message
        const string URL_API_Color              = URL_BingoSync + "api/color";      // Set player color
        const string URL_API_Reveal             = URL_BingoSync + "api/revealed";   // Reveal board
        const string URL_API_NewCard            = URL_BingoSync + "api/new-card";   // Create new card
        const string URL_API_JoinRoom           = URL_BingoSync + "api/join-room";  // Join a room

        readonly Uri WSS_BingoSync = new Uri("wss://sockets.bingosync.com/broadcast");

        private WebSocketHandler socketHandler;

        /// <summary>
        /// If to show Debug info in console about web requests or not.
        /// </summary>
        public static bool DebugMode = false;

        /// <summary>
        /// Sends an event signal with the <see cref="SocketMessage"/> when a websocket message is made in the connected room.
        /// </summary>
        public event MessageReceived OnMessageReceived;

        /// <summary/>
        public delegate void MessageReceived(SocketMessage socketMessage);

        /// <summary>
        /// The current session cookies used in the connected room.
        /// </summary>
        public CookieContainer SessionCookies { get; private set; }

        /// <summary>
        /// The current connection status to a room.
        /// </summary>
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// If there is any type of connection being made to the room, Connected, Connecting, or Disconnecting.
        /// </summary>
        public bool HasAnyConnection => Status == ConnectionStatus.Connected || Status == ConnectionStatus.Connecting || Status == ConnectionStatus.Disconnecting;

        /// <summary>
        /// The currently connected rooms info.
        /// </summary>
        public RoomInfo CurrentRoomInfo { get; private set; }

        /// <summary>
        /// Connection status types.
        /// </summary>
        public enum ConnectionStatus
        {
            /// <summary/>
            Connected,
            /// <summary/>
            Connecting,
            /// <summary/>
            Disconnecting,
            /// <summary/>
            Disconnected
        }

        private bool            _boardUpdating = false;
        private bool            _boardAwaiting = false;
        private string          _board;
        private SlotInfo[]      _boardSlots;
        private RoomSettings    _roomSettings;
        private bool            _waitForColor = false;

        private void MessagePassthrough(string message)
        {
            if (Status != ConnectionStatus.Disconnected)
            {
                if (message == "Socket Closed")
                {
                    LeaveRoom();
                    return;
                }

                SocketMessage socketMessage = new SocketMessage();

                try
                {
                    socketMessage = Json.Net.JsonNet.Deserialize<SocketMessage>(message);
                    socketMessage.originalMsg = message;
                }
                catch
                {
                    return;
                }

                if (socketMessage.type == "new-card")
                {
                    _boardUpdating = true;
                    UpdateBoard();
                }
                else if (socketMessage.type == "goal")
                {
                    string slot = socketMessage.square?.slot;

                    if (slot != null)
                        UpdateBoardSlot(socketMessage.square);
                }
                
                OnMessageReceived?.Invoke(socketMessage);
            }
        }


        /// <summary>
        /// Attempts to communicate with the specified <paramref name="URL"/> using cookies created from joining a room, and returns its response, if specified by <paramref name="returnResponse"/>.
        /// </summary>
        ///
        /// <remarks>
        /// This is mainly useful for things like getting custon JSON to use on a card from a URL but otherwise usually isn't manually necessary for normal interaction on BingoSync yourself.
        /// </remarks>
        /// 
        /// <param name="URL"           >The web URL that will be communicated with</param>
        /// <param name="returnResponse">If true, will attempt to read the response from the URL</param>
        /// <param name="post"          >A post request to send with the URL request, often in the format of JSON</param>
        ///
        /// <returns>
        /// The response from <paramref name="URL"/>, if <paramref name="returnResponse"/> is <see langword="True"/>, such as its HTML or raw text data, or specific data based on the post request. Else, returns <see cref="string.Empty"/>.
        /// </returns>
        public async Task<string> GetResponse(string URL, bool returnResponse, string post = null)
        {
            if (SessionCookies == null)
                return string.Empty;

            return await TryGetResponse(URL, returnResponse, SessionCookies, post);
        }

        /// <summary>
        /// Takes in HTML in the expected format of <see href="https://bingosync.com/">BingoSync</see>'s homepage and searches for the supplied <paramref name="gameTitle"/> and matching <paramref name="variantTitle"/> for their IDs.
        /// </summary>
        ///
        /// <remarks>
        /// If <paramref name="suppliedHTML"/> is <see langword="null"/>, it will attempt a new request to <see href="https://bingosync.com/">BingoSync</see> for its HTML.
        /// <para/><b>Note:</b> This is generally an unnecessary way to get IDs unless you have some type of need to dynamically obtain new IDs you can't supply yourself, since these should always be the same pair, but may be useful for finding the IDs in the first place.
        /// <para/>The variant must match one of the available variants given for the specified game, as each one has a seperate group it's linked to based on the games ID.
        /// </remarks>
        /// 
        /// <param name="gameTitle"     >The full game title from the Game category on BingoSync</param>
        /// <param name="variantTitle"  >The full variant title from the Variant category shown with the selected game on BingoSync</param>
        /// <param name="suppliedHTML"  >If supplied, it will use this HTML to search with rather than attempt a new web request for it</param>
        ///
        /// <returns>
        /// The internal IDs of the game and variant, in the form of <see cref="CardIDs"/>, or <see langword="null"/> if it failed to find a match.
        /// </returns>
        public async Task<CardIDs> GetCardIDs(string gameTitle, string variantTitle = "Normal", string suppliedHTML = null)
        {
            int gameID      = 0;
            int variantID   = 0;

            string response = suppliedHTML;

            if (suppliedHTML == null)
                response = await GetResponse(URL_BingoSync, true);

            if (response == null || response == string.Empty)
                return null;

            try
            {
                Match gameMatch = Regex.Match(response, $"value=\"(?<=)(.*?)(?=)\".?>{Regex.Escape(gameTitle).Trim()}</option>", RegexOptions.RightToLeft | RegexOptions.IgnoreCase);

                if (gameMatch.Groups.Count < 2 || !int.TryParse(gameMatch.Groups[1].Value, out gameID))
                    return null;

                Match variantMatch = Regex.Match(response, $"value=\"(?<=)(.*?)(?=)\".?data-group=\"(?<=){gameID}(?=)\".*?>{Regex.Escape(variantTitle).Trim()}</option>", RegexOptions.RightToLeft | RegexOptions.IgnoreCase);

                if (variantMatch.Groups.Count < 2 || !int.TryParse(variantMatch.Groups[1].Value, out variantID))
                    return null;

                return new CardIDs(gameID, variantID);
            }
            catch
            {
                return null;
            }
        }


        /// <summary>
        /// Joins a BingoSync room using the <see cref="RoomInfo"/> provided information.
        /// </summary>
        ///
        /// <remarks>
        /// </remarks>
        /// 
        /// <param name="roomInfo">The rooms info to join from</param>
        ///
        /// <returns>
        /// The final <see cref="ConnectionStatus"/> on attempting to join the room, where <see cref="ConnectionStatus.Connected"/> is a success and <see cref="ConnectionStatus.Disconnected"/> failed.
        /// </returns>
        public async Task<ConnectionStatus> JoinRoom(RoomInfo roomInfo)
        {
            if (HasAnyConnection)
                return Status;

            Status = ConnectionStatus.Connecting;
            CurrentRoomInfo = null;

            SessionCookies = await TryGetCookies(TryGetRequest(URL_BingoSync));

            if (SessionCookies == null)
                return Status = ConnectionStatus.Disconnected;

            string response = await GetResponse(URL_API_JoinRoom, true, BingoSyncPost.JoinRoom(roomInfo.RoomID, roomInfo.PlayerName, roomInfo.RoomPassword, roomInfo.Spectator));

            if (response == string.Empty)
                return Status = ConnectionStatus.Disconnected;
            
            socketHandler = new WebSocketHandler(WSS_BingoSync, response, MessagePassthrough);
            WebSocketHandler.ConnectionStatus socketStatus = await socketHandler.StartSocket();

            if (socketStatus == WebSocketHandler.ConnectionStatus.Connected)
            {
                CurrentRoomInfo = roomInfo;

                _boardUpdating = true;
                await UpdateBoardAwait();

                _waitForColor = true;
                await SetPlayerColor(CurrentRoomInfo.PlayerColor);

                Status = ConnectionStatus.Connected;
            }
            else
            {
                Status = ConnectionStatus.Disconnected;
            }

            return Status;
        }


        /// <summary>
        /// If connected to a room, sets the players current color, and updates the <see cref="CurrentRoomInfo"/>.
        /// </summary>
        ///
        /// <remarks>
        /// </remarks>
        /// 
        /// <param name="playerColor">The new player color</param>
        public async Task SetPlayerColor(PlayerColors playerColor)
        {
            if (Status == ConnectionStatus.Connected || _waitForColor)
            {
                string response = await GetResponse(URL_API_Color, true, BingoSyncPost.SetColor(CurrentRoomInfo.RoomID, playerColor.ToString().ToLower()));

                if (response != string.Empty)
                    CurrentRoomInfo.PlayerColor = playerColor;

                _waitForColor = false;
            }
        }

        /// <summary>
        /// If connected to a room, sends the specified <paramref name="message"/> to the chat.
        /// </summary>
        ///
        /// <remarks>
        /// </remarks>
        /// 
        /// <param name="message">The chat message to send</param>
        public async Task SendChatMessage(string message)
        {
            if (Status == ConnectionStatus.Connected)
                await GetResponse(URL_API_Chat, false, BingoSyncPost.SendChat(CurrentRoomInfo.RoomID, message));
        }

        /// <summary>
        /// Sends a request to reveal the board for this player.
        /// </summary>
        ///
        /// <remarks>
        /// This is not required to interact with the board, and only really has a function if you have some type of visual connection tied.
        /// </remarks>
        public async Task RevealBoard()
        {
            if (Status == ConnectionStatus.Connected)
                await GetResponse(URL_API_Reveal, false, BingoSyncPost.RevealBoard(CurrentRoomInfo.RoomID));
        }


        /// <summary>
        /// Selects and marks/unmarks a <paramref name="slot"/> on the board based on <paramref name="markState"/> and if the <paramref name="color"/> can apply to the request.
        /// </summary>
        ///
        /// <remarks>
        /// The server-side request also checks if the player making the request is spectator or not.
        /// </remarks>
        /// 
        /// <param name="slot">The slot on the board to select, starting from top left to bottom right</param>
        /// <param name="markState">If the state of the selected slot for this <paramref name="color"/> should be on or off</param>
        /// <param name="color">The color to use in the request. If <see langword="null"/> this will use your current player color from <see cref="CurrentRoomInfo"/></param>
        public async Task SelectSlot(int slot, bool markState = true, PlayerColors? color = null)
        {
            if (Status != ConnectionStatus.Connected)
                return;

            RoomSettings settings = await GetRoomSettings();

            if (settings == null)
                return;

            PlayerColors colorToUse = color == null ? CurrentRoomInfo.PlayerColor : (PlayerColors)color;

            SlotInfo currentSlot = await GetBoardSlot(slot);

            bool flag           = false;
            bool isNotMarked    = currentSlot != null && currentSlot != default(SlotInfo) && currentSlot.Colors.Contains(null);

            if (markState)
                flag = (settings.lockout_mode != "Lockout" && !currentSlot.Colors.Contains(colorToUse)) || (settings.lockout_mode == "Lockout" && isNotMarked);
            else
                flag = !isNotMarked && currentSlot.Colors.Contains(colorToUse);

            if (flag)
                await GetResponse(URL_API_Select, false, BingoSyncPost.Select(CurrentRoomInfo.RoomID, slot.ToString(), colorToUse.ToString().ToLower(), !markState));
        }


        /// <summary>
        /// Sends a request to create a new bingo card with the supplied room settings.
        /// </summary>
        ///
        /// <remarks>
        /// </remarks>
        /// 
        /// <param name="lockout_mode"  >If the card should be in Lockout mode or not</param>
        /// <param name="hide_card"     >If the card should be hidden and need revealing</param>
        /// <param name="cardIDs"       >The IDs of the game and variant to use</param>
        /// <param name="seed"          >The seed to use for the new card, where if -1, will use a random seed</param>
        /// <param name="custom_json"   >Custom JSON to pass for the boards creation, primarily used in Custom games</param>
        public async Task CreateNewCard(bool lockout_mode, bool hide_card, CardIDs cardIDs, int seed = -1, string custom_json = "")
        {
            if (cardIDs == null)
                return;

            if (Status == ConnectionStatus.Connected)
                await GetResponse(URL_API_NewCard, false, BingoSyncPost.NewCard(CurrentRoomInfo.RoomID, lockout_mode ? "2" : "1", hide_card, seed <= -1 ? "" : Math.Abs(seed).ToString(), custom_json, cardIDs.GameID.ToString(), cardIDs.VariantID.ToString()));
        }


        /// <summary>
        /// Gets the current boards slots.
        /// </summary>
        ///
        /// <remarks>
        /// If any updates are being made to the board, it will wait for those updates to be made before returning the result.
        /// </remarks>
        /// 
        /// <returns>
        /// An array of slots in the form of <see cref="SlotInfo"/>'s last updated by the websocket.
        /// </returns>
        public async Task<SlotInfo[]> GetBoardSlots()
        {
            if (Status == ConnectionStatus.Connected)
            {
                while (_boardUpdating) { await Task.Yield(); }
                return _boardSlots;
            }
            return Array.Empty<SlotInfo>();
        }


        /// <summary>
        /// Gets the <see cref="SlotInfo"/> of that <paramref name="slot"/> ID.
        /// </summary>
        ///
        /// <remarks>
        /// If any updates are being made to the board, it will wait for those updates to be made before returning the result.
        /// </remarks>
        /// 
        /// <param name="slot">The slot to get, starting from top left to bottom right</param>
        /// 
        /// <returns>
        /// The <see cref="SlotInfo"/> of the specified <paramref name="slot"/>.
        /// </returns>
        public async Task<SlotInfo> GetBoardSlot(int slot)
        {
            if (Status == ConnectionStatus.Connected)
            {
                while (_boardUpdating) { await Task.Yield(); }
                return _boardSlots?.FirstOrDefault(x => x.ID == slot);
            }
            return null;
        }


        /// <summary>
        /// Gets the current <see cref="RoomSettings"/>.
        /// </summary>
        ///
        /// <remarks>
        /// If any updates are being made to the board, it will wait for those updates to be made before returning the result.
        /// </remarks>
        /// 
        /// <returns>
        /// The current <see cref="RoomSettings"/> last updated by the websocket.
        /// </returns>
        public async Task<RoomSettings> GetRoomSettings()
        {
            if (Status == ConnectionStatus.Connected)
            {
                while (_boardUpdating) { await Task.Yield(); }
                return _roomSettings;
            }
            return null;
        }


        /// <summary>
        /// Attempts to retrieve the rooms feed.
        /// </summary>
        ///
        /// <remarks>
        /// </remarks>
        /// 
        /// <param name="full">If to get the full history of the rooms feed or not</param>
        /// 
        /// <returns>
        /// The rooms feed in the form of JSON, or <see cref="string.Empty"/> if failed.
        /// </returns>
        public async Task<string> GetFeed(bool full = false)
        {
            if (Status == ConnectionStatus.Connected)
                return await GetFromURL($"{URL_BignoSyncFeed}?full={full}");
            return string.Empty;
        }


        /// <summary>
        /// Disconnects from the room and closes the sockets.
        /// </summary>
        ///
        /// <remarks>
        /// </remarks>
        public async Task Disconnect()
        {
            if (Status == ConnectionStatus.Connected)
            {
                Status = ConnectionStatus.Disconnecting;

                await GetFromURL(URL_BignoSyncSettings, false);

                if (socketHandler != null && socketHandler.Status == WebSocketHandler.ConnectionStatus.Connected)
                    await socketHandler.CloseSocket();

                Status = ConnectionStatus.Disconnected;
            }
        }

        private async void UpdateBoard()
        {
            await UpdateBoardAwait();
        }

        private async Task UpdateBoardAwait()
        {
            while (_boardAwaiting) await Task.Yield();

            _boardAwaiting  = true;

            _roomSettings   = await UpdateRoomSettings();
            _boardSlots     = await UpdateBoardSlots();

            _boardUpdating  = false;
            _boardAwaiting  = false;
        }

        private async Task<RoomSettings> UpdateRoomSettings()
        {
            string settings = await GetFromURL(URL_BignoSyncSettings, true, true);

            if (settings == string.Empty)
                return null;

            string search = "\"settings\":";
            settings = settings.Substring(settings.LastIndexOf(search));
            settings = settings.Substring(search.Length, settings.Length - search.Length - 1);

            try
            {
                RoomSettings roomSettings = Json.Net.JsonNet.Deserialize<RoomSettings>(settings);
                return roomSettings;
            }
            catch
            {
                return null;
            }
        }

        private async Task<SlotInfo[]> UpdateBoardSlots()
        {
            _board = await GetFromURL(URL_BignoSyncBoard, true, true);

            if (_board == string.Empty)
                return Array.Empty<SlotInfo>();

            try
            {
                SlotInfo[] slots = Json.Net.JsonNet.Deserialize<Slot[]>(_board).Select(x => new SlotInfo(x)).Cast<SlotInfo>().ToArray();
                return slots;
            }
            catch
            {
                return Array.Empty<SlotInfo>();
            }
        }

        private async void UpdateBoardSlot(SocketSlot socketSlot)
        {
            string slot = socketSlot.slot;

            if (slot.Contains("slot") && int.TryParse(slot.Replace("slot", ""), out int result))
            {
                SlotInfo slotInfo = await GetBoardSlot(result);

                if (slotInfo == null)
                    return;

                if (slotInfo.slot != null)
                {
                    slotInfo.slot.name   = socketSlot.name;
                    slotInfo.slot.colors = socketSlot.colors;
                }
            }
        }

        private async void LeaveRoom()
        {
            await Disconnect();
        }

        private async Task<string> GetFromURL(string URL, bool response = true, bool force = false)
        {
            if (Status == ConnectionStatus.Connected || force)
                return await GetResponse(string.Format(URL, CurrentRoomInfo.RoomID), response);

            return string.Empty;
        }
    }
}
