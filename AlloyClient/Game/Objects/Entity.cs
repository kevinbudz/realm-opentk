using System;
using System.Collections.Generic;
using System.Linq;
using Alloy.Common;
using AlloyClient.Assets;
using AlloyClient.Assets.Libraries;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Game.Objects.Enums;
using AlloyClient.Networking.Enums;
using AlloyClient.Networking.Structs.DataObjects;
using AlloyClient.ParticleEffects;
using AlloyClient.Rendering;
using AlloyClient.Rendering.Types;
using Alloy.UiLib.Signals;
using AlloyClient.Utils;
using Alloy.Common.Structs;
using AlloyClient.Logging;
using Microsoft.Extensions.Logging;
using OpenTK.Mathematics;

namespace AlloyClient.Game.Objects;

public class Entity {
    private readonly static ILogger Logger = ILogger.CreateLogger(nameof(Entity));

    public const float AttackPeriod = 300;

    public int ObjectId;
    public ushort Type;
    
    public readonly Signal<int> InventoryUpdate = new();

    public float HeightOffset;
    public Vector2 Position;
    public float Rotation;
    public float X => Position.X;
    public float Y => Position.Y;
    public float Z;

    public Vector2 MovementVector;
    public Vector2 TickPosition;
    public Vector2 PositionAtTick;

    public int LastTickId;
    public double LastTickUpdateTime;

    public MapTile Tile;
    public ObjectProperties Properties;
    
    public double AttackStart;
    public float AttackAngle;

    #region StatData

    public string Name;

    public int Texture1Id;
    public int Texture2Id;

    public int GlowColor;

    public int MaxHp;
    public int Hp;
    public int Defense;

    public int Size = 100;

    public int Level;

    public ItemDesc[] Equipment = new ItemDesc[20];

    // Per-slot ItemData bitmask (ITEMDATA_n stats), -1 when unknown.
    // Mirrors the Flash itemData ints carried alongside each equipment slot.
    public int[] ItemData = Enumerable.Repeat(-1, 20).ToArray();

    public ConditionEffectBucket EffectBuckets;

    public int ConnectType;

    public int PlayerHost;

    public bool PermaPet;

    public int MadnessPullRadius;
    public int MadnessPullMaxSpeed;

    public int QuestGlowColor;

    public int MinimumHp;

    public bool DontFaceAttacks;

    public bool AllDebuffsImmune;

    public int DamagersCount;

    public int CustomTexture;

    // Flash parity (Portal.active_): defaults to true; the wire Active stat
    // (34) drives it, so usable portals show Enter until closed.
    public bool PortalUsable = true;

    // Flash parity (SellableObject/Merchant): shop stats carried on the wire
    // as MerchandiseType(31)/Price(32)/Currency(37)/Count(39)/MinsLeft(40)/
    // Discount(41)/RankReq(42). Defaults mirror the Flash field initializers
    // (merchandiseType_/count_/minsLeft_ = -1, currency_ = INVALID).
    public int MerchandiseType = -1;
    public int MerchandisePrice;
    public int MerchandiseCurrency = -1;
    public int MerchandiseCount = -1;
    public int MerchandiseMinsLeft = -1;
    public int MerchandiseDiscount;
    public int MerchandiseRankReq;

    #endregion

    public RenderBase RenderBaseType;

    public TextureData TextureData;

    public AtlasData Texture;
    
    public bool Flipped;

    public float FacingAngle;
    
    public float Jitter;

    public ParticleEffect Effect;

    public readonly Dictionary<ProjectileKey, double> MultiHitUsed = [];

    public void SetObjectId(int id) {
        ObjectId = id;
        Jitter = Random.Shared.NextSingle() * 0.00002f - 0.00001f;
        Effect = ParticleEffect.FromProperties(Properties.Effect, this);
    }

    public void SetType(ushort type) {
        Type = type;
        TextureData = ObjectLibrary.TypeToTextureData[type];
        Texture = TextureData.HasAnimationData ? TextureData.AnimatedTextures.FaceRight[0] : TextureData.GetTexture();
        RenderBaseType = GetRenderType(type);
        // Flash parity (GameObject defense_ = int(objectXML.Defense)):
        // enemies keep their XML defense; players overwrite this via the
        // Defense stat (total base + boost) in UpdateStats.
        if (Properties != null)
            Defense = Properties.Defense;
    }

    // Flash parity (GameObject.damageWithDefense + realm-server
    // GameUtils.GetDefenseDamage): predicted hit numbers must subtract the
    // target's defense first. Raw projectile/AoE damage above the player is
    // wrong without this. Pure for testability.
    public static int DamageWithDefense(int origDamage, int targetDefense, bool armorPiercing, Entity target) =>
        DamageWithDefense(origDamage, targetDefense, armorPiercing,
            target?.HasConditionEffect(ConditionEffect.ArmorBroken) == true,
            target?.HasConditionEffect(ConditionEffect.Armored) == true,
            target?.HasConditionEffect(ConditionEffect.Invulnerable) == true);

    public static int DamageWithDefense(int origDamage, int targetDefense, bool armorPiercing, bool armorBroken, bool armored, bool invulnerable) {
        var def = targetDefense;
        if (armorPiercing || armorBroken)
            def = 0;
        else if (armored)
            def *= 2;
        var min = origDamage * 3 / 20;
        var d = Math.Max(min, origDamage - def);
        if (invulnerable)
            d = 0;
        return d;
    }

    // Flash parity (Merchant.setMerchandiseType/getTexture): a shopkeeper
    // renders the sold item's texture, not the Merchant base sprite. Only the
    // Merchant class overrides getTexture in Flash; GuildMerchant and
    // ClosedVaultChest keep their own sprite. Unknown merchandise types keep
    // the base texture instead of throwing.
    public void RefreshMerchandiseTexture() {
        if (Properties?.Class != "Merchant")
            return;
        if (MerchandiseType <= 0 || MerchandiseType > ushort.MaxValue)
            return;
        if (!ObjectLibrary.TypeToTextureData.TryGetValue((ushort)MerchandiseType, out var merchandise))
            return;
        TextureData = merchandise;
        Texture = merchandise.HasAnimationData ? merchandise.AnimatedTextures.FaceRight[0] : merchandise.GetTexture();
        RenderBaseType?.SetTexture(Texture);
    }

    public Color GetDominateColor() {
        if (RenderBaseType is TypeWall) return TextureData.TopTexture.DominantColor;
        return TextureData.DominantColor;
    }

    protected virtual RenderBase GetRenderType(ushort type) {
        ObjectLibrary.TypeToObjectProps.TryGetValue(type, out var props);

        if (props == null) {
            return new TypeNullObject();
        }

        if (props.RealSize != -1) {
            Size = props.RealSize;
        }

        if (props.MinSize != props.MaxSize) {
            var maxSteps = (props.MaxSize - props.MinSize) / props.SizeStep;
            Size = props.MinSize + (int)(Random.Shared.NextSingle() * maxSteps) * props.SizeStep;
        }

        if (!string.IsNullOrEmpty(props.Model)) {
            return new TypeModel3D(props.Model, this);
        }
        
        if (props.DrawOnGround) {
            return new TypeGroundObject(this);
        }

        return props.Class switch {
            "Wall" => new TypeWall(this),
            "DoubleWall" => new TypeWall(this, ModelType.PbDoubleWall),
            "DoubleWall2" => new TypeWall(this, ModelType.PbDoubleWall), // ModelType.PbDoubleWall2
            "TripleWall" => new TypeWall(this, ModelType.PbDoubleWall), // ModelType.PbTripleWall
            _ => new TypeGameObject(this)
        };
    }

    public void SetPos(float x, float y) {
        Position.X = x;
        Position.Y = y;

        Rotation = Properties.Rotation;
        RenderBaseType.SetPosition(x, y);
        Effect?.SetEntityPosition(Position);
    }

    public bool HasConditionEffect(ConditionEffect effect) => EffectBuckets.HasConditionEffect(effect);

    // Immediate single-effect apply for Damage/Aoe packet effect bytes.
    // Stats re-sync wholesale via SetBucket a tick later.
    public void AddConditionEffect(ConditionEffect effect) => EffectBuckets.AddConditionEffect(effect);

    public virtual bool Update(double time, double dt) {
        if (Settings.MovementInterpolation) {
            var dx = TickPosition.X - Position.X;
            var dy = TickPosition.Y - Position.Y;
            var distSqr = dx * dx + dy * dy;

            if (distSqr > 0.0001) {
                var tickDt = dt * 0.004;
                var pX = tickDt * TickPosition.X + (1 - tickDt) * Position.X;
                var pY = tickDt * TickPosition.Y + (1 - tickDt) * Position.Y;
                MoveTo((float) pX, (float) pY);
            } else {
                MovementVector.X = 0;
                MovementVector.Y = 0;
            }
        } else {
            if (MovementVector is not {X: 0, Y: 0}) {
                if (LastTickId >= Map.LastTickId) {
                    var tickDt = time - LastTickUpdateTime;
                    var pX = PositionAtTick.X + tickDt * MovementVector.X;
                    var pY = PositionAtTick.Y + tickDt * MovementVector.Y;
                    MoveTo((float) pX, (float) pY);
                } else {
                    MovementVector.X = 0;
                    MovementVector.Y = 0;
                    MoveTo(TickPosition.X, TickPosition.Y);
                }
            } else {
                MovementVector.X = 0;
                MovementVector.Y = 0;
            }
        }

        // add wall support here at some point
        if (TextureData.HasAnimationData) {
            AnimateCharacter(time);
        }

        foreach (var (key, value) in MultiHitUsed) {
            if (value < time) {
                MultiHitUsed.Remove(key);
            }
        }

        Effect?.Update(time, dt);

        RenderBaseType.SetPosition(Position.X, Position.Y, Z);
        return true;
    }

    public bool UpdateVisibility(in Camera camera) {
        var visible = camera.IsVisible(Position, RenderBaseType.GetCullRadius(), Settings.MaxRenderDistance.Value);
        RenderBaseType.SetVisibility(visible);

        if (!visible) {
            return false;
        }

        var matrix = camera.DepthMatrix;
        var sort = Position.X * matrix.M12 + Position.Y * matrix.M22 + matrix.M42;
        RenderBaseType.SetDepth(0.5f + 0.4f * sort + Jitter);
        return true;
    }

    public bool MoveTo(float x, float y) {
        var tile = Map.LookupTile((int)x, (int)y);

        if (tile == null) {
            return false;
        }
        
        Position.X = x;
        Position.Y = y;

        if (Properties.Static) {
            if (Tile != null) {
                Tile.OccupiedObject = null;
            }

            tile.OccupiedObject = this;
        }

        Tile = tile;

        return true;
    }

    // Flash parity: TextureRedrawer scales sprites 5x and one atlas texel
    // covers 0.1 world units, so one Flash pixel of sink clips 0.02 worlds.
    public const float SinkPixelToWorld = 0.02f;

    // Flash parity for GameObject.draw's h2 = tile sink + sinkLevel, zeroed
    // while flying or standing on a ProtectFromSink object. Pure for testability.
    public static float ComputeSinkHeight(bool flying, bool protectFromSink, bool tileSink, bool tileOverlay, int sinkLevel) {
        if (flying || protectFromSink) {
            return 0f;
        }

        var px = 0;
        if (tileSink) {
            px += tileOverlay ? 6 : 12; // Square.sink_: 12, or 6 on redrawn (blended) tiles
        }

        if (sinkLevel > 0) {
            px += sinkLevel;
        }

        return px * SinkPixelToWorld;
    }

    // World-space sink clip: Drop lowers the head side by the Flash pixel
    // count, Rise lifts the feet side by the atlas/flash margin difference
    // (Alloy pads 1 texel per side, Flash's texture has 12px top / 1px bottom
    // margins: 5k-1 px) so the visible rows and below-feet line match Flash.
    public readonly record struct SinkClip(float Drop, float Rise);

    public static float ComputeSinkRise(float sizeScale) =>
        MathF.Max(0f, (5f * sizeScale - 1f) * SinkPixelToWorld);

    public static SinkClip ComputeSinkClip(bool flying, bool protectFromSink, bool tileSink, bool tileOverlay, int sinkLevel, float sizeScale) {
        if (flying || protectFromSink) {
            return default;
        }

        var px = 0;
        if (tileSink) {
            px += tileOverlay ? 6 : 12; // Square.sink_: 12, or 6 on redrawn (blended) tiles
        }

        if (sinkLevel > 0) {
            px += sinkLevel;
        }

        if (px <= 0) {
            return default;
        }

        return new SinkClip(px * SinkPixelToWorld, ComputeSinkRise(sizeScale));
    }

    // World-space sink clip from the entity's live state. Only players
    // accumulate a sink level; mobs sink from the tile alone.
    public SinkClip GetSinkClip() {
        var tile = Tile;
        var props = Properties;
        if (tile == null || props == null) {
            return default;
        }

        var sinkLevel = this is Player player ? player.SinkLevel : 0;
        return ComputeSinkClip(props.Flying, tile.OccupiedObject?.Properties?.ProtectFromSink == true,
            tile.GroundProperties.Sink, tile.HasOverlay, sinkLevel, Size / 100f);
    }

    public void OnTickPosition(float x, float y, double tickTime, int tickId, bool isPlayer) {
        if (!Settings.MovementInterpolation && LastTickId < Map.LastTickId && !isPlayer) {
            MoveTo(TickPosition.X, TickPosition.Y);
        }

        TickPosition.X = x;
        TickPosition.Y = y;
        LastTickId = tickId;
        LastTickUpdateTime = tickTime;
        PositionAtTick.X = Position.X;
        PositionAtTick.Y = Position.Y;

        if (!isPlayer) {
            MovementVector.X = (float)((TickPosition.X - PositionAtTick.X) / tickTime);
            MovementVector.Y = (float)((TickPosition.Y - PositionAtTick.Y) / tickTime);
        }
    }

    public virtual void UpdateStats(StatData[] statData, int offset, int count) {
        for (var i = 0; i < count; i++) {
            var stat = statData[offset + i];
            switch (stat.Type) {
                case StatsType.MaximumHp:
                    MaxHp = stat.Value;
                    break;
                case StatsType.Hp:
                    Hp = stat.Value;
                    break;
                case StatsType.Defense:
                    Defense = stat.Value;
                    break;
                case StatsType.Size:
                    Size = stat.Value;
                    break;
                case StatsType.Level:
                    Level = stat.Value;
                    break;
                case StatsType.Inventory0:
                case StatsType.Inventory1:
                case StatsType.Inventory2:
                case StatsType.Inventory3:
                case StatsType.Inventory4:
                case StatsType.Inventory5:
                case StatsType.Inventory6:
                case StatsType.Inventory7:
                case StatsType.Inventory8:
                case StatsType.Inventory9:
                case StatsType.Inventory10:
                case StatsType.Inventory11:
                    var index = stat.Type - StatsType.Inventory0;
                    if (stat.Value == -1) {
                        Equipment[index] = null;
                    } else if (Equipment[index] == null || (Equipment[index] != null && stat.Value != Equipment[index].ObjectType)) {
                        Equipment[index] = ObjectLibrary.TypeToItem[(ushort)stat.Value];
                    }
                    InventoryUpdate.Dispatch(index);
                    break;
                case StatsType.Condition1:
                    //Server bits follow its Nothing=0, Quiet=1, ... numbering;
                    //translate to this client's Dead-shifted enum values.
                    EffectBuckets.SetBucket(0, ConditionEffects.TranslateServerMask(stat.Value));
                    RenderBaseType.Extra.Alpha = HasConditionEffect(ConditionEffect.Invisible) ? 0.5f : 1;
                    break;
                case StatsType.Name:
                    if (Name != stat.Text) {
                        Name = string.Intern(stat.Text);
                        RenderBaseType.SetName(stat.Text);
                    }
                    break;
                case StatsType.Texture1:
                    if (stat.Value == Texture1Id) {
                        break;
                    }

                    Texture1Id = stat.Value;
                    // TexturingCache = new Dict;
                    // Portrait = null;
                    break;
                case StatsType.Texture2:
                    if (stat.Value == Texture2Id) {
                        break;
                    }

                    Texture2Id = stat.Value;
                    // TexturingCache = new Dict;
                    // Portrait = null;
                    break;
                case StatsType.Glow:
                    GlowColor = stat.Value;
                    break;
                case StatsType.AltTexture:
                    SetAltTexture(stat.Value);
                    break;
                case StatsType.BackPack0:
                case StatsType.BackPack1:
                case StatsType.BackPack2:
                case StatsType.BackPack3:
                case StatsType.BackPack4:
                case StatsType.BackPack5:
                case StatsType.BackPack6:
                case StatsType.BackPack7:
                    index = 12 + stat.Type - StatsType.BackPack0;
                    if (stat.Value == -1) {
                        Equipment[index] = null;
                    } else if (Equipment[index] == null || (Equipment[index] != null && stat.Value != Equipment[index].ObjectType)) {
                        Equipment[index] = ObjectLibrary.TypeToItem[(ushort)stat.Value];
                    }
                    InventoryUpdate.Dispatch(index);
                    break;
                case StatsType.HasBackpack:
                    //todo
                    break;
                case StatsType.InventoryData0:
                case StatsType.InventoryData1:
                case StatsType.InventoryData2:
                case StatsType.InventoryData3:
                case StatsType.InventoryData4:
                case StatsType.InventoryData5:
                case StatsType.InventoryData6:
                case StatsType.InventoryData7:
                case StatsType.InventoryData8:
                case StatsType.InventoryData9:
                case StatsType.InventoryData10:
                case StatsType.InventoryData11:
                case StatsType.InventoryData12:
                case StatsType.InventoryData13:
                case StatsType.InventoryData14:
                case StatsType.InventoryData15:
                case StatsType.InventoryData16:
                case StatsType.InventoryData17:
                case StatsType.InventoryData18:
                case StatsType.InventoryData19:
                    var dataIndex = (int)stat.Type - (int)StatsType.InventoryData0;
                    if (dataIndex >= 0 && dataIndex < ItemData.Length)
                        ItemData[dataIndex] = stat.Value;
                    break;
                case StatsType.PortalUsable:
                    PortalUsable = stat.Value != 0;
                    break;
                case StatsType.Active:
                    // Flash parity (GameServerConnection ACTIVE_STAT handler):
                    // portal.active_ = value != 0. The realm server tracks
                    // usability in Portal.Usable but never exports it, so
                    // without this the panel would always show Full.
                    PortalUsable = stat.Value != 0;
                    break;
                case StatsType.MerchandiseType:
                    MerchandiseType = stat.Value;
                    RefreshMerchandiseTexture();
                    break;
                case StatsType.MerchandisePrice:
                    MerchandisePrice = stat.Value;
                    break;
                case StatsType.MerchandiseCurrency:
                    MerchandiseCurrency = stat.Value;
                    break;
                case StatsType.MerchandiseCount:
                    MerchandiseCount = stat.Value;
                    break;
                case StatsType.MerchandiseMinsLeft:
                    MerchandiseMinsLeft = stat.Value;
                    break;
                case StatsType.MerchandiseDiscount:
                    MerchandiseDiscount = stat.Value;
                    break;
                case StatsType.MerchandiseRankReq:
                    MerchandiseRankReq = stat.Value;
                    break;
            }
        }
    }

    //public virtual void Draw() {
    //    Renderer?.Render();
    //}

    public virtual void Reset() {
        ObjectId = 0;

        Position.X = 0;
        Position.Y = 0;
        Z = 0;

        MovementVector.X = 0;
        MovementVector.Y = 0;

        Tile = null;
    }

    public AtlasData GetTexture() {
        return Texture;
    }

    public void SetAttack(int containerType, float attackAngle) {
        AttackStart = Main.GameTime.TotalMs;
        AttackAngle = attackAngle;
    }
    
    public virtual AtlasData GetTexture(double time, out bool attackFrame, out bool flipped) {
        const float ZeroLimit = 0.00001f;
        const float NegZeroLimit = -ZeroLimit;
        
        
        var texture = new AtlasData();
        var action = AnimationType.Stand;
        attackFrame = flipped = false;
        
        if (TextureData.HasAnimationData) {
            var idx = 0d;
            if (time < AttackStart + AttackPeriod) {
                attackFrame = true;
                action = AnimationType.Attack;
                idx = (time - AttackStart) % AttackPeriod / AttackPeriod;
                FacingAngle = AttackAngle;
            } else if (MovementVector != Vector2.Zero) {
                var walkPer = 0.5f / (MovementVector.Length * 4);
                walkPer = walkPer + (400 - walkPer % 400);

                if (MovementVector.X > ZeroLimit || MovementVector.X < NegZeroLimit || MovementVector.Y > ZeroLimit || MovementVector.Y < NegZeroLimit) {
                    FacingAngle = MathF.Atan2(MovementVector.Y, MovementVector.X);
                    action = AnimationType.Walk;
                } else {
                    action = AnimationType.Stand;
                }
                
                idx = time % walkPer / walkPer;
            }

            texture = TextureData.AnimatedTextures.TextureFromFacing(FacingAngle, action, (float)idx, out attackFrame, out flipped);
        }
        return texture;
    }

    // we should move this shit somewhere else too
    public void AnimateCharacter(double time) {
        Texture = GetTexture(time, out var attackFrame, out Flipped);
        RenderBaseType.SetTexture(Texture, attackFrame);
    }

    private void SetAltTexture(int index) {
    }

    public void OnAddedToMap(Position position) {
        PositionAtTick = Position;
        TickPosition = Position;

        if (!MoveTo(position.X, position.Y)) {
            Logger.Log(LogLevel.Warning, $"Failed to add entity {ObjectId} to map.");
        }

        // Add entity's effect
    }

    public void OnRemovedFromMap() {
        if (Properties.Static && Tile != null) {
            if (Tile.OccupiedObject == this) {
                Tile.OccupiedObject = null;
            }

            Tile = null;
        }

        // Remove entity's effect

        // Remove dmg counter

        // Clear Madness dict

        // Dispose
    }
}
