using hExternal.Modules;
using Swed64;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static hExternal.Program;

namespace hExternal.Features
{
    public class TriggerBot
    {
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        public static void TriggerBotThread(Renderer renderer)
        {
            Swed swed = new Swed("cs2");
            IntPtr client = swed.GetModuleBase("client.dll");

            // Attack offset
            IntPtr Attack = client + 0x1883720; // dwForceAttack
            const int TRIGGER_KEY = 0x06; // Mouse 4 for trigger bot

            while (true)
            {
                try
                {
                    if (!renderer.triggerBotEnabled)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    IntPtr localPlayerPawn = swed.ReadPointer(client, Offsets.dwLocalPlayerPawn);
                    int entIndex = swed.ReadInt(localPlayerPawn, 0x1458); // Crosshair ID

                    // Activate trigger bot when key pressed and entity in crosshair
                    if (GetAsyncKeyState(TRIGGER_KEY) < 0 && entIndex > 0)
                    {
                        swed.WriteInt(Attack, 65537); // +attack
                        Thread.Sleep(1);
                        swed.WriteInt(Attack, 256); // -attack
                    }
                }
                catch (Exception)
                {
                    // Silently handle exceptions
                }

                Thread.Sleep(1);
            }
        }
    }
}
