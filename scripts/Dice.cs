using Godot;

public partial class Dice : RigidBody3D
{
	int frameNumber = 0;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		
	}

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
		// Only simulate physics on server
		//if (!Multiplayer.IsServer()) return;

		// Wait for inertia to be calculated before applying forces
		if (frameNumber == 1) ApplyForces();

		frameNumber++;
	}
}
