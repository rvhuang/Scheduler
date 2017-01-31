using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace ModelWorkshop.Scheduling.Redis
{
    public abstract class ObservableRedisCollectionBase<TItem>
        : IProducerConsumerCollection<TItem>, IReadOnlyCollection<TItem>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        #region Fields

        private readonly JsonSerializer serializer;
        private readonly ConnectionMultiplexer conn;
        
        private readonly RedisKey key;
        private readonly int dbIndex;

        #endregion

        #region Properties

        public int Count
        {
            get
            {
                return (int)conn.GetDatabase(this.dbIndex).ListLength(this.key);
            }
        }

        public RedisKey Key
        {
            get { return this.key; }
        }

        public int DatabaseIndex
        {
            get { return this.dbIndex; }
        }

        bool ICollection.IsSynchronized
        {
            get { return true; }
        }

        object ICollection.SyncRoot
        {
            get { throw new NotImplementedException(); }
        }

        #endregion

        #region Events

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Constructors

        protected ObservableRedisCollectionBase(ConnectionMultiplexer conn, RedisKey key, int db)
        {
            if (conn == null) throw new ArgumentNullException("conn");
            
            this.conn = conn;
            this.key = key;
            this.dbIndex = db;
        }

        #endregion
        
        #region Methods

        public bool TryAdd(TItem item)
        {
            if (this.OnAdd(item))
            {
                return true;
            }
            return false;
        }

        public bool TryTake(out TItem item)
        {
            if (this.OnTake(out item))
            {
                return true;
            }
            return false;
        }

        public bool TryPeek(out TItem item)
        {
            return this.OnPeek(out item);
        }

        public void Clear()
        {
            this.conn.GetDatabase(this.dbIndex).KeyDelete(this.key);
        }

        public IEnumerator<TItem> GetEnumerator()
        {
            var enumerator = new RedisCollectionEnumerator<TItem>(this.conn.GetDatabase(this.dbIndex), this.key, this.FromRedisValue);

            enumerator.Disposed += (o, e) => conn.Dispose();

            return enumerator;
        }

        public TItem[] ToArray()
        {
            return this.conn.GetDatabase(this.dbIndex).ListRange(this.key).Select(this.FromRedisValue).ToArray();
        }

        #endregion

        #region Abstract Methods

        protected abstract bool OnAdd(TItem item);

        protected abstract bool OnTake(out TItem item);

        protected abstract bool OnPeek(out TItem item);

        #endregion
        
        #region Explicit Interface Implementations

        void IProducerConsumerCollection<TItem>.CopyTo(TItem[] array, int index)
        {
            this.ToArray().CopyTo(array, index);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            this.ToArray().CopyTo(array, index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        #endregion

        #region Serialization Methods

        protected TItem FromRedisValue(RedisValue value)
        {
            using (var ms = new MemoryStream(value, false))
            using (var sr = new StreamReader(ms))
            using (var jr = new JsonTextReader(sr))
                return this.serializer.Deserialize<TItem>(jr);
        }

        protected RedisValue ToRedisValue(TItem result)
        {
            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms))
                using (var jw = new JsonTextWriter(sw))
                {
                    this.serializer.Serialize(jw, result);
                }
                return ms.ToArray();
            }
        }

        #endregion

        #region Event Raisers

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            try
            {
                this.PropertyChanged(this, e);
            }
            catch (NullReferenceException)
            {
                // Do nothing
            }
            catch (Exception error)
            {
                throw error;
            }
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            try
            {
                this.CollectionChanged(this, e);
            }
            catch (NullReferenceException)
            {
                // Do nothing
            }
            catch (Exception error)
            {
                throw error;
            }
        }

        #endregion
    }
}
