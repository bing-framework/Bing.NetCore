-- 仅供显式部署旧 VARCHAR 库存表时使用，不在服务注册或扣减期间运行。
-- 执行前备份、核对存量，并停止所有库存写入、消费者及定时任务。
-- MySQL DDL 会隐式提交，四条语句不是一笔原子事务。
-- 先按旧字符集解码并转换为 UTF-8，再改成二进制，避免保留 latin1 等旧编码字节。
-- 两表全部完成并升级全部调用方后才恢复写入，不与旧实例混跑。

ALTER TABLE `bing_inventory_stock`
    ENGINE=InnoDB,
    CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_bin;

ALTER TABLE `bing_inventory_stock`
    MODIFY COLUMN `TenantId` VARBINARY(512) NOT NULL,
    MODIFY COLUMN `WarehouseId` VARBINARY(512) NOT NULL,
    MODIFY COLUMN `Sku` VARBINARY(1024) NOT NULL;

ALTER TABLE `bing_inventory_request`
    ENGINE=InnoDB,
    CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_bin;

ALTER TABLE `bing_inventory_request`
    MODIFY COLUMN `TenantId` VARBINARY(512) NOT NULL,
    MODIFY COLUMN `RequestId` VARBINARY(512) NOT NULL,
    MODIFY COLUMN `WarehouseId` VARBINARY(512) NOT NULL,
    MODIFY COLUMN `Sku` VARBINARY(1024) NOT NULL;
