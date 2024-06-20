public partial class GoofyDice : Dice
{
	// Map of numbers on die to functions on goofy die
	// 0: Heal, 1: Shoot, 2: Boogie Bomb, 3: Wall
	readonly int[] functions = { 2, 1, 0, 0, 1, 3 };

	protected override int GetOrientationResult(int face)
	{
		return functions[face];
	}
}
