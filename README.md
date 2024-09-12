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

  switch (message.type) // connection, goal, revealed, color, chat, new-card
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
