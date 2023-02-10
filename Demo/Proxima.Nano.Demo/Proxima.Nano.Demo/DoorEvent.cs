using System;
using System.Text;

namespace Proxima.Nano.Demo
{
    public class DoorEvent
    {
        #region Properties

        public Guid Id { get; set; }

        public long DataTicks { get; set; }

        public string Sender { get; set; }

        public string Name { get; set; }

        public DoorEventType EventType { get; set; }

        #endregion
    }
}
