using Godot;
using System.Collections.Generic;

public partial class LobbyManager : Node
{
	[Signal]
	public delegate void PlayerConnectedEventHandler(long id, string username);
	[Signal]
	public delegate void PlayerDisconnectedEventHandler(long id);
	[Signal]
	public delegate void ServerDisconnectedEventHandler();
	[Signal]
	public delegate void AllPlayersConnectedEventHandler();

	public static Dictionary<long, Player> Players { get; } = new();
	readonly Player player = new() { Name = "Username", Order = 0 };

	// Server info
	const int PORT = 7000;
	const int MAX_CONNECTIONS = 4;
	int playersLoaded = 0;

	// Game Scene
	PackedScene _gameScene;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Multiplayer.PeerConnected += OnPlayerConnected;
		Multiplayer.PeerDisconnected += OnPlayerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedSuccess;
		Multiplayer.ConnectionFailed += OnConnectedFail;
		Multiplayer.ServerDisconnected += OnServerDisconnected;

		// Load Game Scene
		_gameScene = GD.Load<PackedScene>("res://scenes/game.tscn");
	}

	// Called when create button is pressed
	public void CreateLobby(string username)
	{
		ENetMultiplayerPeer peer = new();
		Error error = peer.CreateServer(PORT, MAX_CONNECTIONS);
		if (error != Error.Ok)
		{
			GD.PrintErr(error);
			return;
		}
		Multiplayer.MultiplayerPeer = peer;

		// Add player to list
		player.Name = username;
		Players.Add(1, new() { Name = username });

		// Show Lobby
		EmitSignal(SignalName.PlayerConnected, 1, username);
	}
	// Called when join button is pressed
	public void JoinLobby(string username, string ip)
	{
		player.Name = username;

		ENetMultiplayerPeer peer = new();
		Error error = peer.CreateClient(ip, PORT);
		if (error != Error.Ok)
		{
			GD.PrintErr(error);
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
	}
	// Called when start button is pressed
	public void StartGame() => Rpc(nameof(LoadGame));

	// RPCs
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void RegisterPlayer(Godot.Collections.Dictionary player)
	{
		Player plr = new(player);
		int plrId = Multiplayer.GetRemoteSenderId();
		Players.Add(plrId, plr);
		EmitSignal(SignalName.PlayerConnected, plrId, plr.Name);
	}
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void LoadGame() => GetTree().ChangeSceneToPacked(_gameScene);
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void LoadPlayer()
	{
		if (Multiplayer.IsServer())
		{
			// The first to load in is Player 1
			long id = Multiplayer.GetRemoteSenderId();
			Player loadedPlayer = Players[id];
			loadedPlayer.Order = playersLoaded;
			Players[id] = loadedPlayer;

			playersLoaded++;
			if (playersLoaded == Players.Count)
			{
				// START GAME
				EmitSignal(SignalName.AllPlayersConnected);
			}
		}
	}

	// Multiplayer Event Handlers
	void OnPlayerConnected(long id)
	{
		RpcId(id, nameof(RegisterPlayer), player);
	}
	void OnPlayerDisconnected(long id)
	{
		Players.Remove(id);
		EmitSignal(SignalName.PlayerDisconnected, id);
	}
	void OnConnectedSuccess()
	{
		long id = Multiplayer.GetUniqueId();
		Players[id] = player;
		EmitSignal(SignalName.PlayerConnected, id, player.Name);
	}
	void OnConnectedFail()
	{
		Multiplayer.MultiplayerPeer = null;
	}
	void OnServerDisconnected()
	{
		Multiplayer.MultiplayerPeer = null;
		Players.Clear();
		EmitSignal(SignalName.ServerDisconnected);
	}



	// DEBUG
	public void CreateDebug()
	{
		ENetMultiplayerPeer peer = new();
		Error error = peer.CreateServer(PORT, MAX_CONNECTIONS);
		Multiplayer.MultiplayerPeer = peer;

		// Add player to list
		player.Name = "HOST";
		Players.Add(1, new() { Name = "HOST" });

		// Show Lobby
		EmitSignal(SignalName.PlayerConnected, 1, "HOST");
	}
	public void JoinDebug()
	{
		player.Name = "CLIENT";

		ENetMultiplayerPeer peer = new();
		Error error = peer.CreateClient("127.0.0.1", PORT);
		if (error != Error.Ok)
		{
			GD.PrintErr(error);
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
	}
}

public readonly struct Player
{
	readonly Godot.Collections.Dictionary _dict;

	public readonly string Name { get => _dict["Name"].AsString(); set => _dict["Name"] = value; }
	public readonly int Order { get => _dict["Order"].AsInt32(); set => _dict["Order"] = value; }

	public Player() => _dict = new();
	public Player(Godot.Collections.Dictionary dict) => _dict = dict;

	public static Player FromVariant(Variant var) => new(var.AsGodotDictionary());
	public static implicit operator Variant(Player plr)
	{
		return plr._dict;
	}
}
