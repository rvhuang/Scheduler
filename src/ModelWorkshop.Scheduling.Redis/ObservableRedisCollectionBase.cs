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
using System.Runtime.Serialization;
using System.Threading;

namespace ModelWorkshop.Scheduling.Redis
{
    public abstract class ObservableRedisCollectionBase<TItem>
        : IProducerConsumerCollection<TItem>, IReadOnlyCollection<TItem>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        #region Fields

        private readonly JsonSerializer serializer;
        private readonly ConnectionMultiplexer conn;

        private readonly RedisKey key;
        private readonly RedisChannel ch;
        private readonly int dbIndex;

        private readonly Lazy<ISubscriber> subscriber;
        private readonly Lazy<IDatabase> database;

        private event NotifyCollectionChangedEventHandler collectionChanged;
        private event PropertyChangedEventHandler propertyChanged;

        #endregion

        #region Properties

        public int Count
        {
            get { return (int)this.database.Value.ListLength(this.key); }
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

        protected ISubscriber Subscriber
        {
            get { return this.subscriber.Value; }
        }

        protected IDatabase Database
        {
            get { return this.database.Value; }
        }

        #endregion

        #region Events

        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add
            {
                this.subscriber.Value.Ping(); // Initialize and make sure the connection works.
                this.collectionChanged += value;
            }
            remove
            {
                this.collectionChanged -= value;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                this.subscriber.Value.Ping(); // Initialize and make sure the connection works.
                this.propertyChanged += value;
            }
            remove
            {
                this.propertyChanged -= value;
            }
        }

        #endregion

        #region Constructors

        protected ObservableRedisCollectionBase(ConnectionMultiplexer conn, RedisKey key, int db)
        {
            if (conn == null) throw new ArgumentNullException("conn");

            this.conn = conn;
            this.key = key;
            this.ch = new RedisChannel(this.key.ToString(), RedisChannel.PatternMode.Auto);
            this.dbIndex = db;

            this.serializer = new JsonSerializer();

            this.subscriber = new Lazy<ISubscriber>(this.CreateSubscriber, LazyThreadSafetyMode.PublicationOnly);
            this.database = new Lazy<IDatabase>(this.CreateDatabase, LazyThreadSafetyMode.PublicationOnly);
        }

        #endregion

        #region Methods

        public bool TryAdd(TItem item)
        {
            if (this.OnAdd(item))
            {
                this.Subscriber.Publish(this.ch, this.SignalToRedisValue(item, NotifyCollectionChangedAction.Add));
                return true;
            }
            return false;
        }

        public bool TryTake(out TItem item)
        {
            if (this.OnTake(out item))
            {
                this.Subscriber.Publish(this.ch, this.SignalToRedisValue(item, NotifyCollectionChangedAction.Remove));
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
            this.database.Value.KeyDelete(this.key);
        }

        public IEnumerator<TItem> GetEnumerator()
        {
            return new RedisCollectionEnumerator<TItem>(this.database.Value, this.key, this.FromRedisValue);
        }

        public TItem[] ToArray()
        {
            return this.database.Value.ListRange(this.key).Select(this.FromRedisValue).ToArray();
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

        protected RedisCollectionChangedSignal RedisValueToSignal(RedisValue value)
        {
            using (var ms = new MemoryStream(value, false))
            using (var sr = new StreamReader(ms))
            using (var jr = new JsonTextReader(sr))
                return this.serializer.Deserialize<RedisCollectionChangedSignal>(jr);
        }

        protected RedisValue SignalToRedisValue(TItem item, NotifyCollectionChangedAction action)
        {
            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms))
                using (var jw = new JsonTextWriter(sw))
                {
                    this.serializer.Serialize(jw, new RedisCollectionChangedSignal(item, action));
                }
                return ms.ToArray();
            }
        }

        #endregion

        #region Redis Pop and Sub Related

        private IDatabase CreateDatabase()
        {
            return this.conn.GetDatabase(this.dbIndex);
        }

        private ISubscriber CreateSubscriber()
        {
            var result = this.conn.GetSubscriber();

            result.Subscribe(this.key.ToString(), this.RedisSubscriberHandler, CommandFlags.HighPriority);

            return result;
        }

        private void RedisSubscriberHandler(RedisChannel channel, RedisValue value)
        {
            this.OnCollectionChanged(this.RedisValueToSignal(value).ToEventArgs());
            this.OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        }

        #endregion

        #region Event Raisers

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            try
            {
                this.collectionChanged(this, e);
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

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            try
            {
                this.propertyChanged(this, e);
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

        #region Class

        [DataContract]
        public class RedisCollectionChangedSignal
        {
            [DataMember]
            public TItem Item
            {
                get; set;
            }

            [DataMember]
            public NotifyCollectionChangedAction Action
            {
                get; set;
            }

            public RedisCollectionChangedSignal() { }

            public RedisCollectionChangedSignal(TItem item, NotifyCollectionChangedAction action)
            {
                this.Item = item;
                this.Action = action;
            }

            public NotifyCollectionChangedEventArgs ToEventArgs()
            {
                return new NotifyCollectionChangedEventArgs(this.Action, this.Item);
            }
        }

        #endregion
    }
}
