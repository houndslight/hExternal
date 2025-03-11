using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hExternal.Modules
{
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
            public const int m_bIsScoped = 0x1420;            // Player scoped state
            public const int m_bSpotted = 0x1438;             // Is entity spotted by local player
            public const int m_hActiveWeapon = 0x60;          // Handle to active weapon
            public const int m_iItemDefinitionIndex = 0x28A0; // Weapon type ID
    }
}
