using Godot;
using System;
using System.Threading.Tasks;

public partial class Game : Node
{
    [Signal]
    public delegate void GoofyFunctionFinishedEventHandler();
    [Signal]
    public delegate void GoofyFunctionReadyEventHandler();

    enum Location { Paradise, Dusty, Tomato, Snobby, Viking, Retail, Lonely, Pleasant,
                    Flush, Wailing, Salty, Haunted, Greasy, Loot, Lazy, Tilted }
    Action[] _board;
    Func<Task>[] _diceFuncs;

    // Location of a wall if there is one, -1 otherwise
    int _wallSpace = -1;
    int _rolledNumber = 0;
    float _distanceBetweenSpaces = 0.92057f;
    Vector3[] _directions = { Vector3.Left, Vector3.Forward, Vector3.Right, Vector3.Back };
    bool _goofyReady = false;
    bool _goofyFinished = false;
    bool _firstAnimation = true;

    async void ReadNormalDice(int num)
    {
        _rolledNumber = num + 1;

        // Wait for goofy dice to finish rolling
        EmitSignal(SignalName.GoofyFunctionReady);
        _goofyReady = true;
        if (!_goofyFinished) await ToSignal(this, SignalName.GoofyFunctionFinished);

        int spacesToMove = _wallSpace > 0 ? _wallSpace : _rolledNumber;
        MovePlayer(_currentPlayer, spacesToMove);
        _goofyFinished = false;
    }
    /// <param name="funcIdx">0: Heal, 1: Shoot, 2: Boogie Bomb, 3: Wall</param>
    async void ReadGoofyDice(int funcIdx)
    {
        await _diceFuncs[funcIdx].Invoke();

        EmitSignal(SignalName.GoofyFunctionFinished);
        _goofyReady = false;
        _goofyFinished = true;
    }

    async Task HealDice()
    {
        long id = _turnOrder[_currentPlayer.Order];
        ChangeHealth(id, 1);
    }
    async Task Shoot()
    {
        
    }
    async Task BoogieBomb()
    {

    }
    async Task Wall()
    {
        // Wait for number to be rolled
        if (!_goofyReady) await ToSignal(this, SignalName.GoofyFunctionReady);


    }

    void MovePlayer(Player player, int numSpaces)
    {
        Node3D playerModel = players[player.Order];

        AnimationPlayer animPlayer = playerModel.GetNode<AnimationPlayer>("AnimationPlayer");
        string animName = playerModel.Name.Equals("Banana") ? "M_MED_Banana_ao | M_MED_Banana_ao | M_MED_Banana_ao | Emote_Boogie_Down_Loop_CMM" :
                          playerModel.Name.Equals("Batman") ? "Armature | Armature | mixamo_com | Layer0_002" :
                          "mixamo_com";
        Animation anim = animPlayer.GetAnimation(animName);

        // Animate movement as well
        int posTrack = anim.GetTrackCount() - 1;

        // Remove previous keyframes
        if (!_firstAnimation)
            while (anim.TrackGetKeyCount(posTrack) > 0) anim.TrackRemoveKey(posTrack, 0);

        Vector3 finalPosition = playerModel.Position;
        anim.TrackInsertKey(posTrack, 0, playerModel.Position);
        // Check for corner rounding
        int overflow = (player.Space + numSpaces) % 8;
        GD.Print(overflow);
        if (overflow < numSpaces)
        {
            int underflow = numSpaces - overflow;
            Vector3 midPoint = _distanceBetweenSpaces * underflow * _directions[numSpaces / 8];
            anim.TrackInsertKey(posTrack, anim.Length * underflow / numSpaces, midPoint);
            finalPosition = midPoint;
        }
        finalPosition += _distanceBetweenSpaces * overflow * _directions[(player.Space + numSpaces) / 8];

        anim.TrackInsertKey(posTrack, anim.Length, finalPosition);
        animPlayer.Play(animName);

        // Check if player passes go
        player.Space += numSpaces;
        bool passedGo = false;
        if (player.Space > 31)
        {
            player.Space -= 31;
            passedGo = true;
        }

        animPlayer.AnimationFinished += (StringName _) => LandOnSpace(player, passedGo);
        _firstAnimation = false;
    }

    void LandOnSpace(Player player, bool passedGo)
    {
        // TODO: Pass go

        _board[player.Space].Invoke();
    }

    // Space Functions
    void SpikeTrap()
    {
        ChangeHealth(_turnOrder[_currentPlayer.Order], -1);
    }
    void Campfire()
    {
        ChangeHealth(_turnOrder[_currentPlayer.Order], 1);
    }
    void Chest()
    {
        GD.Print("CHEST");
    }
    void LocationSpace()
    {
        GD.Print((Location)(_currentPlayer.Space / 2));
    }
    void Nothing() { }
    void Go()
    {
        GD.Print("PASSED GO");
    }
    void GoToJail()
    {
        GD.Print("JAIL");
    }
}