using Godot;

public partial class GameManager : Node
{
	PackedScene _playerScene;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// Load player scene
		_playerScene = GD.Load<PackedScene>("res://scenes/player.tscn");

		// Tell server that this player is loaded
		LobbyManager lobbyManager = GetNode<LobbyManager>("/root/LobbyManager");
		lobbyManager.RpcId(1, "LoadPlayer", this);
	}

	// Called when Server receives verification that all players have connected
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void StartGame()
	{
		AddChild(_playerScene.Instantiate());
	}
}
