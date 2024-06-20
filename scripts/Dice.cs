using Godot;

public abstract partial class Dice : RigidBody3D
{
	[Signal]
	public delegate void DiceRolledEventHandler(int faceFunc);

	int _frameNumber = 0;
	bool _finished = false;
	public bool SimulatePhysics { get; set; } = false; // Only simulate physics on server

	void ApplyForces()
	{
		RandomNumberGenerator rng = new();
		// Apply force for movement
		float strength = rng.RandfRange(1, 6);
		float randDirStrength = rng.RandfRange(-1, 1);
		Vector3 dir = (Vector3.Up + Vector3.Forward + Vector3.Right * randDirStrength).Normalized();
		ApplyImpulse(strength * dir);

		// Torque for rotation
		float rotStrength = rng.RandfRange(0.1f, 1);
		Vector3 rotDir = new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-1, 1), rng.RandfRange(-1, 1)).Normalized();
		ApplyTorqueImpulse(rotDir * rotStrength);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _PhysicsProcess(double delta)
	{
		if (!SimulatePhysics) return;
		if (_finished) return;

		// Wait for inertia to be calculated before applying forces
		if (_frameNumber == 1) ApplyForces();

		// Check velocity, and find orientation when it is stationary
		if (LinearVelocity.Length() <= 0.01f && AngularVelocity.Length() <= 0.01f && _frameNumber > 0)
		{
			CheckOrientation();
		}

		_frameNumber++;
	}

	void CheckOrientation()
	{
		// Measure dot products to see which direction is pointed either Up or Down
		Vector3 dots = new(Basis.Tdotx(Vector3.Up), Basis.Tdoty(Vector3.Up), Basis.Tdotz(Vector3.Up));

		// Use dot products to convert 1 or -1 to the corresponding face
		int xFace = (int)(3.5f * Mathf.Abs(dots.X) + dots.X * -1.5f);
		int yFace = (int)(3.5f * Mathf.Abs(dots.Y) + dots.Y * 0.5f);
		int zFace = (int)(3.5f * Mathf.Abs(dots.Z) + dots.Z * -2.5f);

		int num = GetOrientationResult(xFace != 0 ? xFace : yFace != 0 ? yFace : zFace);

		EmitSignal(SignalName.DiceRolled, num);
		_finished = true;
	}

	protected abstract int GetOrientationResult(int face);
}
