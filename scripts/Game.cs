using Godot;
using System.Threading;

public partial class Game : Node
{
	PackedScene _playerScene;
	MultiplayerSpawner _spawner;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// Load player scene
		_playerScene = GD.Load<PackedScene>("res://scenes/player.tscn");
		// Get reference to multiplayer spawner
		_spawner = GetNode<MultiplayerSpawner>("%MultiplayerSpawner");

		// TODO: Load All Cards
		

		// Tell server that this player is loaded
		LobbyManager lobbyManager = GetNode<LobbyManager>("/root/LobbyManager");
		Thread.Sleep(10); // Give time for LobbyManager to recognize game is loaded
		lobbyManager.RpcId(1, "LoadPlayer");
	}

	// Called once per frame
	public override void _Process(double delta)
	{
		
	}

	// Called when Server receives verification that all players have connected
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void StartGame()
	{
		Node3D player = _playerScene.Instantiate() as Node3D;
		AddChild(player);
		(player.GetChild(0) as Camera3D).Current = true;
	}
}
