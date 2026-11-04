// FIFO limitada: preserva gestos validos, recusa excesso e expira comandos antigos antes do envio.
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace XGBP.Media
{
    public sealed class MediaCommandQueue : IDisposable
    {
        sealed class Work { public MediaCommand Command; public Action<Exception> Done; public DateTime Expires; }
        readonly object gate = new object();
        readonly Queue<Work> queue = new Queue<Work>();
        readonly Action<MediaCommand> send;
        readonly Action<Action> schedule, post;
        readonly int capacity;
        readonly TimeSpan lifetime;
        readonly Func<DateTime> now;
        bool running, stopped;
        int deliveryFailures;
        public MediaCommandQueue(Action<MediaCommand> send, Action<Action> schedule, Action<Action> post)
            : this(send, schedule, post, 32, TimeSpan.FromSeconds(5), CreateClock()) { }
        internal MediaCommandQueue(Action<MediaCommand> send, Action<Action> schedule, Action<Action> post,
            int capacity, TimeSpan lifetime, Func<DateTime> now)
        {
            if (capacity < 1 || lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException("capacity");
            if (send == null || schedule == null || post == null || now == null) throw new ArgumentNullException("callback");
            this.send = send; this.schedule = schedule; this.post = post;
            this.capacity = capacity; this.lifetime = lifetime; this.now = now;
        }
        public bool Pending { get { lock (gate) return running || queue.Count > 0; } }
        public int Count { get { lock (gate) return queue.Count; } }
        public int DeliveryFailures { get { lock (gate) return deliveryFailures; } }
        static Func<DateTime> CreateClock()
        {
            DateTime origin = DateTime.UtcNow; var elapsed = Stopwatch.StartNew();
            return () => origin + elapsed.Elapsed;
        }
        public bool Enqueue(MediaCommand command, Action<Exception> done)
        {
            if (command == null || done == null) throw new ArgumentNullException("command");
            bool start = false, full;
            lock (gate)
            {
                if (stopped) return false;
                full = queue.Count >= capacity;
                if (!full)
                {
                    queue.Enqueue(new Work { Command = new MediaCommand { Kind = command.Kind, SessionKey = command.SessionKey,
                        Shuffle = command.Shuffle, Repeat = command.Repeat, Position = command.Position }, Done = done, Expires = now() + lifetime });
                    if (!running) { running = true; start = true; }
                }
            }
            if (full) { Notify(done, new InvalidOperationException("Aguarde os comandos de midia anteriores.")); return false; }
            if (start) try { schedule(Drain); }
                catch (Exception ex)
                {
                    Work[] rejected;
                    lock (gate) { rejected = queue.ToArray(); queue.Clear(); running = false; }
                    foreach (var work in rejected) Notify(work.Done, ex);
                    return false;
                }
            return true;
        }
        void Notify(Action<Exception> done, Exception error)
        {
            try { post(delegate {
                lock (gate) { if (stopped) return; }
                try { done(error); } catch (Exception) { lock (gate) deliveryFailures++; }
            }); }
            catch (Exception) { lock (gate) deliveryFailures++; }
        }
        void Drain()
        {
            while (true)
            {
                Work work;
                lock (gate)
                {
                    if (stopped || queue.Count == 0) { running = false; return; }
                    work = queue.Dequeue();
                }
                Exception error = null;
                try
                {
                    if (now() >= work.Expires) throw new TimeoutException("Comando de midia expirou antes do envio.");
                    send(work.Command);
                }
                catch (Exception ex) { error = ex; }
                Notify(work.Done, error);
            }
        }
        public void Dispose() { lock (gate) { stopped = true; queue.Clear(); } }
    }
}
