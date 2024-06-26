using Godot;
using System.ComponentModel;
using FortniteMonopolyExtensions;
using System.Linq;

public partial class TreasureCard : Node3D
{
	public enum CardType
	{
		// RARE ITEMS
		[Description("Rare Sniper")] RareSniper, // 2 of these
		[Description("Shoot Wherever")] ShootWherever, // 2 of these
		[Description("Med Kit")] MedKit,
		[Description("Clinger")] Clinger, // 2 of these
		[Description("Bounce Pad")] Bouncer, // 2 of these
		// EPIC ITEMS
		[Description("Stink Bomb")] StinkBomb,
		[Description("Epic Sniper")] EpicSniper, // 2 of these
		// LEGENDARY ITEMS
		[Description("Bush")] Bush,
		[Description("Legendary Sniper")] LegendarySniper, // 2 of these
		[Description("Chug Jug")] ChugJug
	}
	public CardType Type { get; private set; }
	public bool OneTimeUse { get; private set; }
	readonly int[] _oneTimeUseIndices = { 2, 3, 4, 9 };
	string[] _itemDescriptions =
	{
		"When you roll (SHOOT),\nthe targeted player pays 2 HP\nto the bank",
		"When you roll (SHOOT),\nyou may ignore line of sight\nand target any player",
		"(One Time Use)\nCollect 5 HP from the bank",
		"(One Time Use)\nChoose any space. All players\non that space pay 4 HP\nto the bank",
		"(One Time Use)\nMove any player up to 4 spaces.\nThey must complete the action\nof the space where they land",
		"When you roll (SHOOT),\nall players in your line of sight\npay 3 HP to the bank.",
		"When you roll (SHOOT),\nthe targeted player pays 3 HP\nto the bank",
		"Boogie bombs do not\naffect you",
		"When you roll (SHOOT),\nthe targeted player pays 4 HP\nto the bank",
		"(One Time Use)\nCollect HP from the bank\nuntil you have 15!"
	};

	Label3D _itemName;
	Label3D _itemDescription;

	public override void _Ready()
	{
		_itemName = GetNode<Label3D>("ItemName");
		_itemDescription = GetNode<Label3D>("ItemDescription");
	}

	public void SetCardType(int cardIdx)
	{
		Type = (CardType)cardIdx;
		// Update text on card
		_itemName.Text = Type.GetDescription();
		_itemName.Modulate = cardIdx < 5 ? Color.FromHtml("0000aaff") :
							 cardIdx < 7 ? Color.FromHtml("aa00aaff") :
							 Color.FromHtml("dddd00ff");
		_itemDescription.Text = _itemDescriptions[cardIdx];
		if (_oneTimeUseIndices.Contains(cardIdx)) OneTimeUse = true;
	}
}