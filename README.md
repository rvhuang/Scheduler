## Overview

**Scheduler** is an asynchronous producer-consumer model that allows items to be added from multiple threads but allows only single thread to consume the collection.

![Overview](https://raw.githubusercontent.com/rvhuang/Scheduler/master/doc/images/scheduler-overview.png)

In this model, a producer thread from the left will **NOT** be blocked when adding the item to the collection, which means the thread will continue while the item is waiting to be consumed in the collection. The main loop thread will be launched after first item is added to the collection, and will stop when all items are consumed by the action. Items can be added later while main loop thread is running. Main loop thread can be canceled at any time.

The thread safe collection between producer and consumer can also be a wrapper of [Redis list](https://redis.io/topics/data-types). This can avoid large local memory use when a number of items are not consumed yet. Storing items in Redis can also make multiple applications sharing same collection possible.

![Overview Redis](https://raw.githubusercontent.com/rvhuang/Scheduler/master/doc/images/scheduler-overview-redis.png)

## Platforms

This project targets .Net Core and .Net Framework 4.5. 
