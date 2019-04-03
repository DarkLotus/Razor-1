using System;
using System.Diagnostics;
using System.Net;
using Assistant.MapUO;
using CUO_API;

namespace Assistant
{
    public abstract class ClientCommunication
    {
        public static ClientCommunication Instance;

        public abstract void SendToServer(Packet packet);

        public abstract void SendToClient(Packet packet);

        public abstract void ForceSendToClient(Packet packet);

        public abstract bool ClientEncrypted { get; set; }
        public abstract bool ServerEncrypted { get; set; }
        public abstract DateTime ConnectionStart { get; set; }
        public abstract IPAddress LastConnection { get; set; }
        public abstract bool ClientRunning { get; }
        public static Process ClientProcess { get; set; }
        public abstract IntPtr ClientWindow { get; set; }

        public abstract void SetConnectionInfo(IPAddress none, int p1);

        public abstract Loader_Error LaunchClient(string clientPath);

        public abstract void Close();
        
        public enum Loader_Error
        {
            SUCCESS = 0,
            NO_OPEN_EXE,
            NO_MAP_EXE,
            NO_READ_EXE_DATA,

            NO_RUN_EXE,
            NO_ALLOC_MEM,

            NO_WRITE,
            NO_VPROTECT,
            NO_READ,

            UNKNOWN_ERROR = 99
        };

        public abstract bool Attach(int attPid);

        public abstract string GetWindowsUserName();

        public abstract unsafe bool InstallCUOHooks(ref PluginHeader* plugin);

        public static void Init(bool isOSIClient)
        {
            if(isOSIClient)
                Instance = new OSIClientCommunication();
            else
            {
                Instance = new CUOClientCommunication();
            }
        }

        public abstract IntPtr FindUOWindow();

        public abstract void BringToFront(IntPtr findUoWindow);

        public abstract void SetMapWndHandle(MapWindow mainWindowMapWindow);

        public abstract void CalibratePosition(uint positionX, uint positionY, uint positionZ, byte mDirection);

        public abstract int HandleNegotiate(ulong features);
        public abstract bool InstallHooks(IntPtr mainWindow);

        public static uint TotalOut()
        {
            return 0;
        }        
        public static uint TotalIn()
         {
             return 0;
         }

        public static void SetGameSize(int p0, int p1)
        {
           // throw new NotImplementedException();
        }

        public static void SetCustomNotoHue(int i)
        {
            //throw new NotImplementedException();
        }

        public static void RequestStatbarPatch(bool @checked)
        {
            //throw new NotImplementedException();
        }       

        public static void SetNegotiate(bool negotiateChecked)
        {
            //throw new NotImplementedException();
        }
    }
}