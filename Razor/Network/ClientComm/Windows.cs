using Assistant.Core;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Assistant.UI;

namespace Assistant
{
	
	public static unsafe class Windows
	{
		internal static unsafe class LinuxWindows
		{
			[DllImport("libX11")]
			private static extern IntPtr XOpenDisplay(IntPtr display);
			[DllImport("libX11")]
			private static extern IntPtr XCloseDisplay(IntPtr display);
			[DllImport("libX11")]
			private static extern int XRaiseWindow(IntPtr display, IntPtr window);
			
			[DllImport("libX11")]
			private static extern int XGetInputFocus(IntPtr display, IntPtr window, IntPtr focus_return);
			
			
			[DllImport("libX11")]
			private static extern int XQueryKeymap(IntPtr display, byte[] keys);
			[DllImport("libX11")]
			private static extern int XKeysymToKeycode(IntPtr display, int key);
			
			public static void RaiseWindow(IntPtr clientWindow)
			{
				XRaiseWindow(Display, clientWindow);
				
			}
			public static IntPtr Display
			{
				get
				{
					if (m_Display == IntPtr.Zero)
						m_Display = XOpenDisplay(IntPtr.Zero);
					return m_Display;
				}
			}

			private static IntPtr m_Display = IntPtr.Zero;
			public static IntPtr GetInputFocus()
			{
				IntPtr res = IntPtr.Zero;
				IntPtr focus = IntPtr.Zero;
				XGetInputFocus(Display, res, focus);
				return res;
			}


			public static bool KeyDown(Keys keys)
			{
				
				try
				{
					var szKey = new byte[32];
					int res = XQueryKeymap(Display, szKey);
					Console.WriteLine("Res: " + res + " Key: " + keys.ToString());
					//foreach(var xx in szKey)
					//Console.WriteLine(xx + "-");
					int code = XKeysymToKeycode(Display, (int) keys);
					bool pressed = (szKey[code >> 3] & (1 << (code & 7))) == 0;
					var r = szKey[code / 8];
					var s = (1 << (code % 8));
					var x = r & s;
					Console.WriteLine("key " + (int) keys + " code " + code + " r " + r + " s " + s + " x " + x);
					return r == s;
				}
				catch
				{
					return false;
				}
				
			}
			[DllImport ("libX11")]
			public extern static int XStoreName(IntPtr display, IntPtr window, string window_name);
			public static void DrawTitleBar(IntPtr clientWindow, string str)
			{
				XStoreName(Display, clientWindow, str);

			}
		}
		internal static unsafe class Win32Windows
		{
			[DllImport("WinUtil.dll")]
			internal static extern unsafe IntPtr CaptureScreen(IntPtr handle, bool isFullScreen, string msgStr);
			[DllImport("WinUtil.dll")]
			internal static extern unsafe void BringToFront(IntPtr hWnd);
			[DllImport("WinUtil.dll")]
			internal static extern unsafe int HandleNegotiate(ulong word);
			[DllImport("WinUtil.dll")]
			internal static extern unsafe bool AllowBit(ulong bit);
			[DllImport("WinUtil.dll")]
			internal static extern unsafe void InitTitleBar(string path);
			[DllImport("WinUtil.dll")]
			internal static extern unsafe void DrawTitleBar(IntPtr handle, string path);
			[DllImport("WinUtil.dll")]
			internal static extern unsafe void FreeTitleBar();
			[DllImport("WinUtil.dll")]
			internal static extern unsafe void CreateUOAWindow(IntPtr razorWindow);
			[DllImport("WinUtil.dll")]
			internal static extern unsafe void DestroyUOAWindow();
			[DllImport("user32.dll")]
			internal static extern bool SetForegroundWindow(IntPtr hWnd);
			[DllImport("user32.dll")]
			internal static extern IntPtr GetForegroundWindow();

			[DllImport("user32.dll")]
			internal static extern uint PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
			[DllImport("kernel32.dll")]
			internal static extern ushort GlobalAddAtom(string str);
			[DllImport("kernel32.dll")]
			internal static extern ushort GlobalDeleteAtom(ushort atom);
			[DllImport("kernel32.dll")]
			internal static extern uint GlobalGetAtomName(ushort atom, StringBuilder buff, int bufLen);

			[DllImport("Advapi32.dll")]
			internal static extern int GetUserNameA(StringBuilder buff, int* len);
		}
		



		public static string GetWindowsUserName()
		{
			int len = 1024;
			StringBuilder sb = new StringBuilder(len);
			if (Win32Windows.GetUserNameA(sb, &len) != 0)
				return sb.ToString();
			else
				return "";
		}

		public static string EncodeColorStat(int val, int max)
		{
			double perc = ((double)val) / ((double)max);

			if (perc <= 0.25)
				return String.Format("~#FF0000{0}~#~", val);
			else if (perc <= 0.75)
				return String.Format("~#FFFF00{0}~#~", val);
			else
				return val.ToString();
		}

		private static Timer m_TBTimer;
		private static string m_LastStr = "";
		private static StringBuilder m_TBBuilder = new StringBuilder();
		private static string m_LastPlayerName = "";

		public static void RequestTitlebarUpdate()
		{
			// throttle updates, since things like counters might request 1000000 million updates/sec
			if (m_TBTimer == null)
				m_TBTimer = new TitleBarThrottle();

			if (!m_TBTimer.Running)
				m_TBTimer.Start();
		}
	    private static void UpdateTitleBar()
	    {
		    if (ClientCommunication.Instance.ClientWindow == IntPtr.Zero)
			    return;

	        if (World.Player != null && Config.GetBool("TitleBarDisplay"))
	        {
	            // reuse the same sb each time for less damn allocations
	            m_TBBuilder.Remove(0, m_TBBuilder.Length);
	            m_TBBuilder.Insert(0, Config.GetString("TitleBarText"));
	            StringBuilder sb = m_TBBuilder;
	            //StringBuilder sb = new StringBuilder( Config.GetString( "TitleBarText" ) ); // m_TitleCapacity

	            PlayerData p = World.Player;

	            if (p.Name != m_LastPlayerName)
	            {
	                m_LastPlayerName = p.Name;

	                Engine.MainWindow.UpdateTitle();
	            }

	            sb.Replace(@"{char}",
	                Config.GetBool("ShowNotoHue") ? $"~#{p.GetNotorietyColor() & 0x00FFFFFF:X6}{p.Name}~#~" : p.Name);

	            sb.Replace(@"{shard}", World.ShardName);

	            sb.Replace(@"{crimtime}", p.CriminalTime != 0 ? $"~^C0C0C0{p.CriminalTime}~#~" : "-");

	            sb.Replace(@"{str}", p.Str.ToString());
	            sb.Replace(@"{hpmax}", p.HitsMax.ToString());

	            sb.Replace(@"{hp}", p.Poisoned ? $"~#FF8000{p.Hits}~#~" : EncodeColorStat(p.Hits, p.HitsMax));

	            sb.Replace(@"{dex}", World.Player.Dex.ToString());
	            sb.Replace(@"{stammax}", World.Player.StamMax.ToString());
	            sb.Replace(@"{stam}", EncodeColorStat(p.Stam, p.StamMax));
	            sb.Replace(@"{int}", World.Player.Int.ToString());
	            sb.Replace(@"{manamax}", World.Player.ManaMax.ToString());
	            sb.Replace(@"{mana}", EncodeColorStat(p.Mana, p.ManaMax));

	            sb.Replace(@"{ar}", p.AR.ToString());
	            sb.Replace(@"{tithe}", p.Tithe.ToString());

	            sb.Replace(@"{physresist}", p.AR.ToString());
	            sb.Replace(@"{fireresist}", p.FireResistance.ToString());
	            sb.Replace(@"{coldresist}", p.ColdResistance.ToString());
	            sb.Replace(@"{poisonresist}", p.PoisonResistance.ToString());
	            sb.Replace(@"{energyresist}", p.EnergyResistance.ToString());

	            sb.Replace(@"{luck}", p.Luck.ToString());

	            sb.Replace(@"{damage}", String.Format("{0}-{1}", p.DamageMin, p.DamageMax));

	            sb.Replace(@"{weight}",
	                World.Player.Weight >= World.Player.MaxWeight
	                    ? $"~#FF0000{World.Player.Weight}~#~"
	                    : World.Player.Weight.ToString());

	            sb.Replace(@"{maxweight}", World.Player.MaxWeight.ToString());

	            sb.Replace(@"{followers}", World.Player.Followers.ToString());
	            sb.Replace(@"{followersmax}", World.Player.FollowersMax.ToString());

	            sb.Replace(@"{gold}", World.Player.Gold.ToString());

	            sb.Replace(@"{gps}", GoldPerHourTimer.Running ? $"{GoldPerHourTimer.GoldPerSecond:N2}" : "-");
	            sb.Replace(@"{gpm}", GoldPerHourTimer.Running ? $"{GoldPerHourTimer.GoldPerMinute:N2}" : "-");
	            sb.Replace(@"{gph}", GoldPerHourTimer.Running ? $"{GoldPerHourTimer.GoldPerHour:N2}" : "-");
	            sb.Replace(@"{goldtotal}", GoldPerHourTimer.Running ? $"{GoldPerHourTimer.GoldSinceStart}" : "-");
                 sb.Replace(@"{goldtotalmin}", GoldPerHourTimer.Running ? $"{GoldPerHourTimer.TotalMinutes:N2} min" : "-");

                 sb.Replace(@"{bandage}", BandageTimer.Running ? $"~#FF8000{BandageTimer.Count}~#~" : "-");

	            sb.Replace(@"{skill}", SkillTimer.Running ? $"{SkillTimer.Count}" : "-");
	            sb.Replace(@"{gate}", GateTimer.Running ? $"{GateTimer.Count}" : "-");

	            sb.Replace(@"{stealthsteps}", StealthSteps.Counting ? StealthSteps.Count.ToString() : "-");
                //ClientCommunication.ConnectionStart != DateTime.MinValue )
                //time = (int)((DateTime.UtcNow - ClientCommunication.ConnectionStart).TotalSeconds);
                 sb.Replace(@"{uptime}", ClientCommunication.Instance.ConnectionStart != DateTime.MinValue ? Utility.FormatTime((int)((DateTime.UtcNow - ClientCommunication.Instance.ConnectionStart).TotalSeconds)) : "-");

	            sb.Replace(@"{dps}", DamageTracker.Running ? $"{DamageTracker.DamagePerSecond:N2}" : "-");
	            sb.Replace(@"{maxdps}", DamageTracker.Running ? $"{DamageTracker.MaxDamagePerSecond:N2}" : "-");
                 sb.Replace(@"{maxdamagedealt}", DamageTracker.Running ? $"{DamageTracker.MaxSingleDamageDealt}" : "-");
	            sb.Replace(@"{maxdamagetaken}", DamageTracker.Running ? $"{DamageTracker.MaxSingleDamageTaken}" : "-");
                 sb.Replace(@"{totaldamagedealt}", DamageTracker.Running ? $"{DamageTracker.TotalDamageDealt}" : "-");
	            sb.Replace(@"{totaldamagetaken}", DamageTracker.Running ? $"{DamageTracker.TotalDamageTaken}" : "-");


                string buffList = string.Empty;

	            if (BuffsTimer.Running)
	            {
	                StringBuilder buffs = new StringBuilder();
	                foreach (BuffsDebuffs buff in World.Player.BuffsDebuffs)
	                {
	                    int timeLeft = 0;

	                    if (buff.Duration > 0)
	                    {
	                        TimeSpan diff = DateTime.UtcNow - buff.Timestamp;
	                        timeLeft = buff.Duration - (int)diff.TotalSeconds;
                         }

	                    buffs.Append(timeLeft <= 0
	                        ? $"{buff.ClilocMessage1}, "
	                        : $"{buff.ClilocMessage1} ({timeLeft}), ");
	                }

	                buffs.Length = buffs.Length - 2;
                     buffList = buffs.ToString();
                     sb.Replace(@"{buffsdebuffs}", buffList);

                 }
	            else
	            {
	                sb.Replace(@"{buffsdebuffs}", "-");
                     buffList = string.Empty;
                 }

                 string statStr = String.Format("{0}{1:X2}{2:X2}{3:X2}",
	                (int) (p.GetStatusCode()),
	                (int) (World.Player.HitsMax == 0 ? 0 : (double) World.Player.Hits / World.Player.HitsMax * 99),
	                (int) (World.Player.ManaMax == 0 ? 0 : (double) World.Player.Mana / World.Player.ManaMax * 99),
	                (int) (World.Player.StamMax == 0 ? 0 : (double) World.Player.Stam / World.Player.StamMax * 99));

	            sb.Replace(@"{statbar}", $"~SR{statStr}");
	            sb.Replace(@"{mediumstatbar}", $"~SL{statStr}");
	            sb.Replace(@"{largestatbar}", $"~SX{statStr}");

	            bool dispImg = Config.GetBool("TitlebarImages");
	            for (int i = 0; i < Counter.List.Count; i++)
	            {
	                Counter c = Counter.List[i];
	                if (c.Enabled)
	                    sb.Replace($"{{{c.Format}}}", c.GetTitlebarString(dispImg && c.DisplayImage));
	            }

	            SetTitleStr(sb.ToString());
	        }
	        else
	        {
	            SetTitleStr("");
	        }
	    }



		private class TitleBarThrottle : Timer
		{
			public TitleBarThrottle() : base(TimeSpan.FromSeconds(0.25))
			{
			}

			protected override void OnTick()
			{
				UpdateTitleBar();
			}
		}

		
		public static void SetTitleStr(string str)
		{
			if (m_LastStr == str)
				return;

			m_LastStr = str;
			
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				Win32Windows.DrawTitleBar(ClientCommunication.Instance.ClientWindow, str);
			else
			{
				LinuxWindows.DrawTitleBar(ClientCommunication.Instance.ClientWindow, str);
			}
		}

		public static bool AllowBit(uint agent)
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				return Win32Windows.AllowBit(agent);
			return true;
		}

		public static IntPtr CaptureScreen( bool getBool, string timestamp)
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				Win32Windows.CaptureScreen(ClientCommunication.Instance.ClientWindow,getBool,timestamp);
			return IntPtr.Zero;
		}

		public static void BringToFront(IntPtr clientWindow)
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				Win32Windows.BringToFront(clientWindow);
			else
			{
				LinuxWindows.RaiseWindow(clientWindow);
			}
		}

		public static void FreeTitleBar()
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				Win32Windows.FreeTitleBar();
		}

		public static int HandleNegotiate(ulong features)
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				Win32Windows.HandleNegotiate(features);
			return 0;
		}

		public static void CreateUOAWindow(IntPtr clientWindow)
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				Win32Windows.CreateUOAWindow(clientWindow);
		}

		public static void DestroyUOAWindow()
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				Win32Windows.DestroyUOAWindow();
		}

		public static void InitTitleBar(string path)
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				Win32Windows.InitTitleBar(path);
		}

		public static void SetForegroundWindow(IntPtr clientWindow)
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				Win32Windows.SetForegroundWindow(clientWindow);
			else
			{
				LinuxWindows.RaiseWindow(clientWindow);
			}
		}

		public static IntPtr GetForegroundWindow()
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				return Win32Windows.GetForegroundWindow();
			else
			{
				return LinuxWindows.GetInputFocus();
			}
		}

		public static ushort GlobalAddAtom(string str)
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				return Win32Windows.GlobalAddAtom(str);
			return 0;
		}

		public static uint PostMessage(IntPtr hWnd, uint msg, IntPtr atom, IntPtr zero)
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				return Win32Windows.PostMessage(hWnd,msg,atom,zero);
			return 0;
		}

		public static uint GlobalGetAtomName(ushort lParam, StringBuilder sb, int p2)
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				return Win32Windows.GlobalGetAtomName(lParam,sb,p2);
			return 0;
		}

		public static void GlobalDeleteAtom(ushort lParam)
		{
			if(Environment.OSVersion.Platform != PlatformID.Unix)
				 Win32Windows.GlobalDeleteAtom(lParam);
			return;
		}
		
		[DllImport( "user32.dll" )]
		private static extern ushort GetAsyncKeyState( int key );
		
		public static bool KeyDown(Keys keys)
		{
			 if(Environment.OSVersion.Platform != PlatformID.Unix)
            			    return ( GetAsyncKeyState( (int)keys ) & 0xFF00 ) != 0 ;//|| ClientCommunication.IsKeyDown( (int)k );
			 else
			 {
				return LinuxWindows.KeyDown(keys);
			 }
		}
	}
}
