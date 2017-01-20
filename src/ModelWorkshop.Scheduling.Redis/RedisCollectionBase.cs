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

        #region Consturctors

        protected RedisCollectionBase(string configuration, RedisKey key, int db)
            : this(key, db)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");

            this.configurationStr = configuration;
            this.connectionFactory = this.GetConnectionWithString;
        }

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

        public abstract bool TryAdd(TItem item);

        public abstract bool TryTake(out TItem item);

        public abstract bool TryPeek(out TItem item);

        #endregion

        #region Methods

        public void Clear()
        {
            using (var conn = this.connectionFactory())
            {
                conn.GetDatabase(this.dbIndex).KeyDelete(this.key);
            }
        }

        public IEnumerator<TItem> GetEnumerator()
        {
            var values = default(RedisValue[]);

            using (var conn = this.connectionFactory())
                values = conn.GetDatabase(this.dbIndex).ListRange(this.key);

            if (values == null || values.Length == 0)
                return Enumerable.Empty<TItem>().GetEnumerator();
            else
                return values.Select(this.FromRedisValue).GetEnumerator();
        }

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

        #region Redis Connection Factory

        protected ConnectionMultiplexer GetConnection()
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
