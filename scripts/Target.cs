using Godot;

public partial class Target : Node3D
{
	const int RAYLENGTH = 20;

	long _playerId;
	public long PlayerId { get => _playerId; private set => _playerId = value; }
	long _shooterId = 0;
	bool _detecting = false;
	Game _game;
	Camera3D _camera;
	Target _aimedAt;

	public override void _Ready()
	{
		_game = GetNode<Game>("/root/Game");
		_camera = _game.GetNode<Camera3D>("Camera3D");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_detecting || Multiplayer.GetUniqueId() != _shooterId || _playerId != _shooterId) return;

		PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
		Vector2 mousePos = GetViewport().GetMousePosition();

		Vector3 origin = _camera.ProjectRayOrigin(mousePos);
		Vector3 end = origin + _camera.ProjectRayNormal(mousePos) * RAYLENGTH;
		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
		query.CollisionMask = 0b10; // Only detect layer 2

		Godot.Collections.Dictionary result = spaceState.IntersectRay(query);
		if (result.Count == 0)
		{
			_aimedAt = null;
			return;
		}
		_aimedAt = result["collider"].AsGodotObject() as Target;
		// Don't shoot yourself
		if (_aimedAt.Name.Equals(Name))
			_aimedAt = null;
	}

	public override void _Input(InputEvent @event)
	{
		if (!_detecting || _shooterId != PlayerId) return;
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
		{
			// Shoot current _aimedAt
			if (_aimedAt != null) _game.Shoot(_aimedAt.PlayerId);
		}
	}

	public void SetId(long id) => PlayerId = id;

	public void BeginDetectingShots(long shooterId) => RpcId(shooterId, nameof(DetectShots));
	[Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void DetectShots()
	{
		_shooterId = Multiplayer.GetUniqueId();
		_detecting = true;
	}
	public void StopDetectingShots()
	{
		_shooterId = 0;
		_detecting = false;
	}
}
