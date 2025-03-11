using hExternal.Modules;
using Swed64;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static hExternal.Program;

namespace hExternal.Features
{
    internal class Aimbot
    {
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);
        public static void AimbotThread(Renderer renderer)
        {
            // Initialize game memory reader
            Swed swed = new Swed("cs2");
            IntPtr client = swed.GetModuleBase("client.dll");

            const int HOTKEY = 0x05; // Mouse 5 for aimbot

            List<Entity> entities = new List<Entity>();
            Entity localPlayer = new Entity();

            while (true)
            {
                try
                {
                    if (!renderer.aimbotEnabled)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    entities.Clear();

                    // Get entity list
                    IntPtr entityList = swed.ReadPointer(client, Offsets.dwEntityList);
                    IntPtr listEntry = swed.ReadPointer(entityList, 0x10);

                    // Update local player information
                    localPlayer.pawnAddress = swed.ReadPointer(client, Offsets.dwLocalPlayerPawn);
                    localPlayer.team = swed.ReadInt(localPlayer.pawnAddress, Offsets.m_iTeamNum);
                    localPlayer.origin = swed.ReadVec(localPlayer.pawnAddress, Offsets.m_vOldOrigin);
                    localPlayer.view = swed.ReadVec(localPlayer.pawnAddress, Offsets.m_vecViewOffset);

                    // Check if player is scoped
                    localPlayer.isScoped = swed.ReadBool(localPlayer.pawnAddress, Offsets.m_bIsScoped);

                    // Get current weapon handle
                    int activeWeaponHandle = swed.ReadInt(localPlayer.pawnAddress, Offsets.m_hActiveWeapon);
                    IntPtr weaponList = swed.ReadPointer(entityList, 0x8 * ((activeWeaponHandle & 0x7FFF) >> 9) + 0x10);

                    if (weaponList != IntPtr.Zero)
                    {
                        IntPtr currentWeapon = swed.ReadPointer(weaponList, 0x78 * (activeWeaponHandle & 0x1FF));
                        if (currentWeapon != IntPtr.Zero)
                        {
                            // Check if weapon is a sniper rifle
                            int weaponType = swed.ReadInt(currentWeapon, Offsets.m_iItemDefinitionIndex);
                            localPlayer.hasSniper = IsSniper(weaponType);
                        }
                    }

                    // Loop through entity list
                    for (int i = 0; i < 64; i++)
                    {
                        if (listEntry == IntPtr.Zero)
                            continue;

                        IntPtr currentController = swed.ReadPointer(listEntry, i * 0x78);

                        if (currentController == IntPtr.Zero)
                            continue;

                        int pawnHandle = swed.ReadInt(currentController, Offsets.m_hPlayerPawn);

                        if (pawnHandle == 0)
                            continue;

                        // Next entry
                        IntPtr listEntry2 = swed.ReadPointer(entityList, 0x8 * ((pawnHandle & 0x7FFF) >> 9) + 0x10);
                        IntPtr currentPawn = swed.ReadPointer(listEntry2, 0x78 * (pawnHandle & 0x1FF));

                        if (currentPawn == localPlayer.pawnAddress)
                            continue;

                        // Get pawn attributes
                        int health = swed.ReadInt(currentPawn, Offsets.m_iHealth);
                        int team = swed.ReadInt(currentPawn, Offsets.m_iTeamNum);
                        uint lifeState = swed.ReadUInt(currentPawn, Offsets.m_lifeState);

                        if (lifeState != 256)
                            continue;

                        if (team == localPlayer.team && !renderer.aimOnTeam)
                            continue;

                        Entity entity = new Entity
                        {
                            pawnAddress = currentPawn,
                            controllerAddress = currentController,
                            health = health,
                            team = team,
                            lifeState = lifeState,
                            origin = swed.ReadVec(currentPawn, Offsets.m_vOldOrigin),
                            view = swed.ReadVec(currentPawn, Offsets.m_vecViewOffset)
                        };

                        // Add head position
                        IntPtr gameSceneNode = swed.ReadPointer(currentPawn, Offsets.m_pGameSceneNode);
                        entity.head = Vector3.Add(entity.origin, new Vector3(0, 0, 75)); // Approximate head position
                        entity.distance = Vector3.Distance(entity.origin, localPlayer.origin);

                        // Calculate 2D screen position
                        ViewMatrix viewMatrix = ReadMatrix(swed, client + Offsets.dwViewMatrix);
                        entity.head2d = Calculate.WorldToScreen(viewMatrix, entity.head, (int)renderer.screenSize.X, (int)renderer.screenSize.Y);
                        entity.pixelDistance = Vector2.Distance(entity.head2d, new Vector2(renderer.screenSize.X / 2, renderer.screenSize.Y / 2));

                        // Check visibility - Skip targets not visible
                        if (renderer.visibilityCheck)
                        {
                            bool isVisible = IsVisible(swed, localPlayer, entity, client);
                            if (!isVisible)
                                continue;
                        }

                        entities.Add(entity);
                    }

                    // Sort entities by distance from crosshair
                    entities = entities.OrderBy(o => o.pixelDistance).ToList();

                    // Apply aimbot if enabled and key pressed
                    if (entities.Count > 0 && GetAsyncKeyState(HOTKEY) < 0 && renderer.aimbotEnabled)
                    {
                        // Get target
                        Entity target = entities[0];

                        // Adjust FOV if scoped
                        float currentFOV = renderer.FOV;
                        if (localPlayer.isScoped && renderer.adjustScopedFOV)
                        {
                            currentFOV = renderer.scopedFOV;
                        }

                        // Check if target is within FOV
                        if (target.pixelDistance < currentFOV)
                        {
                            Vector3 playerView = Vector3.Add(localPlayer.origin, localPlayer.view);

                            // Choose aim point (head or body)
                            Vector3 aimPoint;
                            if (renderer.aimAtHead)
                            {
                                aimPoint = target.head;
                            }
                            else
                            {
                                // Aim at body - slightly lower than head
                                aimPoint = Vector3.Add(target.origin, new Vector3(0, 0, 45));
                            }

                            // Calculate angles
                            Vector2 newAngles = Calculate.CalculateAngles(playerView, aimPoint);

                            // Apply smoothing if enabled
                            if (renderer.smoothAim && renderer.smoothFactor > 0)
                            {
                                // Read current view angles
                                Vector3 currentAngles = swed.ReadVec(client, Offsets.dwViewAngles);

                                // Adjust angles with smoothing
                                float smoothFactor = renderer.smoothFactor;
                                // Apply less smoothing when scoped for more accuracy
                                if (localPlayer.isScoped && localPlayer.hasSniper)
                                {
                                    smoothFactor = renderer.scopedSmoothFactor;
                                }

                                newAngles.X = LerpAngle(currentAngles.Y, newAngles.X, 1.0f / smoothFactor);
                                newAngles.Y = LerpAngle(currentAngles.X, newAngles.Y, 1.0f / smoothFactor);
                            }

                            Vector3 newAnglesVec3 = new Vector3(newAngles.Y, newAngles.X, 0.0f);

                            // Apply aim
                            swed.WriteVec(client, Offsets.dwViewAngles, newAnglesVec3);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Silently handle exceptions to keep thread alive
                }

                Thread.Sleep(10);
            }
        }

        // Check if two players have line of sight to each other
        private static bool IsVisible(Swed swed, Entity localPlayer, Entity target, IntPtr client)
        {
            // For a full implementation, this would use game's ray tracing or read spotted flag
            // This simplified version checks if the entity is "spotted" 
            // (the game sets this flag when the player is visible)
            bool isSpotted = swed.ReadBool(target.pawnAddress, Offsets.m_bSpotted);

            // Combine with a basic distance check for close enemies
            // (The spotted flag might be false if player hasn't looked directly at enemy)
            bool isClose = target.distance < 300.0f; // Close distance check

            return isSpotted || isClose;
        }

        // Determine if weapon is a sniper rifle based on item definition index
        private static bool IsSniper(int weaponId)
        {
            // Common sniper rifle IDs
            return weaponId == 9 ||    // AWP
                   weaponId == 40 ||   // Scout
                   weaponId == 11 ||   // Auto Sniper
                   weaponId == 38;     // Auto Sniper T
        }

        // Smoothly interpolate between angles
        private static float LerpAngle(float currentAngle, float targetAngle, float amount)
        {
            float delta = targetAngle - currentAngle;

            // Normalize delta to [-180, 180]
            if (delta > 180)
                delta -= 360;
            else if (delta < -180)
                delta += 360;

            // Apply smoothing
            float result = currentAngle + delta * amount;

            // Normalize result
            while (result > 180)
                result -= 360;
            while (result < -180)
                result += 360;

            return result;
        }

        static ViewMatrix ReadMatrix(Swed swed, IntPtr matrixAddress)
        {
            var viewMatrix = new ViewMatrix();
            var matrix = swed.ReadMatrix(matrixAddress);

            viewMatrix.m11 = matrix[0];
            viewMatrix.m12 = matrix[1];
            viewMatrix.m13 = matrix[2];
            viewMatrix.m14 = matrix[3];

            viewMatrix.m21 = matrix[4];
            viewMatrix.m22 = matrix[5];
            viewMatrix.m23 = matrix[6];
            viewMatrix.m24 = matrix[7];

            viewMatrix.m31 = matrix[8];
            viewMatrix.m32 = matrix[9];
            viewMatrix.m33 = matrix[10];
            viewMatrix.m34 = matrix[11];

            viewMatrix.m41 = matrix[12];
            viewMatrix.m42 = matrix[13];
            viewMatrix.m43 = matrix[14];
            viewMatrix.m44 = matrix[15];

            return viewMatrix;
        }
    }
}