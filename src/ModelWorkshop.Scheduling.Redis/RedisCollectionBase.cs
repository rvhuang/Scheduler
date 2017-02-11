using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModelWorkshop.Scheduling.Redis
{
    /// <summary>
    /// Defines the base wrapper class of <see cref="RedisQueue{TItem}"/> and <see cref="RedisStack{TItem}"/>.
    /// </summary>
    /// <typeparam name="TItem">Specifies the type of elements in the collection.</typeparam>
    public abstract class RedisCollectionBase<TItem> : IProducerConsumerCollection<TItem>, IReadOnlyCollection<TItem>
    {
        #region Fields

        private readonly JsonSerializer serializer;

        private readonly string configurationStr;
        private readonly ConfigurationOptions configurationOptions;
        private readonly Func<ConnectionMultiplexer> connectionFactory;

        private readonly RedisKey key;
        private readonly int dbIndex;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of elements contained in the Redis list.
        /// </summary>
        public int Count
        {
            get
            {
                using (var conn = this.connectionFactory())
                {
                    return (int)conn.GetDatabase(this.dbIndex).ListLength(this.key);
                }
            }
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

        #endregion

        #region Consturctors

        /// <summary>
        /// Initializes a new instance of the class with Redis configuration string, key and database index. 
        /// </summary>
        /// <param name="configuration">The Redis configuration string.</param>
        /// <param name="key">The key that is associated with the Redis list.</param>
        /// <param name="db">The index of database that the list is stored.</param>
        /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
        protected RedisCollectionBase(string configuration, RedisKey key, int db)
            : this(key, db)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");

            this.configurationStr = configuration;
            this.connectionFactory = this.GetConnectionWithString;
        }

        /// <summary>
        /// Initializes a new instance of the class with Redis configuration, key and database index. 
        /// </summary>
        /// <param name="configuration">The Redis configuration options.</param>
        /// <param name="key">The key that is associated with the Redis list.</param>
        /// <param name="db">The index of database that the list is stored.</param>
        /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
        protected RedisCollectionBase(ConfigurationOptions configuration, RedisKey key, int db)
            : this(key, db)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");

            this.configurationOptions = configuration;
            this.connectionFactory = this.GetConnectionWithOptions;
        }

        private RedisCollectionBase(RedisKey key, int db)
        {
            this.key = key;
            this.dbIndex = db;

            this.serializer = new JsonSerializer();
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Attempts to add an object to the Redis list.
        /// </summary>
        /// <param name="item">The object to add to the list.</param>
        /// <returns>true if the object was added successfully; otherwise, false.</returns>
        public abstract bool TryAdd(TItem item);

        /// <summary>
        /// Attempts to remove and return an object from the Redis list.
        /// </summary>
        /// <param name="item">When this method returns, if the operation was successful, item contains the object removed. If no object was available to be removed, the value is unspecified.</param>
        /// <returns>true if an element was removed and returned succesfully; otherwise, false.</returns>
        public abstract bool TryTake(out TItem item);

        /// <summary>
        /// Tries to return an object from the Redis list without removing it.
        /// </summary>
        /// <param name="item">When this method returns, result contains an object from the Redis list or an unspecified value if the operation failed.</param>
        /// <returns>true if an object was returned successfully; otherwise, false.</returns>
        public abstract bool TryPeek(out TItem item);

        #endregion

        #region Methods

        /// <summary>
        /// Removes all items from the Reids list.
        /// </summary>
        public void Clear()
        {
            using (var conn = this.connectionFactory())
            {
                conn.GetDatabase(this.dbIndex).KeyDelete(this.key);
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the Redis list.
        /// </summary>
        /// <returns>An enumerator for the Redis list.</returns>
        public IEnumerator<TItem> GetEnumerator()
        {
            var conn = this.connectionFactory();
            var enumerator = new RedisCollectionEnumerator<TItem>(conn.GetDatabase(this.dbIndex), this.key, this.FromRedisValue);

            enumerator.Disposed += (o, e) => conn.Dispose();

            return enumerator;
        }

        /// <summary>
        /// Copies the Redis list to a new array.
        /// </summary>
        /// <returns>A new array containing a snapshot of elements copied from the Redis list.</returns>
        public TItem[] ToArray()
        {
            using (var conn = this.connectionFactory())
            {
                return conn.GetDatabase(this.dbIndex).ListRange(this.key).Select(this.FromRedisValue).ToArray();
            }
        }

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

        #endregion

        #region Redis Connection Factory

        protected internal ConnectionMultiplexer GetConnection()
        {
            return this.connectionFactory();
        }

        private ConnectionMultiplexer GetConnectionWithString()
        {
            return ConnectionMultiplexer.Connect(this.configurationStr);
        }

        private ConnectionMultiplexer GetConnectionWithOptions()
        {
            return ConnectionMultiplexer.Connect(this.configurationOptions);
        }

        #endregion
    }
}
