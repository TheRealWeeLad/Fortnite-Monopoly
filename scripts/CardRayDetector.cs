using Godot;

public partial class CardRayDetector : Node3D
{
	Game _game;
	Camera3D _camera;
	TreasureCard _aimedAt;
	AnimationPlayer _animPlayer;

	bool _detecting;
	long _playerId;
	const int RAYLENGTH = 20;

	public override void _Ready()
	{
		_game = GetNode<Game>("/root/Game");
		_camera = _game.GetNode<Camera3D>("Camera3D");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_detecting || Multiplayer.GetUniqueId() != _playerId) return;

		PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
		Vector2 mousePos = GetViewport().GetMousePosition();

		Vector3 origin = _camera.ProjectRayOrigin(mousePos);
		Vector3 end = origin + _camera.ProjectRayNormal(mousePos) * RAYLENGTH;
		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
		query.CollisionMask = 0b100; // Only detect layer 3

		Godot.Collections.Dictionary result = spaceState.IntersectRay(query);
		if (result.Count == 0)
		{
			_aimedAt = null;
			_animPlayer?.PlayBackwards("Hover");
			_animPlayer = null;
			return;
		}
		TreasureCard hit = result["collider"].AsGodotObject() as TreasureCard;

		// Set hover animation
		if (hit.GetInstanceId() == _aimedAt?.GetInstanceId()) return;
		else if (_aimedAt != null) _animPlayer?.PlayBackwards("Hover");
		// Don't register unusable cards
		if (!hit.IsOneTimeUse() || hit.Holder != _playerId) return;
		_aimedAt = hit;

		_animPlayer = _aimedAt.GetNode<AnimationPlayer>("AnimationPlayer");
		Animation hoverAnim = _animPlayer.GetAnimation("Hover");

		hoverAnim.BezierTrackSetKeyValue(0, 0, _aimedAt.Position.Y);
		hoverAnim.BezierTrackSetKeyValue(0, 1, _aimedAt.Position.Y + 0.5f);

		_animPlayer.Play("Hover");
	}

	public override void _Input(InputEvent @event)
	{
		if (!_detecting || Multiplayer.GetUniqueId() != _playerId) return;
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
		{
			// Use the card that is being aimed at
			if (_aimedAt != null)
			{
				_game.UseCard(_aimedAt);
			}
		}
	}

	public void SetPlayerId(long playerId) => _playerId = playerId;
	void BeginDetecting() => _detecting = true;
	void StopDetecting() => _detecting = false;
}
