using System;
using System.Collections.Generic;
using System.Text;

namespace Misaka.Classes
{
    class SimpleStopWatch
    {
        private DateTime StartTime;

        public SimpleStopWatch()
        {
            StartTime = DateTime.Now;
        }

        public TimeSpan Stop()
        {
            DateTime oldStartTime = StartTime;
            StartTime = DateTime.Now;
            return DateTime.Now.Subtract(oldStartTime);
        }
    }
}
