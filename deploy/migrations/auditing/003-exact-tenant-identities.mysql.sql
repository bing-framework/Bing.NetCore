-- 显式迁移审计租户身份；执行前备份、核对存量并停止全部审计写入。
-- MySQL DDL 隐式提交，两条语句不是原子事务，不承诺一键回滚。
-- 先按旧字符集转换到 UTF-8，再保留其字节，禁止直接把旧 latin1 字节当 UTF-8。
-- 不修改审计 ID 和用户 ID 的现有校验规则。
-- 全部调用方升级、两条语句执行完成后再恢复写入；不与旧应用混跑。

ALTER TABLE `bing_audit_log`
    ENGINE=InnoDB,
    MODIFY COLUMN `TenantId` VARCHAR(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL;

ALTER TABLE `bing_audit_log`
    MODIFY COLUMN `TenantId` VARBINARY(512) NOT NULL;
