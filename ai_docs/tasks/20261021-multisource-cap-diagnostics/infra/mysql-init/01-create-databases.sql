CREATE DATABASE IF NOT EXISTS bing_sqluow_orders CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE DATABASE IF NOT EXISTS bing_sqluow_reporting CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'cap_app'@'%' IDENTIFIED BY 'cap_local_only';
GRANT ALL PRIVILEGES ON bing_sqluow_orders.* TO 'cap_app'@'%';
GRANT ALL PRIVILEGES ON bing_sqluow_reporting.* TO 'cap_app'@'%';
