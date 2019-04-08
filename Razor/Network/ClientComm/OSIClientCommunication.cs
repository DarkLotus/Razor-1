using Assistant.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Assistant
{
    public unsafe class OSIClientCommunication : ClientCommunication
    {
        public static OSIClientCommunication OSIInstance => Instance as OSIClientCommunication;

        private const int WM_USER = 0x400;
        public const int WM_COPYDATA = 0x4A;
        public const int WM_UONETEVENT = WM_USER + 1;
        private const int WM_CUSTOMTITLE = WM_USER + 2;

        private static bool m_Ready = false;
        private static Mutex CommMutex;
        private static byte* m_TitleStr;
        private static string m_LastStr = "";
        private static StringBuilder m_TBBuilder = new StringBuilder();
        private static string m_LastPlayerName = "";

        private static bool m_QueueRecv;
        private static bool m_QueueSend;

        private static Buffer* m_InRecv;
        private static Buffer* m_OutRecv;
        private static Buffer* m_InSend;
        private static Buffer* m_OutSend;
        private static Queue<Packet> m_SendQueue = new Queue<Packet>();
        private static Queue<Packet> m_RecvQueue = new Queue<Packet>();

        private static uint m_ServerIP;
        private static ushort m_ServerPort;

        private static IPAddress m_LastConnection;
        public override IPAddress LastConnection { get { return m_LastConnection; } }
        private static DateTime m_ConnStart;

        private static Process m_ClientProcess;
        public override Process ClientProcess { get { return m_ClientProcess; } }
        public override DateTime ConnectionStart { get { return m_ConnStart; } }


        public override bool ClientRunning
        {
            get
            {
                try
                {
                    return m_ClientProcess != null && !m_ClientProcess.HasExited;
                }
                catch
                {
                    return m_ClientProcess != null && ClientWindow != IntPtr.Zero;
                }
            }
        }

        public override IntPtr ClientWindow => NativeMethods.FindUOWindow();

        internal override Version GetUOVersion()
        {
            Version result;
            //TODO WHY DOES THIS CRASH NOW??
            var ver = NativeMethods.GetUOVersion();
            string[] split = ver.Split( '.' );

            if ( split.Length < 3 )
                result = new Version( 4, 0, 0, 0 );

            int rev = 0;

            if ( split.Length > 3 )
                rev = Utility.ToInt32( split[3], 0 );

            result = new Version(
                Utility.ToInt32( split[0], 0 ),
                Utility.ToInt32( split[1], 0 ),
                Utility.ToInt32( split[2], 0 ),
                rev );

            if ( result == null || result.Major == 0 ) // sanity check if the client returns 0.0.0.0
                result = new Version( 4, 0, 0, 0 );
            return result;
        }

        public override void SetNegotiate( bool negotiate )
        {
            NativeMethods.PostMessage( ClientWindow, WM_UONETEVENT, (IntPtr)UONetMessage.Negotiate, (IntPtr)( negotiate ? 1 : 0 ) );
        }
        public static bool Attach( int pid )
        {
            m_ClientProcess = null;
            m_ClientProcess = Process.GetProcessById( pid );
            return m_ClientProcess != null;
        }

        internal override void Close()
        {
            NativeMethods.Shutdown( true );
            if ( m_ClientProcess != null && !m_ClientProcess.HasExited )
                m_ClientProcess.CloseMainWindow();
            m_ClientProcess = null;
        }

        internal static bool InstallHooks( IntPtr mainWindow )
        {
            InitError error;
            int flags = 0;

            if ( m_Ready )
                return false; // double init

            if ( Config.GetBool( "Negotiate" ) )
                flags |= 0x04;

            if ( ClientEncrypted )
                flags |= 0x08;

            if ( ServerEncrypted )
                flags |= 0x10;

            NativeMethods.WaitForWindow( m_ClientProcess.Id );

            error = (InitError)NativeMethods.InstallLibrary( mainWindow, m_ClientProcess.Id, flags );
            if ( error != InitError.SUCCESS )
            {
                FatalInit( error );
                return false;
            }

            // When InstallLibrary finishes, we get a UONETEVENT saying it's ready.
            return true;
        }
        private static void FatalInit( InitError error )
        {
            StringBuilder sb = new StringBuilder( Language.GetString( LocString.InitError ) );
            sb.AppendFormat( "{0}\n", error );
            sb.Append( Language.GetString( (int)( LocString.InitError + (int)error ) ) );

            MessageBox.Show( Engine.ActiveWindow, sb.ToString(), "Init Error", MessageBoxButtons.OK, MessageBoxIcon.Stop );
        }

        public static Loader_Error LaunchClient( string client )
        {
            /*string dir = Directory.GetCurrentDirectory();
			Directory.SetCurrentDirectory( Path.GetDirectoryName( client ) );
			Directory.SetCurrentDirectory( dir );

			try
			{
				ProcessStartInfo psi = new ProcessStartInfo( client );
				psi.WorkingDirectory = Path.GetDirectoryName( client );

				ClientProc = Process.Start( psi );

				if ( ClientProc != null && !Config.GetBool( "SmartCPU" ) )
					ClientProc.PriorityClass = (ProcessPriorityClass)Enum.Parse( typeof(ProcessPriorityClass), Config.GetString( "ClientPrio" ), true );
			}
			catch
			{
			}*/

            string dll = Path.Combine( Config.GetInstallDirectory(), "Crypt.dll" );
            uint pid = 0;
            Loader_Error err = (Loader_Error)NativeMethods.Load( client, dll, "OnAttach", null, 0, out pid );

            if ( err == Loader_Error.SUCCESS )
            {
                try
                {
                    m_ClientProcess = Process.GetProcessById( (int)pid );

                    /*if ( ClientProc != null && !Config.GetBool( "SmartCPU" ) )
						ClientProc.PriorityClass = (ProcessPriorityClass)Enum.Parse( typeof(ProcessPriorityClass), Config.GetString( "ClientPrio" ), true );*/
                }
                catch
                {
                }
            }

            if ( m_ClientProcess == null )
                return Loader_Error.UNKNOWN_ERROR;
            else
                return err;
        }

        public static void UpdateTitleBar()
        {
            if ( !m_Ready )
                return;

            if ( World.Player != null && Config.GetBool( "TitleBarDisplay" ) )
            {
                // reuse the same sb each time for less damn allocations
                m_TBBuilder.Remove( 0, m_TBBuilder.Length );
                m_TBBuilder.Insert( 0, Config.GetString( "TitleBarText" ) );
                StringBuilder sb = m_TBBuilder;
                //StringBuilder sb = new StringBuilder( Config.GetString( "TitleBarText" ) ); // m_TitleCapacity

                PlayerData p = World.Player;

                if ( p.Name != m_LastPlayerName )
                {
                    m_LastPlayerName = p.Name;

                    Engine.MainWindow.UpdateTitle();
                }

                sb.Replace( @"{char}",
                    Config.GetBool( "ShowNotoHue" ) ? $"~#{p.GetNotorietyColor() & 0x00FFFFFF:X6}{p.Name}~#~" : p.Name );

                sb.Replace( @"{shard}", World.ShardName );

                sb.Replace( @"{crimtime}", p.CriminalTime != 0 ? $"~^C0C0C0{p.CriminalTime}~#~" : "-" );

                sb.Replace( @"{str}", p.Str.ToString() );
                sb.Replace( @"{hpmax}", p.HitsMax.ToString() );

                sb.Replace( @"{hp}", p.Poisoned ? $"~#FF8000{p.Hits}~#~" : EncodeColorStat( p.Hits, p.HitsMax ) );

                sb.Replace( @"{dex}", World.Player.Dex.ToString() );
                sb.Replace( @"{stammax}", World.Player.StamMax.ToString() );
                sb.Replace( @"{stam}", EncodeColorStat( p.Stam, p.StamMax ) );
                sb.Replace( @"{int}", World.Player.Int.ToString() );
                sb.Replace( @"{manamax}", World.Player.ManaMax.ToString() );
                sb.Replace( @"{mana}", EncodeColorStat( p.Mana, p.ManaMax ) );

                sb.Replace( @"{ar}", p.AR.ToString() );
                sb.Replace( @"{tithe}", p.Tithe.ToString() );

                sb.Replace( @"{physresist}", p.AR.ToString() );
                sb.Replace( @"{fireresist}", p.FireResistance.ToString() );
                sb.Replace( @"{coldresist}", p.ColdResistance.ToString() );
                sb.Replace( @"{poisonresist}", p.PoisonResistance.ToString() );
                sb.Replace( @"{energyresist}", p.EnergyResistance.ToString() );

                sb.Replace( @"{luck}", p.Luck.ToString() );

                sb.Replace( @"{damage}", String.Format( "{0}-{1}", p.DamageMin, p.DamageMax ) );

                sb.Replace( @"{weight}",
                    World.Player.Weight >= World.Player.MaxWeight
                        ? $"~#FF0000{World.Player.Weight}~#~"
                        : World.Player.Weight.ToString() );

                sb.Replace( @"{maxweight}", World.Player.MaxWeight.ToString() );

                sb.Replace( @"{followers}", World.Player.Followers.ToString() );
                sb.Replace( @"{followersmax}", World.Player.FollowersMax.ToString() );

                sb.Replace( @"{gold}", World.Player.Gold.ToString() );

                sb.Replace( @"{gps}", GoldPerHourTimer.Running ? $"{GoldPerHourTimer.GoldPerSecond:N2}" : "-" );
                sb.Replace( @"{gpm}", GoldPerHourTimer.Running ? $"{GoldPerHourTimer.GoldPerMinute:N2}" : "-" );
                sb.Replace( @"{gph}", GoldPerHourTimer.Running ? $"{GoldPerHourTimer.GoldPerHour:N2}" : "-" );
                sb.Replace( @"{goldtotal}", GoldPerHourTimer.Running ? $"{GoldPerHourTimer.GoldSinceStart}" : "-" );
                sb.Replace( @"{goldtotalmin}", GoldPerHourTimer.Running ? $"{GoldPerHourTimer.TotalMinutes:N2} min" : "-" );

                sb.Replace( @"{bandage}", BandageTimer.Running ? $"~#FF8000{BandageTimer.Count}~#~" : "-" );

                sb.Replace( @"{skill}", SkillTimer.Running ? $"{SkillTimer.Count}" : "-" );
                sb.Replace( @"{gate}", GateTimer.Running ? $"{GateTimer.Count}" : "-" );

                sb.Replace( @"{stealthsteps}", StealthSteps.Counting ? StealthSteps.Count.ToString() : "-" );
                //ClientCommunication.ConnectionStart != DateTime.MinValue )
                //time = (int)((DateTime.UtcNow - ClientCommunication.ConnectionStart).TotalSeconds);
                sb.Replace( @"{uptime}", m_ConnStart != DateTime.MinValue ? Utility.FormatTime( (int)( ( DateTime.UtcNow - m_ConnStart ).TotalSeconds ) ) : "-" );

                sb.Replace( @"{dps}", DamageTracker.Running ? $"{DamageTracker.DamagePerSecond:N2}" : "-" );
                sb.Replace( @"{maxdps}", DamageTracker.Running ? $"{DamageTracker.MaxDamagePerSecond:N2}" : "-" );
                sb.Replace( @"{maxdamagedealt}", DamageTracker.Running ? $"{DamageTracker.MaxSingleDamageDealt}" : "-" );
                sb.Replace( @"{maxdamagetaken}", DamageTracker.Running ? $"{DamageTracker.MaxSingleDamageTaken}" : "-" );
                sb.Replace( @"{totaldamagedealt}", DamageTracker.Running ? $"{DamageTracker.TotalDamageDealt}" : "-" );
                sb.Replace( @"{totaldamagetaken}", DamageTracker.Running ? $"{DamageTracker.TotalDamageTaken}" : "-" );


                string buffList = string.Empty;

                if ( BuffsTimer.Running )
                {
                    StringBuilder buffs = new StringBuilder();
                    foreach ( BuffsDebuffs buff in World.Player.BuffsDebuffs )
                    {
                        int timeLeft = 0;

                        if ( buff.Duration > 0 )
                        {
                            TimeSpan diff = DateTime.UtcNow - buff.Timestamp;
                            timeLeft = buff.Duration - (int)diff.TotalSeconds;
                        }

                        buffs.Append( timeLeft <= 0
                            ? $"{buff.ClilocMessage1}, "
                            : $"{buff.ClilocMessage1} ({timeLeft}), " );
                    }

                    buffs.Length = buffs.Length - 2;
                    buffList = buffs.ToString();
                    sb.Replace( @"{buffsdebuffs}", buffList );

                }
                else
                {
                    sb.Replace( @"{buffsdebuffs}", "-" );
                    buffList = string.Empty;
                }

                string statStr = String.Format( "{0}{1:X2}{2:X2}{3:X2}",
                   (int)( p.GetStatusCode() ),
                   (int)( World.Player.HitsMax == 0 ? 0 : (double)World.Player.Hits / World.Player.HitsMax * 99 ),
                   (int)( World.Player.ManaMax == 0 ? 0 : (double)World.Player.Mana / World.Player.ManaMax * 99 ),
                   (int)( World.Player.StamMax == 0 ? 0 : (double)World.Player.Stam / World.Player.StamMax * 99 ) );

                sb.Replace( @"{statbar}", $"~SR{statStr}" );
                sb.Replace( @"{mediumstatbar}", $"~SL{statStr}" );
                sb.Replace( @"{largestatbar}", $"~SX{statStr}" );

                bool dispImg = Config.GetBool( "TitlebarImages" );
                for ( int i = 0; i < Counter.List.Count; i++ )
                {
                    Counter c = Counter.List[i];
                    if ( c.Enabled )
                        sb.Replace( $"{{{c.Format}}}", c.GetTitlebarString( dispImg && c.DisplayImage ) );
                }

                SetTitleStr( sb.ToString() );
            }
            else
            {
                SetTitleStr( "" );
            }
        }
        public static void SetSmartCPU( bool enabled )
        {
            if ( enabled )
                try { ClientCommunication.Instance.ClientProcess.PriorityClass = System.Diagnostics.ProcessPriorityClass.Normal; } catch { }

            NativeMethods.PostMessage( ClientCommunication.Instance.ClientWindow, WM_UONETEVENT, (IntPtr)UONetMessage.SmartCPU, (IntPtr)( enabled ? 1 : 0 ) );
        }

        public static void SetGameSize( int x, int y )
        {
            NativeMethods.PostMessage( ClientCommunication.Instance.ClientWindow, WM_UONETEVENT, (IntPtr)UONetMessage.SetGameSize, (IntPtr)( ( x & 0xFFFF ) | ( ( y & 0xFFFF ) << 16 ) ) );
        }
        
        public static void SetMapWndHandle( Form mapWnd )
        {
            NativeMethods.PostMessage( ClientCommunication.Instance.ClientWindow, WM_UONETEVENT, (IntPtr)UONetMessage.SetMapHWnd, mapWnd.Handle );
        }

        public static void RequestStatbarPatch( bool preAOS )
        {
            NativeMethods.PostMessage( ClientCommunication.Instance.ClientWindow, WM_UONETEVENT, (IntPtr)UONetMessage.StatBar, preAOS ? (IntPtr)1 : IntPtr.Zero );
        }

        internal override void SetCustomNotoHue( int hue )
        {
            NativeMethods.PostMessage( ClientWindow, WM_UONETEVENT, (IntPtr)UONetMessage.NotoHue, (IntPtr)hue );
        }

        internal override uint TotalOut()
        {
            return NativeMethods.TotalOut();
        }
        internal override uint TotalIn()
        {
            return NativeMethods.TotalIn();
        }
        public static void OnLogout()
        {
            OnLogout( true );
        }
        internal static bool OnMessage( MainForm razor, uint wParam, int lParam )
        {
            bool retVal = true;

            switch ( (UONetMessage)( wParam & 0xFFFF ) )
            {
                case UONetMessage.Ready: //Patch status
                    if ( lParam == (int)InitError.NO_MEMCOPY )
                    {
                        if ( MessageBox.Show( Engine.ActiveWindow, Language.GetString( LocString.NoMemCpy ), "No Client MemCopy", MessageBoxButtons.YesNo, MessageBoxIcon.Warning ) == DialogResult.No )
                        {
                            m_Ready = false;
                            m_ClientProcess = null;
                            Engine.MainWindow.CanClose = true;
                            Engine.MainWindow.Close();
                            break;
                        }
                    }

                    byte* baseAddr = (byte*)NativeMethods.GetSharedAddress().ToPointer();
                    m_InRecv = (Buffer*)baseAddr;
                    m_OutRecv = (Buffer*)( baseAddr + sizeof( Buffer ) );
                    m_InSend = (Buffer*)( baseAddr + sizeof( Buffer ) * 2 );
                    m_OutSend = (Buffer*)( baseAddr + sizeof( Buffer ) * 3 );
                    m_TitleStr = (byte*)( baseAddr + sizeof( Buffer ) * 4 );

                    NativeMethods.SetServer( m_ServerIP, m_ServerPort );

                    CommMutex = new Mutex();
#pragma warning disable 618
                    CommMutex.Handle = NativeMethods.GetCommMutex();
#pragma warning restore 618

                    try
                    {
                        string path = Ultima.Files.GetFilePath( "art.mul" );
                        if ( path != null && path != string.Empty )
                            NativeMethods.SetDataPath( Path.GetDirectoryName( path ) );
                        else
                            NativeMethods.SetDataPath( Path.GetDirectoryName( Ultima.Files.Directory ) );
                    }
                    catch
                    {
                        NativeMethods.SetDataPath( "" );
                    }

                    if ( Config.GetBool( "OldStatBar" ) )
                        RequestStatbarPatch( true );

                    m_Ready = true;
                    Engine.MainWindow.MainForm_EndLoad();
                    break;

                case UONetMessage.NotReady:
                    m_Ready = false;
                    FatalInit( (InitError)lParam );
                    m_ClientProcess = null;
                    Engine.MainWindow.CanClose = true;
                    Engine.MainWindow.Close();
                    break;

                // Network events
                case UONetMessage.Recv:
                    OnRecv();
                    break;
                case UONetMessage.Send:
                    OnSend();
                    break;
                case UONetMessage.Connect:
                    m_ConnStart = DateTime.UtcNow;
                    try
                    {
                        m_LastConnection = new IPAddress( (uint)lParam );
                    }
                    catch
                    {
                    }
                    break;
                case UONetMessage.Disconnect:
                    OnLogout( false );
                    break;
                case UONetMessage.Close:
                    OnLogout();
                    m_ClientProcess = null;
                    Engine.MainWindow.CanClose = true;
                    Engine.MainWindow.Close();
                    break;

                // Hot Keys
                case UONetMessage.Mouse:
                    HotKey.OnMouse( (ushort)( lParam & 0xFFFF ), (short)( lParam >> 16 ) );
                    break;
                case UONetMessage.KeyDown:
                    retVal = HotKey.OnKeyDown( lParam );
                    break;

                // Activation Tracking
                case UONetMessage.Activate:
                    /*if ( Config.GetBool( "AlwaysOnTop" ) )
					{
						if ( (lParam&0x0000FFFF) == 0 && (lParam&0xFFFF0000) != 0 && razor.WindowState != FormWindowState.Minimized && razor.Visible )
						{// if uo is deactivating and minimized and we are not minimized
							if ( !razor.ShowInTaskbar && razor.Visible )
								razor.Hide();
							razor.WindowState = FormWindowState.Minimized;
							m_LastActivate = DateTime.UtcNow;
						}
						else if ( (lParam&0x0000FFFF) != 0 && (lParam&0xFFFF0000) != 0 && razor.WindowState != FormWindowState.Normal )
						{ // is UO is activating and minimized and we are minimized
							if ( m_LastActivate+TimeSpan.FromSeconds( 0.2 ) < DateTime.UtcNow )
							{
								if ( !razor.ShowInTaskbar && !razor.Visible )
									razor.Show();
								razor.WindowState = FormWindowState.Normal;
								//SetForegroundWindow( FindUOWindow() );
							}
							m_LastActivate = DateTime.UtcNow;
						}
					}*/
                    break;

                case UONetMessage.Focus:
                    if ( Config.GetBool( "AlwaysOnTop" ) )
                    {
                        if ( lParam != 0 && !razor.TopMost )
                        {
                            razor.TopMost = true;
                            NativeMethods.SetForegroundWindow( ClientCommunication.Instance.ClientWindow );
                        }
                        else if ( lParam == 0 && razor.TopMost )
                        {
                            razor.TopMost = false;
                            razor.SendToBack();
                        }
                    }

                    // always use smartness for the map window
                    if ( razor.MapWindow != null && razor.MapWindow.Visible )
                    {
                        if ( lParam != 0 && !razor.MapWindow.TopMost )
                        {
                            razor.MapWindow.TopMost = true;
                            NativeMethods.SetForegroundWindow( ClientCommunication.Instance.ClientWindow );
                        }
                        else if ( lParam == 0 && razor.MapWindow.TopMost )
                        {
                            razor.MapWindow.TopMost = false;
                            razor.MapWindow.SendToBack();
                        }
                    }

                    break;

                case UONetMessage.DLL_Error:
                    {
                        string error = "Unknown";
                        switch ( (UONetMessage)lParam )
                        {
                            case UONetMessage.StatBar:
                                error = "Unable to patch status bar.";
                                break;
                        }

                        MessageBox.Show( Engine.ActiveWindow, "An Error has occured : \n" + error, "Error Reported", MessageBoxButtons.OK, MessageBoxIcon.Warning );
                        break;
                    }

                case UONetMessage.FindData:
                    FindData.Message( ( wParam & 0xFFFF0000 ) >> 16, lParam );
                    break;

                // Unknown
                default:
                    MessageBox.Show( Engine.ActiveWindow, "Unknown message from uo client\n" + ( (int)wParam ).ToString(), "Error?" );
                    break;
            }

            return retVal;
        }
        private static void CopyToBuffer( Buffer* buffer, byte* data, int len )
        {
            //if ( buffer->Length + buffer->Start + len >= SHARED_BUFF_SIZE )
            //	throw new NullReferenceException( String.Format( "Buffer OVERFLOW in CopyToBuffer [{0} + {1}] <- {2}", buffer->Start, buffer->Length, len ) );

            NativeMethods.memcpy( ( &buffer->Buff0 ) + buffer->Start + buffer->Length, data, len );
            buffer->Length += len;
        }

        private static void HandleComm( Buffer* inBuff, Buffer* outBuff, Queue<Packet> queue, PacketPath path )
        {
            CommMutex.WaitOne();
            while ( inBuff->Length > 0 )
            {
                byte* buff = ( &inBuff->Buff0 ) + inBuff->Start;

                short len = PacketsTable.GetPacketLength( buff, inBuff->Length );
                if ( len > inBuff->Length || len <= 0 )
                    break;

                inBuff->Start += len;
                inBuff->Length -= len;

                bool viewer = false;
                bool filter = false;

                switch ( path )
                {
                    case PacketPath.ClientToServer:
                        viewer = PacketHandler.HasClientViewer( buff[0] );
                        filter = PacketHandler.HasClientFilter( buff[0] );
                        break;
                    case PacketPath.ServerToClient:
                        viewer = PacketHandler.HasServerViewer( buff[0] );
                        filter = PacketHandler.HasServerFilter( buff[0] );
                        break;
                }

                Packet p = null;
                PacketReader pr = null;
                if ( viewer )
                {
                    pr = new PacketReader( buff, len, PacketsTable.IsDynLength( buff[0] ) );
                    if ( filter )
                        p = MakePacketFrom( pr );
                }
                else if ( filter )
                {
                    byte[] temp = new byte[len];
                    fixed ( byte* ptr = temp )
                        NativeMethods.memcpy( ptr, buff, len );
                    p = new Packet( temp, len, PacketsTable.IsDynLength( buff[0] ) );
                }

                bool blocked = false;
                switch ( path )
                {
                    // yes it should be this way
                    case PacketPath.ClientToServer:
                        {
                            blocked = PacketHandler.OnClientPacket( buff[0], pr, p );
                            break;
                        }
                    case PacketPath.ServerToClient:
                        {
                            blocked = PacketHandler.OnServerPacket( buff[0], pr, p );
                            break;
                        }
                }

                if ( filter )
                {
                    byte[] data = p.Compile();
                    fixed ( byte* ptr = data )
                    {
                        Packet.Log( path, ptr, data.Length, blocked );
                        if ( !blocked )
                            CopyToBuffer( outBuff, ptr, data.Length );
                    }
                }
                else
                {
                    Packet.Log( path, buff, len, blocked );
                    if ( !blocked )
                        CopyToBuffer( outBuff, buff, len );
                }

                while ( queue.Count > 0 )
                {
                    p = (Packet)queue.Dequeue();
                    byte[] data = p.Compile();
                    fixed ( byte* ptr = data )
                    {
                        CopyToBuffer( outBuff, ptr, data.Length );
                        Packet.Log( (PacketPath)( ( (int)path ) + 1 ), ptr, data.Length );
                    }
                }
            }
            CommMutex.ReleaseMutex();
        }

        internal static Packet MakePacketFrom( PacketReader pr )
        {
            byte[] data = pr.CopyBytes( 0, pr.Length );
            return new Packet( data, pr.Length, pr.DynamicLength );
        }

        private static void OnRecv()
        {
            m_QueueRecv = true;
            HandleComm( m_InRecv, m_OutRecv, m_RecvQueue, PacketPath.ServerToClient );
            m_QueueRecv = false;
        }

        private static void OnSend()
        {
            m_QueueSend = true;
            HandleComm( m_InSend, m_OutSend, m_SendQueue, PacketPath.ClientToServer );
            m_QueueSend = false;
        }
        internal static void SetConnectionInfo( IPAddress addr, int port )
        {
#pragma warning disable 618
            m_ServerIP = (uint)addr.Address;
#pragma warning restore 618
            m_ServerPort = (ushort)port;
        }

        private static void OnLogout( bool fake )
        {
            if ( !fake )
            {
                PacketHandlers.Party.Clear();

                SetTitleStr( "" );
                Engine.MainWindow.UpdateTitle();
                UOAssist.PostLogout();
                m_ConnStart = DateTime.MinValue;
            }

            World.Player = null;
            World.Items.Clear();
            World.Mobiles.Clear();
            Macros.MacroManager.Stop();
            ActionQueue.Stop();
            Counter.Reset();
            GoldPerHourTimer.Stop();
            DamageTracker.Stop();
            BandageTimer.Stop();
            GateTimer.Stop();
            BuffsTimer.Stop();
            StealthSteps.Unhide();
            Engine.MainWindow.OnLogout();
            if ( Engine.MainWindow.MapWindow != null )
                Engine.MainWindow.MapWindow.Close();
            PacketHandlers.Party.Clear();
            PacketHandlers.IgnoreGumps.Clear();
            Config.Save();

            //TranslateEnabled = false;
        }
        internal static void SetTitleStr( string str )
        {
            if ( m_LastStr == str )
                return;

            m_LastStr = str;
            byte[] copy = System.Text.Encoding.ASCII.GetBytes( str );
            int clen = copy.Length;
            if ( clen >= 512 )
                clen = 511;

            CommMutex.WaitOne();
            if ( clen > 0 )
            {
                fixed ( byte* array = copy )
                    NativeMethods.memcpy( m_TitleStr, array, clen );
            }
            *( m_TitleStr + clen ) = 0;
            CommMutex.ReleaseMutex();

            NativeMethods.PostMessage( ClientCommunication.Instance.ClientWindow, WM_CUSTOMTITLE, IntPtr.Zero, IntPtr.Zero );
        }
        internal override void SendToClient( Packet p )
        {
            if ( !m_Ready || p.Length <= 0 )
                return;

            if ( !m_QueueRecv )
            {
                ForceSendToClient( p );
            }
            else
            {
                m_RecvQueue.Enqueue( p );
            }
        }

        internal override void SendToServer( Packet p )
        {
            if ( !m_Ready )
                return;

            if ( !m_QueueSend )
            {
                ForceSendToServer( p );
            }
            else
            {
                m_SendQueue.Enqueue( p );
            }
        }
        internal override void ForceSendToClient( Packet p )
        {
            byte[] data = p.Compile();

            CommMutex.WaitOne();
            fixed ( byte* ptr = data )
            {
                Packet.Log( PacketPath.RazorToClient, ptr, data.Length );
                CopyToBuffer( m_OutRecv, ptr, data.Length );
            }
            CommMutex.ReleaseMutex();
        }

        internal override void ForceSendToServer( Packet p )
        {
            if ( p == null || p.Length <= 0 )
                return;

            byte[] data = p.Compile();

            CommMutex.WaitOne();
            InitSendFlush();
            fixed ( byte* ptr = data )
            {
                Packet.Log( PacketPath.RazorToServer, ptr, data.Length );
                CopyToBuffer( m_OutSend, ptr, data.Length );
            }
            CommMutex.ReleaseMutex();
        }

        private static void InitSendFlush()
        {
            if ( m_OutSend->Length == 0 )
                NativeMethods.PostMessage( ClientCommunication.Instance.ClientWindow, WM_UONETEVENT, (IntPtr)UONetMessage.Send, IntPtr.Zero );
        }

        internal override bool AllowBit( uint bit )
        {
            return NativeMethods.AllowBit( bit );
        }

        private const int SHARED_BUFF_SIZE = 524288; // 262144; // 250k
        [StructLayout( LayoutKind.Explicit, Size = 8 + SHARED_BUFF_SIZE )]
        private struct Buffer
        {
            [FieldOffset( 0 )] public int Length;
            [FieldOffset( 4 )] public int Start;
            [FieldOffset( 8 )] public byte Buff0;
        }

        private enum UONetMessage
        {
            Send = 1,
            Recv = 2,
            Ready = 3,
            NotReady = 4,
            Connect = 5,
            Disconnect = 6,
            KeyDown = 7,
            Mouse = 8,
            Activate = 9,
            Focus = 10,
            Close = 11,
            StatBar = 12,
            NotoHue = 13,
            DLL_Error = 14,
            SetGameSize = 19,
            FindData = 20,
            SmartCPU = 21,
            Negotiate = 22,
            SetMapHWnd = 23
        }
        private enum InitError
        {
            SUCCESS,
            NO_UOWND,
            NO_TID,
            NO_HOOK,
            NO_SHAREMEM,
            LIB_DISABLED,
            NO_PATCH,
            NO_MEMCOPY,
            INVALID_PARAMS,

            UNKNOWN
        }
    }
}
