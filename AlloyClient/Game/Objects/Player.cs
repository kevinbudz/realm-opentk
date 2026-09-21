using System;
using AlloyClient.Assets.Libraries;
using AlloyClient.Game.Objects.Enums;
using AlloyClient.Game.Objects.Util;
using AlloyClient.Networking;
using AlloyClient.Networking.Enums;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Networking.Structs.DataObjects;
using AlloyClient.ParticleEffects;
using AlloyClient.Rendering;
using AlloyClient.Rendering.Types;
using AlloyClient.Ui.Character;
using AlloyClient.Utils;
using Alloy.Common.Structs;
using Alloy.Engine;
using AlloyClient.Logging;
using Microsoft.Extensions.Logging;
using OpenTK.Mathematics;

namespace AlloyClient.Game.Objects;

public class Player : Entity {
    private const float MoveThreshold = 0.4f;
    private const int FocusedSpeed = 15;
    private const float MinMoveSpeed = 0.004f;
    private const float MaxMoveSpeed = 0.0096f;
    private const float MinAttackFreq = 0.0015f;
    private const float MaxAttackFreq = 0.008f;

    private readonly static ILogger Logger = ILogger.CreateLogger(nameof(Player));

    public float Rotate;
    public Vector2 RelativeMoveVector;

    public float MovementMultiplier = 1;

    public bool Focused;

    public int SinkLevel;

    public bool Locked;

    public bool Ignored;



    #region StatData

    public int MaxMp;
    public int Mp;
    public int Attack;
    public int Speed;
    public int Dexterity;
    public int Vitality;
    public int Wisdom;

    public int MaxHpBoost;
    public int MaxMpBoost;
    public int AttackBoost;
    public int DefenseBoost;
    public int SpeedBoost;
    public int DexterityBoost;
    public int VitalityBoost;
    public int WisdomBoost;
    
    public double AttackPeriod;
    public double AttackStart;

    public double NextAbilityTime;

    public int AccountId;

    public int NextLevelExp;
    public int Experience;

    public int Stars;

    public int Credits;

    public int Fame;
    public int CurrentFame;
    public int FameGoal;

    public bool NameChosen = true;

    public string Guild;
    public int GuildRank;

    public int OxygenBar;

    public int HealthStackCount;
    public int MagicStackCount;

    public ushort Skin;

    public int PartyId;

    public int LdBoosted;
    public int LdBoostAmount;

    public int XpBoostTime;

    public bool HasBackPack;

    public bool IsFellowGuild;

    #endregion

    protected override RenderBase GetRenderType(ushort type) {
        ObjectLibrary.TypeToObjectProps.TryGetValue(type, out var props);
        if (props == null) {
            return null;
        }

        if (props.RealSize != -1) {
            Size = props.RealSize;
        }

        if (props.MinSize != props.MaxSize) {
            var maxSteps = (props.MaxSize - props.MinSize) / props.SizeStep;
            Size = props.MinSize + (int) (Random.Shared.NextSingle() * maxSteps) * props.SizeStep;
        }

        return new TypePlayer(this);
    }

    private void HandleRelativeMovement(double time, double dt) {
        float angle = Settings.CameraAngle;

        if (Rotate != 0) {
            angle = (float) (angle + dt * Settings.RotateSpeed * Rotate);
            Settings.CameraAngle.Set((angle % MathHelper.TwoPi + MathHelper.TwoPi) % MathHelper.TwoPi);
        }
        
        var moveSpeed = GetMoveSpeed();
            var moveVectorAngle = MathF.Atan2(RelativeMoveVector.Y, RelativeMoveVector.X);

            // TODO: Madness debuffs and dashes
            if (RelativeMoveVector.X != 0 || RelativeMoveVector.Y != 0) {
                if (Tile.GroundProperties.SlideAmount > 0) {
                    var slideVector = new Vector2 {
                        X = moveSpeed * MathF.Cos(angle + moveVectorAngle),
                        Y = moveSpeed * MathF.Sin(angle + moveVectorAngle)
                    };

                    //TODO: double check monogame to make sure its the same thing
                    //var slideLen = slideVector.Length();
                    var slideLen = slideVector.LengthFast;
                    slideVector *= -1 * (Tile.GroundProperties.SlideAmount - 1);
                    MovementVector *= Tile.GroundProperties.SlideAmount;

                    if (MovementVector.LengthFast < slideLen) {
                        MovementVector += slideVector;
                    }
                }
                else {
                    MovementVector.X = moveSpeed * MathF.Cos(angle + moveVectorAngle);
                    MovementVector.Y = moveSpeed * MathF.Sin(angle + moveVectorAngle);
                }
            }
            else if (MovementVector.LengthFast > 0.00012 && Tile.GroundProperties.SlideAmount > 0) {
                MovementVector *= Tile.GroundProperties.SlideAmount;
            }
            else {
                MovementVector.X = 0;
                MovementVector.Y = 0;
            }

            // TODO: Push tiles
            // if (Tile.GroundProperties.Push) {
            //     MovementVector.X = MovementVector.X - Tile.GroundProperties.Animate.Dx / 1000;
            //     MovementVector.Y = MovementVector.Y - Tile.GroundProperties.Animate.Dy / 1000;
            // }

            if (TextureData.HasAnimationData && this == Map.LocalPlayer) {
                AnimateCharacter(time);
            }

            WalkTo((float) (Position.X + dt * MovementVector.X), (float) (Position.Y + dt * MovementVector.Y));
            RenderBaseType.SetPosition(Position.X, Position.Y, Z);
        
    }

    public override bool Update(double time, double dt) {
        if (ObjectId == Map.LocalPlayerId) {
            HandleRelativeMovement(time, dt);
        } else if (!base.Update(time, dt)) {
            return false;
        }
        Effect?.Update(time, dt);
        return true;
    }

    public override void UpdateStats(StatData[] statData, int offset, int count) {
        base.UpdateStats(statData, offset, count);

        #region Parse StatData

        for (var i = 0; i < count; i++) {
            var stat = statData[offset + i];
            switch (stat.Type) {
                case StatsType.MaximumMp:
                    MaxMp = stat.Value;
                    break;
                case StatsType.Mp:
                    Mp = stat.Value;
                    break;
                case StatsType.Attack:
                    Attack = stat.Value;
                    break;
                case StatsType.Speed:
                    Speed = stat.Value;
                    break;
                case StatsType.Dexterity:
                    Dexterity = stat.Value;
                    break;
                case StatsType.Vitality:
                    Vitality = stat.Value;
                    break;
                case StatsType.Wisdom:
                    Wisdom = stat.Value;
                    break;
                case StatsType.MaxHpBonus:
                    MaxHpBoost = stat.Value;
                    break;
                case StatsType.MaxMpBonus:
                    MaxMpBoost = stat.Value;
                    break;
                case StatsType.AttackBonus:
                    AttackBoost = stat.Value;
                    break;
                case StatsType.DefenseBonus:
                    DefenseBoost = stat.Value;
                    break;
                case StatsType.SpeedBonus:
                    SpeedBoost = stat.Value;
                    break;
                case StatsType.DexterityBonus:
                    DexterityBoost = stat.Value;
                    break;
                case StatsType.VitalityBonus:
                    VitalityBoost = stat.Value;
                    break;
                case StatsType.WisdomBonus:
                    WisdomBoost = stat.Value;
                    break;
                case StatsType.AccountId:
                    AccountId = stat.Value;
                    break;
                case StatsType.NextLevelXp:
                    NextLevelExp = stat.Value;
                    break;
                case StatsType.Experience:
                    Experience = stat.Value;
                    break;
                case StatsType.NumStars:
                    Stars = stat.Value;
                    break;
                case StatsType.Credits:
                    Credits = stat.Value;
                    break;
                case StatsType.Fame:
                    Fame = stat.Value;
                    break;
                case StatsType.CharFame:
                    CurrentFame = stat.Value;
                    break;
                case StatsType.NextClassQuestFame:
                    FameGoal = stat.Value;
                    break;
                case StatsType.Guild:
                    if (Guild != stat.Text)
                        Guild = string.Intern(stat.Text);
                    break;
                case StatsType.GuildRank:
                    GuildRank = stat.Value;
                    break;
                case StatsType.NameChosen:
                    // Flash parity (GameServerConnection NAME_CHOSEN_STAT
                    // handler): player.nameChosen_ = value != 0. Without this
                    // the name changer always shows the buy branch.
                    NameChosen = stat.Value != 0;
                    break;
                case StatsType.Oxygen:
                    OxygenBar = stat.Value;
                    break;
                case StatsType.HealthPotionStack:
                    HealthStackCount = stat.Value;
                    break;
                case StatsType.MagicPotionStack:
                    MagicStackCount = stat.Value;
                    break;
                case StatsType.Texture:
                    Skin = (ushort)stat.Value;
                    SetPlayerSkinTemplate(Skin);
                    break;
                case StatsType.HasBackpack:
                    HasBackPack = stat.Value != 0;
                    // add backpack signal
                    break;
                case StatsType.SinkLevel:
                    // The local player's sink is predicted in OnMove like the Flash
                    // client (which skips this stat for itself); the server value
                    // only drives remote players.
                    if (this != Map.LocalPlayer)
                        SinkLevel = stat.Value;
                    break;
            }
        }

        #endregion

    }

    public override AtlasData GetTexture(double time, out bool attackFrame, out bool flipped) {
        var texture = new AtlasData();
        
        var action = AnimationType.Stand;
        var idx = 0d;
        
        if (time < AttackStart + AttackPeriod) {
            action = AnimationType.Attack;
            idx = (time - AttackStart) % AttackPeriod / AttackPeriod;
            FacingAngle = AttackAngle;
        } else if (MovementVector != Vector2.Zero) {
            var walkPer = 3.5f / GetMoveSpeed();
            FacingAngle = MathF.Atan2(MovementVector.Y, MovementVector.X);
            action = AnimationType.Walk;
            idx = time % walkPer / walkPer;
        }

        texture = TextureData.AnimatedTextures.TextureFromFacing(FacingAngle, action, (float) idx, out attackFrame, out flipped);

        return texture;
    }

    private void SetPlayerSkinTemplate(ushort skin) {
        if (skin == 0) return;

        TextureData = ObjectLibrary.TypeToTextureData[skin];
        Texture = TextureData.HasAnimationData ? TextureData.AnimatedTextures.FaceRight[0] : TextureData.GetTexture();
        RenderBaseType.SetTexture(Texture, false);
    }

    private void WalkTo(float x, float y) {
        var pos = ModifyMove(x, y);
        MoveTo(pos.X, pos.Y);
    }

    private float AttackFrequency() {
        if (HasConditionEffect(ConditionEffect.Dazed)) {
            return MinAttackFreq;
        }
        
        var attFreq = MinAttackFreq + Dexterity / 75f * (MaxAttackFreq - MinAttackFreq);

        //Flash Player.attackFrequency and the server GetAttackFrequency both use 1.5x.
        if (HasConditionEffect(ConditionEffect.Berserk))
            attFreq *= 1.5f;
        
        return attFreq;
    }

    public void Shoot(float attackAngle, GameTime gameTime) {
        if (HasConditionEffect(ConditionEffect.Stunned) || HasConditionEffect(ConditionEffect.Paused))
            return;
        
        var item = Equipment[0];

        if (item == null)
            return;

        var temp = AttackFrequency();
        
        AttackPeriod = 1 / temp * (1 / item.RateOfFire);

        if (gameTime.TotalMs < AttackStart + AttackPeriod)
            return;

        AttackAngle = attackAngle;
        AttackStart = gameTime.TotalMs;
        
        var props = ObjectLibrary.TypeToObjectProps[item.ObjectType];
        
        var projType = ObjectLibrary.IdToObjectType[props.Projectiles[0].ObjectId];
        var objProps =  ObjectLibrary.TypeToObjectProps[projType];
        var projProps = props.Projectiles[0];

        for (int i = 0; i < props.NumProjectiles; i++) {
            var arc = MathHelper.DegreesToRadians(props.ArcGap) * (props.NumProjectiles - 1);
            var startAngle = AttackAngle - arc / 2;
            var angle = startAngle + MathHelper.DegreesToRadians(props.ArcGap) * i;

            //Bullet ids must stay in lockstep with the server: both start
            //at 0 and decrement per shot, like the Flash client's
            //map_.nextProjectileId_. The server keys ShotProjectiles by
            //these ids, so any other scheme makes EnemyHit unknown.
            var bId = Map.NextProjectileId - i;
            var proj = ObjectPools.Projectiles.Pop();
            var dmg = Random.Shared.NextRange(projProps.MinDamage, projProps.MaxDamage); // Migrate to match server rng
            proj.Reset(bId, dmg, angle, this, objProps, projProps, null, Position);
            Map.AddProjectile(proj);
        }
        Map.NextProjectileId -= props.NumProjectiles;

        //One packet per attack: the server fans out NumShots around Angle
        //itself and rejects a count that does not match the weapon.
        var shoot = PlayerShoot.CreatePacket();
        shoot.Time = Environment.TickCount;
        shoot.StartingPos = new Position { X = Position.X, Y = Position.Y };
        shoot.Angle = attackAngle;
        shoot.Ability = false;
        shoot.NumShots = props.NumProjectiles;

        Client.QueuePacket(shoot);
    }

    public bool TryUseAbility(Vector2 target, float angle, GameTime gameTime) {
        if (Equipment.Length <= AbilityHelper.AbilitySlotId)
            return false;

        var ability = Equipment[AbilityHelper.AbilitySlotId];
        var data = ItemData.Length > AbilityHelper.AbilitySlotId ? ItemData[AbilityHelper.AbilitySlotId] : -1;
        var now = gameTime.TotalMs;

        if (!AbilityHelper.CanUse(ability, Mp, now, NextAbilityTime))
            return false;

        NextAbilityTime = AbilityHelper.NextUseTime(ability, data, now);

        var use = UseItem.CreatePacket();
        // The server's per-player time gate compares every packet's time
        // against one baseline, so UseItem must share Move/PlayerShoot's
        // Environment.TickCount epoch. Game-time TotalMs is a different
        // epoch and gets rejected as "Invalid time useitem" + disconnect.
        use.Time = Environment.TickCount;
        use.SlotObject.ObjectId = ObjectId;
        use.SlotObject.SlotId = AbilityHelper.AbilitySlotId;
        use.ItemUsePos.X = target.X;
        use.ItemUsePos.Y = target.Y;
        use.UseType = (byte)UseType.START_USE;
        Client.QueuePacket(use);

        if (AbilityHelper.HasShootActivate(ability))
            ShootAbility(angle, gameTime);

        return true;
    }

    private void ShootAbility(float attackAngle, GameTime gameTime) {
        if (HasConditionEffect(ConditionEffect.Stunned) || HasConditionEffect(ConditionEffect.Paused))
            return;

        if (Equipment.Length <= AbilityHelper.AbilitySlotId)
            return;

        var item = Equipment[AbilityHelper.AbilitySlotId];
        if (item == null)
            return;

        if (!ObjectLibrary.TypeToObjectProps.TryGetValue(item.ObjectType, out var props))
            return;
        if (!props.Projectiles.TryGetValue(0, out var projProps))
            return;

        var projType = ObjectLibrary.IdToObjectType[projProps.ObjectId];
        var objProps = ObjectLibrary.TypeToObjectProps[projType];

        var shoot = PlayerShoot.CreatePacket();
        shoot.Time = Environment.TickCount;
        shoot.StartingPos = new Position { X = Position.X, Y = Position.Y };
        shoot.Angle = attackAngle;
        shoot.Ability = true;
        shoot.NumShots = props.NumProjectiles;

        Client.QueuePacket(shoot);

        // Bullet ids must stay in lockstep with the server even when the
        // local visuals below are skipped.
        var baseId = Map.NextProjectileId;
        Map.NextProjectileId = baseId - props.NumProjectiles;

        // Local visuals need baked textures, which headless tests never load;
        // the queued ability shoot above is the authoritative result.
        if (!ObjectLibrary.TypeToTextureData.ContainsKey(projType))
            return;

        for (var i = 0; i < props.NumProjectiles; i++) {
            var arc = MathHelper.DegreesToRadians(props.ArcGap) * (props.NumProjectiles - 1);
            var startAngle = attackAngle - arc / 2;
            var angle = startAngle + MathHelper.DegreesToRadians(props.ArcGap) * i;

            var bId = baseId - i;
            var proj = ObjectPools.Projectiles.Pop();
            var dmg = Random.Shared.NextRange(projProps.MinDamage, projProps.MaxDamage);
            proj.Reset(bId, dmg, angle, this, objProps, projProps, null, Position);
            Map.AddProjectile(proj);
        }
    }

    private Vector2 ModifyMove(float x, float y) {
        var result = new Vector2();

        //Flash modifyMove and the server both freeze local movement while paralyzed.
        if (HasConditionEffect(ConditionEffect.Paralyzed)) {
            result.X = X;
            result.Y = Y;
            return result;
        }

        var dX = x - Position.X;
        var dY = y - Position.Y;

        if (dX < MoveThreshold && dX > -MoveThreshold && dY < MoveThreshold && dY > -MoveThreshold) {
            result = ModifyStep(x, y);
            return result;
        }

        result.X = Position.X;
        result.Y = Position.Y;

        var stepSize = MoveThreshold / Math.Max(Math.Abs(dX), Math.Abs(dY));
        var d = 0.0f;
        var done = false;

        while (!done) {
            if (d + stepSize >= 1) {
                stepSize = 1 - d;
                done = true;
            }

            result = ModifyStep(result.X + dX * stepSize, result.Y + dY * stepSize);
            d += stepSize;
        }

        return result;
    }

    // Try to keep it as close to the original as possible?
    // Don't wanna mess with it too much.
    // ReSharper disable PossibleLossOfFraction
    // ReSharper disable CompareOfFloatsByEqualityOperator
    private Vector2 ModifyStep(float x, float y) {
        var xCross = Position.X % 0.5f == 0 && x != Position.X || (int) (Position.X / 0.5f) != (int) (x / 0.5f);
        var yCross = Position.Y % 0.5f == 0 && y != Position.Y || (int) (Position.Y / 0.5f) != (int) (y / 0.5f);

        if (!xCross && !yCross || IsValidPosition(x, y)) {
            return new Vector2(x, y);
        }

        float nextXBorder = 0;
        float nextYBorder = 0;

        if (xCross) {
            nextXBorder = x > Position.X ? (int) (x * 2) / 2f : (int) (Position.X * 2) / 2f;

            if ((int) nextXBorder > (int) Position.X) {
                nextXBorder -= 0.01f;
            }
        }

        if (yCross) {
            nextYBorder = y > Position.Y ? (int) (y * 2) / 2f : (int) (Position.Y * 2) / 2f;

            if ((int) nextYBorder > (int) Position.Y) {
                nextYBorder -= 0.01f;
            }
        }

        if (!xCross) {
            if (Tile.GroundProperties.SlideAmount == 0) {
                return new Vector2(x, nextYBorder);
            }

            MovementVector *= -0.5f;
            MovementVector.X *= -1f;

            return new Vector2(x, nextYBorder);
        }

        if (!yCross) {
            if (Tile.GroundProperties.SlideAmount == 0) {
                return new Vector2(nextXBorder, y);
            }

            MovementVector *= -0.5f;
            MovementVector.Y *= -1f;

            return new Vector2(nextXBorder, y);
        }

        var xBorderDist = x > Position.X ? x - nextXBorder : nextXBorder - x;
        var yBorderDist = y > Position.Y ? y - nextYBorder : nextYBorder - y;

        if (xBorderDist > yBorderDist) {
            if (IsValidPosition(x, nextYBorder)) {
                return new Vector2(x, nextYBorder);
            }

            if (IsValidPosition(nextXBorder, y)) {
                return new Vector2(nextXBorder, y);
            }
        }
        else {
            if (IsValidPosition(nextXBorder, y)) {
                return new Vector2(nextXBorder, y);
            }

            if (IsValidPosition(x, nextYBorder)) {
                return new Vector2(x, nextYBorder);
            }
        }

        return new Vector2(nextXBorder, nextYBorder);
    }

    private bool IsValidPosition(float x, float y) {
        var tile = Map.LookupTile((int) x, (int) y);

        if (Tile != tile && (tile == null || !tile.IsWalkable())) {
            return false;
        }

        var xFrac = x - (int) x;
        var yFrac = y - (int) y;

        if (xFrac < 0.5) {
            if (IsFullOccupy(x - 1, y)) {
                return false;
            }

            if (yFrac < 0.5) {
                if (IsFullOccupy(x, y - 1) || IsFullOccupy(x - 1, y - 1)) {
                    return false;
                }
            }
            else if (yFrac > 0.5) {
                if (IsFullOccupy(x, y + 1) || IsFullOccupy(x - 1, y + 1)) {
                    return false;
                }
            }
        }
        else if (xFrac > 0.5) {
            if (IsFullOccupy(x + 1, y)) {
                return false;
            }

            if (yFrac < 0.5) {
                if (IsFullOccupy(x, y - 1) || IsFullOccupy(x + 1, y - 1)) {
                    return false;
                }
            }
            else if (yFrac > 0.5) {
                if (IsFullOccupy(x, y + 1) || IsFullOccupy(x + 1, y + 1)) {
                    return false;
                }
            }
        }
        else if (yFrac < 0.5) {
            if (IsFullOccupy(x, y - 1)) {
                return false;
            }
        }
        else if (yFrac > 0.5) {
            if (IsFullOccupy(x, y + 1)) {
                return false;
            }
        }

        return true;
    }

    public void SetRelativeMovement(float rotate, float relMoveVecX, float relMoveVecY) {
        Rotate = rotate;
        RelativeMoveVector.X = relMoveVecX;
        RelativeMoveVector.Y = relMoveVecY;

        if (HasConditionEffect(ConditionEffect.Confused)) {
            var temp = RelativeMoveVector.X;
            RelativeMoveVector.X = -RelativeMoveVector.Y;
            RelativeMoveVector.Y = -temp;
            Rotate = -Rotate;
        }
    }

    private float GetMoveSpeed() {
        // The Speed stat arrives combined (base + boost, like Flash speed_);
        // Focused caps it the same way for everyone.
        var speed = Focused ? FocusedSpeed : Speed;
        return ComputeMoveSpeed(speed,
            HasConditionEffect(ConditionEffect.Slowed),
            HasConditionEffect(ConditionEffect.Speedy) || HasConditionEffect(ConditionEffect.NinjaSpeedy),
            MovementMultiplier);
    }

    // Flash parity (Player.getMoveSpeed): pure for testability. Speed 0 walks
    // at MinMoveSpeed, 75 at MaxMoveSpeed, scaled by the ground multiplier;
    // Slowed pins to the minimum and Speedy multiplies by 1.5.
    public static float ComputeMoveSpeed(int speed, bool slowed, bool speedy, float multiplier) {
        if (slowed) {
            return MinMoveSpeed * multiplier;
        }

        var moveSpeed = MinMoveSpeed + speed / 75f * (MaxMoveSpeed - MinMoveSpeed);

        if (speedy) {
            moveSpeed *= 1.5f;
        }

        return moveSpeed * multiplier;
    }

    private static bool IsFullOccupy(float x, float y) {
        var tile = Map.LookupTile((int) x, (int) y);

        if (tile == null) {
            return true;
        }

        if (tile.Type == 255) {
            return true;
        }

        if (tile.OccupiedObject?.Properties.FullOccupy == true) {
            return true;
        }

        return false;
    }

    public void OnMove() {
        var tile = Map.LookupTile((int) Position.X, (int) Position.Y);

        if (tile == null) {
            return;
        }

        // if (tile.GroundProperties.Interactive) {
        //     var activateGround = ActivateGround.CreatePacket();
        //     activateGround.X = tile.X;
        //     activateGround.Y = tile.Y;
        //     Client.QueuePacket(activateGround);
        // }
        //
        // var interactiveObject = Map.GetInteractiveObject((int) Position.X, (int) Position.Y);
        // if (interactiveObject != null) {
        //     var objectInteract = ObjectInteract.CreatePacket();
        //     objectInteract.ObjectId = interactiveObject.ObjectId;
        //     Client.QueuePacket(objectInteract);
        // }

        const float maxSinkLevel = 18;

        if (tile.GroundProperties is { Sinking: true }) {
            // TODO: IMPORTANT! PHARAOH"S CURSE FORCE SINK NEEDS TO BE IMPLEMENTED
            SinkLevel = (int) MathF.Min(SinkLevel + 1, maxSinkLevel);
            MovementMultiplier = 0.1f + (1 - SinkLevel / maxSinkLevel) * (tile.GroundProperties.Speed - 0.1f);
        }
        else {
            SinkLevel = 0;
            MovementMultiplier = tile.GroundProperties.Speed;
        }

        // Flash parity (Player.onMove): immediate local feedback for damaging
        // ground (Lava, etc.), like Flash's damage() call. HP itself stays
        // server-authoritative (Damage packet + Hp stats); this only shows
        // the hit and never touches Hp.
        var covered = tile.OccupiedObject?.Properties?.ProtectFromGroundDamage == true;
        if (TakesGroundDamage(tile.GroundProperties.MinDamage, tile.GroundProperties.MaxDamage,
                HasConditionEffect(ConditionEffect.Invincible), covered)) {
            var min = Math.Min(tile.GroundProperties.MinDamage, tile.GroundProperties.MaxDamage);
            var max = Math.Max(tile.GroundProperties.MinDamage, tile.GroundProperties.MaxDamage);
            var amount = Random.Shared.Next(min, max + 1);
            Map.AddParticleEffect(new HitEffect(this, 0xFF0000));
            if (amount > 0) {
                var color = HasConditionEffect(ConditionEffect.ArmorBroken)
                    ? CharacterStatusText.PiercedColor : CharacterStatusText.NormalColor;
                NotificationLayer.AddStatusText(this, $"-{amount}", color, 1000, 0, true);
            }
        }
    }

    // Flash parity (Player.onMove ground branch): damaging ground hurts
    // unless invincible or standing on a ProtectFromGroundDamage cover.
    public static bool TakesGroundDamage(int minDamage, int maxDamage, bool invincible, bool covered) =>
        (minDamage > 0 || maxDamage > 0) && !invincible && !covered;
}