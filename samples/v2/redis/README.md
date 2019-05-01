# Redis Storage Provider

## Current State: *Experimental*

## Current limitations
- No extended sessions support
- No durable timers support
- No history retrieval
- No suborchestration support
- No ability to terminate orchestrations
- Only one worker per TaskHub supported.
- No Azure Functions consumption plan scaling support

## How to install Redis locally

Redis provides an excellent [quick start guide](https://redis.io/topics/quickstart) that walks through how to install and run Redis on a Linux device. For Windows machines, it is highly recommended to install the [Windows Subsystem for Linux](https://docs.microsoft.com/en-us/windows/wsl/faq) and to follow the Redis quickstart guide on your Linux subsystem.

## Setup for example
If necessary, change the connection string value under `RedisConnectionString` in the `local.settings.json` file.