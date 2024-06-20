public partial class NormalDice : Dice
{
	protected override int GetOrientationResult(int face)
	{
		return face;
	}
}
