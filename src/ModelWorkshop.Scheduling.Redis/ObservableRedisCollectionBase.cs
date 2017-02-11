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
    /// <summary>
    /// Defines the base class for <see cref="ObservableRedisQueue{TItem}"/> and <see cref="ObservableRedisStack{TItem}"/>.
    /// </summary>
    /// <typeparam name="TItem">Specifies the type of elements in the collection.</typeparam>
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

        /// <summary>
        /// Gets the number of elements contained in the Redis list.
        /// </summary>
        public int Count
        {
            get { return (int)this.database.Value.ListLength(this.key); }
        }

        /// <summary>
        /// Gets the key that is associated with the Redis list.
        /// </summary>
        public RedisKey Key
        {
            get { return this.key; }
        }

        /// <summary>
        /// Gets the index of database that the list is stored. 
        /// </summary>
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
        
        /// <summary>
        /// Gets the Redis channel subscriber that is used to notify and receive changed event.
        /// </summary>
        protected ISubscriber Subscriber
        {
            get { return this.subscriber.Value; }
        }

        /// <summary>
        /// Gets the Redis database interface. 
        /// </summary>
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

        /// <summary>
        /// Initializes a new instance of the class with Redis connection, key and database index.
        /// </summary>
        /// <param name="conn">The Redis connection.</param> 
        /// <param name="key">The key that is associated with the Redis list.</param>
        /// <param name="db">The index of database that the list is stored.</param>
        /// <exception cref="ArgumentNullException"><paramref name="conn"/> is null.</exception>
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
        
        /// <summary>
        /// Attempts to add an object to the Redis list.
        /// </summary>
        /// <param name="item">The object to add to the list.</param>
        /// <returns>true if the object was added successfully; otherwise, false.</returns>
        public bool TryAdd(TItem item)
        {
            if (this.OnAdd(item))
            {
                this.Subscriber.Publish(this.ch, this.SignalToRedisValue(item, NotifyCollectionChangedAction.Add));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to remove and return an object from the Redis list.
        /// </summary>
        /// <param name="item">When this method returns, if the operation was successful, item contains the object removed. If no object was available to be removed, the value is unspecified.</param>
        /// <returns>true if an element was removed and returned succesfully; otherwise, false.</returns>
        public bool TryTake(out TItem item)
        {
            if (this.OnTake(out item))
            {
                this.Subscriber.Publish(this.ch, this.SignalToRedisValue(item, NotifyCollectionChangedAction.Remove));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tries to return an object from the Redis list without removing it.
        /// </summary>
        /// <param name="item">When this method returns, result contains an object from the Redis list or an unspecified value if the operation failed.</param>
        /// <returns>true if an object was returned successfully; otherwise, false.</returns>
        public bool TryPeek(out TItem item)
        {
            return this.OnPeek(out item);
        }
        
        /// <summary>
        /// Removes all items from the Reids list.
        /// </summary>
        public void Clear()
        {
            this.database.Value.KeyDelete(this.key);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the Redis list.
        /// </summary>
        /// <returns>An enumerator for the Redis list.</returns>
        public IEnumerator<TItem> GetEnumerator()
        {
            return new RedisCollectionEnumerator<TItem>(this.database.Value, this.key, this.FromRedisValue);
        }

        /// <summary>
        /// Copies the Redis list to a new array.
        /// </summary>
        /// <returns>A new array containing a snapshot of elements copied from the Redis list.</returns>
        public TItem[] ToArray()
        {
            return this.database.Value.ListRange(this.key).Select(this.FromRedisValue).ToArray();
        }

        #endregion

        #region Abstract Methods
        
        /// <summary>
        /// Implemented in derived classes. Attempts to add an object to the Redis list.
        /// </summary>
        /// <param name="item">The object to add to the list.</param>
        /// <returns>true if the object was added successfully; otherwise, false.</returns>
        protected abstract bool OnAdd(TItem item);

        /// <summary>
        /// Implemented in derived classes. Attempts to remove and return an object from the Redis list.
        /// </summary>
        /// <param name="item">When this method returns, if the operation was successful, item contains the object removed. If no object was available to be removed, the value is unspecified.</param>
        /// <returns>true if an element was removed and returned succesfully; otherwise, false.</returns>
        protected abstract bool OnTake(out TItem item);

        /// <summary>
        /// Implemented in derived classes. Tries to return an object from the Redis list without removing it.
        /// </summary>
        /// <param name="item">When this method returns, result contains an object from the Redis list or an unspecified value if the operation failed.</param>
        /// <returns>true if an object was returned successfully; otherwise, false.</returns>
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

        /// <summary>
        /// Deserializes a Redis value to <typeparamref name="TItem"/> instance.
        /// </summary>
        /// <param name="value">The Redis value to be deserialized.</param>
        /// <returns>The item converted from the Redis value.</returns>
        protected virtual TItem FromRedisValue(RedisValue value)
        {
            if (value.IsNull)
                return default(TItem);

            using (var ms = new MemoryStream(value, false))
            using (var sr = new StreamReader(ms))
            using (var jr = new JsonTextReader(sr))
                return this.serializer.Deserialize<TItem>(jr);
        }

        /// <summary>
        /// Serializes a <typeparamref name="TItem"/> instance to Redis value.
        /// </summary>
        /// <param name="item">The item to be serialized.</param>
        /// <returns>A Redis value converted from the item.</returns>
        protected virtual RedisValue ToRedisValue(TItem item)
        {
            if (item == null)
                return RedisValue.Null;

            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms))
                using (var jw = new JsonTextWriter(sw))
                {
                    this.serializer.Serialize(jw, item);
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Deserializes a Redis value to <see cref="NotifyCollectionChangedSignal"/> instance.
        /// </summary>
        /// <param name="value">The Redis value to be deserialized.</param>
        /// <returns>The instance converted from the Redis value.</returns>
        protected NotifyCollectionChangedSignal RedisValueToSignal(RedisValue value)
        {

            using (var ms = new MemoryStream(value, false))
            using (var sr = new StreamReader(ms))
            using (var jr = new JsonTextReader(sr))
                return this.serializer.Deserialize<NotifyCollectionChangedSignal>(jr);
        }

        /// <summary>
        /// Creates a Redis value of collection change signal from item and collection change action.
        /// </summary>
        /// <param name="item">The item that is affected.</param>
        /// <param name="action">The collection changed action.</param>
        /// <returns>A Redis value of collection change signal.</returns>
        protected RedisValue SignalToRedisValue(TItem item, NotifyCollectionChangedAction action)
        {
            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms))
                using (var jw = new JsonTextWriter(sw))
                {
                    this.serializer.Serialize(jw, new NotifyCollectionChangedSignal(item, action));
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

        /// <summary>
        /// Raises the <see cref="CollectionChanged"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
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

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
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

        /// <summary>
        /// Defines the collection changed notification sent via a Redis channel.
        /// </summary>
        [DataContract]
        public class NotifyCollectionChangedSignal
        {
            /// <summary>
            /// Gets ore sets the item that is affected by the change.
            /// </summary>
            [DataMember]
            public TItem Item
            {
                get; set;
            }

            /// <summary>
            /// Gets or sets action that caused the event.
            /// </summary>
            [DataMember]
            public NotifyCollectionChangedAction Action
            {
                get; set;
            }

            /// <summary>
            /// Initializes a new instance of the instance.
            /// </summary>
            public NotifyCollectionChangedSignal() { }

            /// <summary>
            /// Initializes a new instance of the instance.
            /// </summary>
            /// <param name="item">The item that is affected by the change.</param>
            /// <param name="action">The action that caused the event.</param>
            public NotifyCollectionChangedSignal(TItem item, NotifyCollectionChangedAction action)
            {
                this.Item = item;
                this.Action = action;
            }

            internal NotifyCollectionChangedEventArgs ToEventArgs()
            {
                return new NotifyCollectionChangedEventArgs(this.Action, this.Item);
            }
        }

        #endregion
    }
}
