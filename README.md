## Overview

**Scheduler** is a thread-safe asynchronous producer-consumer model that allows items to be added from multiple threads but allows only single thread to consume the collection.

![Overview](https://raw.githubusercontent.com/rvhuang/Scheduler/master/doc/images/scheduler-overview.png)

The main loop thread will be launched once first item is added to the collection, and will stop when all items are consumed by the action. Items can be added later while main loop thread is running. Main loop thread can be canceled at any time.

This is an experimental project targeting .Net Core and .Net Framework 4.5. 
