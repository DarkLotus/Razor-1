using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Net;
using Assistant.Core;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Assistant
{
	public class FeatureBit
	{
		public static readonly uint WeatherFilter	=  0;
		public static readonly uint LightFilter		=  1;
		public static readonly uint SmartLT			=  2;
		public static readonly uint RangeCheckLT	=  3;
		public static readonly uint AutoOpenDoors	=  4;
		public static readonly uint UnequipBeforeCast= 5;
		public static readonly uint AutoPotionEquip	=  6;
		public static readonly uint BlockHealPoisoned= 7;
		public static readonly uint LoopingMacros	=  8; // includes fors and macros running macros
		public static readonly uint UseOnceAgent	=  9;
		public static readonly uint RestockAgent	= 10;
		public static readonly uint SellAgent		= 11;
		public static readonly uint BuyAgent		= 12;
		public static readonly uint PotionHotkeys	= 13;
		public static readonly uint RandomTargets	= 14;
		public static readonly uint ClosestTargets	= 15;
		public static readonly uint OverheadHealth	= 16;

		public static readonly uint MaxBit			= 16;
	}

	public abstract unsafe class ClientCommunication
	{
        public static ClientCommunication Instance;

        internal abstract Version GetUOVersion();

       
        public abstract IntPtr ClientWindow { get;  }


        



        public abstract IPAddress LastConnection { get; }


        private static Timer m_TBTimer;
        public abstract DateTime ConnectionStart { get; }



		public abstract Process ClientProcess{ get; }
        public abstract bool ClientRunning { get; }
       

        internal static void Init( bool isOSI )
        {
            if ( isOSI )
                Instance = new OSIClientCommunication();
            else
                Instance = new CUOClientCommunication();
        }

        private static bool m_ClientEnc = false;
		internal static bool ClientEncrypted { get { return m_ClientEnc; } set { m_ClientEnc = value; } }

		private static bool m_ServerEnc = false;
		internal static bool ServerEncrypted { get { return m_ServerEnc; } set { m_ServerEnc = value; } }

		



        public abstract void SetNegotiate( bool negotiate );

        internal abstract bool AllowBit( uint bit );

      

		public static string EncodeColorStat( int val, int max )
		{
			double perc = ((double)val)/((double)max);

			if ( perc <= 0.25 )
				return String.Format( "~#FF0000{0}~#~", val );
			else if ( perc <= 0.75 )
				return String.Format( "~#FFFF00{0}~#~", val );
			else
				return val.ToString();
		}

		public static void RequestTitlebarUpdate()
		{
			// throttle updates, since things like counters might request 1000000 million updates/sec
			if ( m_TBTimer == null )
				m_TBTimer = new TitleBarThrottle();

			if ( !m_TBTimer.Running )
				m_TBTimer.Start();
		}

		private class TitleBarThrottle : Timer
		{
			public TitleBarThrottle() : base( TimeSpan.FromSeconds( 0.25 )  )
			{
			}

			protected override void OnTick()
			{
                //TODO CUO Titlebar
                if(Instance is OSIClientCommunication)
				    OSIClientCommunication.UpdateTitleBar();
			}
		}

        private enum UONetMessageCopyData
        {
            Position = 1,
        }
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
        [StructLayout(LayoutKind.Sequential, Pack=1)]
		private struct CopyData
		{
			public int dwData;
			public int cbDAta;
			public IntPtr lpData;
		};

		[StructLayout(LayoutKind.Sequential, Pack=1)]
		private struct Position
		{
			public ushort x;
			public ushort y;
			public ushort z;
		};

		internal static unsafe bool OnCopyData(IntPtr wparam, IntPtr lparam)
		{
			CopyData copydata = (CopyData)Marshal.PtrToStructure(lparam, typeof(CopyData));

			switch ((UONetMessageCopyData)copydata.dwData)
			{
				case UONetMessageCopyData.Position:
					if (World.Player != null)
					{
						Position pos = (Position)Marshal.PtrToStructure(copydata.lpData, typeof(Position));
						Point3D pt = new Point3D();

						pt.X = pos.x;
						pt.Y = pos.y;
						pt.Z = pos.z;

						World.Player.Position = pt;
					}
					return true;
			}

			return false;
		}

		

        internal abstract void SendToServer( Packet p );
        internal abstract void SendToClient( Packet p );
        internal abstract void ForceSendToServer( Packet p );
        internal abstract void ForceSendToClient( Packet p );

        internal static void CalibratePosition( uint x, uint y, uint z, byte direction )
        {
            NativeMethods.CalibratePosition( x, y, z, direction );
        }

        internal static int HandleNegotiate( ulong features )
        {
            return NativeMethods.HandleNegotiate(features);
        }

        internal abstract uint TotalOut(); 
        internal abstract uint TotalIn();
        internal abstract void SetCustomNotoHue( int v );
        internal abstract void Close();
    }

}

