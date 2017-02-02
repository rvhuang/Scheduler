using System;

namespace ModelWorkshop.Example.Redis
{
    public class Request
    {
        public int ProcessId { get; set; }

        public long Timestamp { get; set; }
        
        public DateTime Date { get; set; }

        public Guid GUID { get; set; }
    }
}
