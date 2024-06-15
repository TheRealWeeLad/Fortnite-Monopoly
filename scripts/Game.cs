using Godot;
using Godot.Collections;
using System.Linq;
using System.Threading;

public partial class Game : Node
{
	[Signal]
	public delegate void TurnStartedEventHandler(long playerId);

	PackedScene _playerScene;
	PackedScene[] _characterModels;
	Node3D[] _players;
	long[] _turnOrder;

	int currentPlayerIdx = 0;

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
		_players = new Node3D[LobbyManager.Players.Count];

		// TODO: Load All Cards
		
		// TODO: Reconnect Signals for connecting/disconnecting

		// Tell server that this player is loaded
		LobbyManager lobbyManager = GetNode<LobbyManager>("/root/LobbyManager");
		lobbyManager.RpcId(1, "LoadPlayer");
	}

	// Called after character selection
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void StartGame(Dictionary<int, int> playerCharacters)
	{
		Vector3 startPosition = new(3.6f, 0, 3.5f);
		Vector3 startDeltaX = new(0.5f, 0, 0);
		Vector3 startDeltaZ = new(0, 0, 0.6f);
		
		// Place and assign character models
		foreach ((int playerNum, int characterIdx) in playerCharacters)
		{
			Node3D character = _characterModels[characterIdx].Instantiate() as Node3D;
			character.Name = $"Player{playerNum + 1}";
			character.Position = startPosition + playerNum / 2 * startDeltaX + playerNum % 2 * startDeltaZ;
			AddChild(character);
			_players[playerNum] = character;
		}

		// Get turn order
		_turnOrder = LobbyManager.Players.OrderBy(x => x.Value.Order).Select(x => x.Key).ToArray();

		// Only server should begin the player's turn
		if (Multiplayer.IsServer())
			// Start 1st player's turn
			EmitSignal(SignalName.TurnStarted, _turnOrder[currentPlayerIdx]);
	}
}
