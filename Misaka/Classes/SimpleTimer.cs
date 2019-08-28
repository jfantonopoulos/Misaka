using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Misaka.Classes
{
    class SimpleTimer
    {
        private Timer Timer;
        private Func<Task> Task;
        private int Interval;
        private bool Repeat;

        public SimpleTimer(Func<Task> task, int interval, bool repeat)
        {
            Task = task;
            Interval = interval;
            Repeat = repeat;
        }

        public void Start()
        {
            Timer = new Timer(async (e) =>
            {
                await Task();

                if (Repeat)
                    Timer.Change(Interval, 0);
                else
                    Timer.Dispose();
            }, null, Interval, 0);
        }

        public void Stop()
        {
            Timer.Dispose();
            Timer = null;
        }

        public void ChangeInterval(int interval)
        {
            Interval = interval;
            Timer.Change(interval, 0);
        }
    }
}
