using Xunit;

// 本项目的总线断连/暂停用例共用专用 Redis，避免与其他 Hybrid 断言互相干扰。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
