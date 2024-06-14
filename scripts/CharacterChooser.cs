using Godot;
using Godot.Collections;

public partial class CharacterChooser : VBoxContainer
{
	TextureButton[] _choiceButtons;
	Texture2D[] _choiceBorders;
	Label _timerText;
	Timer _timer;
	Game _game;
	MarginContainer _characterMenu;

	Dictionary<int, int> chosenButtons = new();

	// Choice prefab
	PackedScene _choiceScene;

	int numChosen = 0;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// Build button list
		TextureButton cuddleButt = GetNode<TextureButton>("%CuddleTeamLeaderButton");
		TextureButton batmanButt = GetNode<TextureButton>("%BatmanButton");
		TextureButton bananaButt = GetNode<TextureButton>("%BananaButton");
		TextureButton travisButt = GetNode<TextureButton>("%TravisScottButton");
		_choiceButtons = new TextureButton[] { cuddleButt, batmanButt, bananaButt, travisButt };

		// Build border list
		Texture2D player1 = GD.Load<Texture2D>("res://assets/visuals/player1.png");
		Texture2D player2 = GD.Load<Texture2D>("res://assets/visuals/player2.png");
		Texture2D player3 = GD.Load<Texture2D>("res://assets/visuals/player3.png");
		Texture2D player4 = GD.Load<Texture2D>("res://assets/visuals/player4.png");
		_choiceBorders = new Texture2D[] { player1, player2, player3, player4 };

		_choiceScene = GD.Load<PackedScene>("res://scenes/choice_border.tscn");
		_timerText = GetNode<Label>("%CharacterChooseTimerLabel");
		_timer = GetNode<Timer>("%CharacterChooseTimer");
		_game = GetNode<Game>("/root/Game");
		_characterMenu = GetNode<MarginContainer>("%CharacterMenu");
	}

	// Called once per frame
	public override void _Process(double delta)
	{
		if (_timer.TimeLeft > 0)
		{
			_timerText.Text = Mathf.Ceil(_timer.TimeLeft).ToString();
		}
	}

	void Timeout()
	{
		_timerText.Text = "0";

		// Start Game
		if (Multiplayer.IsServer())
		{
			// Hide Menu
			_characterMenu.Visible = false;

			_game.Rpc("StartGame", chosenButtons);
		}
	}

	void CuddleChosen() => RpcId(1, nameof(Choose), 0);
	void BatmanChosen() => RpcId(1, nameof(Choose), 1);
	void BananaChosen() => RpcId(1, nameof(Choose), 2);
	void TravisChosen() => RpcId(1, nameof(Choose), 3);

	// Idx is (0, 1, 2, 3) for (Cuddle, Batman, Banana, and Travis) respectively
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void Choose(int idx)
	{
		TextureButton buttonChosen = _choiceButtons[idx];
		// Fail if button has already been chosen
		if (buttonChosen.GetChildCount() > 0) return;

		// Get reference to player
		long id = Multiplayer.GetRemoteSenderId();
		int playerOrder = LobbyManager.Players[id].Order;

		// Remove previous border if there was one
		if (chosenButtons.TryGetValue(playerOrder, out int previous))
			Rpc(nameof(RemoveBorder), previous);

		// Add new border
		Rpc(nameof(AddBorder), playerOrder, idx);

		numChosen++;

		// Start/Restart timer if everyone has chosen
		if (numChosen >= LobbyManager.Players.Count)
			Rpc(nameof(StartTimer));
	}

	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void StartTimer()
	{
		_timerText.Visible = true;
		_timer.Start();
	}
	// RPCs to emulate MultiplayerSpawner under several paths
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void AddBorder(int order, int idx)
	{
		// Instantiate choice border under correct texture button
		TextureRect border = _choiceScene.Instantiate() as TextureRect;
		_choiceButtons[idx].AddChild(border);

		// Set overlay to correct number
		border.Texture = _choiceBorders[order];
		chosenButtons[order] = idx;
	}
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void RemoveBorder(int idx) => _choiceButtons[idx].GetChild(0).QueueFree();
}
