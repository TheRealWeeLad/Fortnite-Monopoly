using FortniteMonopolyExtensions;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Game : Node
{
	[Signal]
	public delegate void TurnStartedEventHandler(long playerId);
	[Signal]
	public delegate void HealthChangedEventHandler(int playerOrder, int health, bool increased);

	LobbyManager _lobbyManager;

	PackedScene[] _characterModels;
	PackedScene _dice;
	PackedScene _goofyDice;
	PackedScene _treasureCard;
	Node3D _treasureCardPile;
	// Store cards for pickup
	Stack<TreasureCard> _treasureCards = new();
	// Store dice to remove them when next turn starts
	RigidBody3D[] _diceModels = new RigidBody3D[2];
	// Allocate 4 players in case someone joins halfway
	public readonly Target[] players = new Target[4];
	long[] _turnOrder;

	Player _currentPlayer;
	public static int CurrentTurn { get; private set; } = 0;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// Load character models
		PackedScene cuddle = GD.Load<PackedScene>("res://scenes/cuddle_team_leader.tscn");
		PackedScene batman = GD.Load<PackedScene>("res://scenes/batman.tscn");
		PackedScene banana = GD.Load<PackedScene>("res://scenes/banana.tscn");
		PackedScene travis = GD.Load<PackedScene>("res://scenes/travis_scott.tscn");
		_characterModels = new PackedScene[] { cuddle, batman, banana, travis };
		// Load dice
		_dice = GD.Load<PackedScene>("res://scenes/dice.tscn");
		_goofyDice = GD.Load<PackedScene>("res://scenes/goofy_dice.tscn");
		_diceFuncs = new Action[4] { HealDice, Shoot, BoogieBomb, Wall };

		// Load board
		/*_board = new Action[32] { Nothing, LocationSpace, Campfire, LocationSpace, Chest, LocationSpace,
								  SpikeTrap, LocationSpace, Nothing, LocationSpace, Campfire,
								  LocationSpace, Chest, LocationSpace, SpikeTrap, LocationSpace,
								  Nothing, LocationSpace, Campfire, LocationSpace, Chest, LocationSpace,
								  SpikeTrap, LocationSpace, GoToJail, LocationSpace, Campfire,
								  LocationSpace, Chest, LocationSpace, SpikeTrap, LocationSpace };
*/		_board = new Action[32] { Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest, Chest };
		// Load Cards
		_treasureCard = GD.Load<PackedScene>("res://scenes/treasure_chest_card.tscn");
		_treasureCardPile = GetNode<Node3D>("%TreasureCardPile");
		// Spawn Cards
		SpawnCards();

		// TODO: Reconnect Signals for connecting/disconnecting

		// Tell server that this player is loaded
		_lobbyManager = GetNode<LobbyManager>("/root/LobbyManager");
		_lobbyManager.Rpc("LoadPlayer");
	}

	void SpawnCards()
	{
		// Some cards have duplicates
		int[] cardIndices = { 0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 6, 6, 7, 8, 8, 9 };
		cardIndices.Shuffle();
		for (int i = 0; i < cardIndices.Length; i++)
		{
			int idx = cardIndices[i];
			TreasureCard card = _treasureCard.Instantiate() as TreasureCard;
			card.Position += 0.02f * i * Vector3.Up;
			_treasureCardPile.AddChild(card);
			if (Multiplayer.IsServer()) _treasureCards.Push(card);

			card.SetCardType(idx);
		}
	}

	// Called after character selection
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void StartGame(Godot.Collections.Dictionary<long, int> playerCharacters)
	{
		Vector3 startPosition = new(_distanceBetweenSpaces * 4, 0, 3.5f);
		// Vector3 startDeltaX = new(0.6f, 0, 0);
		// Vector3 startDeltaZ = new(0, 0, 0.6f);

		// Place and assign character models
		foreach ((long playerId, int characterIdx) in playerCharacters)
		{
			int playerNum = LobbyManager.Players[playerId].Order;
			Target character = _characterModels[characterIdx].Instantiate() as Target;
			// Initialize player's targetability
			character.SetId(playerId);
			Shooting += character.BeginDetectingShots;
			Shot += character.StopDetectingShots;
			// Place character on Go
			character.Position = startPosition; // + playerNum / 2 * startDeltaX + playerNum % 2 * startDeltaZ;
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
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void SpawnDice()
	{
		// Spawn dice
		RigidBody3D die = _dice.Instantiate() as RigidBody3D;
		_diceModels[0] = die;
		if (Multiplayer.IsServer())
		{
			NormalDice dieDice = die as NormalDice;
			dieDice.DiceRolled += ReadNormalDice;
			dieDice.SimulatePhysics = true;
		}
		AddChild(die);

		RigidBody3D goofyDie = _goofyDice.Instantiate() as RigidBody3D;
		_diceModels[1] = goofyDie;
		if (Multiplayer.IsServer())
		{
			GoofyDice goofyDieDice = goofyDie as GoofyDice;
			goofyDieDice.DiceRolled += ReadGoofyDice;
			goofyDieDice.SimulatePhysics = true;
		}
		AddChild(goofyDie);
	}

	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void EndTurn()
	{
		// Remove dice from playing field
		for (int i = 0; i < 2; i++) _diceModels[i].QueueFree();

		// Increment current player and turn
		int currentOrder = (_currentPlayer.Order + 1) % LobbyManager.Players.Count;
		long currentId = _turnOrder[currentOrder];
		_currentPlayer = LobbyManager.Players[currentId];
		CurrentTurn++;

		if (Multiplayer.IsServer()) EmitSignal(SignalName.TurnStarted, currentId);
	}

	void ChangeHealth(long playerId, int health)
	{
		Player player = LobbyManager.Players[playerId];
		if (player.Health == 15 && health > 0) return; // Can't go over 15 health

		player.Health = Math.Clamp(player.Health + health, 0, 15);
		// Update player on all clients
		_lobbyManager.SynchronizePlayers();

		EmitSignal(SignalName.HealthChanged, player.Order, player.Health, health > 0);
	}
}
