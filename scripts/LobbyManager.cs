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

	public struct Player
	{
		readonly Godot.Collections.Dictionary _dict;

		public string Name { get => _dict["Name"].AsString(); set => _dict["Name"] = value; }

		public Player() => _dict = new();
		public Player(Godot.Collections.Dictionary dict) => _dict = dict;

		public static Player FromVariant(Variant var) => new(var.AsGodotDictionary());
		public static implicit operator Variant(Player plr)
		{
			return plr._dict;
		}
	}
	readonly Dictionary<long, Player> _players = new();
	Player player = new() { Name = "Username" };

	// Server info
	const int PORT = 7000;
	const int MAX_CONNECTIONS = 4;

	// UI Elements
	TextEdit _createNameField;
	TextEdit _joinNameField;
	TextEdit _ipField;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Multiplayer.PeerConnected += OnPlayerConnected;
		Multiplayer.PeerDisconnected += OnPlayerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedSuccess;
		Multiplayer.ConnectionFailed += OnConnectedFail;
		Multiplayer.ServerDisconnected += OnServerDisconnected;

		// Initialize UI Elements
		_createNameField = GetNode<TextEdit>("%Create Name Field");
		_joinNameField = GetNode<TextEdit>("%Join Name Field");
		_ipField = GetNode<TextEdit>("%IP Field");
	}

	// Called when create button is pressed
	void CreateLobby()
	{
		ENetMultiplayerPeer peer = new();
		Error error = peer.CreateServer(PORT, MAX_CONNECTIONS);
		if (error != Error.Ok)
		{
			GD.PrintErr(error);
			return;
		}
		Multiplayer.MultiplayerPeer = peer;

		// Get username
		string username = _createNameField.Text;
		player.Name = username;
		if (username.Equals(""))
		{
			GD.Print("Username Empty");
			return;
		}
		_players.Add(1, new() { Name = username });

		// Show Lobby
		EmitSignal(SignalName.PlayerConnected, 1, username);
	}
	// Called when join button is pressed
	void JoinLobby()
	{
		string username = _joinNameField.Text;
		if (username.Equals(""))
		{
			GD.Print("No username");
			return;
		}
		player.Name = username;

		string ip = _ipField.Text;
		if (ip.Equals(""))
		{
			GD.Print("No ip found");
			return;
		}

		ENetMultiplayerPeer peer = new();
		Error error = peer.CreateClient(ip, PORT);
		if (error != Error.Ok)
		{
			GD.PrintErr(error);
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
	}

	// RPCs
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void RegisterPlayer(Godot.Collections.Dictionary player)
	{
		Player plr = new(player);
		int plrId = Multiplayer.GetRemoteSenderId();
		_players.Add(plrId, plr);
		EmitSignal(SignalName.PlayerConnected, plrId, plr.Name);
	}

	// Multiplayer Event Handlers
	void OnPlayerConnected(long id)
	{
		RpcId(id, nameof(RegisterPlayer), player);
	}
	void OnPlayerDisconnected(long id)
	{
		_players.Remove(id);
		EmitSignal(SignalName.PlayerDisconnected, id);
	}
	void OnConnectedSuccess()
	{
		long id = Multiplayer.GetUniqueId();
		_players[id] = player;
		EmitSignal(SignalName.PlayerConnected, id, player.Name);
	}
	void OnConnectedFail()
	{
		Multiplayer.MultiplayerPeer = null;
	}
	void OnServerDisconnected()
	{
		Multiplayer.MultiplayerPeer = null;
		_players.Clear();
		EmitSignal(SignalName.ServerDisconnected);
	}
}