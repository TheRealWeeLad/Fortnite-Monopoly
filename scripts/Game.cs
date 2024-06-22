using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class Game : Node
{
	[Signal]
	public delegate void TurnStartedEventHandler(long playerId);
	[Signal]
	public delegate void HealthChangedEventHandler(int playerOrder, int health, bool increased);

	LobbyManager _lobbyManager;

	PackedScene _playerScene;
	PackedScene[] _characterModels;
	PackedScene _dice;
	PackedScene _goofyDice;
	// Allocate 4 players in case someone joins halfway
	public readonly Node3D[] players = new Node3D[4];
	long[] _turnOrder;

	Player _currentPlayer;
	public static int CurrentTurn { get; private set; } = 0;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// Load player scene
		_playerScene = GD.Load<PackedScene>("res://scenes/player.tscn");
		// Load character models
		PackedScene cuddle = GD.Load<PackedScene>("res://scenes/cuddle_team_leader.tscn");
		PackedScene batman = GD.Load<PackedScene>("res://scenes/batman.tscn");
		PackedScene banana = GD.Load<PackedScene>("res://scenes/banana.tscn");
		PackedScene travis = GD.Load<PackedScene>("res://scenes/travis_scott.tscn");
		_characterModels = new PackedScene[] { cuddle, batman, banana, travis };
		// Load dice
		_dice = GD.Load<PackedScene>("res://scenes/dice.tscn");
		_goofyDice = GD.Load<PackedScene>("res://scenes/goofy_dice.tscn");
		_diceFuncs = new Func<Task>[4] { HealDice, Shoot, BoogieBomb, Wall };

		// Load board
		_board = new Action[32] { Nothing, LocationSpace, Campfire, LocationSpace, Chest, LocationSpace,
								  SpikeTrap, LocationSpace, Nothing, LocationSpace, Campfire,
								  LocationSpace, Chest, LocationSpace, SpikeTrap, LocationSpace,
								  Nothing, LocationSpace, Campfire, LocationSpace, Chest, LocationSpace,
								  SpikeTrap, LocationSpace, GoToJail, LocationSpace, Campfire,
								  LocationSpace, Chest, LocationSpace, SpikeTrap, LocationSpace };

		// TODO: Load All Cards

		// TODO: Reconnect Signals for connecting/disconnecting

		// Tell server that this player is loaded
		_lobbyManager = GetNode<LobbyManager>("/root/LobbyManager");
		_lobbyManager.Rpc("LoadPlayer");
	}

	// Called after character selection
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void StartGame(Godot.Collections.Dictionary<long, int> playerCharacters)
	{
		Vector3 startPosition = new(_distanceBetweenSpaces * 4, 0, 3.5f);
		Vector3 startDeltaZ = new(0, 0, 0.6f);

		// Place and assign character models
		foreach ((long playerId, int characterIdx) in playerCharacters)
		{
			int playerNum = LobbyManager.Players[playerId].Order;
			Node3D character = _characterModels[characterIdx].Instantiate() as Node3D;
			character.Position = startPosition + playerNum % 2 * startDeltaZ;
			AddChild(character);
			players[playerNum] = character;
		}

		// Get turn order
		_turnOrder = LobbyManager.Players.OrderBy(x => x.Value.Order).Select(x => x.Key).ToArray();
		long currentPlayerId = _turnOrder[0];
		_currentPlayer = LobbyManager.Players[currentPlayerId];

		// Only server should begin the player's turn
		CurrentTurn++;
		if (Multiplayer.IsServer())
			// Start 1st player's turn
			EmitSignal(SignalName.TurnStarted, currentPlayerId);
	}

	// Called when Roll button is pressed
	void Roll() => Rpc(nameof(SpawnDice));
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void SpawnDice()
	{
		// Spawn dice
		RigidBody3D die = _dice.Instantiate() as RigidBody3D;
		if (Multiplayer.IsServer())
		{
			NormalDice dieDice = die as NormalDice;
			dieDice.DiceRolled += ReadNormalDice;
			dieDice.SimulatePhysics = true;
		}
		AddChild(die);

		RigidBody3D goofyDie = _goofyDice.Instantiate() as RigidBody3D;
		if (Multiplayer.IsServer())
		{
			GoofyDice goofyDieDice = goofyDie as GoofyDice;
			goofyDieDice.DiceRolled += ReadGoofyDice;
			goofyDieDice.SimulatePhysics = true;
		}
		AddChild(goofyDie);
	}

	void ChangeHealth(long playerId, int health)
	{
		Player player = LobbyManager.Players[playerId];
		if (player.Health == 15) return; // Can't go over 15 health

		player.Health = Math.Clamp(player.Health + health, 0, 15);
		// Update player on all clients
		_lobbyManager.SynchronizePlayers();

		EmitSignal(SignalName.HealthChanged, _currentPlayer.Order, player.Health, health > 0);
	}
}
