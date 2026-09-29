using System;
using System.Collections.Generic;

namespace PokeProfessions.UI
{
    /// <summary>Global toast queue; HUD drains it each frame.</summary>
    public static class Toast
    {
        static readonly Queue<string> queue = new Queue<string>();
        public static event Action<string> Pushed;

        public static void Push(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            queue.Enqueue(msg);
            if (Pushed != null) Pushed(msg);
        }

        public static bool TryPop(out string msg)
        {
            if (queue.Count == 0) { msg = null; return false; }
            msg = queue.Dequeue();
            return true;
        }

        public static void Clear() { queue.Clear(); }
    }
}
