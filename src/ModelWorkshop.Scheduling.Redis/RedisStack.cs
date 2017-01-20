using StackExchange.Redis;
using System;
using System.Diagnostics;

namespace ModelWorkshop.Scheduling.Redis
{
    public class RedisStack<TItem> : RedisCollectionBase<TItem>
    {
        #region Consturctors

        public RedisStack(string configuration, RedisKey key, int db)
            : base(configuration, key, db)
        {
        }

        public RedisStack(ConfigurationOptions configuration, RedisKey key, int db)
            : base(configuration, key, db)
        {
        }

        #endregion

        #region RedisCollectionBase Methods

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
