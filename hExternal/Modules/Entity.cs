using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace hExternal.Modules
{
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

        public bool isScoped;      // Is player currently scoped in

        public bool hasSniper;     // Is player holding a sniper rifle

        public bool isVisible;     // Is player visible (not behind walls)
    }
}
