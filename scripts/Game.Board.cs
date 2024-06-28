﻿using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
    public delegate void WallRolledEventHandler(long playerId, int numRolled);
    [Signal]
    public delegate void DiceTaskFinishedEventHandler();
    [Signal]
    public delegate void CardPulledEventHandler();
    [Signal]
    public delegate void TurnResumedEventHandler();
    [Signal]
    public delegate void CardFinishedEventHandler();

    enum Location { Paradise, Dusty, Tomato, Snobby, Viking, Retail, Lonely, Pleasant,
        Flush, Wailing, Salty, Haunted, Greasy, Loot, Lazy, Tilted }
    Action[] _board;
    Action[] _diceFuncs;

    // Store each player's cards
    List<TreasureCard>[] _playerCards = { new(), new(), new(), new() };
    AnimationPlayer _currentCardAnimationPlayer;
    TreasureCard _cardToReveal;
    TreasureCard _cardInUse;
    CardRayDetector _cardRayDetector;

    // Location of a wall if there is one, -1 otherwise
    int _wallSpace = -1;
    int _rolledNumber = 0;
    float _distanceBetweenSpaces = 0.92057f;
    Vector3[] _directions = { Vector3.Left, Vector3.Forward, Vector3.Right, Vector3.Back };
    bool _goofyReady = false;
    bool _goofyFinished = false;
    bool _waitForDiceTask = false;
    bool _turnStalled = false;

    async void ReadNormalDice(int num)
    {
        _rolledNumber = num + 1;

        // Wait for goofy dice to finish rolling
        _goofyReady = true;
        EmitSignal(SignalName.GoofyFunctionReady);
        if (!_goofyFinished) await ToSignal(this, SignalName.GoofyFunctionFinished);

        int spacesToMove = _wallSpace >= 0 ? _wallSpace : _rolledNumber;
        _wallSpace = -1;
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

    // Card Ray Detection
    void BeginCardRayDetection() => _cardRayDetector.SetPlayerId(_turnOrder[_currentPlayer.Order]);
    // Using Cards
    public void UseCard(TreasureCard card)
    {
        _cardInUse = card;
        RpcId(1, nameof(UseCardRpc), card.Type.ToString());
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void UseCardRpc(string cardType)
    {
        long playerId = Multiplayer.GetRemoteSenderId();
        MethodInfo method = typeof(Game).GetMethod(cardType, BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(this, new object[] { playerId });
    }
    void DiscardCard() => RpcId(_turnOrder[_currentPlayer.Order], nameof(DiscardCardLocal));
    [Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void DiscardCardLocal()
    {
        int cardIdx = _playerCards[_currentPlayer.Order].IndexOf(_cardInUse);
        _cardInUse = null;
        Rpc(nameof(DiscardCardRpc), cardIdx);
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void DiscardCardRpc(int cardIdx)
    {
        TreasureCard card = _playerCards[_currentPlayer.Order][cardIdx];
        // Disable Raycast towards this card
        card.Die();

        // Play animation
        AnimationPlayer animPlayer = card.GetNode<AnimationPlayer>("AnimationPlayer");
        animPlayer.AnimationFinished += (StringName _) => card.QueueFree();
        // Set correct positions
        Animation anim = animPlayer.GetAnimation("Leave");
        anim.BezierTrackSetKeyValue(0, 0, card.Position.Y);
        anim.BezierTrackSetKeyValue(0, 1, card.Position.Y + 20);
        animPlayer.Play("Leave");
    }
    void MedKit(long playerId)
    {
        ChangeHealth(playerId, 5);
        EmitSignal(SignalName.CardFinished);
    }
    void ChugJug(long playerId)
    {
        ChangeHealth(playerId, 15);
        EmitSignal(SignalName.CardFinished);
    }
    void Clinger(long playerId)
    {
        GD.Print("CLINGER");
        // TODO: IMPLEMENT
    }
    void Bouncer(long playerId)
    {
        GD.Print("BOUNCER");
        // TODO: IMPLEMENT
    }

    // Dice Functions
    void HealDice()
    {
        long id = _turnOrder[_currentPlayer.Order];
        ChangeHealth(id, 1);
    }
    void Shoot()
    {
        long shooterId = _turnOrder[_currentPlayer.Order];
        bool ignoreLOS = false;
        // Apply any shoot-based cards
        List<TreasureCard> cards = _playerCards[_currentPlayer.Order];
        for (int i = 0; i < cards.Count; i++)
        {
            TreasureCard card = cards[i];
            switch (card.Type)
            {
                case TreasureCard.CardType.RareSniper: RevealCard(_currentPlayer.Order, i); break;
                case TreasureCard.CardType.EpicSniper: RevealCard(_currentPlayer.Order, i); break;
                case TreasureCard.CardType.LegendarySniper: RevealCard(_currentPlayer.Order, i); break;
                case TreasureCard.CardType.StinkBomb: RevealCard(_currentPlayer.Order, i); StinkBomb(shooterId); break;
                case TreasureCard.CardType.ShootWherever: ignoreLOS = true; RevealCard(_currentPlayer.Order, i); break;
            }
        }

        // Give up in no one in line of sight
        if (!ignoreLOS && !AnyoneInLineOfSight()) return;

        EmitSignal(SignalName.Shooting, shooterId);
        // Wait for player to be shot before continuing
        _waitForDiceTask = true;
    }
    public void Shoot(long id)
    {
        bool checkLOS = true;
        // Check for LOS bypass
        List<TreasureCard> copy = new(_playerCards[_currentPlayer.Order]);
        foreach (TreasureCard card in copy)
            switch (card.Type)
            {
                case TreasureCard.CardType.ShootWherever: checkLOS = false; break;
            }
        // Return if not in LOS
        if (checkLOS && !InLineOfSight(LobbyManager.Players[id])) return;

        EmitSignal(SignalName.Shot);
        RpcId(1, nameof(OnShot), id);
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void OnShot(long shotPlayerId)
    {
        ChangeHealth(shotPlayerId, -_currentPlayer.Damage);
        EmitSignal(SignalName.DiceTaskFinished);
    }
    void StinkBomb(long stinkerId)
    {
        foreach (long playerId in LobbyManager.Players.Keys)
        {
            if (playerId == stinkerId) continue;
            if (InLineOfSight(LobbyManager.Players[playerId])) ChangeHealth(playerId, -3);
        }
    }
    void BoogieBomb()
    {
        // TODO: FIX BUSH
        // Hit every other player
        long thisPlayerId = _turnOrder[_currentPlayer.Order];
        // Convert Player keys to array to prevent error due to collection modification from synchronizing players O_O
        foreach (long playerId in LobbyManager.Players.Keys.ToArray())
        {
            if (playerId == thisPlayerId) continue;
            // Check for Bush card
            int playerOrder = LobbyManager.Players[playerId].Order;
            List<TreasureCard> cards = _playerCards[playerOrder];
            for (int i = 0; i < cards.Count; i++)
            {
                TreasureCard card = cards[i];
                if (card.Type == TreasureCard.CardType.Bush) { RevealCard(playerOrder, i); continue; }
            }
            ChangeHealth(playerId, -1);
        }
    }
    void Wall()
    {
        EmitSignal(SignalName.WallRolled, _turnOrder[_currentPlayer.Order], _rolledNumber);
        // Wait for space to be chosen
        _waitForDiceTask = true;
    }
    public void SetWallSpace(int spaceChosen) => RpcId(1, nameof(SetWallSpaceRpc), spaceChosen);
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void SetWallSpaceRpc(int spaceChosen)
    {
        _wallSpace = spaceChosen;
        EmitSignal(SignalName.DiceTaskFinished);
    }

    // Player Movement
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

    async void LandOnSpace(Player player, bool passedGo)
    {
        if (passedGo)
        {
            // TODO: Pass go
        }

        _board[player.Space].Invoke();
        if (_turnStalled) await ToSignal(this, SignalName.TurnResumed);
        _turnStalled = false;

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
        if (_treasureCards.Count == 0) return; // Cry
        TreasureCard card = _treasureCards.Pop();

        // Update damage if the card is a sniper
        switch (card.Type)
        {
            case TreasureCard.CardType.RareSniper: IncreasePlayerDamage(_currentPlayer, 2); break;
            case TreasureCard.CardType.EpicSniper: IncreasePlayerDamage(_currentPlayer, 3); break;
            case TreasureCard.CardType.LegendarySniper: IncreasePlayerDamage(_currentPlayer, 4); break;
        }
        _lobbyManager.SynchronizePlayers();

        _turnStalled = true;
        Rpc(nameof(PickUpTreasureCard));
    }
    [Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void PickUpTreasureCard()
    {
        int playerOrder = LobbyManager.Players[Multiplayer.GetUniqueId()].Order;
        int pickerOrder = _currentPlayer.Order;
        TreasureCard card = _treasureCardPile.GetChild(-1) as TreasureCard;
        card.SetHolder(_turnOrder[pickerOrder]);
        _playerCards[_currentPlayer.Order].Add(card);
        // Remove card from pile
        card.Reparent(this);
        _currentCardAnimationPlayer = card.GetNode<AnimationPlayer>("AnimationPlayer");

        if (playerOrder == pickerOrder)
            _currentCardAnimationPlayer.Play("PickupP1");
        else
            // Find animation number
            _currentCardAnimationPlayer.Play($"PickupP{GetAnimIdx(playerOrder, pickerOrder)}");

        // Wait for player to confirm
        if (playerOrder == pickerOrder) EmitSignal(SignalName.CardPulled);
    }
    void ConfirmCard() => Rpc(nameof(StowCard));
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void StowCard()
    {
        int playerOrder = LobbyManager.Players[Multiplayer.GetUniqueId()].Order;
        int pickerOrder = _currentPlayer.Order;

        // Change final position to account for cards already there
        int animIdx = playerOrder == pickerOrder ? 1 : GetAnimIdx(playerOrder, pickerOrder);
        Animation anim = _currentCardAnimationPlayer.GetAnimation($"SetdownP{animIdx}");
        Vector3 finalPos = new()
        {
            X = anim.BezierTrackGetKeyValue(0, 1),
            Y = anim.BezierTrackGetKeyValue(1, 1),
            Z = anim.BezierTrackGetKeyValue(2, 1)
        };
        // Offset position by number of cards before this one
        finalPos += 0.8f * -_directions[animIdx - 1];
        finalPos += 0.05f * Vector3.Up;
        // Set final position
        for (int i = 0; i < 3; i++) anim.BezierTrackSetKeyValue(i, 1, finalPos[i]);

        // Play animation
        _currentCardAnimationPlayer.Play($"SetdownP{animIdx}");
        _currentCardAnimationPlayer = null;

        if (Multiplayer.IsServer()) EmitSignal(SignalName.TurnResumed);
    }
    void RevealCard(int playerOrder, int cardIdx)
    {
        TreasureCard card = _playerCards[playerOrder][cardIdx];
        if (!card.Hidden) return;

        Rpc(nameof(RevealAnimate), playerOrder, cardIdx);
    }
    [Rpc(CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    void RevealAnimate(int playerOrder, int cardIdx)
    {
        TreasureCard card = _playerCards[playerOrder][cardIdx];
        card.Reveal();
        AnimationPlayer animPlayer = card.GetNode<AnimationPlayer>("AnimationPlayer");

        if (Multiplayer.GetUniqueId() != _turnOrder[_currentPlayer.Order])
            animPlayer.Play("Reveal");
    }

    void LocationSpace()
    {
        GD.Print((Location)(_currentPlayer.Space / 2));
        // TODO: IMPLEMENT
    }
    void Nothing() { }
    void Go()
    {
        GD.Print("PASSED GO");
        // TODO: IMPLEMENT
    }
    void GoToJail()
    {
        GD.Print("JAIL");
        // TODO: IMPLEMENT
    }

    // Helper functions
    void IncreasePlayerDamage(Player player, int amount)
    {
        if (player.Damage == 1) player.Damage = amount;
        else player.Damage += amount;
    }
    bool AnyoneInLineOfSight()
    {
        foreach (long playerId in LobbyManager.Players.Keys)
            if (playerId != _turnOrder[_currentPlayer.Order] && InLineOfSight(LobbyManager.Players[playerId]))
                return true;
        return false;
    }
    bool InLineOfSight(Player player)
    {
        int currentSpace = _currentPlayer.Space;
        int otherSpace = player.Space;
        int diff = otherSpace - currentSpace;
        if (diff > 8) return false;

        // Num spaces past corner
        int past = currentSpace % 8;

        // Special case if current player is on a corner
        if (past == 0 && diff <= 8) return true;

        if (diff >= 0) // Other player is ahead
            return diff < 9 - past; // 9 spaces per row
        return -diff <= past;
    }
    int GetAnimIdx(int playerOrder, int cardHolderOrder)
    {
        int c = 0;
        for (int i = 0; i < LobbyManager.Players.Count; i++)
        {
            if (playerOrder == i) continue;
            if (i == cardHolderOrder)
            {
                return c + 2;
            }
            c++;
        }
        return c;
    }
}