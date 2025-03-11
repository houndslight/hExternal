using hExternal.Modules;
using Swed64;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static hExternal.Program;

namespace hExternal.Features
{
    public class FOVChanger
    {
        public static void FovChangerThread(Renderer renderer)
        {
            Swed swed = new Swed("cs2");
            IntPtr client = swed.GetModuleBase("client.dll");

            // Camera offsets
            int m_pCameraServices = 0x11E0;
            int m_iFOV = 0x210;
            int m_bIsScoped = 0x23E8;

            while (true)
            {
                try
                {
                    if (!renderer.fovChangerEnabled)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    uint desiredFov = (uint)renderer.fovValue;

                    IntPtr localPlayerPawn = swed.ReadPointer(client, Offsets.dwLocalPlayerPawn);
                    IntPtr cameraServices = swed.ReadPointer(localPlayerPawn, m_pCameraServices);
                    uint currentFov = swed.ReadUInt(cameraServices + m_iFOV);
                    bool isScoped = swed.ReadBool(localPlayerPawn, m_bIsScoped);

                    // Update FOV if not scoped and different from desired
                    if (!isScoped && currentFov != desiredFov)
                    {
                        swed.WriteUInt(cameraServices + m_iFOV, desiredFov);
                    }
                }
                catch (Exception)
                {
                    // Silently handle exceptions
                }

                Thread.Sleep(10);
            }
        }
    }
}
