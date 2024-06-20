using Godot;

public partial class HealthIndicator : Label3D
{
	float _startPos;
	float _endPos;
	float _time = 0;
	Timer _timer;

	public override void _Ready()
	{
		_timer = GetChild(0) as Timer;
		GlobalPosition = new(GlobalPosition.X, _startPos, GlobalPosition.Z);
	}

	public void SetPositions(float start, float end)
	{
		_startPos = start;
		_endPos = end;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_startPos == 0) return;
		if (GlobalPosition.Y >= _endPos) return;

		float newY = Mathf.Lerp(_startPos, _endPos, _time);
		GlobalPosition = new(GlobalPosition.X, newY, GlobalPosition.Z);

		if (_endPos <= newY)
		{
			_timer.Start();
		}

		_time += (float)delta;
	}

	void Timeout()
	{
		QueueFree();
	}
}
