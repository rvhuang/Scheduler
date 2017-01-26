## Overview

**Scheduler** is a thread-safe asynchronous producer-consumer model that allows items to be added from multiple threads but allows only single thread to consume the collection.

![Overview](https://raw.githubusercontent.com/rvhuang/Scheduler/master/doc/images/scheduler-overview.png)

In this model, producer threads from the left will **not** be blocked when adding the item to the collection. The main loop thread will be launched after first item is added to the collection, and will stop when all items are consumed by the action. Items can be added later while main loop thread is running. Main loop thread can be canceled at any time.

The thread safe collection between producer and consumer can also be a wrapper of [Redis list](https://redis.io/topics/data-types). This can avoid large local memory use when a large number of items are not consumed yet. 

![Overview Redis](https://raw.githubusercontent.com/rvhuang/Scheduler/master/doc/images/scheduler-overview-redis.png)

Storing items in Redis can also make multiple applications sharing same queue or stack possible. 

This is an experimental project targeting .Net Core and .Net Framework 4.5. 
