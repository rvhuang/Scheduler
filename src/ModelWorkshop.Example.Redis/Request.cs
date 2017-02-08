using System;

namespace ModelWorkshop.Example.Redis
{
    public class Request
    {
        public int ProcessId { get; set; }

        public long Timestamp { get; set; }
        
        public int SequenceNumber { get; set; }
    }
}
