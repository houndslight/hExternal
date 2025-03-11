using hExternal.Modules;
using Swed64;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace hExternal.Features
{
    internal class BunnyHop
    {
        public static void BunnyhopThread()
        {
            Swed swed = new Swed("cs2");
            IntPtr client = swed.GetModuleBase("client.dll");

            [DllImport("user32.dll")]
            static extern short GetAsyncKeyState(int vKey);

            // Constants
            const int SPACE_BAR = 0x20;
            const uint STANDING = 65665;
            const uint CROUCHED = 65667;
            const uint pJump = 65537; // +jump
            const uint mJump = 16777472; // -jump

            // Force jump offset
            IntPtr forceJump = client + 0x1883C30;

            while (true)
            {
                try
                {
                    IntPtr playerPawnAddress = swed.ReadPointer(client, Offsets.dwLocalPlayerPawn);
                    uint fFlag = swed.ReadUInt(playerPawnAddress, 0x3EC);

                    if (GetAsyncKeyState(SPACE_BAR) < 0)
                    {
                        if (fFlag == STANDING || fFlag == CROUCHED) // Grounded
                        {
                            Thread.Sleep(1);
                            swed.WriteUInt(forceJump, pJump); // +jump
                        }
                        else
                        {
                            swed.WriteUInt(forceJump, mJump); // -jump
                        }
                    }
                }
                catch (Exception)
                {
                    // Silently handle exceptions
                }

                Thread.Sleep(5);
            }
        }
    }
}
