## Scheduler

### Overview

**Scheduler** is an asynchronous producer-consumer model that allows producers concurrently add items to the collection and consumes each item from the collection in single thread.

![Overview](https://raw.githubusercontent.com/rvhuang/Scheduler/master/doc/images/scheduler-overview.png)

The flow can be summarized with following steps:

1. A producer adds an item to the collection by calling [AddAndRun](https://github.com/rvhuang/Scheduler/blob/master/src/ModelWorkshop.Scheduling/Scheduler.cs#L182) or [TryAddAndRun](https://github.com/rvhuang/Scheduler/blob/master/src/ModelWorkshop.Scheduling/Scheduler.cs#L232) method. 
2. Scheduler checks whether the item is the first one in the collection. If so, the main loop thread will be launched. 
3. Action consumes the item from the collection.
4. Scheduler checks whether the collection is empty. If so, main loop thread ends. If not, repeats step 3. 

The Scheduler performs following behaviors to handle main loop thread:

* If the collection implements [INotifyCollectionChanged](https://docs.microsoft.com/en-us/dotnet/core/api/system.collections.specialized.inotifycollectionchanged) interface, when *CollectionChanged* event is fired to notify that new items has been added, the main loop thread will also be launched if it is in idle. 
* Main loop thread can be canceled at any time with [Stop](https://github.com/rvhuang/Scheduler/blob/master/src/ModelWorkshop.Scheduling/Scheduler.cs#L277) method, and resumed with [Run](https://github.com/rvhuang/Scheduler/blob/master/src/ModelWorkshop.Scheduling/Scheduler.cs#L138) method.

### Redis Integration

This project defines a set of Redis wrapper that can be used to replace in-memory collection. In this scenario, items are stored in Redis list instead of local memory and wrapped by [RedisQueue](https://github.com/rvhuang/Scheduler/blob/master/src/ModelWorkshop.Scheduling.Redis/RedisQueue.cs) or [RedisStack](https://github.com/rvhuang/Scheduler/blob/master/src/ModelWorkshop.Scheduling.Redis/RedisStack.cs). This can avoid large local memory use when a number of items are not consumed yet.

![Overview Redis](https://raw.githubusercontent.com/rvhuang/Scheduler/master/doc/images/scheduler-overview-redis.png)

By applying [ObservableRedisQueue](https://github.com/rvhuang/Scheduler/blob/master/src/ModelWorkshop.Scheduling.Redis/ObservableRedisQueue.cs) or [ObservableRedisStack](https://github.com/rvhuang/Scheduler/blob/master/src/ModelWorkshop.Scheduling.Redis/ObservableRedisStack.cs), which implements [INotifyCollectionChanged](https://docs.microsoft.com/en-us/dotnet/core/api/system.collections.specialized.inotifycollectionchanged) interface, it is also possible that multiple application instances share same collection. Once new items have been added via an application instance, all instances sharing same Redis collection will receive the notification and start the main loop thread respectively. 

![Overview Observable Redis](https://raw.githubusercontent.com/rvhuang/Scheduler/master/doc/images/scheduler-overview-observable.png)

The Redis wrappers are defined in [ModelWorkshop.Scheduling.Redis](https://github.com/rvhuang/Scheduler/tree/master/src/ModelWorkshop.Scheduling.Redis) namespace.

### Platforms

This project targets .Net Core and .Net Framework 4.5. 
