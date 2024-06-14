using Godot;

public partial class UIManager : Node
{
	LobbyManager _lobbyManager;
	MarginContainer _characterMenu;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_lobbyManager = GetTree().Root.GetNode<LobbyManager>("LobbyManager");
		_characterMenu = GetNode<MarginContainer>("%CharacterMenu");

		// Connect signals
		_lobbyManager.AllPlayersConnected += PlayersConnected;
	}

	void PlayersConnected() => Rpc(nameof(StartGame));
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void StartGame()
	{
		// Show character selection screen
		_characterMenu.Visible = true;
	}
}
