using Godot;
using System;
using System.Linq;

public partial class Game : Node
{
    [Signal]
    public delegate void GoofyFunctionFinishedEventHandler();
    [Signal]
    public delegate void GoofyFunctionReadyEventHandler();
    [Signal]
    public delegate void ShootingEventHandler(long shooterId);
    [Signal]
    public delegate void ShotEventHandler();
    [Signal]
    public delegate void DiceTaskFinishedEventHandler();

    enum Location { Paradise, Dusty, Tomato, Snobby, Viking, Retail, Lonely, Pleasant,
                    Flush, Wailing, Salty, Haunted, Greasy, Loot, Lazy, Tilted }
    Action[] _board;
    Action[] _diceFuncs;

    // Location of a wall if there is one, -1 otherwise
    int _wallSpace = -1;
    int _rolledNumber = 0;
    float _distanceBetweenSpaces = 0.92057f;
    Vector3[] _directions = { Vector3.Left, Vector3.Forward, Vector3.Right, Vector3.Back };
    bool _goofyReady = false;
    bool _goofyFinished = false;
    bool _waitForDiceTask = false;

    async void ReadNormalDice(int num)
    {
        _rolledNumber = num + 1;

        // Wait for goofy dice to finish rolling
        _goofyReady = true;
        EmitSignal(SignalName.GoofyFunctionReady);
        if (!_goofyFinished) await ToSignal(this, SignalName.GoofyFunctionFinished);

        int spacesToMove = _wallSpace > 0 ? _wallSpace : _rolledNumber;
        Rpc(nameof(MovePlayer), _turnOrder[_currentPlayer.Order], spacesToMove);
        _goofyFinished = false;
    }
    /// <param name="funcIdx">0: Heal, 1: Shoot, 2: Boogie Bomb, 3: Wall</param>
    async void ReadGoofyDice(int funcIdx)
    {
        // Wait for number to be rolled
        if (!_goofyReady) await ToSignal(this, SignalName.GoofyFunctionReady);

        _diceFuncs[funcIdx].Invoke();
        // Wait for dice task to finish
        if (_waitForDiceTask) await ToSignal(this, SignalName.DiceTaskFinished);
        _waitForDiceTask = false;

        _goofyReady = false;
        _goofyFinished = true;
        EmitSignal(SignalName.GoofyFunctionFinished);
    }

    void HealDice()
    {
        long id = _turnOrder[_currentPlayer.Order];
        ChangeHealth(id, 1);
    }
    void Shoot()
    {
        EmitSignal(SignalName.Shooting, _turnOrder[_currentPlayer.Order]);
        // Wait for player to be shot before continuing
        _waitForDiceTask = true;
    }
    public void Shoot(long id)
    {
        // TODO: Apply active cards

        EmitSignal(SignalName.Shot);
        RpcId(1, nameof(OnShot), id);
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void OnShot(long shotPlayerId)
    {
        ChangeHealth(shotPlayerId, -1);
        EmitSignal(SignalName.DiceTaskFinished);
    }
    void BoogieBomb()
    {
        // TODO: Implement Bush

        // Hit every other player
        long thisPlayerId = _turnOrder[_currentPlayer.Order];
        // Convert Player keys to array to prevent error due to collection modification from synchronizing players O_O
        foreach (long playerId in LobbyManager.Players.Keys.ToArray())
            if (playerId != thisPlayerId) ChangeHealth(playerId, -1);
    }
    void Wall()
    {
        GD.Print("WALL");
    }

    [Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void MovePlayer(long playerId, int numSpaces)
    {
        Player player = LobbyManager.Players[playerId];
        Target playerModel = players[player.Order];

        AnimationPlayer animPlayer = playerModel.GetNode<AnimationPlayer>("AnimationPlayer");
        string animName = playerModel.Name.Equals("Banana") ? "M_MED_Banana_ao|M_MED_Banana_ao|M_MED_Banana_ao|Emote_Boogie_Down_Loop_CMM" :
                          playerModel.Name.Equals("Batman") ? "Armature|Armature|mixamo_com|Layer0_002" :
                          "mixamo_com";
        Animation anim = animPlayer.GetAnimation(animName);

        // Animate movement as well
        int posTrack = anim.GetTrackCount() - 1;

        // Remove previous keyframes
        while (anim.TrackGetKeyCount(posTrack) > 0) anim.TrackRemoveKey(posTrack, 0);

        Vector3 finalPosition = playerModel.Position;
        anim.TrackInsertKey(posTrack, 0, playerModel.Position);
        // Check for corner rounding
        int newSpace = player.Space + numSpaces;
        int overflow = newSpace % 8;
        if (overflow < numSpaces)
        {
            int underflow = numSpaces - overflow;
            Vector3 midPoint = playerModel.Position + _distanceBetweenSpaces * underflow * _directions[player.Space / 8];
            anim.TrackInsertKey(posTrack, anim.Length * underflow / numSpaces, midPoint);
            finalPosition = midPoint;
        }
        else overflow = numSpaces;
        finalPosition += _distanceBetweenSpaces * overflow * _directions[newSpace / 8 % 4];

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

        void Land(StringName _)
        {
            if (Multiplayer.IsServer()) LandOnSpace(player, passedGo);
            animPlayer.AnimationFinished -= Land;
        }
        animPlayer.AnimationFinished += Land;
    }

    void LandOnSpace(Player player, bool passedGo)
    {
        if (passedGo)
        {
            // TODO: Pass go
        }

        _board[player.Space].Invoke();

        Rpc(nameof(EndTurn));
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