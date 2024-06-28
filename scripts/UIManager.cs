using Godot;
using System;
using System.Linq;

public partial class UIManager : Node
{
	LobbyManager _lobbyManager;
	MarginContainer _characterMenu;
	SplitContainer _turnUI;
	MarginContainer _turnActions;
	MarginContainer _useCardsActions;
	Label _turnCounter;
	Label _shootLabel;
	CenterContainer _wallChoices;
	HBoxContainer _numberContainer;
	CenterContainer _treasureCardConfirmation;
	PackedScene _numberButton;
	PackedScene _playerHealth;
	VBoxContainer _playerHealthContainer;
	PackedScene _turnAnnouncement;
	PackedScene _healthIndicator;
	Game _game;

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
		_useCardsActions = GetNode<MarginContainer>("%UseCardsActions");
		_turnCounter = GetNode<Label>("%TurnCount");
		_shootLabel = GetNode<Label>("%ShootLabel");
		_wallChoices = GetNode<CenterContainer>("%WallChoices");
		_numberContainer = GetNode<HBoxContainer>("%NumberContainer");
		_treasureCardConfirmation = GetNode<CenterContainer>("%TreasureCardConfirmation");
		_numberButton = GD.Load<PackedScene>("res://scenes/number_button.tscn");
		_playerHealth = GD.Load<PackedScene>("res://scenes/player_health.tscn");
		_playerHealthContainer = GetNode<VBoxContainer>("%PlayerHealths");
		_turnAnnouncement = GD.Load<PackedScene>("res://scenes/turn_announcement.tscn");
		_healthBars = new TextureProgressBar[LobbyManager.Players.Count];
		_healthIndicator = GD.Load<PackedScene>("res://scenes/health_change.tscn");

		// Get character icons
		Texture2D cuddle = GD.Load<Texture2D>("res://assets/visuals/cuddle_team_leader.png");
		Texture2D batman = GD.Load<Texture2D>("res://assets/visuals/batman.png");
		Texture2D banana = GD.Load<Texture2D>("res://assets/visuals/banana.jpg");
		Texture2D travis = GD.Load<Texture2D>("res://assets/visuals/travis-scott.jpg");
		_characterIcons = new Texture2D[] { cuddle, batman, banana, travis };

		// Connect signals
		_lobbyManager.AllPlayersConnected += PlayersConnected;
		_game = GetNode<Game>("/root/Game");
		_game.HealthChanged += UpdateHealthBar;
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
	void SpawnTurnUI(Godot.Collections.Dictionary<long, int> playerCharacters)
	{
		_turnUI.Visible = true;

		// Add player health scenes
		foreach ((long id, Player player) in LobbyManager.Players.OrderBy(x => x.Value.Order))
		{
			int order = player.Order;
			TextureRect playerHealth = _playerHealth.Instantiate() as TextureRect;
			playerHealth.Texture = _characterIcons[playerCharacters[id]];
			(playerHealth.GetChild(0) as Label).Text = player.Name;
			TextureProgressBar healthBar = playerHealth.GetChild(1) as TextureProgressBar;
			healthBar.Value = player.Health;
			(healthBar.GetChild(0) as Label).Text = player.Health.ToString();
			_healthBars[order] = healthBar;

			_playerHealthContainer.AddChild(playerHealth);
		}
	}

	// Called when turn begins
	void BeginTurn(long playerId) => Rpc(nameof(ShowTurnAnnouncement), playerId, LobbyManager.Players[playerId]);
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ShowTurnAnnouncement(long playerId, Godot.Collections.Dictionary playerDict)
	{
		// Update turn counter
		UpdateTurnCounter();

		Player player = new(playerDict);
		// Only let the active player use turn actions
		if (Multiplayer.GetUniqueId() == playerId) ShowTurnActions();

		CenterContainer announcement = _turnAnnouncement.Instantiate() as CenterContainer;
		Label text = announcement.GetChild(0) as Label;
		text.Text = $"{player.Name}'s Turn";
		text.AddThemeColorOverride("font_color", _playerColors[player.Order]);

		AddChild(announcement);
		// Delete object as soon as animation finishes
		(announcement.GetChild(1) as AnimationPlayer).AnimationFinished += (StringName _) => announcement.QueueFree();
	}

	void ShowTurnActions() => _turnActions.Visible = true;
	void HideTurnActions() => _turnActions.Visible = false;
	void UpdateTurnCounter() => _turnCounter.Text = $"Turn {Game.CurrentTurn}";
	void ShowCardActions() => _useCardsActions.Visible = true;
	void HideCardActions() => _useCardsActions.Visible = false;
	// Shoot
	void ShowShootLabel(long playerId) => RpcId(playerId, nameof(ShowShootLabelRpc));
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ShowShootLabelRpc() => _shootLabel.Visible = true;
	void HideShootLabel() => _shootLabel.Visible = false;
	
	// Wall
	void ShowWallChoices(long playerId, int numRolled) => RpcId(playerId, nameof(ShowWallChoicesRpc), numRolled);
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ShowWallChoicesRpc(int numRolled)
	{
		// Clear number Container
		foreach (Node button in _numberContainer.GetChildren()) button.QueueFree();

		for (int i = 0; i <= numRolled; i++)
		{
			Button numberButton = _numberButton.Instantiate() as Button;
			int space = i;
			numberButton.Text = $" {space} ";
			numberButton.Pressed += HideWallChoices;
			numberButton.Pressed += () => _game.SetWallSpace(space);
			_numberContainer.AddChild(numberButton);
		}

		_wallChoices.Visible = true;
	}
	void HideWallChoices() => _wallChoices.Visible = false;

	// Treasure Card
	void ShowTreasureConfirmation() => _treasureCardConfirmation.Visible = true;
	void HideTreasureConfirmation() => _treasureCardConfirmation.Visible = false;

	void UpdateHealthBar(int playerOrder, int health, bool increased) =>
		Rpc(nameof(UpdateHealthRpc), playerOrder, health, increased);
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void UpdateHealthRpc(int playerOrder, int health, bool increased)
	{
		Target playerModel = _game.players[playerOrder];
		// Update health bar
		TextureProgressBar healthBar = _healthBars[playerOrder];
		int healthChange = health - (int)healthBar.Value;
		healthBar.Value = health;
		(healthBar.GetChild(0) as Label).Text = health.ToString();

		// Spawn health indicator
		Label3D healthIndicator = _healthIndicator.Instantiate() as Label3D;
		string oper = healthChange > 0 ? "+" : "-";
		healthIndicator.Text = oper + Math.Abs(healthChange).ToString();
		HealthIndicator script = healthIndicator as HealthIndicator;
		script.SetPositions(playerModel.GlobalPosition.Y + 1.3f, playerModel.GlobalPosition.Y + 1.6f);
		healthIndicator.Modulate = increased ? Color.FromHtml("00ff00ff") : Color.FromHtml("ff0000ff");
		playerModel.AddChild(healthIndicator);
		healthIndicator.Scale = playerModel.Scale.Inverse();
	}
}
