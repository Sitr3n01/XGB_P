// Uma fila FIFO para todo trabalho de layout da barra e do teclado. Nunca espera a UI.
using System;
using System.Collections.Generic;
using System.Threading;

namespace XGBP.Design
{
    sealed class SteamInputWorkQueue
    {
        const int Capacity = 16;
        readonly object sync = new object();
        readonly Queue<Action> work = new Queue<Action>();
        readonly Action<Action> start;
        bool running;
        public SteamInputWorkQueue() : this(delegate(Action run) {
            new Thread(delegate() { run(); }) { IsBackground = true, Name = "XGBP Steam Input FIFO" }.Start();
        }) { }
        internal SteamInputWorkQueue(Action<Action> start) { this.start = start; }
        public bool Enqueue(Action body)
        {
            bool launch;
            lock (sync)
            {
                if (work.Count >= Capacity) return false;
                work.Enqueue(body); launch = !running; running = true;
            }
            if (launch) start(Drain);
            return true;
        }
        void Drain()
        {
            for (;;)
            {
                Action next;
                lock (sync)
                {
                    if (work.Count == 0) { running = false; return; }
                    next = work.Dequeue();
                }
                // Os donos tratam erros/diarios. Um erro de um trabalho nao abandona a limpeza seguinte.
                try { next(); } catch (Exception) { }
            }
        }
    }
    sealed partial class Preview
    {
        readonly SteamInputWorkQueue steamInputWork = new SteamInputWorkQueue();
    }
}
