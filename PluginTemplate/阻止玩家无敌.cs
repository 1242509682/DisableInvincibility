using System;
using System.IO;
using System.Timers;
using Terraria;
using TerrariaApi.Server;
using static 阻止玩家无敌;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

[ApiVersion(2, 1)]
public class 阻止玩家无敌 : TerrariaPlugin
{
    public class LPlayer
    {
        public int Index { get; set; }

        public int Tr { get; set; }

        public int Dm { get; set; }

        public int LHp { get; set; }

        public int LMaxHp { get; set; }

        public bool Heal { get; set; }

        public bool Skip { get; set; }

        public bool BAA { get; set; }

        public int Mis { get; set; }

        public DateTime LastTiem { get; set; }

        public DateTime LCheckTiem { get; set; }

        public int KickL { get; set; }

        public DateTime LastTiemKickL { get; set; }

        public LPlayer(int index)
        {
            Tr = 0;
            Dm = 0;
            Mis = 0;
            LHp = 0;
            LMaxHp = 0;
            Skip = true;
            Heal = false;
            BAA = false;
            Index = index;
            LastTiem = DateTime.UtcNow;
            KickL = 0;
            LastTiemKickL = DateTime.UtcNow;
            LCheckTiem = DateTime.UtcNow;
        }
    }

    private static readonly System.Timers.Timer Update = new System.Timers.Timer(3000.0);

    public static bool ULock = false;

    public override string Author => "GK 修改：羽学";

    public override string Description => "如果玩家无敌那么就断开它！";

    public override string Name => "阻止玩家无敌";

    public override Version Version => new Version(1, 0, 2, 1);

    private LPlayer[] LPlayers { get; set; }

    public 阻止玩家无敌(Main game)
        : base(game)
    {
        LPlayers = new LPlayer[256];
        base.Order = 1000;
    }

    public override void Initialize()
    {
        ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
        ServerApi.Hooks.NetGetData.Register(this, GetData);
        ServerApi.Hooks.NpcStrike.Register(this, NpcStrike);
        ServerApi.Hooks.NetSendData.Register(this, SendData);
        ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
        ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        ServerApi.Hooks.NpcSpawn.Register(this, OnSpawn);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
            ServerApi.Hooks.NetGetData.Deregister(this, GetData);
            ServerApi.Hooks.NetSendData.Deregister(this, SendData);
            ServerApi.Hooks.NpcStrike.Deregister(this, NpcStrike);
            ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
            ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            ServerApi.Hooks.NpcSpawn.Deregister(this, OnSpawn);
            Update.Elapsed -= OnUpdate;
            Update.Stop();
        }
        base.Dispose(disposing);
    }

    private void OnInitialize(EventArgs args)
    {
        Update.Elapsed += OnUpdate;
        Update.Start();
    }

    public void OnUpdate(object sender, ElapsedEventArgs e)
    {
    }

    public static bool Timeout(DateTime Start, bool warn = true, int ms = 500)
    {
        bool flag = (DateTime.Now - Start).TotalMilliseconds >= (double)ms;
        if (flag)
        {
            ULock = false;
        }
        if (warn && flag)
        {
            TShock.Log.Error("阻止无敌超时,已抛弃部分提示");
        }
        return flag;
    }

    private void OnGreetPlayer(GreetPlayerEventArgs e)
    {
        lock (LPlayers)
        {
            LPlayers[e.Who] = new LPlayer(e.Who);
        }
    }

    private void OnLeave(LeaveEventArgs e)
    {
        lock (LPlayers)
        {
            if (LPlayers[e.Who] != null)
            {
                LPlayers[e.Who] = null;
            }
        }
    }

    private void OnSpawn(NpcSpawnEventArgs args)
    {
        if (args.Handled || !Main.npc[args.NpcId].boss || !((Entity)Main.npc[args.NpcId]).active)
        {
            return;
        }
        lock (LPlayers)
        {
            for (int i = 0; i < LPlayers.Length; i++)
            {
                if (LPlayers[i] != null)
                {
                    LPlayers[i].LCheckTiem = DateTime.UtcNow;
                    LPlayers[i].Tr = 0;
                    LPlayers[i].Mis = 0;
                }
            }
        }
    }

    private void SendData(SendDataEventArgs args)
    {
        if (args.Handled || args.MsgId != PacketTypes.PlayerHealOther)
        {
            return;
        }
        int number = args.number;
        if (number < 0)
        {
            return;
        }
        lock (LPlayers)
        {
            if (LPlayers[number] != null && LPlayers[number].Tr == 3)
            {
                LPlayers[number].Heal = true;
            }
        }
    }

    private void NpcStrike(NpcStrikeEventArgs args)
    {
        if (args.Handled)
        {
            return;
        }
        lock (LPlayers)
        {
            if (LPlayers[((Entity)args.Player).whoAmI] != null && (DateTime.UtcNow - LPlayers[((Entity)args.Player).whoAmI].LCheckTiem).TotalMilliseconds > 600000.0)
            {
                LPlayers[((Entity)args.Player).whoAmI].LCheckTiem = DateTime.UtcNow;
                LPlayers[((Entity)args.Player).whoAmI].Tr = 0;
                LPlayers[((Entity)args.Player).whoAmI].Mis = 0;
            }
        }
    }

    private void GetData(GetDataEventArgs args)
    {
        TSPlayer tSPlayer = TShock.Players[args.Msg.whoAmI];
        if (tSPlayer == null || !tSPlayer.ConnectionAlive || args.Handled || LPlayers[args.Msg.whoAmI] == null || tSPlayer.Group.HasPermission("免检无敌") || tSPlayer.Group.Name == "owner")
        {
            return;
        }
        lock (LPlayers)
        {
            if (args.MsgID == PacketTypes.PlayerHp)
            {
                if (tSPlayer.IsBeingDisabled())
                {
                    return;
                }
                using BinaryReader binaryReader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length));
                byte b = binaryReader.ReadByte();
                short num = binaryReader.ReadInt16();
                short num2 = binaryReader.ReadInt16();
                if (LPlayers[args.Msg.whoAmI].Tr == 0)
                {
                    if (!LPlayers[args.Msg.whoAmI].BAA)
                    {
                        for (int i = 0; i < LPlayers.Length; i++)
                        {
                            if (LPlayers[i] != null && LPlayers[i].BAA)
                            {
                                return;
                            }
                        }
                        LPlayers[args.Msg.whoAmI].BAA = true;
                    }
                    if (LPlayers[args.Msg.whoAmI].Skip && (DateTime.UtcNow - LPlayers[args.Msg.whoAmI].LastTiem).TotalMilliseconds > 1500.0)
                    {
                        LPlayers[args.Msg.whoAmI].Skip = false;
                    }
                    if (num > 1 && !LPlayers[args.Msg.whoAmI].Skip)
                    {
                        LPlayers[args.Msg.whoAmI].Tr = 1;
                        LPlayers[args.Msg.whoAmI].LHp = num;
                        tSPlayer.DamagePlayer(1);
                    }
                }
                else if (LPlayers[args.Msg.whoAmI].Tr == 1)
                {
                    if (LPlayers[args.Msg.whoAmI].LHp != num)
                    {
                        LPlayers[args.Msg.whoAmI].Tr = 2;
                        LPlayers[args.Msg.whoAmI].BAA = false;
                    }
                    else
                    {
                        if (LPlayers[args.Msg.whoAmI].Mis >= 1)
                        {
                            tSPlayer.Kick($"玩家 {tSPlayer.Name} 因无敌被踢出.", force: true, silent: false, "Server");
                            return;
                        }
                        LPlayers[args.Msg.whoAmI].Mis++;
                        LPlayers[args.Msg.whoAmI].Tr = 0;
                        if (LPlayers[args.Msg.whoAmI].Skip && (DateTime.UtcNow - LPlayers[args.Msg.whoAmI].LastTiem).TotalMilliseconds > 1500.0)
                        {
                            LPlayers[args.Msg.whoAmI].Skip = false;
                        }
                        if (num > 1 && !LPlayers[args.Msg.whoAmI].Skip)
                        {
                            LPlayers[args.Msg.whoAmI].Tr = 1;
                            LPlayers[args.Msg.whoAmI].LHp = num;
                            tSPlayer.DamagePlayer(1);
                        }
                    }
                }
                //else if (LPlayers[args.Msg.whoAmI].Tr == 2 && num > tSPlayer.TPlayer.statLifeMax2 && num > tSPlayer.TPlayer.statLifeMax2 + (num2 - tSPlayer.TPlayer.statLifeMax))
                //{
                //    if ((DateTime.UtcNow - LPlayers[args.Msg.whoAmI].LastTiemKickL).TotalMilliseconds > 30000.0)
                //    {
                //        LPlayers[args.Msg.whoAmI].KickL = 0;
                //        LPlayers[args.Msg.whoAmI].LastTiemKickL = DateTime.UtcNow;
                //    }
                //    LPlayers[args.Msg.whoAmI].KickL++;
                //    if (LPlayers[args.Msg.whoAmI].KickL > 3)
                //    {
                //        tSPlayer.Kick($"玩家 {tSPlayer.Name} 血量溢出被踢出.", force: true, silent: false, "Server");
                //    }
                //    return;
                //}
                if (LPlayers[args.Msg.whoAmI].LMaxHp != 0 && num2 != LPlayers[args.Msg.whoAmI].LMaxHp)
                {
                    if (LPlayers[args.Msg.whoAmI].LMaxHp >= 400 && LPlayers[args.Msg.whoAmI].LMaxHp < 500)
                    {
                        if (num2 != LPlayers[args.Msg.whoAmI].LMaxHp + 5 && num2 != LPlayers[args.Msg.whoAmI].LMaxHp + 10)
                        {
                            string text = $"玩家 {tSPlayer.Name} 修改血量上限({LPlayers[args.Msg.whoAmI].LMaxHp}>{num2 - LPlayers[args.Msg.whoAmI].LMaxHp}>{num2})";
                            //TShock.Bans.InsertBan($"{Identifier.Account}{tSPlayer.Account.Name}", text + "被封号.", "阻止玩家无敌", DateTime.UtcNow, DateTime.MaxValue);
                            tSPlayer.Kick(text + "被踢出.", force: true, silent: false, "Server");
                            return;
                        }
                    }
                    else if (LPlayers[args.Msg.whoAmI].LMaxHp < 400)
                    {
                        if (num2 != LPlayers[args.Msg.whoAmI].LMaxHp + 20 && num2 != LPlayers[args.Msg.whoAmI].LMaxHp + 40)
                        {
                            string text2 = $"玩家 {tSPlayer.Name} 修改血量上限({LPlayers[args.Msg.whoAmI].LMaxHp}>{num2 - LPlayers[args.Msg.whoAmI].LMaxHp}>{num2})";
                            //TShock.Bans.InsertBan($"{Identifier.Account}{tSPlayer.Account.Name}", text2 + "被封号.", "阻止玩家无敌", DateTime.UtcNow, DateTime.MaxValue);
                            tSPlayer.Kick(text2 + "被踢出.", force: true, silent: false, "Server");
                            return;
                        }
                    }
                    else if (num2 > LPlayers[args.Msg.whoAmI].LMaxHp)
                    {
                        string text3 = $"玩家 {tSPlayer.Name} 修改血量上限({LPlayers[args.Msg.whoAmI].LMaxHp}>{num2 - LPlayers[args.Msg.whoAmI].LMaxHp}>{num2})";
                        //TShock.Bans.InsertBan($"{Identifier.Account}{tSPlayer.Account.Name}", text3 + "被封号.", "阻止玩家无敌", DateTime.UtcNow, DateTime.MaxValue);
                        tSPlayer.Kick(text3 + "被踢出.", force: true, silent: false, "Server");
                        return;
                    }
                }
                LPlayers[args.Msg.whoAmI].LHp = num;
                LPlayers[args.Msg.whoAmI].LMaxHp = num2;
                return;
            }
            if (args.MsgID == PacketTypes.PlayerHurtV2)
            {
                if (LPlayers[args.Msg.whoAmI].Tr == 2 && tSPlayer.TPlayer.statDefense <= 199)
                {
                }
            }
            else if (args.MsgID == PacketTypes.PlayerStealth)
            {
                if (LPlayers[args.Msg.whoAmI].Tr == 0)
                {
                    LPlayers[args.Msg.whoAmI].Skip = true;
                    LPlayers[args.Msg.whoAmI].LastTiem = DateTime.UtcNow;
                }
                else if (LPlayers[args.Msg.whoAmI].Tr == 1)
                {
                    LPlayers[args.Msg.whoAmI].Tr = 2;
                    LPlayers[args.Msg.whoAmI].BAA = false;
                }
            }
            else if (args.MsgID == PacketTypes.PlayerDodge)
            {
                if (LPlayers[args.Msg.whoAmI].Tr == 0)
                {
                    LPlayers[args.Msg.whoAmI].Skip = true;
                    LPlayers[args.Msg.whoAmI].LastTiem = DateTime.UtcNow;
                }
                else if (LPlayers[args.Msg.whoAmI].Tr == 1)
                {
                    LPlayers[args.Msg.whoAmI].Tr = 2;
                    LPlayers[args.Msg.whoAmI].BAA = false;
                }
            }
            else if (args.MsgID == PacketTypes.EffectHeal)
            {
                if (LPlayers[args.Msg.whoAmI].Tr == 3)
                {
                    LPlayers[args.Msg.whoAmI].Heal = true;
                }
            }
            else if (args.MsgID == PacketTypes.PlayerHealOther)
            {
                if (LPlayers[args.Msg.whoAmI].Tr == 3)
                {
                    LPlayers[args.Msg.whoAmI].Heal = true;
                }
            }
            else if (args.MsgID == PacketTypes.PlayerSpawn)
            {
                if (LPlayers[args.Msg.whoAmI].Tr == 0)
                {
                    LPlayers[args.Msg.whoAmI].Skip = true;
                    LPlayers[args.Msg.whoAmI].LastTiem = DateTime.UtcNow;
                }
                else if (LPlayers[args.Msg.whoAmI].Tr == 3)
                {
                    LPlayers[args.Msg.whoAmI].Heal = true;
                }
            }
        }
    }
}
