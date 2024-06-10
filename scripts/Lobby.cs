using Godot;
using System.Collections.Generic;

public partial class Lobby : PanelContainer
{
	PackedScene lobbyMemberScene;
	PanelContainer lobby;
	VBoxContainer lobbyContainer;
	TabContainer tabContainer;
	Label lobbyTitle;
	Button startButton;

	int numPlayers = 0;

	readonly Dictionary<long, PanelContainer> lobbyMembers = new();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		lobbyMemberScene = GD.Load<PackedScene>("res://scenes/lobby_member.tscn");
		lobby = GetNode<PanelContainer>("%Lobby");
		lobbyContainer = GetNode<VBoxContainer>("%Lobby Container");
		tabContainer = GetNode<TabContainer>("%Tabs");
		lobbyTitle = GetNode<Label>("%Lobby Title");
		startButton = GetNode<Button>("%Start Button");
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
		// Update Start Button
		startButton.Text = $"Start Game ({numPlayers}/4)";
		startButton.Disabled = !Multiplayer.IsServer() || numPlayers < 2; // Can't play with only one player :(
	}

	// Called when any player disconnects
	void PlayerDisconnected(long id)
	{
		lobbyMembers[id].QueueFree();
		lobbyMembers.Remove(id);
		numPlayers--;
	}

	// Called when host player disconnects
	void ServerDisconnected()
	{
		foreach (PanelContainer member in lobbyMembers.Values)
			member.QueueFree();
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
}
