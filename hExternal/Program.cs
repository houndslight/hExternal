using hExternal.Modules;
using hExternal.Features;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
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
            var renderer = new Renderer();

            // Start the renderer
            await renderer.Start();

            // Start the cheat features in separate threads
            Task.Run(() => Aimbot.AimbotThread(renderer));
            Task.Run(() => BunnyHop.BunnyhopThread());
            Task.Run(() => FOVChanger.FovChangerThread(renderer));
            Task.Run(() => TriggerBot.TriggerBotThread(renderer));
            Task.Run(() => SkinChanger.SkinChangerThread(renderer));

            // Menu toggle thread using Insert key
            Task.Run(() => MenuToggleThread(renderer));

            // Keep the main thread alive
            await Task.Delay(-1);
        }

        // Thread to monitor Insert key for menu toggle
        static void MenuToggleThread(Renderer renderer)
        {
            const int VK_INSERT = 0x2D; // Virtual key code for Insert key
            bool keyPressed = false;

            while (true)
            {
                // Check if Insert key is pressed
                bool isKeyDown = (GetAsyncKeyState(VK_INSERT) & 0x8000) != 0;

                // Toggle when key is pressed (not held)
                if (isKeyDown && !keyPressed)
                {
                    renderer.showMenu = !renderer.showMenu;
                    keyPressed = true;
                }
                else if (!isKeyDown)
                {
                    keyPressed = false;
                }

                // Small delay to prevent excessive CPU usage
                Task.Delay(10).Wait();
            }
        }

        // Renderer class
        public class Renderer : Overlay
        {
            // Add menu toggle flag
            public bool showMenu = true;

            // Build date constant
            private string buildDate = "Build Date: 3/10/2025 1:24 PM";

            // State properties
            public bool aimbotEnabled = false;
            public bool aimOnTeam = false;
            public float FOV = 50f;
            public Vector4 circleColor = new Vector4(1, 1, 1, 1);

            public bool triggerBotEnabled = false;
            public bool bunnyhopEnabled = false;
            public bool fovChangerEnabled = false;
            public int fovValue = 110;

            public bool visibilityCheck = true;
            public bool aimAtHead = true;
            public bool smoothAim = true;
            public float smoothFactor = 5.0f;
            public bool adjustScopedFOV = true;
            public float scopedFOV = 20f;
            public float scopedSmoothFactor = 2.0f;

            // Skin Changer settings
            public bool skinChangerEnabled = false;
            public int selectedSkin = 0;
            public int selectedWeapon = 0;
            public float skinWear = 0.01f;
            public int skinStatTrak = -1;
            public bool skinApplyAll = false;
            public string customName = "hExternal";

            public Vector2 screenSize = new Vector2(1920, 1080);

            // ImGui state tracking
            private int activePage = 0; // 0 = Main, 1 = Settings
            private int activeTab = 0;
            private string[] mainTabLabels = new string[] { "Aimbot", "Trigger Bot", "Other Features" };
            private string[] settingsTabLabels = new string[] { "Config" };
            private int activeHeader = 0;
            private string[] headerLabels = new string[] { "Main", "Settings", "About" };

            // Config input/output
            private string configName = "default";
            private string statusMessage = "";
            private bool showStatusMessage = false;
            private float statusMessageTimer = 0f;
            private Vector4 statusMessageColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            private string[] savedConfigs = new string[0];
            private int selectedConfig = -1;

            // Color definitions
            private readonly Vector4 maroonColor = new Vector4(0.5f, 0.0f, 0.0f, 1.0f);
            private readonly Vector4 maroonHoverColor = new Vector4(0.6f, 0.1f, 0.1f, 1.0f);
            private readonly Vector4 maroonActiveColor = new Vector4(0.7f, 0.2f, 0.2f, 1.0f);
            private readonly Vector4 backgroundColor = new Vector4(0.361f, 0.329f, 0.306f, 1.0f); // #5c544e
            private readonly Vector4 childBgColor = new Vector4(0.302f, 0.275f, 0.255f, 1.0f); // Darker shade of #5c544e
            private readonly Vector4 whiteTextColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            private readonly Vector4 greenStatusColor = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
            private readonly Vector4 orangeVacColor = new Vector4(1.0f, 0.65f, 0.0f, 1.0f);
            private readonly Vector4 redErrorColor = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
            private readonly Vector4 greenSuccessColor = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);

            // Config class to serialize/deserialize settings
            public class ConfigFile
            {
                // General
                public bool AimbotEnabled { get; set; }
                public bool AimOnTeam { get; set; }
                public float FOV { get; set; }
                public bool VisibilityCheck { get; set; }
                public bool AimAtHead { get; set; }
                public bool SmoothAim { get; set; }
                public float SmoothFactor { get; set; }
                public bool AdjustScopedFOV { get; set; }
                public float ScopedFOV { get; set; }
                public float ScopedSmoothFactor { get; set; }

                // Other features
                public bool TriggerBotEnabled { get; set; }
                public bool BunnyhopEnabled { get; set; }
                public bool FovChangerEnabled { get; set; }
                public int FovValue { get; set; }

                // Skin changer
                public bool SkinChangerEnabled { get; set; }
                public int SelectedSkin { get; set; }
                public int SelectedWeapon { get; set; }
                public float SkinWear { get; set; }
                public int SkinStatTrak { get; set; }
                public bool SkinApplyAll { get; set; }
                public string CustomName { get; set; }
            }

            protected override void Render()
            {
                screenSize = ImGui.GetIO().DisplaySize;

                // Set global style colors
                SetImGuiStyle();

                // Update status message timer
                if (showStatusMessage)
                {
                    statusMessageTimer -= ImGui.GetIO().DeltaTime;
                    if (statusMessageTimer <= 0)
                    {
                        showStatusMessage = false;
                    }
                }

                // Draw overlay elements
                DrawOverlay();

                // Draw menu if enabled
                if (showMenu)
                {
                    DrawMenu();
                }
            }

            private void SetImGuiStyle()
            {
                ImGuiStylePtr style = ImGui.GetStyle();

                // Set main colors
                style.Colors[(int)ImGuiCol.WindowBg] = backgroundColor;
                style.Colors[(int)ImGuiCol.ChildBg] = childBgColor;
                style.Colors[(int)ImGuiCol.Text] = whiteTextColor;

                // Set checkbox colors
                style.Colors[(int)ImGuiCol.CheckMark] = whiteTextColor;
                style.Colors[(int)ImGuiCol.FrameBg] = maroonColor;
                style.Colors[(int)ImGuiCol.FrameBgHovered] = maroonHoverColor;
                style.Colors[(int)ImGuiCol.FrameBgActive] = maroonActiveColor;

                // Set slider colors
                style.Colors[(int)ImGuiCol.SliderGrab] = maroonColor;
                style.Colors[(int)ImGuiCol.SliderGrabActive] = maroonActiveColor;

                // Set button colors
                style.Colors[(int)ImGuiCol.Button] = maroonColor;
                style.Colors[(int)ImGuiCol.ButtonHovered] = maroonHoverColor;
                style.Colors[(int)ImGuiCol.ButtonActive] = maroonActiveColor;

                // Set header colors
                style.Colors[(int)ImGuiCol.Header] = maroonColor;
                style.Colors[(int)ImGuiCol.HeaderHovered] = maroonHoverColor;
                style.Colors[(int)ImGuiCol.HeaderActive] = maroonActiveColor;

                // Set tab colors
                style.Colors[(int)ImGuiCol.Tab] = maroonColor;
                style.Colors[(int)ImGuiCol.TabHovered] = maroonHoverColor;
                style.Colors[(int)ImGuiCol.TabActive] = maroonActiveColor;
                style.Colors[(int)ImGuiCol.TabUnfocused] = new Vector4(0.4f, 0.0f, 0.0f, 0.8f);
                style.Colors[(int)ImGuiCol.TabUnfocusedActive] = new Vector4(0.5f, 0.1f, 0.1f, 0.9f);

                // Set border colors
                style.Colors[(int)ImGuiCol.Border] = new Vector4(0.6f, 0.1f, 0.1f, 1.0f);
                style.Colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

                // Set scrollbar colors
                style.Colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.2f, 0.2f, 0.2f, 1.0f);
                style.Colors[(int)ImGuiCol.ScrollbarGrab] = maroonColor;
                style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = maroonHoverColor;
                style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = maroonActiveColor;

                // Set title colors
                style.Colors[(int)ImGuiCol.TitleBg] = maroonColor;
                style.Colors[(int)ImGuiCol.TitleBgActive] = maroonActiveColor;
                style.Colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.3f, 0.0f, 0.0f, 0.5f);

                // Set separator colors
                style.Colors[(int)ImGuiCol.Separator] = new Vector4(0.6f, 0.1f, 0.1f, 1.0f);
                style.Colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.7f, 0.2f, 0.2f, 1.0f);
                style.Colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.8f, 0.3f, 0.3f, 1.0f);

                // Hide resize grips (removes pull tab in bottom left)
                style.Colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0, 0, 0, 0);
                style.Colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0, 0, 0, 0);
                style.Colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0, 0, 0, 0);
            }

            private void DrawMenu()
            {
                // Set window position to top left corner with padding
                ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowSize(new Vector2(450, 600), ImGuiCond.FirstUseEver);

                // Begin main window with NoResize flag to remove the resize grip/pull tab
                ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize;
                if (ImGui.Begin("hExternal", ref showMenu, windowFlags))
                {
                    // Top navigation bar (headers) - without scrollbars while keeping hExternal text
                    if (ImGui.BeginChild("HeaderBar", new Vector2(0, 30), ImGuiChildFlags.Border | ImGuiChildFlags.AlwaysUseWindowPadding, ImGuiWindowFlags.NoScrollbar))
                    {
                        // Display logo or icon
                        ImGui.Text("hExternal");
                        ImGui.SameLine(150);

                        // Header buttons
                        for (int i = 0; i < headerLabels.Length; i++)
                        {
                            if (i > 0) ImGui.SameLine();
                            ImGui.PushStyleColor(ImGuiCol.Text, activeHeader == i ? whiteTextColor : new Vector4(0.7f, 0.7f, 0.7f, 1));
                            if (ImGui.Button(headerLabels[i], new Vector2(80, 20)))
                            {
                                activeHeader = i;

                                // Set active page based on header selection
                                if (i == 0) // Main
                                {
                                    activePage = 0;
                                }
                                else if (i == 1) // Settings
                                {
                                    activePage = 1;
                                }
                                // About would be case 2
                            }
                            ImGui.PopStyleColor();
                        }
                    }
                    ImGui.EndChild();

                    // Display status message if active
                    if (showStatusMessage)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, statusMessageColor);
                        ImGui.TextWrapped(statusMessage);
                        ImGui.PopStyleColor();
                        ImGui.Separator();
                    }

                    // Display content based on active page
                    if (activePage == 0) // Main
                    {
                        DrawMainPage();
                    }
                    else if (activePage == 1) // Settings
                    {
                        DrawSettingsPage();
                    }
                    // Add other pages as needed (About, etc.)

                    // Status footer
                    ImGui.Separator();

                    // Use original green color for STATUS: RUNNING
                    ImGui.PushStyleColor(ImGuiCol.Text, greenStatusColor);
                    ImGui.Text("STATUS: RUNNING");
                    ImGui.PopStyleColor();

                    ImGui.SameLine(ImGui.GetWindowWidth() - 150);

                    // Use original orange color for VAC STATUS
                    ImGui.PushStyleColor(ImGuiCol.Text, orangeVacColor);
                    ImGui.Text("VAC STATUS: UNTESTED");
                    ImGui.PopStyleColor();

                    // White for the rest of the text
                    ImGui.Text("Press INSERT to toggle menu visibility");
                }
                ImGui.End();
            }

            private void DrawMainPage()
            {
                // Tab bar for Main page
                if (ImGui.BeginTabBar("MainTabBar"))
                {
                    if (ImGui.BeginTabItem(mainTabLabels[0])) // Aimbot
                    {
                        activeTab = 0;
                        DrawAimbotTab();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem(mainTabLabels[1])) // Trigger Bot
                    {
                        activeTab = 1;
                        DrawTriggerBotTab();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem(mainTabLabels[2])) // Other Features
                    {
                        activeTab = 2;
                        DrawOtherFeaturesTab();
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
            }

            private void DrawSettingsPage()
            {
                // Tab bar for Settings page
                if (ImGui.BeginTabBar("SettingsTabBar"))
                {
                    if (ImGui.BeginTabItem(settingsTabLabels[0])) // Config
                    {
                        DrawConfigTab();
                        ImGui.EndTabItem();
                    }
                    // You can add more settings tabs here
                    ImGui.EndTabBar();
                }
            }

            private void DrawAimbotTab()
            {
                ImGui.Spacing();

                bool temp = aimbotEnabled;
                if (ImGui.Checkbox("Enable Aimbot", ref temp))
                {
                    aimbotEnabled = temp;
                }

                temp = aimOnTeam;
                if (ImGui.Checkbox("Aim on teammates", ref temp))
                {
                    aimOnTeam = temp;
                }

                // FOV Slider
                ImGui.PushItemWidth(ImGui.GetWindowWidth() - 100);
                float tempFOV = FOV;
                ImGui.Text("FOV: " + tempFOV.ToString("0"));
                if (ImGui.SliderFloat("##FOV", ref tempFOV, 10, 300, ""))
                {
                    FOV = tempFOV;
                }
                ImGui.PopItemWidth();

                // Activation info
                ImGui.Spacing();
                if (ImGui.BeginChild("Activation", new Vector2(ImGui.GetWindowWidth() - 16, 30), ImGuiChildFlags.Border, ImGuiWindowFlags.NoScrollbar))
                {
                    ImGui.Text("Activation: Mouse 5 (Side button)");
                }
                ImGui.EndChild();

                // Advanced Settings
                ImGui.Spacing();
                if (ImGui.BeginChild("Advanced", new Vector2(ImGui.GetWindowWidth() - 16, 150), ImGuiChildFlags.Border))
                {
                    ImGui.PushFont(ImGui.GetFont()); // You can use a different font if available
                    ImGui.Text("Advanced Settings");
                    ImGui.PopFont();
                    ImGui.Separator();

                    temp = visibilityCheck;
                    if (ImGui.Checkbox("Visibility Check (No Wall Bang)", ref temp))
                    {
                        visibilityCheck = temp;
                    }

                    temp = aimAtHead;
                    if (ImGui.Checkbox("Aim at Head", ref temp))
                    {
                        aimAtHead = temp;
                    }

                    // Smoothing Factor slider
                    ImGui.PushItemWidth(ImGui.GetWindowWidth() - 130);
                    float tempSmooth = smoothFactor;
                    ImGui.Text("Smoothing Factor: " + tempSmooth.ToString("0") + "%");
                    if (ImGui.SliderFloat("##Smooth", ref tempSmooth, 0, 100, ""))
                    {
                        smoothFactor = tempSmooth;
                    }
                    ImGui.PopItemWidth();
                }
                ImGui.EndChild();
            }

            private void DrawTriggerBotTab()
            {
                ImGui.Spacing();

                // Trigger Bot Settings
                if (ImGui.BeginChild("TriggerBotSettings", new Vector2(ImGui.GetWindowWidth() - 16, 300), ImGuiChildFlags.Border))
                {
                    ImGui.PushFont(ImGui.GetFont());
                    ImGui.Text("Trigger Bot Settings");
                    ImGui.PopFont();
                    ImGui.Separator();

                    bool temp = triggerBotEnabled;
                    if (ImGui.Checkbox("Enable Trigger Bot", ref temp))
                    {
                        triggerBotEnabled = temp;
                    }

                    ImGui.Spacing();
                    ImGui.Text("Activation: Mouse 4 (Side button)");

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    // Additional TriggerBot settings can be added here
                    bool tempVisCheck = visibilityCheck;
                    if (ImGui.Checkbox("Visibility Check (No Wall Bang)", ref tempVisCheck))
                    {
                        visibilityCheck = tempVisCheck;
                    }

                    // Add delay slider (new setting for TriggerBot)
                    ImGui.PushItemWidth(ImGui.GetWindowWidth() - 130);
                    float tempDelay = 10.0f; // Default value, add this as a class variable
                    ImGui.Text("Trigger Delay: " + tempDelay.ToString("0") + "ms");
                    if (ImGui.SliderFloat("##TriggerDelay", ref tempDelay, 0, 200, ""))
                    {
                        // Here you would set the trigger delay class variable
                        // triggerDelay = tempDelay;
                    }
                    ImGui.PopItemWidth();

                    ImGui.Spacing();

                    // Team options
                    bool tempTeamCheck = !aimOnTeam; // Inverse of aim on team
                    if (ImGui.Checkbox("Don't trigger on teammates", ref tempTeamCheck))
                    {
                        aimOnTeam = !tempTeamCheck;
                    }
                }
                ImGui.EndChild();
            }

            private void DrawOtherFeaturesTab()
            {
                ImGui.Spacing();

                // Bunny Hop Section
                if (ImGui.BeginChild("BunnyHop", new Vector2(ImGui.GetWindowWidth() - 16, 80), ImGuiChildFlags.Border, ImGuiWindowFlags.NoScrollbar))
                {
                    ImGui.PushFont(ImGui.GetFont());
                    ImGui.Text("Bunny Hop");
                    ImGui.PopFont();
                    ImGui.Separator();

                    bool temp = bunnyhopEnabled;
                    if (ImGui.Checkbox("Enable Bunny Hop", ref temp))
                    {
                        bunnyhopEnabled = temp;
                    }

                    ImGui.Text("Activation: Spacebar");
                }
                ImGui.EndChild();

                ImGui.Spacing();

                // FOV Changer Section
                if (ImGui.BeginChild("FOVChanger", new Vector2(ImGui.GetWindowWidth() - 16, 120), ImGuiChildFlags.Border, ImGuiWindowFlags.NoScrollbar))
                {
                    ImGui.PushFont(ImGui.GetFont());
                    ImGui.Text("FOV Changer");
                    ImGui.PopFont();
                    ImGui.Separator();

                    bool temp = fovChangerEnabled;
                    if (ImGui.Checkbox("Enable FOV Changer", ref temp))
                    {
                        fovChangerEnabled = temp;
                    }

                    // FOV slider with better layout
                    ImGui.PushItemWidth(ImGui.GetWindowWidth() - 100);
                    int tempFOV = fovValue;
                    ImGui.Text("FOV Value: " + tempFOV.ToString());
                    if (ImGui.SliderInt("##FOVValue", ref tempFOV, 60, 140))
                    {
                        fovValue = tempFOV;
                    }
                    ImGui.PopItemWidth();

                    ImGui.Text("Changes in-game field of view");
                }
                ImGui.EndChild();

                ImGui.Spacing();

                // Skin Changer Section
                if (ImGui.BeginChild("SkinChanger", new Vector2(ImGui.GetWindowWidth() - 16, 200), ImGuiChildFlags.Border, ImGuiWindowFlags.NoScrollbar))
                {
                    ImGui.PushFont(ImGui.GetFont());
                    ImGui.Text("Skin Changer");
                    ImGui.PopFont();
                    ImGui.Separator();

                    bool temp = skinChangerEnabled;
                    if (ImGui.Checkbox("Enable Skin Changer", ref temp))
                    {
                        skinChangerEnabled = temp;
                    }

                    // Add skin selection options
                    ImGui.Text("Weapon:");
                    ImGui.SameLine(100);
                    ImGui.PushItemWidth(ImGui.GetWindowWidth() - 110);
                    int tempWeapon = selectedWeapon;
                    string[] weapons = new string[] { "AK-47", "M4A4", "AWP", "Desert Eagle", "USP-S" };
                    if (ImGui.Combo("##WeaponSelect", ref tempWeapon, weapons, weapons.Length))
                    {
                        selectedWeapon = tempWeapon;
                    }

                    ImGui.Text("Skin:");
                    ImGui.SameLine(100);
                    int tempSkin = selectedSkin;
                    string[] skins = new string[] { "Default", "Asiimov", "Dragon Lore", "Hyper Beast", "Neo-Noir", "Fade" };
                    if (ImGui.Combo("##SkinSelect", ref tempSkin, skins, skins.Length))
                    {
                        selectedSkin = tempSkin;
                    }

                    // Wear slider
                    ImGui.Text("Wear:");
                    ImGui.SameLine(100);
                    float tempWear = skinWear;
                    if (ImGui.SliderFloat("##SkinWear", ref tempWear, 0.0f, 1.0f, "%.2f"))
                    {
                        skinWear = tempWear;
                    }
                    ImGui.PopItemWidth();

                    // Checkbox for Apply to All
                    bool tempApplyAll = skinApplyAll;
                    if (ImGui.Checkbox("Apply to all weapons", ref tempApplyAll))
                    {
                        skinApplyAll = tempApplyAll;
                    }

                    // Apply Changes button
                    if (ImGui.Button("Apply Skin Changes", new Vector2(180, 25)))
                    {
                        // Functionality would be implemented in SkinChanger class
                    }
                }
                ImGui.EndChild();

            ImGui.Spacing();

                // Skin Changer Section
                if (ImGui.BeginChild("SkinChanger", new Vector2(ImGui.GetWindowWidth() - 16, 100), ImGuiChildFlags.Border, ImGuiWindowFlags.NoScrollbar))
                {
                    ImGui.PushFont(ImGui.GetFont());
                    ImGui.Text("Skin Changer (DO NOT ENABLE CURRENTLY BROKEN)");
                    ImGui.PopFont();
                    ImGui.Separator();

                    bool temp = skinChangerEnabled;
                    if (ImGui.Checkbox("Enable Skin Changer", ref temp))
                    {
                        skinChangerEnabled = temp;
                    }

                    if (ImGui.Button("Skin Changer Options", new Vector2(180, 25)))
                    {
                        // Functionality for skin changer options can be added here
                    }
                }
                ImGui.EndChild();
            }

            private void DrawConfigTab()
            {
                ImGui.Spacing();

                // Config Section - Now part of the Config tab in Settings page
                if (ImGui.BeginChild("ConfigSection", new Vector2(ImGui.GetWindowWidth() - 16, 500), ImGuiChildFlags.Border))
                {
                    ImGui.PushFont(ImGui.GetFont());
                    ImGui.Text("Configuration Settings");
                    ImGui.PopFont();
                    ImGui.Separator();

                    // Config name input
                    ImGui.Text("Config Name:");
                    ImGui.SameLine();

                    // Create a buffer with the current config name
                    byte[] configNameBuffer = new byte[128];
                    System.Text.Encoding.UTF8.GetBytes(configName).CopyTo(configNameBuffer, 0);

                    // Use InputText with byte array (works with ImGui.NET)
                    if (ImGui.InputText("##ConfigName", configNameBuffer, 128))
                    {
                        // Convert back to string, handling null termination
                        configName = System.Text.Encoding.UTF8.GetString(configNameBuffer)
                            .TrimEnd('\0');
                    }

                    ImGui.Spacing();

                    // Save Config Button
                    if (ImGui.Button("Save Config", new Vector2(ImGui.GetWindowWidth() / 2 - 10, 30)))
                    {
                        SaveConfig(configName);
                    }

                    ImGui.SameLine();

                    // Refresh Config List Button
                    if (ImGui.Button("Refresh List", new Vector2(ImGui.GetWindowWidth() / 2 - 10, 30)))
                    {
                        RefreshConfigList();
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    // Load Config Section
                    ImGui.Text("Load Configuration");
                    ImGui.Spacing();

                    // Config List
                    if (savedConfigs.Length == 0)
                    {
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "No saved configs found. Save a config first.");
                    }
                    else
                    {
                        // Create a list box with all saved configs
                        if (ImGui.BeginListBox("##ConfigList", new Vector2(ImGui.GetWindowWidth() - 30, 250)))
                        {
                            for (int i = 0; i < savedConfigs.Length; i++)
                            {
                                string configFileName = savedConfigs[i];
                                bool isSelected = (selectedConfig == i);
                                if (ImGui.Selectable(configFileName, isSelected))
                                {
                                    selectedConfig = i;
                                }

                                if (isSelected)
                                {
                                    ImGui.SetItemDefaultFocus();
                                }
                            }
                            ImGui.EndListBox();
                        }

                        ImGui.Spacing();

                        // Load and Delete buttons
                        if (selectedConfig >= 0 && selectedConfig < savedConfigs.Length)
                        {
                            if (ImGui.Button("Load Selected Config", new Vector2(ImGui.GetWindowWidth() / 2 - 10, 30)))
                            {
                                LoadConfig(savedConfigs[selectedConfig]);
                            }

                            ImGui.SameLine();

                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                            if (ImGui.Button("Delete Selected Config", new Vector2(ImGui.GetWindowWidth() / 2 - 10, 30)))
                            {
                                DeleteConfig(savedConfigs[selectedConfig]);
                            }
                            ImGui.PopStyleColor();
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Select a config to load or delete.");
                        }
                    }
                }
                ImGui.EndChild();
            }

            private string GetConfigFolderPath()
            {
                // Get the Documents folder path for the current user
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                // Create hExternal configs directory if it doesn't exist
                string configFolder = Path.Combine(documentsPath, "hExternal");
                if (!Directory.Exists(configFolder))
                {
                    Directory.CreateDirectory(configFolder);
                }

                return configFolder;
            }

            private void SaveConfig(string configName)
            {
                try
                {
                    // Sanitize config name - replace invalid characters with underscores
                    string safeConfigName = string.Join("_", configName.Split(Path.GetInvalidFileNameChars()));

                    // If empty, use default
                    if (string.IsNullOrWhiteSpace(safeConfigName))
                    {
                        safeConfigName = "default";
                    }

                    // Create config object with current settings
                    ConfigFile config = new ConfigFile
                    {
                        // General
                        AimbotEnabled = aimbotEnabled,
                        AimOnTeam = aimOnTeam,
                        FOV = FOV,
                        VisibilityCheck = visibilityCheck,
                        AimAtHead = aimAtHead,
                        SmoothAim = smoothAim,
                        SmoothFactor = smoothFactor,
                        AdjustScopedFOV = adjustScopedFOV,
                        ScopedFOV = scopedFOV,
                        ScopedSmoothFactor = scopedSmoothFactor,

                        // Other features
                        TriggerBotEnabled = triggerBotEnabled,
                        BunnyhopEnabled = bunnyhopEnabled,
                        FovChangerEnabled = fovChangerEnabled,
                        FovValue = fovValue,

                        // Skin changer
                        SkinChangerEnabled = skinChangerEnabled,
                        SelectedSkin = selectedSkin,
                        SelectedWeapon = selectedWeapon,
                        SkinWear = skinWear,
                        SkinStatTrak = skinStatTrak,
                        SkinApplyAll = skinApplyAll,
                        CustomName = customName
                    };

                    // Serialize the config object
                    string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    // Get config folder path and ensure it exists
                    string configFolder = GetConfigFolderPath();
                    string filePath = Path.Combine(configFolder, $"{safeConfigName}.json");

                    // Write the JSON to file
                    File.WriteAllText(filePath, json);

                    // Show success message
                    ShowStatusMessage($"Configuration saved to {filePath}", greenSuccessColor);

                    // Refresh the config list
                    RefreshConfigList();
                }
                catch (Exception ex)
                {
                    ShowStatusMessage($"Failed to save config: {ex.Message}", redErrorColor);
                }
            }

            private void LoadConfig(string configFileName)
            {
                try
                {
                    string configFolder = GetConfigFolderPath();
                    string filePath = Path.Combine(configFolder, configFileName);

                    if (!File.Exists(filePath))
                    {
                        ShowStatusMessage($"Config file not found: {filePath}", redErrorColor);
                        return;
                    }

                    // Read and deserialize the config file
                    string json = File.ReadAllText(filePath);
                    ConfigFile config = JsonSerializer.Deserialize<ConfigFile>(json);

                    // Apply the loaded settings
                    // General
                    aimbotEnabled = config.AimbotEnabled;
                    aimOnTeam = config.AimOnTeam;
                    FOV = config.FOV;
                    visibilityCheck = config.VisibilityCheck;
                    aimAtHead = config.AimAtHead;
                    smoothAim = config.SmoothAim;
                    smoothFactor = config.SmoothFactor;
                    adjustScopedFOV = config.AdjustScopedFOV;
                    scopedFOV = config.ScopedFOV;
                    scopedSmoothFactor = config.ScopedSmoothFactor;

                    // Other features
                    triggerBotEnabled = config.TriggerBotEnabled;
                    bunnyhopEnabled = config.BunnyhopEnabled;
                    fovChangerEnabled = config.FovChangerEnabled;
                    fovValue = config.FovValue;

                    // Skin changer
                    skinChangerEnabled = config.SkinChangerEnabled;
                    selectedSkin = config.SelectedSkin;
                    selectedWeapon = config.SelectedWeapon;
                    skinWear = config.SkinWear;
                    skinStatTrak = config.SkinStatTrak;
                    skinApplyAll = config.SkinApplyAll;
                    customName = config.CustomName;

                    // Update UI with the loaded config name
                    configName = Path.GetFileNameWithoutExtension(configFileName);

                    // Show success message
                    ShowStatusMessage($"Configuration loaded from {filePath}", greenSuccessColor);
                }
                catch (Exception ex)
                {
                    ShowStatusMessage($"Failed to load config: {ex.Message}", redErrorColor);
                }
            }

            private void DeleteConfig(string configFileName)
            {
                try
                {
                    string configFolder = GetConfigFolderPath();
                    string filePath = Path.Combine(configFolder, configFileName);

                    if (!File.Exists(filePath))
                    {
                        ShowStatusMessage($"Config file not found: {filePath}", redErrorColor);
                        return;
                    }

                    // Delete the file
                    File.Delete(filePath);

                    // Reset selected config
                    selectedConfig = -1;

                    // Show success message
                    ShowStatusMessage($"Configuration {configFileName} deleted", greenSuccessColor);

                    // Refresh the config list
                    RefreshConfigList();
                }
                catch (Exception ex)
                {
                    ShowStatusMessage($"Failed to delete config: {ex.Message}", redErrorColor);
                }
            }

            private void RefreshConfigList()
            {
                try
                {
                    string configFolder = GetConfigFolderPath();

                    // Get all JSON files in the config folder
                    string[] files = Directory.GetFiles(configFolder, "*.json");

                    // Extract just the file names
                    savedConfigs = files.Select(Path.GetFileName).ToArray();

                    // Reset selection if it's invalid
                    if (selectedConfig >= savedConfigs.Length)
                    {
                        selectedConfig = -1;
                    }
                }
                catch (Exception ex)
                {
                    ShowStatusMessage($"Failed to refresh config list: {ex.Message}", redErrorColor);
                    savedConfigs = new string[0];
                }
            }

            private void ShowStatusMessage(string message, Vector4 color)
            {
                statusMessage = message;
                statusMessageColor = color;
                statusMessageTimer = 5.0f; // Show message for 5 seconds
                showStatusMessage = true;
            }

            private void DrawOverlay()
            {
                {
                    // Draw FOV circle if aimbot is enabled
                    if (aimbotEnabled)
                    {
                        Vector2 center = new Vector2(screenSize.X / 2, screenSize.Y / 2);
                        ImGui.GetBackgroundDrawList().AddCircle(center, FOV, ImGui.ColorConvertFloat4ToU32(circleColor), 100, 1.0f);
                    }

                    // Draw welcome text in top right corner
                    float padding = 10.0f;
                    float textPositionX = screenSize.X - 350 - padding; // Adjust as needed
                    float textPositionY = padding;
                    float lineHeight = 20.0f;

                    // Welcome message
                    ImGui.GetBackgroundDrawList().AddText(
                        new Vector2(textPositionX, textPositionY),
                        ImGui.ColorConvertFloat4ToU32(whiteTextColor),
                        "hExternal Welcome, hounds");

                    // Current date and time
                    string currentDateTime = DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss tt");
                    ImGui.GetBackgroundDrawList().AddText(
                        new Vector2(textPositionX, textPositionY + lineHeight),
                        ImGui.ColorConvertFloat4ToU32(whiteTextColor),
                        currentDateTime);

                    // Build date
                    ImGui.GetBackgroundDrawList().AddText(
                        new Vector2(textPositionX, textPositionY + lineHeight * 2),
                        ImGui.ColorConvertFloat4ToU32(whiteTextColor),
                        "Build Date: 3/10/2025 7:32 PM");
                }
            }
        }
    }
}