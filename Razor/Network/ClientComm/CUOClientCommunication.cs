using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using CUO_API;

namespace Assistant
{
    public enum ClientVersions
    {
        CV_OLD = ( 1 << 24 ) | ( 0 << 16 ) | ( 0 << 8 ) | 0, // Original game
        CV_200 = ( 2 << 24 ) | ( 0 << 16 ) | ( 0 << 8 ) | 0, // T2A Introduction. Adds screen dimensions packet
        CV_204C = ( 2 << 24 ) | ( 0 << 16 ) | ( 4 << 8 ) | 2, // Adds *.def files
        CV_305D = ( 3 << 24 ) | ( 0 << 16 ) | ( 5 << 8 ) | 3, // Renaissance. Expanded character slots.
        CV_306E = ( 3 << 24 ) | ( 0 << 16 ) | ( 0 << 8 ) | 0, // Adds a packet with the client type, switches to mp3 from midi for sound files
        CV_308D = ( 3 << 24 ) | ( 0 << 16 ) | ( 8 << 8 ) | 3, // Adds maximum stats to the status bar
        CV_308J = ( 3 << 24 ) | ( 0 << 16 ) | ( 8 << 8 ) | 9, // Adds followers to the status bar
        CV_308Z = ( 3 << 24 ) | ( 0 << 16 ) | ( 8 << 8 ) | 25, // Age of Shadows. Adds paladin, necromancer, custom housing, resists, profession selection window, removes save password checkbox
        CV_400B = ( 4 << 24 ) | ( 0 << 16 ) | ( 0 << 8 ) | 1, // Deletes tooltips
        CV_405A = ( 4 << 24 ) | ( 0 << 16 ) | ( 5 << 8 ) | 0, // Adds ninja, samurai
        CV_4011D = ( 4 << 24 ) | ( 0 << 16 ) | ( 11 << 8 ) | 3, // Adds elven race
        CV_500A = ( 5 << 24 ) | ( 0 << 16 ) | ( 0 << 8 ) | 0, // Paperdoll buttons journal becomes quests, chat becomes guild. Use mega FileManager.Cliloc. Removes verdata.mul.
        CV_5020 = ( 5 << 24 ) | ( 0 << 16 ) | ( 2 << 8 ) | 0, // Adds buff bar
        CV_5090 = ( 5 << 24 ) | ( 0 << 16 ) | ( 9 << 8 ) | 0, //
        CV_6000 = ( 6 << 24 ) | ( 0 << 16 ) | ( 0 << 8 ) | 0, // Adds colored guild/all chat and ignore system. New targeting systems, object properties and handles.
        CV_6013 = ( 6 << 24 ) | ( 0 << 16 ) | ( 1 << 8 ) | 3, //
        CV_6017 = ( 6 << 24 ) | ( 0 << 16 ) | ( 1 << 8 ) | 8, //
        CV_6040 = ( 6 << 24 ) | ( 0 << 16 ) | ( 4 << 8 ) | 0, // Increased number of player slots
        CV_6060 = ( 6 << 24 ) | ( 0 << 16 ) | ( 6 << 8 ) | 0, //
        CV_60142 = ( 6 << 24 ) | ( 0 << 16 ) | ( 14 << 8 ) | 2, //
        CV_60144 = ( 6 << 24 ) | ( 0 << 16 ) | ( 14 << 8 ) | 4, // Adds gargoyle race.
        CV_7000 = ( 7 << 24 ) | ( 0 << 16 ) | ( 0 << 8 ) | 0, //
        CV_7090 = ( 7 << 24 ) | ( 0 << 16 ) | ( 9 << 8 ) | 0, //
        CV_70130 = ( 7 << 24 ) | ( 0 << 16 ) | ( 13 << 8 ) | 0, //
        CV_70160 = ( 7 << 24 ) | ( 0 << 16 ) | ( 16 << 8 ) | 0, //
        CV_70180 = ( 7 << 24 ) | ( 0 << 16 ) | ( 18 << 8 ) | 0, //
        CV_70240 = ( 7 << 24 ) | ( 0 << 16 ) | ( 24 << 8 ) | 0, // *.mul -> *.uop
        CV_70331 = ( 7 << 24 ) | ( 0 << 16 ) | ( 33 << 8 ) | 1 //
    }
    public unsafe class CUOClientCommunication : ClientCommunication
    {
        public static CUOClientCommunication CUOInstance => Instance as CUOClientCommunication;

        private OnPacketSendRecv _sendToClient, _sendToServer, _recv, _send;
        private OnGetPacketLength _getPacketLength;
        private OnGetPlayerPosition _getPlayerPosition;
        private OnCastSpell _castSpell;
        private OnGetStaticImage _getStaticImage;

        public override IntPtr ClientWindow => m_ClientWindow;

        private OnHotkey _onHotkeyPressed;
        private OnMouse _onMouse;
        private OnUpdatePlayerPosition _onUpdatePlayerPosition;
        private OnClientClose _onClientClose;
        private OnInitialize _onInitialize;
        private OnConnected _onConnected;
        private OnDisconnected _onDisconnected;
        private OnFocusGained _onFocusGained;
        private OnFocusLost _onFocusLost;
        private static Version m_UOVersion;
        private IntPtr m_ClientWindow;
        private DateTime m_ConnectionStart;

        public override DateTime ConnectionStart => m_ConnectionStart;

        public override Process ClientProcess => Process.GetCurrentProcess();

        public override bool ClientRunning => true;

        //TODO Get Address from CUO
        public override IPAddress LastConnection => IPAddress.Loopback;

        public override void SetNegotiate( bool negotiate )
        {
            throw new NotImplementedException();
        }



        internal override void ForceSendToServer( Packet p )
        {
            SendToServer( p );
        }

        internal override Version GetUOVersion()
        {
            return m_UOVersion;
        }

        internal override void SendToServer( Packet p )
        {
            var len = p.Length;
            _sendToServer( p.Compile(), (int)len ); ;
        }

        internal override void SendToClient( Packet p )
        {
            var len = p.Length;
            _sendToClient( p.Compile(), (int)len );
        }

        internal override void ForceSendToClient( Packet p )
        {
            SendToClient( p );
        }

        internal override void SetCustomNotoHue( int v )
        {
            //throw new NotImplementedException();
        }

        internal unsafe bool InstallCUOHooks( PluginHeader* header )
        {
            var v = header->ClientVersion;
            m_UOVersion = new Version( v << 24, v << 16, v << 8, (byte)v );

            _sendToClient = Marshal.GetDelegateForFunctionPointer<OnPacketSendRecv>( header->Recv );
            _sendToServer = Marshal.GetDelegateForFunctionPointer<OnPacketSendRecv>( header->Send );
            _getPacketLength = Marshal.GetDelegateForFunctionPointer<OnGetPacketLength>( header->GetPacketLength );
            _getPlayerPosition = Marshal.GetDelegateForFunctionPointer<OnGetPlayerPosition>( header->GetPlayerPosition );
            _castSpell = Marshal.GetDelegateForFunctionPointer<OnCastSpell>( header->CastSpell );
            _getStaticImage = Marshal.GetDelegateForFunctionPointer<OnGetStaticImage>( header->GetStaticImage );

            m_ClientWindow = header->HWND;

            _recv = OnRecv;
            _send = OnSend;
            _onHotkeyPressed = OnHotKeyHandler;
            _onMouse = OnMouseHandler;
            _onUpdatePlayerPosition = OnPlayerPositionChanged;
            _onClientClose = OnClientClosing;
            _onInitialize = OnInitialize;
            _onConnected = OnConnected;
            _onDisconnected = OnDisconnected;
            _onFocusGained = OnFocusGained;
            _onFocusLost = OnFocusLost;

            header->OnRecv = Marshal.GetFunctionPointerForDelegate( _recv );
            header->OnSend = Marshal.GetFunctionPointerForDelegate( _send );
            header->OnHotkeyPressed = Marshal.GetFunctionPointerForDelegate( _onHotkeyPressed );
            header->OnMouse = Marshal.GetFunctionPointerForDelegate( _onMouse );
            header->OnPlayerPositionChanged = Marshal.GetFunctionPointerForDelegate( _onUpdatePlayerPosition );
            header->OnClientClosing = Marshal.GetFunctionPointerForDelegate( _onClientClose );
            header->OnInitialize = Marshal.GetFunctionPointerForDelegate( _onInitialize );
            header->OnConnected = Marshal.GetFunctionPointerForDelegate( _onConnected );
            header->OnDisconnected = Marshal.GetFunctionPointerForDelegate( _onDisconnected );
            header->OnFocusGained = Marshal.GetFunctionPointerForDelegate( _onFocusGained );
            header->OnFocusLost = Marshal.GetFunctionPointerForDelegate( _onFocusLost );

            return true;
        }
        private void OnInitialize()
        {
            var last = Console.BackgroundColor;
            var lastFore = Console.ForegroundColor;
            Console.BackgroundColor = ConsoleColor.Green;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine( "Initialized Razor instance" );
            Console.BackgroundColor = last;
            Console.ForegroundColor = lastFore;
        }
        private void OnConnected()
        {
            m_ConnectionStart = DateTime.Now;
           
        }

        private void OnDisconnected()
        {
            PacketHandlers.Party.Clear();
            //Windows.SetTitleStr("");
            Engine.MainWindow.UpdateTitle();
            UOAssist.PostLogout();

            World.Player = null;
            World.Items.Clear();
            World.Mobiles.Clear();
            Macros.MacroManager.Stop();
            ActionQueue.Stop();
            Counter.Reset();
            GoldPerHourTimer.Stop();
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
        }

        private void OnClientClosing()
        {
            var last = Console.BackgroundColor;
            var lastFore = Console.ForegroundColor;
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine( "Closing Razor instance" );
            Console.BackgroundColor = last;
            Console.ForegroundColor = lastFore;
        }
        private void OnFocusGained()
        {

        }

        private void OnFocusLost()
        {

        }
        private bool OnHotKeyHandler( int key, int mod, bool ispressed )
        {
            if ( ispressed )
            {
                bool code = HotKey.OnKeyDown( (int)( key | mod ) );

                return code;
            }

            return true;
        }
        private void OnMouseHandler( int button, int wheel )
        {
            if ( button > 4 )
                button = 3;
            else if ( button > 3 )
                button = 2;
            else if ( button > 2 )
                button = 2;
            else if ( button > 1 )
                button = 1;

            HotKey.OnMouse( button, wheel );
        }
        private void OnPlayerPositionChanged( int x, int y, int z )
        {
            World.Player.Position = new Point3D( x, y, z );
        }
        private bool OnRecv( byte[] data, int length )
        {
            fixed ( byte* ptr = data )
            {
                PacketReader p = new PacketReader( ptr, length, PacketsTable.GetPacketLength( data[0] ) < 0 );
                Packet packet = new Packet( data, length, p.DynamicLength );

                return !PacketHandler.OnServerPacket( p.PacketID, p, packet );
            }
        }

        private bool OnSend( byte[] data, int length )
        {
            fixed ( byte* ptr = data )
            {
                PacketReader p = new PacketReader( ptr, length, PacketsTable.GetPacketLength( data[0] ) < 0 );
                Packet packet = new Packet( data, length, p.DynamicLength );

                return !PacketHandler.OnClientPacket( p.PacketID, p, packet );
            }
        }

        internal override void Close()
        {
            throw new NotImplementedException();
        }

        internal override bool AllowBit( uint bit )
        {
            return true;
        }

        internal override uint TotalOut()
        {
            return 0;
        }

        internal override uint TotalIn()
        {
            return 0;
        }
    }
}
