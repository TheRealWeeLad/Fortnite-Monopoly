using Godot;
using System.Linq;

public partial class UIManager : Node
{
	LobbyManager _lobbyManager;
	MarginContainer _characterMenu;
	SplitContainer _turnUI;
	MarginContainer _turnActions;
	PackedScene _playerHealth;
	VBoxContainer _playerHealthContainer;
	PackedScene _turnAnnouncement;

	Color[] _playerColors = new Color[] { Color.FromHtml("e43131"), Color.FromHtml("31e431"),
										  Color.FromHtml("33b5e1"), Color.FromHtml("f6f932") };
	Texture2D[] _characterIcons;
	TextureProgressBar[] _healthBars;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_lobbyManager = GetTree().Root.GetNode<LobbyManager>("LobbyManager");
		_characterMenu = GetNode<MarginContainer>("%CharacterMenu");
		_turnUI = GetNode<SplitContainer>("%TurnUI");
		_turnActions = GetNode<MarginContainer>("%TurnActions");
		_playerHealth = GD.Load<PackedScene>("res://scenes/player_health.tscn");
		_playerHealthContainer = GetNode<VBoxContainer>("%PlayerHealths");
		_turnAnnouncement = GD.Load<PackedScene>("res://scenes/turn_announcement.tscn");
		_healthBars = new TextureProgressBar[LobbyManager.Players.Count];

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
	void SpawnTurnUI(Godot.Collections.Dictionary<int, int> playerCharacters)
	{
		_turnUI.Visible = true;
		HideTurnActions(); // Disable turn actions until it's their turn

		// Add player health scenes, sort to get in correct order
		string[] playerNames = LobbyManager.Players.OrderBy(x => x.Value.Order).Select(x => x.Value.Name).ToArray();
		for (int player = 0; player < playerNames.Length; player++)
		{
			TextureRect playerHealth = _playerHealth.Instantiate() as TextureRect;
			playerHealth.Texture = _characterIcons[playerCharacters[player]];
			(playerHealth.GetChild(0) as Label).Text = playerNames[player];
			_healthBars[player] = playerHealth.GetChild(1) as TextureProgressBar;

			_playerHealthContainer.AddChild(playerHealth);
		}
	}

	// Called when turn begins
	void BeginTurn(long playerId) => Rpc(nameof(ShowTurnAnnouncement), playerId, LobbyManager.Players[playerId]);
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ShowTurnAnnouncement(long playerId, Godot.Collections.Dictionary playerDict)
	{
		Player player = new(playerDict);
		if (Multiplayer.GetUniqueId() == playerId) ShowTurnActions();

		CenterContainer announcement = _turnAnnouncement.Instantiate() as CenterContainer;
		Label text = announcement.GetChild(0) as Label;
		text.Text = $"{player.Name}'s Turn";
		text.AddThemeColorOverride("Font Color", _playerColors[player.Order]);

		AddChild(announcement);
		// Delete object as soon as animation finishes
		(announcement.GetChild(1) as AnimationPlayer).AnimationFinished += (StringName _) => announcement.QueueFree();
	}

	void ShowTurnActions() => _turnActions.Visible = true;
	void HideTurnActions() => _turnActions.Visible = false;
}
