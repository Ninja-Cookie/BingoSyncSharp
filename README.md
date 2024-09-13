# BingoSyncSharp
A simple C# [BingoSync](https://bingosync.com/) API interactor, for easy interactions with [BingoSync](https://bingosync.com/) rooms.

## About the creation of BingoSyncSharp
Firstly, a question to myself that must be answered clearly:
- Q: **Am I the right person to make something like this?**
- A: **No**. I only started learning C# 9 to 10 months ago (December of 2023) and did my best to even learn web interactions with C# only **3 weeks ago** (August of 2024). I am not the right person to make this, but needed a way to interact with [BingoSync](https://bingosync.com/) for a [plugin](https://github.com/Ninja-Cookie/TrueBingo) I was creating, and could not find an easy and simple way to interact with BingoSync using C# anywhere else, so I made one.

I still think what I have made is good, but I also simply don't have the experience or know-how to know what "good" is, and wanted to be transparent about that.

# How to use BingoSyncSharp
Firstly, simply add `BingoSyncSharp.dll` (from one of the [releases](https://github.com/Ninja-Cookie/BingoSyncSharp/releases/)) as a reference in your project. You can also place `BingoSyncSharp.xml` with the DLL, for added documentation in the code on what things do and how they can be used.

The internal namespace for `BingoSyncSharp` goes by `BingoSyncAPI` which has everything you need.


## Setting up:
Simply add `using BingoSyncAPI;` to the top of the code you wish to use BingoSyncSharp in. You can then create a `new BingoSync();` and begin using it.

Example:

``` C#
using BingoSyncAPI;

// ...

private readonly BingoSync bingoSync = new BingoSync();

// ...
```


## Connecting to a room:
Connecting to a room is simple! Take your BingoSync field (in this example; `bingoSync`) and do: `bingoSync.JoinRoom(...RoomInfo...);` with your existing or new `RoomInfo` class from `BingoSyncAPI.BingoSyncTypes`, within an asynchronous method if you wish to wait for a response on if the connection was successful or not.

Example:
``` C#
using BingoSyncAPI;
using static BingoSyncAPI.BingoSyncTypes;

// ...

private readonly BingoSync bingoSync = new BingoSync();

public async void JoinBingoSyncRoom()
{
  RoomInfo myRoomInfo = new RoomInfo
  (
    "7IeuxJRATh6PKSZqpmE0si",   // The Room ID found in the URL of a BingoSync room after /room/
    "MyCoolPassword123",        // The rooms password.
    "My Player Name",           // The name of this player to join.
    PlayerColors.Red,           // The color of this player on the board.
    false                       // If this player will be a spectator or not.
  );

  if (await bingoSync.JoinRoom(myRoomInfo) == BingoSync.ConnectionStatus.Connected)
  {
    // Successfully connected to room!
  }
  else
  {
    // Connection failed / Info was wrong ...
  }
}

// ...
```

(Idealy you would pass in the `RoomInfo` with the function, but for the sake of this example to show what to do, we just create the RoomInfo within the function)


## Disconnecting from the room:
Simply call `bingoSync.Disconnect()`, and if the connection status of this is Connected, it will handle the disconnection for you, leaving the room and closing sockets.


## Listening to activity in the room:
A `BingoSync` instance comes with the event `OnMessageReceived` which you can listen to. Just create a function accepting the type `SocketMessage` from `BingoSyncAPI.BingoSyncTypes`, then add your function to `OnMessageReceived`.

Example:
``` C#
public void Init()
{
  bingoSync.OnMessageReceived += OnRoomEvent;
}

private void OnRoomEvent(SocketMessage message)
{

  // Example of then using this information ...

  int changedSquareOnBoard = 0;
  string chatMessage = string.Empty;

  switch (message.type) // expect: connection, goal, revealed, color, chat, new-card
  {
    case "goal":
      if (int.TryParse(message.square.slot.Replace("slot", ""), out int result))
        changedSquareOnBoard = result;
    break;

    case "chat":
      chatMessage = message.text;
    break;
  }

  // ...

}
```

Note that you can get what information will generally be passed through into "message" of its type by following the "broadcast" socket in the network panel of a browsers inspector when in a room, or if that makes no sense, just debugging the "message" in your function to see what info exists for the "message.type" that comes in, just to get a general understanding of what you're looking for.

For example, a `message.type` of "chat" won't have any info in `message.square`, which will be `null`, but will have info for `message.text` containing the chat message, and `message.player` about the player who sent the message in the chat, such as their name (`message.player.name`).


## Send a message in chat:
Sending a message in chat is easy! When connected to a room, just asynchronously call `bingoSync.SendChatMessage("your chat message");` with the desired message.


## Mark/Unmark a slot on the board:
You can mark/unmark a slot on the board with the `SelectSlot` function, passing in the slots ID, if to mark or unmark it, and what color its basing the decision from.

Example:
``` C#
public async void SelectFirstSlot()
{
  await bingoSync.SelectSlot
  (
    1,                  // The first slot on the board, ranging from 1 to 25 on a 5x5 grid, starting top left ending bottom right.
    true,               // The state of the color for this slot on the board (if to mark or unmark it). Defaults to `true` if no input is given to the parameter.
    PlayerColors.Red    // The color trying to be marked/unmarked from this slot. Defaults to the currently set color of the player if `null` / no input is given to the parameter.
  );
}
```

If you just want to mark something with your current player color, this can be simplified to:
``` C#
public async void SelectFirstSlot()
{
  await bingoSync.SelectSlot(1);
}
```

*But how do I know what slot should be marked/unmarked?*

You can use the function `GetBoardSlots();` to get an array of `SlotInfo` containing information about each slot!

Example:
``` C#
using System.Linq;

// ...

public async void SelectSlotWithName(string slotName)
{
  // Finds the slot that matches the given name.
  SlotInfo slot = await bingoSync.GetBoardSlots().FirstOrDefault(x => x.Info == slotName);

  // If a slot was found with this name, try to select it.
  if (slot != null)
    await bingoSync.SelectSlot(slot.ID);
}

// ...

```

From here you can get more creative and do smarter ways to search slots for a given name, such as regex matching, or if you have very strict objectives, you can handle those more directly.


## Set the player color:
If you want to change the player color mid-game, you can simply call the `SetPlayerColor(...playerColor...)` function with the desired `BingoSyncTypes.PlayerColors`


## Create a new board:
If you want to generate a new board, you can use the `CreateNewCard(...)` function, passing in the Lockout type, if the card should be hidden, IDs for the game and associated variant, then optionally a specified seed and custom JSON.

Example:
``` C#
  public async void GenerateNewCard()
  {
    CardIDs customAdvSRL = new CardIDs        
    (
      18,    // The game ID for "Custom (Advanced)"
      187    // The variant ID for "SRL v5" within game ID group "18"
    ),

    await CreateNewCard
    (
      true,                  // If lockout mode or not.
      true,                  // If the card should be hidden and need revealing by players.
      customAdvSRL,          // The CardIDs for the game / matching variant desired.
      452189,                // A custom seed for the board. Defaults to -1 if parameter is not filled, which will pick a random seed.
      "[{"customJson":""}]"  // Custom JSON to use for the board. This is specific to the "Custom (Advanced)" game on BingoSync. Unless you know what you're doing, you probably don't want / need to use this, and can pick a specific game instead.
    );
  }
```

Realistically, you're likely not using "Custom", and the JSON part won't be used, and if you are, you'd pull it in from a file or URL of some kind, and the game IDs are going to be for a specific game so you don't need to create new CardIDs each time. For the average user, it will probably look more like this:
``` C#
readonly CardIDs cardIDs = new CardIDs
(
  52,  // Hollow Knight ID
  52,  // Hollow Knight Normal Variant ID (Default IDs match the game ID)
);

public async void GenerateNewCard()
{
  await CreateNewCard
  (
    true,    // If lockout mode or not.
    true,    // If the card should be hidden and need revealing by players.
    cardIDs  // The CardIDs for the game / matching variant desired.
  );
}
```

Note that you can get the game IDs by looking at [BingoSync](https://bingosync.com/)'s homepage with an inspector, search for the game in the HTML and you should find its associated `value`. Alternatively, you can use the `GetCardIDs(...)` function, to call the website to find them automatically based on the provided Game name and Variant name, but unless you somehow need to dynamically obtain these during runtime, this should only be used for debugging to get the IDs you need.

#
There are still some more functions you can find within BingoSyncSharp, but this hopefully should cover the most prominent use-cases.
#


# Using this code:
I used the NuGet packages [Json.Net](https://www.nuget.org/packages/Json.Net/1.0.33) and [ILMerge](https://www.nuget.org/packages/ILMerge/3.0.41) to merge Json.Net into the single BingoSyncSharp DLL.

I generally wanted to use as little packages and libraries as I could for this to make it as usable as possible for anyone. Everything other than Json.Net, which I ended up needing for cleanly interacting with web requests, should be default standard libraries that can be used anywhere, is my hope at least. As said at the start, I don't really know what I am doing, but I did my best. If you feel you can do it better, feel free.
