using StackExchange.Redis;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ModelWorkshop.Scheduling.Redis
{
    internal class RedisCollectionEnumerator<TItem> : IEnumerator<TItem>
    {
        #region Fields

        private readonly RedisKey key;
        private readonly IDatabase db;
        private readonly Func<RedisValue, TItem> converter;

        private long index;

        #endregion

        #region Event

        public event EventHandler Disposed;

        #endregion

        #region Constructor

        public RedisCollectionEnumerator(IDatabase db, RedisKey key, Func<RedisValue, TItem> converter)
        {
            this.db = db;
            this.key = key;
            this.converter = converter;

            this.index = -1;
        }

        #endregion

        #region Properties

        public TItem Current
        {
            get
            {
                if (this.index < 0)
                    return default(TItem);
                else
                    return this.converter(this.db.ListGetByIndex(this.key, this.index));
            }
        }

        object IEnumerator.Current
        {
            get { return this.Current; }
        }

        #endregion 

        #region Methods

        bool IEnumerator.MoveNext()
        {
            if (this.index + 1 < this.db.ListLength(this.key))
            {
                this.index++;
                return true;
            }
            return false;
        }

        void IEnumerator.Reset()
        {
            this.index = -1;
        }

        void IDisposable.Dispose()
        {
            if (this.Disposed != null) this.Disposed(this, EventArgs.Empty);
        }

        #endregion
    }
}
