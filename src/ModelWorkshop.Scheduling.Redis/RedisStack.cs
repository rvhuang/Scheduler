using StackExchange.Redis;
using System;
using System.Diagnostics;

namespace ModelWorkshop.Scheduling.Redis
{
    /// <summary>
    /// Represents a first-in, last-out collection of objects stored in Redis.
    /// </summary>
    /// <typeparam name="TItem">Specifies the type of elements in the queue.</typeparam>
    public class RedisStack<TItem> : RedisCollectionBase<TItem>
    {
        #region Consturctors

        /// <summary>
        /// Initializes a new instance of the class with Redis configuration string, key and database index. 
        /// </summary>
        /// <param name="configuration">The Redis configuration string.</param>
        /// <param name="key">The key that is associated with the Redis list.</param>
        /// <param name="db">The index of database that the list is stored.</param>
        /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
        public RedisStack(string configuration, RedisKey key, int db)
            : base(configuration, key, db)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class with Redis configuration, key and database index. 
        /// </summary>
        /// <param name="configuration">The Redis configuration options.</param>
        /// <param name="key">The key that is associated with the Redis list.</param>
        /// <param name="db">The index of database that the list is stored.</param>
        /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
        public RedisStack(ConfigurationOptions configuration, RedisKey key, int db)
            : base(configuration, key, db)
        {
        }

        #endregion

        #region RedisCollectionBase Methods

        /// <summary>
        /// Attempts to add an object to the Redis list.
        /// </summary>
        /// <param name="item">The object to add to the list.</param>
        /// <returns>true if the object was added successfully; otherwise, false.</returns>
        public override bool TryAdd(TItem item)
        {
            using (var conn = base.GetConnection())
            {
                try
                {
                    conn.GetDatabase(base.DatabaseIndex).ListRightPush(base.Key, base.ToRedisValue(item));
                }
                catch (Exception error)
                {
                    Debug.WriteLine(error);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Attempts to remove and return the last object from the Redis list.
        /// </summary>
        /// <param name="item">When this method returns, if the operation was successful, item contains the object removed. If no object was available to be removed, the value is unspecified.</param>
        /// <returns>true if an element was removed and returned succesfully; otherwise, false.</returns>
        public override bool TryTake(out TItem item)
        {
            var value = RedisValue.Null;

            using (var conn = base.GetConnection())
            {
                try
                {
                    value = conn.GetDatabase(base.DatabaseIndex).ListRightPop(base.Key);
                }
                catch (Exception error)
                {
                    Debug.WriteLine(error);
                }
            }
            if (value.IsNull)
            {
                item = default(TItem);
                return false;
            }
            else
            {
                item = base.FromRedisValue(value);
                return true;
            }
        }

        /// <summary>
        /// Tries to return the last object from the Redis list without removing it.
        /// </summary>
        /// <param name="item">When this method returns, result contains an object from the Redis list or an unspecified value if the operation failed.</param>
        /// <returns>true if an object was returned successfully; otherwise, false.</returns>
        public override bool TryPeek(out TItem item)
        {
            var value = RedisValue.Null;

            using (var conn = base.GetConnection())
            {
                try
                {
                    value = conn.GetDatabase(base.DatabaseIndex).ListGetByIndex(base.Key, 0);
                }
                catch (Exception error)
                {
                    Debug.WriteLine(error);
                }
            }
            if (value.IsNull)
            {
                item = default(TItem);
                return false;
            }
            else
            {
                item = base.FromRedisValue(value);
                return true;
            }
        }

        #endregion
    }
}
