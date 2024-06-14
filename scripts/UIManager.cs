using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class UIManager : Node
{
	LobbyManager _lobbyManager;
	MarginContainer _characterMenu;
	SplitContainer _turnUI;
	PackedScene _playerHealth;
	VBoxContainer _playerHealthContainer;

	Texture2D[] _characterIcons;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_lobbyManager = GetTree().Root.GetNode<LobbyManager>("LobbyManager");
		_characterMenu = GetNode<MarginContainer>("%CharacterMenu");
		_turnUI = GetNode<SplitContainer>("%TurnUI");
		_playerHealth = GD.Load<PackedScene>("res://scenes/player_health.tscn");
		_playerHealthContainer = GetNode<VBoxContainer>("%PlayerHealths");

		// Get character icons
		Texture2D cuddle = GD.Load<Texture2D>("res://assets/visuals/cuddle_team_leader.png");
		Texture2D batman = GD.Load<Texture2D>("res://assets/visuals/batman.png");
		Texture2D banana = GD.Load<Texture2D>("res://assets/visuals/banana.jpg");
		Texture2D travis = GD.Load<Texture2D>("res://assets/visuals/travis-scott.jpg");
		_characterIcons = new Texture2D[] { cuddle, batman, banana, travis };

		// Connect signals
		_lobbyManager.AllPlayersConnected += PlayersConnected;
	}

	// Called when all players are connected in game
	void PlayersConnected() => Rpc(nameof(StartGame));
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void StartGame()
	{
		// Show character selection screen
		_turnUI.Visible = false;
		_characterMenu.Visible = true;
	}

	// Called when characters have been chosen
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void SpawnTurnUI()
	{
		_turnUI.Visible = true;

		// Add player health scenes, sort to get in correct order
		for (int player = 0; player < 4; player++)
		{
			TextureRect playerHealth = _playerHealth.Instantiate() as TextureRect;
			playerHealth.Texture = _characterIcons[player];

			_playerHealthContainer.AddChild(playerHealth);
		}
	}
}
