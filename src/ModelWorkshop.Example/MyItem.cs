using System;

namespace ModelWorkshop.Example
{
    public class MyItem
    {
        public int SourceThreadID
        {
            get;
            private set;
        }

        public int ID
        {
            get;
            private set;
        }

        public MyItem(int srcThreadId)
        {
            this.SourceThreadID = srcThreadId;
            this.ID = Environment.TickCount;
        }
    }
}
