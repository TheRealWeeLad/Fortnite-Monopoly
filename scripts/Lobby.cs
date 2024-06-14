using Godot;
using System.Collections.Generic;

public partial class Lobby : PanelContainer
{
	// Signals to LobbyManager
	[Signal]
	public delegate void CreateMultiplayerLobbyEventHandler(string username);
	[Signal]
	public delegate void JoinMultiplayerLobbyEventHandler(string username, string ip);

	LobbyManager lobbyManager;
	PackedScene lobbyMemberScene;
	PanelContainer lobby;
	VBoxContainer lobbyContainer;
	TabContainer tabContainer;
	Label lobbyTitle;
	Button startButton;
	TextEdit createNameField;
	TextEdit joinNameField;
	TextEdit ipField;

	int numPlayers = 0;

	readonly Dictionary<long, PanelContainer> lobbyMembers = new();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		lobbyManager = GetTree().Root.GetNode<LobbyManager>("LobbyManager");
		lobbyMemberScene = GD.Load<PackedScene>("res://scenes/lobby_member.tscn");
		lobby = GetNode<PanelContainer>("%Lobby");
		lobbyContainer = GetNode<VBoxContainer>("%Lobby Container");
		tabContainer = GetNode<TabContainer>("%Tabs");
		lobbyTitle = GetNode<Label>("%Lobby Title");
		startButton = GetNode<Button>("%Start Button");
		createNameField = GetNode<TextEdit>("%Create Name Field");
		joinNameField = GetNode<TextEdit>("%Join Name Field");
		ipField = GetNode<TextEdit>("%IP Field");

		// Connect signals
		lobbyManager.PlayerConnected += PlayerConnected;
		lobbyManager.PlayerDisconnected += PlayerDisconnected;
		lobbyManager.ServerDisconnected += ServerDisconnected;
		// Signals to lobby manager
		GetNode<Button>("%Start Button").Pressed += lobbyManager.StartGame;
		CreateMultiplayerLobby += lobbyManager.CreateLobby;
		JoinMultiplayerLobby += lobbyManager.JoinLobby;
		// DEBUG
		GetNode<Button>("%Debug Create").Pressed += lobbyManager.CreateDebug;
		GetNode<Button>("%Debug Join").Pressed += lobbyManager.JoinDebug;
	}
	// Called when this object is unloaded
	public override void _ExitTree()
	{
		// Reset Autoload Signals
		lobbyManager.PlayerConnected -= PlayerConnected;
		lobbyManager.PlayerDisconnected -= PlayerDisconnected;
		lobbyManager.ServerDisconnected -= ServerDisconnected;
	}

	// Called when Create Lobby button pressed
	void CreateLobby()
	{
		// Get username
		string username = createNameField.Text;
		if (username.Equals(""))
		{
			GD.Print("No username");
			return;
		}

		// Create multiplayer lobby
		EmitSignal(SignalName.CreateMultiplayerLobby, username);
	}

	// Called when Join Lobby button pressed
	void JoinLobby()
	{
		// Get username
		string username = joinNameField.Text;
		if (username.Equals(""))
		{
			GD.Print("No username");
			return;
		}

		// Get ip
		string ip = ipField.Text;
		if (ip.Equals(""))
		{
			GD.Print("No ip found");
			return;
		}

		// Join multiplayer lobby
		EmitSignal(SignalName.JoinMultiplayerLobby, username, ip);
	}

	// Called when any player connects
	void PlayerConnected(long id, string username)
	{
		// Show Lobby
		lobby.Visible = true;
		tabContainer.Visible = false;
		// Change Title to host's name
		if (id == 1) lobbyTitle.Text = username + "'s Lobby";

		// Add member panel
		PanelContainer member = lobbyMemberScene.Instantiate() as PanelContainer;
		lobbyMembers.Add(id, member);
		lobbyContainer.AddChild(member);
		if (id == 1) username += " (Host)";
		member.GetNode<MarginContainer>("MarginContainer")
			.GetNode<Label>("Username").Text = username;

		numPlayers++;
		UpdateStartButton();
	}

	// Called when any player disconnects
	void PlayerDisconnected(long id)
	{
		lobbyMembers[id].QueueFree();
		lobbyMembers.Remove(id);
		numPlayers--;
		UpdateStartButton();
	}

	// Called when host player disconnects
	void ServerDisconnected()
	{
		foreach (PanelContainer member in lobbyMembers.Values)
		{
			member.QueueFree();
			numPlayers--;
		}
		lobbyMembers.Clear();

		// Return to tabs
		tabContainer.Visible = true;
		lobby.Visible = false;
	}

	// Called when Leave Lobby button is pressed
	void LeaveLobby()
	{
		// Disconnect from lobby
		if (Multiplayer.IsServer()) Multiplayer.MultiplayerPeer.Close();
		else Multiplayer.MultiplayerPeer.DisconnectPeer(1);
		
		// Show tabs again
		Visible = false;
		tabContainer.Visible = true;
	}

	void UpdateStartButton()
	{
		// Update Start Button
		startButton.Text = $"Start Game ({numPlayers}/4)";
		startButton.Disabled = !Multiplayer.IsServer() || numPlayers < 2; // Can't play with only one player :(
	}
}
