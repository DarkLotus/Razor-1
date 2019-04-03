using System;
using System.Net;
using System.Runtime.InteropServices;
using Assistant.MapUO;
using CUO_API;

namespace Assistant
{
    public unsafe class CUOClientCommunication : ClientCommunication
    {
        public override IntPtr ClientWindow { get; set; } = IntPtr.Zero;
        private OnPacketSendRecv _sendToClient, _sendToServer, _recv, _send;
        private OnGetPacketLength _getPacketLength;
        private OnGetPlayerPosition _getPlayerPosition;
        private OnCastSpell _castSpell;
        private OnGetStaticImage _getStaticImage;

        private OnHotkey _onHotkeyPressed;
        private OnMouse _onMouse;
        private OnUpdatePlayerPosition _onUpdatePlayerPosition;
        private OnClientClose _onClientClose;
        private OnInitialize _onInitialize;
        private OnConnected _onConnected;
        private OnDisconnected _onDisconnected;
        private OnFocusGained _onFocusGained;
        private OnFocusLost _onFocusLost;
        public override unsafe bool InstallCUOHooks(ref PluginHeader* header)
        {
             _sendToClient = Marshal.GetDelegateForFunctionPointer<OnPacketSendRecv>(header->Recv);
            _sendToServer = Marshal.GetDelegateForFunctionPointer<OnPacketSendRecv>(header->Send);
            _getPacketLength = Marshal.GetDelegateForFunctionPointer<OnGetPacketLength>(header->GetPacketLength);
            _getPlayerPosition = Marshal.GetDelegateForFunctionPointer<OnGetPlayerPosition>(header->GetPlayerPosition);
            _castSpell = Marshal.GetDelegateForFunctionPointer<OnCastSpell>(header->CastSpell);
            _getStaticImage = Marshal.GetDelegateForFunctionPointer<OnGetStaticImage>(header->GetStaticImage);

            ClientWindow = header->HWND;

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

            header->OnRecv = Marshal.GetFunctionPointerForDelegate(_recv);
            header->OnSend = Marshal.GetFunctionPointerForDelegate(_send);
            header->OnHotkeyPressed = Marshal.GetFunctionPointerForDelegate(_onHotkeyPressed);
            header->OnMouse = Marshal.GetFunctionPointerForDelegate(_onMouse);
            header->OnPlayerPositionChanged = Marshal.GetFunctionPointerForDelegate(_onUpdatePlayerPosition);
            header->OnClientClosing = Marshal.GetFunctionPointerForDelegate(_onClientClose);
            header->OnInitialize = Marshal.GetFunctionPointerForDelegate(_onInitialize);
            header->OnConnected = Marshal.GetFunctionPointerForDelegate(_onConnected);
            header->OnDisconnected = Marshal.GetFunctionPointerForDelegate(_onDisconnected);
            header->OnFocusGained = Marshal.GetFunctionPointerForDelegate(_onFocusGained);
            header->OnFocusLost = Marshal.GetFunctionPointerForDelegate(_onFocusLost);

			return true;
        }
        private void OnInitialize()
        {
            var last = Console.BackgroundColor;
            var lastFore = Console.ForegroundColor;
            Console.BackgroundColor = ConsoleColor.Green;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine("Initialized Razor instance");
            Console.BackgroundColor = last;
            Console.ForegroundColor = lastFore;
        }
        private void OnConnected()
        {
            ConnectionStart = DateTime.Now;
            try
            {
                //m_LastConnection = new IPAddress((uint)lParam);
            }
            catch
            {
            }
        }

        private  void OnDisconnected()
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
            if (Engine.MainWindow.MapWindow != null)
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
            Console.WriteLine("Closing Razor instance");
            Console.BackgroundColor = last;
            Console.ForegroundColor = lastFore;
        }
        private void OnFocusGained()
        {

        }

        private void OnFocusLost()
        {
           
        }
        private bool OnHotKeyHandler(int key, int mod, bool ispressed)
        {
            if (ispressed)
            {
                bool code = HotKey.OnKeyDown((int)(key | mod));

                return code;
            }

            return true;
        }
        private void OnMouseHandler(int button, int wheel)
        {
            if (button > 4)
                button = 3;
            else if (button > 3)
                button = 2;
            else if (button > 2)
                button = 2;
            else if (button > 1)
                button = 1;

            HotKey.OnMouse(button, wheel);
        }
        private void OnPlayerPositionChanged(int x, int y, int z)
        {
            World.Player.Position = new Point3D(x, y, z);
        }
        private  bool OnRecv(byte[] data, int length)
        {
            fixed (byte* ptr = data)
            {
                PacketReader p = new PacketReader(ptr, length, PacketsTable.GetPacketLength(data[0]) < 0);
                Packet packet = new Packet(data, length, p.DynamicLength);

                return !PacketHandler.OnServerPacket(p.PacketID, p, packet);
            }
        }

        private  bool OnSend(byte[] data, int length)
        {
            fixed (byte* ptr = data)
            {
                PacketReader p = new PacketReader(ptr, length, PacketsTable.GetPacketLength(data[0]) < 0);
                Packet packet = new Packet(data, length, p.DynamicLength);

                return !PacketHandler.OnClientPacket(p.PacketID, p, packet);
            }
        }
       

      
       

        public override void SendToServer(Packet p)
        {
            var len = p.Length;
            _sendToServer(p.Compile(), (int)len);;
        }

        public override void SendToClient(Packet p)
        {
            var len = p.Length;
            _sendToClient(p.Compile(), (int)len);
        }

        public override void ForceSendToClient(Packet p)
        {
            var len = p.Length;
            _sendToClient(p.Compile(), (int)len);
        }

        public override bool ClientEncrypted { get; set; }
        public override bool ServerEncrypted { get; set; }
        public override DateTime ConnectionStart { get;set;  }
        public override IPAddress LastConnection { get;set;  }
        public override bool ClientRunning { get;  } = true;

        public override void SetConnectionInfo(IPAddress none, int p1)
        {
            //throw new NotImplementedException();
        }

        public override Loader_Error LaunchClient(string clientPath)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
           // throw new NotImplementedException();
        }

        public override bool Attach(int attPid)
        {
            throw new NotImplementedException();
        }

        public override string GetWindowsUserName()
        {
            return "ReallySecureString-WePromise";
        }



        public override IntPtr FindUOWindow()
        {
            return ClientWindow;
        }

        public override void BringToFront(IntPtr ptr)
        {
            //throw new NotImplementedException();
        }

        public override void SetMapWndHandle(MapWindow mainWindowMapWindow)
        {
            //throw new NotImplementedException();
        }

        public override void CalibratePosition(uint positionX, uint positionY, uint positionZ, byte mDirection)
        {
            //throw new NotImplementedException();
        }

        public override int HandleNegotiate(ulong features)
        {
            return 0;
        }

        public override bool InstallHooks(IntPtr mainWindow)
        {
            return true;
        }
    }
}