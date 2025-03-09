using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ClickableTransparentOverlay;
using ImGuiNET;
using Swed64;

namespace hExternal
{
    public class Program
    {
        // DLL import for keyboard/mouse input detection
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        // Main entry point
        static async Task Main(string[] args)
        {
            // Initialize the overlay renderer
            Renderer renderer = new Renderer();
            await renderer.Start();

            // Start the cheat features in separate threads
            Task.Run(() => AimbotThread(renderer));
            Task.Run(() => BunnyhopThread());
            Task.Run(() => FovChangerThread(renderer));
            Task.Run(() => TriggerBotThread(renderer));
            Task.Run(() => SkinChangerThread(renderer));  // New SkinChanger thread

            // Keep the main thread alive
            await Task.Delay(-1);
        }

        // Aimbot thread
        static void AimbotThread(Renderer renderer)
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

                        entities.Add(entity);
                    }

                    // Sort entities by distance from crosshair
                    entities = entities.OrderBy(o => o.pixelDistance).ToList();

                    // Apply aimbot if enabled and key pressed
                    if (entities.Count > 0 && GetAsyncKeyState(HOTKEY) < 0 && renderer.aimbotEnabled)
                    {
                        Vector3 playerView = Vector3.Add(localPlayer.origin, localPlayer.view);
                        Vector3 entityView = Vector3.Add(entities[0].origin, entities[0].view);

                        // Check if target is within FOV
                        if (entities[0].pixelDistance < renderer.FOV)
                        {
                            Vector2 newAngles = Calculate.CalculateAngles(playerView, entityView);
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

        // Bunny hop thread
        static void BunnyhopThread()
        {
            Swed swed = new Swed("cs2");
            IntPtr client = swed.GetModuleBase("client.dll");

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

        // FOV changer thread
        static void FovChangerThread(Renderer renderer)
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

        // Trigger bot thread
        static void TriggerBotThread(Renderer renderer)
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

        // New SkinChanger thread
        static void SkinChangerThread(Renderer renderer)
        {
            Swed swed = new Swed("cs2");
            IntPtr client = swed.GetModuleBase("client.dll");

            // Inventory and item related offsets - these would need to be updated
            // This is just a placeholder for the thread to be added
            while (true)
            {
                try
                {
                    if (!renderer.skinChangerEnabled)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    // SkinChanger logic would go here
                    // You'd need to implement the actual skin changing functionality based on the
                    // renderer.selectedSkin, renderer.selectedWeapon, renderer.skinWear, etc.

                }
                catch (Exception)
                {
                    // Silently handle exceptions
                }

                Thread.Sleep(100);
            }
        }
    }

    // ImGui overlay renderer
    public class Renderer : Overlay
    {
        // Settings
        public bool aimbotEnabled = false;
        public bool aimOnTeam = false;
        public float FOV = 50f;
        public Vector4 circleColor = new Vector4(1, 1, 1, 1);

        public bool triggerBotEnabled = false;
        public bool bunnyhopEnabled = false;
        public bool fovChangerEnabled = false;
        public int fovValue = 110;

        // Skin Changer settings
        public bool skinChangerEnabled = false;
        public bool skinChangerOptionsOpen = false;
        public int selectedSkin = 0;
        public int selectedWeapon = 0;
        public float skinWear = 0.01f;
        public int skinStatTrak = -1;
        public bool skinApplyAll = false;

        public Vector2 screenSize = new Vector2(1920, 1080);

        // Changed colors back to green for "RUNNING" and orange for "VAC STATUS"
        private Vector4 greenColor = new Vector4(0, 1, 0, 1);       // Green for "RUNNING"
        private Vector4 orangeTextColor = new Vector4(1, 0.5f, 0, 1); // Orange for "VAC STATUS"
        private Vector4 whiteTextColor = new Vector4(1, 1, 1, 1);    // White text for UI elements

        // Weapon and skin data for dropdowns
        private string[] weapons = { "AK-47", "M4A4", "M4A1-S", "AWP", "Desert Eagle", "USP-S", "Glock-18", "P250" };
        private string[] skins = { "Asiimov", "Dragon Lore", "Hyper Beast", "Neo-Noir", "Fade", "Doppler", "Marble Fade", "Slaughter" };

        protected override void Render()
        {
            screenSize = ImGui.GetIO().DisplaySize;

            // Set ImGui style colors
            ImGuiStylePtr style = ImGui.GetStyle();
            style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.8f, 0.1f, 0.1f, 1.0f);  // Red title bar
            style.Colors[(int)ImGuiCol.Button] = new Vector4(0.7f, 0.1f, 0.1f, 1.0f);         // Red buttons
            style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.9f, 0.2f, 0.2f, 1.0f);  // Lighter red when hovered
            style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.6f, 0.1f, 0.1f, 1.0f);   // Darker red when active
            style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.7f, 0.1f, 0.1f, 0.5f);        // Red frame background
            style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.8f, 0.2f, 0.2f, 0.7f); // Lighter red when hovered
            style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.9f, 0.3f, 0.3f, 0.9f);  // Even lighter red when active
            style.Colors[(int)ImGuiCol.CheckMark] = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);      // White checkmark
            style.Colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.8f, 0.1f, 0.1f, 1.0f);     // Red slider
            style.Colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.9f, 0.2f, 0.2f, 1.0f); // Lighter red slider when active
            style.Colors[(int)ImGuiCol.Header] = new Vector4(0.7f, 0.1f, 0.1f, 0.9f);         // Red header
            style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.8f, 0.2f, 0.2f, 0.9f);  // Lighter red header when hovered
            style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.9f, 0.3f, 0.3f, 0.9f);   // Even lighter red header when active
            style.Colors[(int)ImGuiCol.Text] = whiteTextColor;                                // Changed to white text for all elements

            // Main window
            ImGui.Begin("hExternal");

            if (ImGui.CollapsingHeader("Aimbot", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Checkbox("Enable Aimbot", ref aimbotEnabled);
                ImGui.Checkbox("Aim on teammates", ref aimOnTeam);
                ImGui.SliderFloat("FOV", ref FOV, 10, 300);
                ImGui.Text("Activation: Mouse 5 (Side button)");

                if (ImGui.CollapsingHeader("FOV Circle Color"))
                {
                    ImGui.ColorPicker4("##CircleColor", ref circleColor);
                }
            }

            if (ImGui.CollapsingHeader("Trigger Bot", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Checkbox("Enable Trigger Bot", ref triggerBotEnabled);
                ImGui.Text("Activation: Mouse 4 (Side button)");
            }

            if (ImGui.CollapsingHeader("Bunny Hop", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Checkbox("Enable Bunny Hop", ref bunnyhopEnabled);
                ImGui.Text("Activation: Spacebar");
            }

            if (ImGui.CollapsingHeader("FOV Changer", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Checkbox("Enable FOV Changer", ref fovChangerEnabled);
                ImGui.SliderInt("FOV Value", ref fovValue, 60, 140);
            }

            // Add SkinChanger section
            if (ImGui.CollapsingHeader("Skin Changer", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Checkbox("Enable Skin Changer (NOT IMPLIMENTED)", ref skinChangerEnabled);

                if (ImGui.Button("Skin Changer Options (NOT IMPLIMENTED)"))
                {
                    skinChangerOptionsOpen = true;
                }
            }

            // Add spacing before status text
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Changed status text colors
            ImGui.TextColored(greenColor, "STATUS: RUNNING");
            ImGui.SameLine();
            ImGui.TextColored(orangeTextColor, "VAC STATUS: UNTESTED");

            ImGui.End();

            // Draw SkinChanger window if open
            if (skinChangerOptionsOpen)
            {
                DrawSkinChangerWindow();
            }

            // Draw FOV circle and welcome message overlay
            DrawOverlay();
        }

        void DrawSkinChangerWindow()
        {
            ImGui.SetNextWindowSize(new Vector2(400, 450), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("SkinChanger", ref skinChangerOptionsOpen))
            {
                // Weapon selection
                ImGui.Text("Select Weapon:");
                ImGui.Combo("##Weapon", ref selectedWeapon, weapons, weapons.Length);

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Skin selection
                ImGui.Text("Select Skin:");
                ImGui.Combo("##Skin", ref selectedSkin, skins, skins.Length);

                ImGui.Spacing();

                // Skin wear slider
                ImGui.Text("Skin Wear:");
                ImGui.SliderFloat("##Wear", ref skinWear, 0.0f, 1.0f, "%.2f");

                ImGui.SameLine();

                if (ImGui.Button("Factory New"))
                    skinWear = 0.01f;

                ImGui.SameLine();

                if (ImGui.Button("BS"))
                    skinWear = 0.9f;

                ImGui.Spacing();

                // StatTrak counter
                ImGui.Text("StatTrak™ Counter:");
                ImGui.SliderInt("##StatTrak", ref skinStatTrak, -1, 9999, skinStatTrak == -1 ? "Disabled" : "%d kills");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Additional options
                ImGui.Checkbox("Apply To All Weapons", ref skinApplyAll);

                ImGui.Spacing();
                ImGui.Spacing();

                // Buttons row
                float windowWidth = ImGui.GetWindowWidth();
                float buttonsWidth = 300;
                float buttonsPosX = (windowWidth - buttonsWidth) * 0.5f;

                ImGui.SetCursorPosX(buttonsPosX);

                if (ImGui.Button("Apply Changes", new Vector2(150, 30)))
                {
                    // Would connect to skin changer function
                    // SkinChangerApply();
                }

                ImGui.SameLine();

                if (ImGui.Button("Reset All", new Vector2(150, 30)))
                {
                    // Would reset all skins
                    // SkinChangerReset();
                }

                ImGui.Spacing();
                ImGui.Spacing();

                // Status display at bottom
                ImGui.TextColored(
                    skinChangerEnabled ? greenColor : orangeTextColor,
                    skinChangerEnabled ? "STATUS: ACTIVE" : "STATUS: DISABLED");

                if (skinChangerEnabled)
                {
                    ImGui.Text($"Currently modifying: {weapons[selectedWeapon]} | {skins[selectedSkin]}");
                    ImGui.Text($"Wear: {GetWearName(skinWear)} | StatTrak: {(skinStatTrak >= 0 ? skinStatTrak.ToString() : "Disabled")}");
                }
            }
            ImGui.End();
        }

        string GetWearName(float wear)
        {
            if (wear < 0.07f) return "Factory New";
            if (wear < 0.15f) return "Minimal Wear";
            if (wear < 0.38f) return "Field-Tested";
            if (wear < 0.45f) return "Well-Worn";
            return "Battle-Scarred";
        }

        void DrawOverlay()
        {
            ImGui.SetNextWindowSize(screenSize);
            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.Begin("hExternal Overlay",
                ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoBackground |
                ImGuiWindowFlags.NoBringToFrontOnFocus |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoInputs |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse);

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();

            // Only draw FOV circle if aimbot is enabled
            if (aimbotEnabled)
            {
                drawList.AddCircle(
                    new Vector2(screenSize.X / 2, screenSize.Y / 2),
                    FOV,
                    ImGui.ColorConvertFloat4ToU32(circleColor));
            }

            // Add welcome text in top right corner
            string welcomeText = "hExternal Welcome, Hounds";
            float welcomeTextWidth = ImGui.CalcTextSize(welcomeText).X;

            // Get current date and time
            string dateTimeText = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");
            float dateTimeTextWidth = ImGui.CalcTextSize(dateTimeText).X;

            // Position in top right with small padding
            float padding = 10.0f;
            float textX = screenSize.X - Math.Max(welcomeTextWidth, dateTimeTextWidth) - padding;

            // Draw welcome text and date/time with shadow for better visibility
            Vector4 textColor = new Vector4(1, 1, 1, 1); // White text
            Vector4 shadowColor = new Vector4(0, 0, 0, 0.7f); // Black shadow

            // Draw welcome text with shadow
            drawList.AddText(new Vector2(textX + 1, padding + 1), ImGui.ColorConvertFloat4ToU32(shadowColor), welcomeText);
            drawList.AddText(new Vector2(textX, padding), ImGui.ColorConvertFloat4ToU32(textColor), welcomeText);

            // Draw date/time with shadow (positioned below welcome text)
            drawList.AddText(new Vector2(textX + 1, padding + ImGui.GetTextLineHeight() + 1), ImGui.ColorConvertFloat4ToU32(shadowColor), dateTimeText);
            drawList.AddText(new Vector2(textX, padding + ImGui.GetTextLineHeight()), ImGui.ColorConvertFloat4ToU32(textColor), dateTimeText);

            ImGui.End();
        }
    }

    // Calculation utilities
    public static class Calculate
    {
        public static Vector2 CalculateAngles(Vector3 from, Vector3 to)
        {
            float yaw;
            float pitch;

            // Calculate yaw
            float deltaX = to.X - from.X;
            float deltaY = to.Y - from.Y;
            yaw = (float)(Math.Atan2(deltaY, deltaX) * 180 / Math.PI);

            // Calculate pitch
            float deltaZ = to.Z - from.Z;
            double distance = Math.Sqrt(Math.Pow(deltaY, 2) + Math.Pow(deltaX, 2));
            pitch = -(float)(Math.Atan2(deltaZ, distance) * 180 / Math.PI);

            return new Vector2(yaw, pitch);
        }

        public static Vector2 WorldToScreen(ViewMatrix matrix, Vector3 pos, int width, int height)
        {
            Vector2 screenCoordinates = new Vector2();

            // Screen width
            float screenW = (matrix.m41 * pos.X) + (matrix.m42 * pos.Y) + (matrix.m43 * pos.Z) + matrix.m44;

            if (screenW > 0.001f)
            {
                // Screen x and y values
                float screenX = (matrix.m11 * pos.X) + (matrix.m12 * pos.Y) + (matrix.m13 * pos.Z) + matrix.m14;
                float screenY = (matrix.m21 * pos.X) + (matrix.m22 * pos.Y) + (matrix.m23 * pos.Z) + matrix.m24;

                // Center camera
                float camX = width / 2;
                float camY = height / 2;

                // Handle perspective division
                float X = camX + (camX * screenX / screenW);
                float Y = camY - (camY * screenY / screenW);

                // Return coordinates
                screenCoordinates.X = X;
                screenCoordinates.Y = Y;
                return screenCoordinates;
            }
            else
            {
                // Out of range
                return new Vector2(-99, -99);
            }
        }
    }

    // Entity class
    public class Entity
    {
        public IntPtr pawnAddress { get; set; }
        public IntPtr controllerAddress { get; set; }
        public Vector3 origin { get; set; }
        public Vector3 view { get; set; }
        public Vector3 head { get; set; }
        public Vector2 head2d { get; set; }
        public int health { get; set; }
        public int team { get; set; }
        public uint lifeState { get; set; }
        public float distance { get; set; }
        public float pixelDistance { get; set; }
    }

    // Memory offsets
    public static class Offsets
    {
        // Main offsets
        public static int dwViewAngles = 0x1AACA70;
        public static int dwLocalPlayerPawn = 0x188AF20;
        public static int dwEntityList = 0x1A36A00;
        public static int dwViewMatrix = 0x1AA27F0;

        // Entity offsets
        public static int m_hPlayerPawn = 0x80C;
        public static int m_iHealth = 0x344;
        public static int m_vOldOrigin = 0x1324;
        public static int m_iTeamNum = 0x3E3;
        public static int m_vecViewOffset = 0xCB0;
        public static int m_lifeState = 0x348;
        public static int m_pGameSceneNode = 0x328;
    }

    // View matrix class
    public class ViewMatrix
    {
        public float m11, m12, m13, m14;
        public float m21, m22, m23, m24;
        public float m31, m32, m33, m34;
        public float m41, m42, m43, m44;
    }
}