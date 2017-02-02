using StackExchange.Redis;
using System;
using System.Diagnostics;

namespace ModelWorkshop.Scheduling.Redis
{
    public class ObservableRedisQueue<TItem> : ObservableRedisCollectionBase<TItem>
    {
        #region Constructor

        public ObservableRedisQueue(ConnectionMultiplexer conn, RedisKey key, int db)
            : base(conn, key, db)
        {
        }

        #endregion

        #region ObservableRedisCollectionBase Methods

        protected override bool OnAdd(TItem item)
        {
            try
            {
                base.Database.ListRightPush(base.Key, base.ToRedisValue(item));
            }
            catch (Exception error)
            {
                Debug.WriteLine(error);
                return false;
            }
            return true;
        }

        protected override bool OnTake(out TItem item)
        {
            var value = RedisValue.Null;

            try
            {
                value = base.Database.ListLeftPop(base.Key);
            }
            catch (Exception error)
            {
                Debug.WriteLine(error);
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

        protected override bool OnPeek(out TItem item)
        {
            var value = RedisValue.Null;

            try
            {
                value = base.Database.ListGetByIndex(base.Key, -1);
            }
            catch (Exception error)
            {
                Debug.WriteLine(error);
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