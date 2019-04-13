﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Assistant
{
    /*static bool Negotiated = false;
static uint8_t AuthBits[16] = {};

DLLFUNCTION bool AllowBit(uint64_t bit)
{
	bit &= 0x0000003F; // limited to 64 bits
	return Negotiated || (AuthBits[7 - (bit / 8)] & (1 << (bit % 8))) == 0;
}

DLLFUNCTION void HandleNegotiate(uint64_t features)
{
	memcpy(AuthBits, &features, 16);
	Negotiated = true;
}*/

    internal static unsafe class Win32Platform
    {
        [DllImport( "Platform.dll" )]
        internal static unsafe extern bool AllowBit( ulong bit );
        [DllImport( "Platform.dll" )]
        internal static unsafe extern int HandleNegotiate( ulong word );
        [DllImport( "Platform.dll" )]
        internal static unsafe extern IntPtr CaptureScreen( IntPtr handle, bool isFullScreen, string msgStr );
        [DllImport( "Platform.dll" )]
        internal static unsafe extern void BringToFront( IntPtr hWnd );
        [DllImport( "user32.dll" )]
        internal static extern ushort GetAsyncKeyState( int key );
    }
    internal static unsafe class LinuxPlatform
    {
        private static byte[] m_AllowedBits = new byte[16];
        private static bool m_Negotiated = false;
        internal static bool AllowBit( ulong bit )
        {
            bit &= 0x0000003F;
            var val = ( 1 << (int)( bit % 8 ) );
            return m_Negotiated || ( m_AllowedBits[7 - ( bit / 8 )] & val ) == 0;
        }

        internal static int HandleNegotiate( ulong word )
        {
            m_AllowedBits = BitConverter.GetBytes( word );
            m_Negotiated = true;
            return 0;
        }

        internal static void BringToFront( IntPtr window )
        {

        }
    }
    internal static unsafe class Platform
    {
        internal static ushort GetAsyncKeyState( int key )
        {
            if ( Environment.OSVersion.Platform == PlatformID.Unix )
                return 0;
            else
                return Win32Platform.GetAsyncKeyState(key);
        }
        internal static IntPtr CaptureScreen( IntPtr handle, bool isFullScreen, string msgStr )
        {
            if ( Environment.OSVersion.Platform == PlatformID.Unix )
                return IntPtr.Zero;
            else
                return Win32Platform.CaptureScreen( handle, isFullScreen, msgStr );
        }
        internal static void BringToFront( IntPtr window )
        {
            if ( Environment.OSVersion.Platform == PlatformID.Unix )
                LinuxPlatform.BringToFront( window );
            else
                Win32Platform.BringToFront( window );
        }
        internal static bool AllowBit(ulong bit)
        {
            if ( Environment.OSVersion.Platform == PlatformID.Unix )
                return LinuxPlatform.AllowBit(bit);
            else
                return Win32Platform.AllowBit( bit );
        }

        internal static int HandleNegotiate( ulong word )
        {
            if ( Environment.OSVersion.Platform == PlatformID.Unix )
                return LinuxPlatform.HandleNegotiate( word );
            else
                return Win32Platform.HandleNegotiate( word );
        }

        [DllImport( "User32.dll" )]
        private static extern IntPtr GetSystemMenu( IntPtr wnd, bool reset );

        [DllImport( "User32.dll" )]
        private static extern IntPtr EnableMenuItem( IntPtr menu, uint item, uint options );

        [DllImport( "msvcrt.dll" )]
        internal static unsafe extern void memcpy( void* to, void* from, int len );

        [DllImport( "user32.dll" )]
        internal static extern uint PostMessage( IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam );
        [DllImport( "user32.dll" )]
        internal static extern bool SetForegroundWindow( IntPtr hWnd );

        [DllImport( "kernel32.dll" )]
        internal static extern uint GlobalGetAtomName( ushort atom, StringBuilder buff, int bufLen );

        [DllImport( "Advapi32.dll" )]
        internal static extern int GetUserNameA( StringBuilder buff, int* len );

        [DllImport( "user32.dll" )]
        internal static extern IntPtr SendMessage( IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam );
        


        public static string GetWindowsUserName()
        {
            int len = 1024;
            StringBuilder sb = new StringBuilder( len );
            if ( GetUserNameA( sb, &len ) != 0 )
                return sb.ToString();
            else
                return "";
        }

        internal static void DisableCloseButton( IntPtr handle )
        {
            if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
            {
                IntPtr menu = GetSystemMenu( handle, false );
                EnableMenuItem( menu, 0xF060, 0x00000002 ); //menu, SC_CLOSE, MF_BYCOMMAND|MF_GRAYED
            }         
        }
    }
}
