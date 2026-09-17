using System.Buffers.Binary;
using System.Text;
using AlloyClient.Networking;
using AlloyClient.Networking.Packets;
using AlloyClient.Networking.Packets.Incoming;
using AlloyClient.Networking.Packets.Outgoing;

namespace Alloy.UiLib.Tests;

//Wire regression tests for the game-server protocol. Each case feeds the
//OpenTK reader the exact bytes realm-server's GameServer writers emit
//(big-endian, see RotMG.Networking.PacketWriter) and asserts the parse.
//These schemas previously drifted from both the server and the Flash
//client (realm-client/.../messaging/impl), silently dropping enemy
//projectiles, AoEs, damage numbers, and ally visuals.
internal static class PacketWireTests {
    public static void Run() {
        EnemyShootSingle();
        EnemyShootMulti();
        Aoe();
        Damage();
        ServerPlayerShoot();
        ShowEffectLayouts();
        AllyShoot();
        NewTickPlayerStats();
        QueuedPacketsKeepOwnBuffers();
        OutgoingAcks();
        PacketIds();
    }

    private static void EnemyShootSingle() {
        //Server omits numShots/angleInc when numShots == 1.
        var body = new Writer();
        body.Int(-5);
        body.Int(100);
        body.Byte(3);
        body.Pos(1.5f, 2.5f);
        body.Float(1.25f);
        body.Short(40);

        var pkt = PacketUtils.CreateIncomingPacket(PacketId.EnemyShoot);
        var shoot = (EnemyShoot)pkt;
        var reader0 = Reader(body);
        shoot.Read(ref reader0);
        Equal(-5, shoot.BulletId);
        Equal(100, shoot.OwnerId);
        Equal((byte)3, shoot.ProjectileIndex);
        Equal(1.5f, shoot.StartPos.X);
        Equal(2.5f, shoot.StartPos.Y);
        Equal(1.25f, shoot.Angle);
        Equal((short)40, shoot.Damage);
        Equal((byte)1, shoot.NumShots);
        Equal(0f, shoot.AngleInc);
        shoot.ReturnPacket();
    }

    private static void EnemyShootMulti() {
        var body = new Writer();
        body.Int(0);
        body.Int(7);
        body.Byte(0);
        body.Pos(0f, 0f);
        body.Float(0f);
        body.Short(10);
        body.Byte(3);
        body.Float(0.5f);

        var shoot = (EnemyShoot)PacketUtils.CreateIncomingPacket(PacketId.EnemyShoot);
        var reader0b = Reader(body);
        shoot.Read(ref reader0b);
        Equal(0, shoot.BulletId);
        Equal((byte)3, shoot.NumShots);
        Equal(0.5f, shoot.AngleInc);
        shoot.ReturnPacket();
    }

    private static void Aoe() {
        var body = new Writer();
        body.Pos(10f, 20f);
        body.Float(3.5f);
        body.UShort(120);
        body.Byte(5);
        body.Int(-1);

        var aoe = (Aoe)PacketUtils.CreateIncomingPacket(PacketId.Aoe);
        var reader = Reader(body);
        aoe.Read(ref reader);
        Equal(10f, aoe.Pos.X);
        Equal(20f, aoe.Pos.Y);
        Equal(3.5f, aoe.Radius);
        Equal((ushort)120, aoe.Damage);
        Equal((byte)5, aoe.Effect);
        Equal(-1, aoe.Color);
        Equal(0, reader.Remaining);
        aoe.ReturnPacket();
    }

    private static void Damage() {
        var body = new Writer();
        body.Int(77);
        body.Byte(2);
        body.Byte(5);
        body.Byte(21);
        body.UShort(300);

        var dmg = (Damage)PacketUtils.CreateIncomingPacket(PacketId.Damage);
        var reader = Reader(body);
        dmg.Read(ref reader);
        Equal(77, dmg.TargetId);
        Equal(2, dmg.EffectCount);
        Equal((byte)5, dmg.Effects[0]);
        Equal((byte)21, dmg.Effects[1]);
        Equal((ushort)300, dmg.DamageAmount);
        //No kill/bullet/object trailer exists on the wire.
        Equal(0, reader.Remaining);
        dmg.ReturnPacket();
    }

    private static void ServerPlayerShoot() {
        var body = new Writer();
        body.Int(-12);
        body.Int(9);
        body.Short(0xAFF);
        body.Pos(3f, 4f);
        body.Float(0.75f);
        body.Float(0.1f);
        body.Byte(2);
        body.Short(55);
        body.Short(60);

        var sps = (ServerPlayerShoot)PacketUtils.CreateIncomingPacket(PacketId.ServerPlayerShoot);
        var reader = Reader(body);
        sps.Read(ref reader);
        Equal(-12, sps.BulletId);
        Equal(9, sps.OwnerId);
        Equal((short)0xAFF, sps.ContainerType);
        Equal(0.75f, sps.Angle);
        Equal(0.1f, sps.AngleInc);
        Equal(2, sps.DamageCount);
        Equal((short)55, sps.Damages[0]);
        Equal((short)60, sps.Damages[1]);
        Equal(0, reader.Remaining);
        sps.ReturnPacket();
    }

    private static void ShowEffectLayouts() {
        //Fixed layout (effect, target, color, pos1) with no pos2.
        var body = new Writer();
        body.Byte(2);
        body.Int(42);
        body.Int(-1);
        body.Pos(5f, 6f);

        var fx = (ShowEffect)PacketUtils.CreateIncomingPacket(PacketId.ShowEffect);
        var reader = Reader(body);
        fx.Read(ref reader);
        Equal(AlloyClient.Networking.Enums.EffectType.Teleport, fx.EffectType);
        Equal(42, fx.TargetObjectId);
        Equal(5f, fx.Pos1.X);
        Equal(false, fx.HasPos2);
        Equal(0, reader.Remaining);
        fx.ReturnPacket();

        //Same layout with the optional pos2 appended.
        var body2 = new Writer();
        body2.Byte(3);
        body2.Int(42);
        body2.Int(123);
        body2.Pos(1f, 2f);
        body2.Pos(3f, 4f);

        var fx2 = (ShowEffect)PacketUtils.CreateIncomingPacket(PacketId.ShowEffect);
        var reader2 = Reader(body2);
        fx2.Read(ref reader2);
        Equal(true, fx2.HasPos2);
        Equal(3f, fx2.Pos2.X);
        Equal(4f, fx2.Pos2.Y);
        Equal(0, reader2.Remaining);
        fx2.ReturnPacket();
    }

    private static void AllyShoot() {
        var body = new Writer();
        body.Int(11);
        body.Short(0xBEE);
        body.Float(2f);

        var ally = (AllyShoot)PacketUtils.CreateIncomingPacket(PacketId.AllyShoot);
        var reader = Reader(body);
        ally.Read(ref reader);
        Equal(11, ally.OwnerId);
        Equal((short)0xBEE, ally.ContainerType);
        Equal(2f, ally.Angle);
        Equal(0, reader.Remaining);
        ally.ReturnPacket();
    }

    private static void NewTickPlayerStats() {
        var body = new Writer();
        body.Short(1);
        body.Int(7);
        body.Pos(1.5f, 2.5f);
        body.Byte(1);
        body.Byte(1); // Hp
        body.Int(100);
        //Appended private stats block.
        body.Byte(2);
        body.Byte(33); // Credits
        body.Int(500);
        body.Byte(28); // Name (string stat)
        body.UTF("Test");

        var tick = (NewTick)PacketUtils.CreateIncomingPacket(PacketId.NewTick);
        var reader = Reader(body);
        tick.Read(ref reader);
        Equal(1, tick.ObjectStatsCount);
        Equal(7, tick.ObjectStats[0].Id);
        Equal(2, tick.PlayerStatCount);
        Equal(500, tick.PlayerStats[0].Value);
        Equal("Test", tick.PlayerStats[1].Text);
        Equal(0, reader.Remaining);
        tick.ReturnPacket();
    }

    private static void QueuedPacketsKeepOwnBuffers() {
        //The network thread reads ahead of the game thread's Handle, so a
        //later packet must not overwrite an earlier queued one.
        static byte[] TickBytes(int id, int hp) {
            var body = new Writer();
            body.Short(1);
            body.Int(id);
            body.Pos(0f, 0f);
            body.Byte(1);
            body.Byte(1);
            body.Int(hp);
            return body.ToArray();
        }

        var first = (NewTick)PacketUtils.CreateIncomingPacket(PacketId.NewTick);
        var r1 = Reader(TickBytes(1, 111));
        first.Read(ref r1);

        var second = (NewTick)PacketUtils.CreateIncomingPacket(PacketId.NewTick);
        var r2 = Reader(TickBytes(2, 222));
        second.Read(ref r2);

        Equal(1, first.ObjectStats[0].Id);
        Equal(2, second.ObjectStats[0].Id);

        static byte[] UpdateBytes(short tileX, ushort objType, int objId) {
            var body = new Writer();
            body.Short(1);
            body.Short(tileX);
            body.Short(0);
            body.UShort(7);
            body.Short(1);
            body.UShort(objType);
            body.Int(objId);
            body.Pos(0f, 0f);
            body.Byte(0);
            body.Short(0);
            return body.ToArray();
        }

        var u1 = (Update)PacketUtils.CreateIncomingPacket(PacketId.Update);
        var ru1 = Reader(UpdateBytes(3, 0x600, 50));
        u1.Read(ref ru1);

        var u2 = (Update)PacketUtils.CreateIncomingPacket(PacketId.Update);
        var ru2 = Reader(UpdateBytes(9, 0x601, 60));
        u2.Read(ref ru2);

        Equal((short)3, u1.Tiles[0].X);
        Equal((ushort)0x600, u1.NewObjs[0].ObjectType);
        Equal(50, u1.NewObjs[0].Id);
        Equal((short)9, u2.Tiles[0].X);

        first.ReturnPacket();
        second.ReturnPacket();
        u1.ReturnPacket();
        u2.ReturnPacket();
    }

    private static void OutgoingAcks() {
        //ShootAck: single int time.
        var ack = ShootAck.CreatePacket();
        ack.Time = 123456;
        Equal([123456], ReadInts(Write(ack, 4), 1));

        //SquareHit: time + bullet id.
        var sq = SquareHit.CreatePacket();
        sq.Time = 10;
        sq.BulletId = -7;
        Equal([10, -7], ReadInts(Write(sq, 8), 2));

        //AoeAck: time + position.
        var aoe = AoeAck.CreatePacket();
        aoe.Time = 99;
        aoe.Pos = new AlloyClient.Networking.Structs.DataObjects.Position { X = 1f, Y = 2f };
        var aoeBytes = Write(aoe, 12);
        Equal(99, BinaryPrimitives.ReadInt32BigEndian(aoeBytes.AsSpan(0, 4)));
        Equal(1f, BinaryPrimitives.ReadSingleBigEndian(aoeBytes.AsSpan(4, 4)));
        Equal(2f, BinaryPrimitives.ReadSingleBigEndian(aoeBytes.AsSpan(8, 4)));

        //GotoAck: single int time (never 0 once the clock is established).
        var go = GotoAck.CreatePacket();
        go.Time = 777;
        Equal([777], ReadInts(Write(go, 4), 1));
    }

    private static void PacketIds() {
        //Must match realm-server GameServer.PacketId and the Flash client.
        Equal((byte)41, (byte)PacketId.EnemyShoot);
        Equal((byte)27, (byte)PacketId.Aoe);
        Equal((byte)8, (byte)PacketId.Damage);
        Equal((byte)7, (byte)PacketId.ServerPlayerShoot);
        Equal((byte)40, (byte)PacketId.AllyShoot);
        Equal((byte)14, (byte)PacketId.ShowEffect);
        Equal((byte)31, (byte)PacketId.ShootAck);
        Equal((byte)30, (byte)PacketId.AoeAck);
        Equal((byte)32, (byte)PacketId.SquareHit);
        Equal((byte)48, (byte)PacketId.GotoAck);
        Equal((byte)49, (byte)PacketId.ChooseName);
        Equal((byte)33, (byte)PacketId.EditAccountList);
        Equal((byte)28, (byte)PacketId.PlayerHit);
        Equal((byte)29, (byte)PacketId.EnemyHit);
        Equal(PacketId.ChooseName, ChooseName.CreatePacket().PacketId);
        Equal(PacketId.EditAccountList, EditAccountList.CreatePacket().PacketId);
        ChooseName.CreatePacket().ReturnPacket();
        EditAccountList.CreatePacket().ReturnPacket();
    }

    private static byte[] Write(IOutgoingPacket pkt, int size) {
        var buf = new byte[size];
        var writer = new SpanWriter(buf.AsSpan(), false);
        pkt.Write(ref writer);
        var used = writer.Position;
        pkt.ReturnPacket();
        Equal(size, used);
        return buf;
    }

    private static int[] ReadInts(byte[] buf, int count) {
        var out_ = new int[count];
        for (var i = 0; i < count; i++)
            out_[i] = BinaryPrimitives.ReadInt32BigEndian(buf.AsSpan(i * 4, 4));
        return out_;
    }

    private static SpanReader Reader(Writer w) => Reader(w.ToArray());

    private static SpanReader Reader(byte[] bytes) {
        return new SpanReader(bytes, false);
    }

    private static void Equal<T>(T expected, T actual) {
        if (expected is Array expectedArray && actual is Array actualArray) {
            if (expectedArray.Length != actualArray.Length)
                throw new Exception($"expected len {expectedArray.Length}, got {actualArray.Length}");
            for (var i = 0; i < expectedArray.Length; i++)
                if (!Equals(expectedArray.GetValue(i), actualArray.GetValue(i)))
                    throw new Exception($"expected [{i}] {expectedArray.GetValue(i)}, got {actualArray.GetValue(i)}");
            return;
        }
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }

    //Big-endian scratch writer mirroring RotMG.Networking.PacketWriter.
    private sealed class Writer {
        private readonly List<byte> _bytes = new();

        public void Byte(byte v) => _bytes.Add(v);

        public void Short(short v) {
            _bytes.Add((byte)(v >> 8));
            _bytes.Add((byte)v);
        }

        public void UShort(ushort v) => Short(unchecked((short)v));

        public void Int(int v) {
            _bytes.Add((byte)(v >> 24));
            _bytes.Add((byte)(v >> 16));
            _bytes.Add((byte)(v >> 8));
            _bytes.Add((byte)v);
        }

        public void Float(float v) => Int(BitConverter.SingleToInt32Bits(v));

        public void Pos(float x, float y) {
            Float(x);
            Float(y);
        }

        public void UTF(string s) {
            var bytes = Encoding.UTF8.GetBytes(s);
            Short((short)bytes.Length);
            _bytes.AddRange(bytes);
        }

        public byte[] ToArray() => _bytes.ToArray();
    }
}
