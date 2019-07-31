/*========================================================== 1. 创建数据库 ===========================================================*/
USE [master]
GO

--删除数据库
EXEC msdb.dbo.sp_delete_database_backuphistory @database_name = N'Bing.PermissionSystem'
GO
IF  EXISTS (SELECT name FROM sys.databases WHERE name = N'Bing.PermissionSystem')
Begin
DROP DATABASE [Bing.PermissionSystem]
End
GO

--创建数据库
CREATE DATABASE [Bing.PermissionSystem]
GO

/*========================================================== 2. 创建架构 ===========================================================*/
USE [Bing.PermissionSystem]
GO

IF  EXISTS (SELECT * FROM sys.schemas WHERE name = N'Systems')
DROP SCHEMA [Systems]
GO
CREATE SCHEMA [Systems] AUTHORIZATION [dbo]
GO

/*========================================================== 3. 创建表及索引 ===========================================================*/

if exists (select 1
            from  sysindexes
           where  id    = object_id('Systems.Claim')
            and   name  = 'clus_idx_creationtime'
            and   indid > 0
            and   indid < 255)
   drop index Systems.Claim.clus_idx_creationtime
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.Claim')
            and   type = 'U')
   drop table Systems.Claim
go

/*==============================================================*/
/* Table: Claim                                                 */
/*==============================================================*/
create table Systems.Claim (
   ClaimId              uniqueidentifier     not null,
   Name                 nvarchar(200)        not null,
   Remark               nvarchar(500)        null,
   CreationTime         datetime             null,
   CreatorId            uniqueidentifier     null,
   LastModificationTime datetime             null,
   LastModifierId       uniqueidentifier     null,
   IsDeleted            bit                  not null,
   Version              timestamp            null,
   constraint PK_CLAIM primary key nonclustered (ClaimId)
)
go

if exists (select 1 from  sys.extended_properties
           where major_id = object_id('Systems.Claim') and minor_id = 0)
begin 
   execute sp_dropextendedproperty 'MS_Description',  
   'schema', 'Systems', 'table', 'Claim' 
 
end 


execute sp_addextendedproperty 'MS_Description',  
   '声明', 
   'schema', 'Systems', 'table', 'Claim'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Claim')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'ClaimId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Claim', 'column', 'ClaimId'

end


execute sp_addextendedproperty 'MS_Description', 
   '声明标识',
   'schema', 'Systems', 'table', 'Claim', 'column', 'ClaimId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Claim')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Name')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Claim', 'column', 'Name'

end


execute sp_addextendedproperty 'MS_Description', 
   '声明名称',
   'schema', 'Systems', 'table', 'Claim', 'column', 'Name'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Claim')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Remark')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Claim', 'column', 'Remark'

end


execute sp_addextendedproperty 'MS_Description', 
   '备注',
   'schema', 'Systems', 'table', 'Claim', 'column', 'Remark'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Claim')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Claim', 'column', 'CreationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建时间',
   'schema', 'Systems', 'table', 'Claim', 'column', 'CreationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Claim')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreatorId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Claim', 'column', 'CreatorId'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建人标识',
   'schema', 'Systems', 'table', 'Claim', 'column', 'CreatorId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Claim')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModificationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Claim', 'column', 'LastModificationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改时间',
   'schema', 'Systems', 'table', 'Claim', 'column', 'LastModificationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Claim')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModifierId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Claim', 'column', 'LastModifierId'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改人标识',
   'schema', 'Systems', 'table', 'Claim', 'column', 'LastModifierId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Claim')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'IsDeleted')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Claim', 'column', 'IsDeleted'

end


execute sp_addextendedproperty 'MS_Description', 
   '是否删除',
   'schema', 'Systems', 'table', 'Claim', 'column', 'IsDeleted'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Claim')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Version')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Claim', 'column', 'Version'

end


execute sp_addextendedproperty 'MS_Description', 
   '版本号',
   'schema', 'Systems', 'table', 'Claim', 'column', 'Version'
go

/*==============================================================*/
/* Index: clus_idx_creationtime                                 */
/*==============================================================*/




create clustered index clus_idx_creationtime on Systems.Claim (CreationTime DESC)
go


if exists (select 1
            from  sysindexes
           where  id    = object_id('Systems.Application')
            and   name  = 'clus_idx_creationtime'
            and   indid > 0
            and   indid < 255)
   drop index Systems.Application.clus_idx_creationtime
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.Application')
            and   type = 'U')
   drop table Systems.Application
go

/*==============================================================*/
/* Table: Application                                           */
/*==============================================================*/
create table Systems.Application (
   ApplicationId        uniqueidentifier     not null,
   Code                 nvarchar(60)         not null,
   Name                 nvarchar(200)        not null,
   Enabled              bit                  not null,
   RegisterEnabled      bit                  not null,
   Remark               nvarchar(500)        null,
   Extend               nvarchar(max)        null,
   CreationTime         datetime             null,
   CreatorId            uniqueidentifier     null,
   LastModificationTime datetime             null,
   LastModifierId       uniqueidentifier     null,
   IsDeleted            bit                  not null,
   Version              timestamp            null,
   constraint PK_APPLICATION primary key nonclustered (ApplicationId)
)
go

if exists (select 1 from  sys.extended_properties
           where major_id = object_id('Systems.Application') and minor_id = 0)
begin 
   execute sp_dropextendedproperty 'MS_Description',  
   'schema', 'Systems', 'table', 'Application' 
 
end 


execute sp_addextendedproperty 'MS_Description',  
   '应用程序', 
   'schema', 'Systems', 'table', 'Application'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'ApplicationId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'ApplicationId'

end


execute sp_addextendedproperty 'MS_Description', 
   '应用程序标识',
   'schema', 'Systems', 'table', 'Application', 'column', 'ApplicationId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Code')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'Code'

end


execute sp_addextendedproperty 'MS_Description', 
   '应用程序编码',
   'schema', 'Systems', 'table', 'Application', 'column', 'Code'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Name')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'Name'

end


execute sp_addextendedproperty 'MS_Description', 
   '应用程序名称',
   'schema', 'Systems', 'table', 'Application', 'column', 'Name'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Enabled')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'Enabled'

end


execute sp_addextendedproperty 'MS_Description', 
   '启用',
   'schema', 'Systems', 'table', 'Application', 'column', 'Enabled'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'RegisterEnabled')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'RegisterEnabled'

end


execute sp_addextendedproperty 'MS_Description', 
   '启用注册',
   'schema', 'Systems', 'table', 'Application', 'column', 'RegisterEnabled'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Remark')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'Remark'

end


execute sp_addextendedproperty 'MS_Description', 
   '备注',
   'schema', 'Systems', 'table', 'Application', 'column', 'Remark'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Extend')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'Extend'

end


execute sp_addextendedproperty 'MS_Description', 
   '扩展',
   'schema', 'Systems', 'table', 'Application', 'column', 'Extend'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'CreationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建时间',
   'schema', 'Systems', 'table', 'Application', 'column', 'CreationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreatorId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'CreatorId'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建人编号',
   'schema', 'Systems', 'table', 'Application', 'column', 'CreatorId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModificationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'LastModificationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改时间',
   'schema', 'Systems', 'table', 'Application', 'column', 'LastModificationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModifierId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'LastModifierId'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改人编号',
   'schema', 'Systems', 'table', 'Application', 'column', 'LastModifierId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'IsDeleted')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'IsDeleted'

end


execute sp_addextendedproperty 'MS_Description', 
   '是否删除',
   'schema', 'Systems', 'table', 'Application', 'column', 'IsDeleted'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Version')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'Version'

end


execute sp_addextendedproperty 'MS_Description', 
   '版本号',
   'schema', 'Systems', 'table', 'Application', 'column', 'Version'
go

/*==============================================================*/
/* Index: clus_idx_creationtime                                 */
/*==============================================================*/




create clustered index clus_idx_creationtime on Systems.Application (CreationTime ASC)
go


if exists (select 1
            from  sysindexes
           where  id    = object_id('Systems."User"')
            and   name  = 'clus_idx_creationtime'
            and   indid > 0
            and   indid < 255)
   drop index Systems."User".clus_idx_creationtime
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems."User"')
            and   type = 'U')
   drop table Systems."User"
go

/*==============================================================*/
/* Table: "User"                                                */
/*==============================================================*/
create table Systems."User" (
   UserId               uniqueidentifier     not null,
   UserName             nvarchar(256)        null,
   NormalizedUserName   nvarchar(256)        null,
   Email                nvarchar(256)        null,
   NormalizedEmail      nvarchar(256)        null,
   EmailConfirmed       bit                  not null,
   PhoneNumber          nvarchar(64)         null,
   PhoneNumberConfirmed bit                  not null,
   Password             nvarchar(256)        null,
   PasswordHash         nvarchar(1024)       null,
   SafePassword         nvarchar(256)        null,
   SafePasswordHash     nvarchar(1024)       null,
   TwoFactorEnabled     bit                  not null,
   Enabled              bit                  not null,
   DisabledTime         datetime             null,
   LockoutEnabled       bit                  not null,
   LockoutEnd           datetimeoffset(7)    null,
   AccessFailedCount    int                  null,
   LoginCount           int                  null,
   RegisterIp           nvarchar(30)         null,
   LastLoginTime        datetime             null,
   LastLoginIp          nvarchar(30)         null,
   CurrentLoginTime     datetime             null,
   CurrentLoginIp       nvarchar(30)         null,
   SecurityStamp        nvarchar(1024)       null,
   Remark               nvarchar(500)        null,
   CreationTime         datetime             null,
   CreatorId            uniqueidentifier     null,
   LastModificationTime datetime             null,
   LastModifierId       uniqueidentifier     null,
   IsDeleted            bit                  not null,
   Version              timestamp            null,
   constraint PK_USER primary key nonclustered (UserId)
)
go

if exists (select 1 from  sys.extended_properties
           where major_id = object_id('Systems."User"') and minor_id = 0)
begin 
   execute sp_dropextendedproperty 'MS_Description',  
   'schema', 'Systems', 'table', 'User' 
 
end 


execute sp_addextendedproperty 'MS_Description',  
   '用户', 
   'schema', 'Systems', 'table', 'User'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'UserId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'UserId'

end


execute sp_addextendedproperty 'MS_Description', 
   '用户标识',
   'schema', 'Systems', 'table', 'User', 'column', 'UserId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'UserName')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'UserName'

end


execute sp_addextendedproperty 'MS_Description', 
   '用户名',
   'schema', 'Systems', 'table', 'User', 'column', 'UserName'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'NormalizedUserName')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'NormalizedUserName'

end


execute sp_addextendedproperty 'MS_Description', 
   '标准化用户名',
   'schema', 'Systems', 'table', 'User', 'column', 'NormalizedUserName'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Email')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'Email'

end


execute sp_addextendedproperty 'MS_Description', 
   '安全邮箱',
   'schema', 'Systems', 'table', 'User', 'column', 'Email'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'NormalizedEmail')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'NormalizedEmail'

end


execute sp_addextendedproperty 'MS_Description', 
   '标准化邮箱',
   'schema', 'Systems', 'table', 'User', 'column', 'NormalizedEmail'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'EmailConfirmed')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'EmailConfirmed'

end


execute sp_addextendedproperty 'MS_Description', 
   '邮箱已确认',
   'schema', 'Systems', 'table', 'User', 'column', 'EmailConfirmed'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'PhoneNumber')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'PhoneNumber'

end


execute sp_addextendedproperty 'MS_Description', 
   '安全手机',
   'schema', 'Systems', 'table', 'User', 'column', 'PhoneNumber'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'PhoneNumberConfirmed')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'PhoneNumberConfirmed'

end


execute sp_addextendedproperty 'MS_Description', 
   '手机已确认',
   'schema', 'Systems', 'table', 'User', 'column', 'PhoneNumberConfirmed'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Password')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'Password'

end


execute sp_addextendedproperty 'MS_Description', 
   '密码',
   'schema', 'Systems', 'table', 'User', 'column', 'Password'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'PasswordHash')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'PasswordHash'

end


execute sp_addextendedproperty 'MS_Description', 
   '密码散列',
   'schema', 'Systems', 'table', 'User', 'column', 'PasswordHash'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'SafePassword')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'SafePassword'

end


execute sp_addextendedproperty 'MS_Description', 
   '安全码',
   'schema', 'Systems', 'table', 'User', 'column', 'SafePassword'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'SafePasswordHash')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'SafePasswordHash'

end


execute sp_addextendedproperty 'MS_Description', 
   '安全码散列',
   'schema', 'Systems', 'table', 'User', 'column', 'SafePasswordHash'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'TwoFactorEnabled')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'TwoFactorEnabled'

end


execute sp_addextendedproperty 'MS_Description', 
   '启用两阶段认证',
   'schema', 'Systems', 'table', 'User', 'column', 'TwoFactorEnabled'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Enabled')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'Enabled'

end


execute sp_addextendedproperty 'MS_Description', 
   '启用',
   'schema', 'Systems', 'table', 'User', 'column', 'Enabled'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'DisabledTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'DisabledTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '冻结时间',
   'schema', 'Systems', 'table', 'User', 'column', 'DisabledTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LockoutEnabled')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'LockoutEnabled'

end


execute sp_addextendedproperty 'MS_Description', 
   '启用锁定',
   'schema', 'Systems', 'table', 'User', 'column', 'LockoutEnabled'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LockoutEnd')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'LockoutEnd'

end


execute sp_addextendedproperty 'MS_Description', 
   '锁定截止',
   'schema', 'Systems', 'table', 'User', 'column', 'LockoutEnd'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'AccessFailedCount')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'AccessFailedCount'

end


execute sp_addextendedproperty 'MS_Description', 
   '登录失败次数',
   'schema', 'Systems', 'table', 'User', 'column', 'AccessFailedCount'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LoginCount')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'LoginCount'

end


execute sp_addextendedproperty 'MS_Description', 
   '登录次数',
   'schema', 'Systems', 'table', 'User', 'column', 'LoginCount'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'RegisterIp')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'RegisterIp'

end


execute sp_addextendedproperty 'MS_Description', 
   '注册Ip',
   'schema', 'Systems', 'table', 'User', 'column', 'RegisterIp'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastLoginTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'LastLoginTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '上次登陆时间',
   'schema', 'Systems', 'table', 'User', 'column', 'LastLoginTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastLoginIp')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'LastLoginIp'

end


execute sp_addextendedproperty 'MS_Description', 
   '上次登陆Ip',
   'schema', 'Systems', 'table', 'User', 'column', 'LastLoginIp'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CurrentLoginTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'CurrentLoginTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '本次登陆时间',
   'schema', 'Systems', 'table', 'User', 'column', 'CurrentLoginTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CurrentLoginIp')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'CurrentLoginIp'

end


execute sp_addextendedproperty 'MS_Description', 
   '本次登陆Ip',
   'schema', 'Systems', 'table', 'User', 'column', 'CurrentLoginIp'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'SecurityStamp')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'SecurityStamp'

end


execute sp_addextendedproperty 'MS_Description', 
   '安全戳',
   'schema', 'Systems', 'table', 'User', 'column', 'SecurityStamp'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Remark')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'Remark'

end


execute sp_addextendedproperty 'MS_Description', 
   '备注',
   'schema', 'Systems', 'table', 'User', 'column', 'Remark'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'CreationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建时间',
   'schema', 'Systems', 'table', 'User', 'column', 'CreationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreatorId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'CreatorId'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建人',
   'schema', 'Systems', 'table', 'User', 'column', 'CreatorId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModificationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'LastModificationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改时间',
   'schema', 'Systems', 'table', 'User', 'column', 'LastModificationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModifierId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'LastModifierId'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改人',
   'schema', 'Systems', 'table', 'User', 'column', 'LastModifierId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'IsDeleted')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'IsDeleted'

end


execute sp_addextendedproperty 'MS_Description', 
   '是否删除',
   'schema', 'Systems', 'table', 'User', 'column', 'IsDeleted'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Version')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'Version'

end


execute sp_addextendedproperty 'MS_Description', 
   '版本号',
   'schema', 'Systems', 'table', 'User', 'column', 'Version'
go

/*==============================================================*/
/* Index: clus_idx_creationtime                                 */
/*==============================================================*/

create clustered index clus_idx_creationtime on Systems."User" (CreationTime DESC)
go


if exists (select 1
            from  sysindexes
           where  id    = object_id('Systems.Role')
            and   name  = 'cls_idx_creationtime'
            and   indid > 0
            and   indid < 255)
   drop index Systems.Role.cls_idx_creationtime
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.Role')
            and   type = 'U')
   drop table Systems.Role
go

/*==============================================================*/
/* Table: Role                                                  */
/*==============================================================*/
create table Systems.Role (
   RoleId               uniqueidentifier     not null,
   Code                 nvarchar(256)        not null,
   Name                 nvarchar(256)        not null,
   NormalizedName       nvarchar(256)        not null,
   Type                 nvarchar(80)         not null,
   IsAdmin              bit                  not null,
   ParentId             uniqueidentifier     null,
   Path                 nvarchar(800)        not null,
   Level                int                  not null,
   SortId               int                  null,
   Enabled              bit                  not null,
   Remark               nvarchar(500)        null,
   PinYin               nvarchar(200)        null,
   Sign                 nvarchar(256)        null,
   CreationTime         datetime             null,
   CreatorId            uniqueidentifier     null,
   LastModificationTime datetime             null,
   LastModifierId       uniqueidentifier     null,
   IsDeleted            bit                  not null,
   Version              timestamp            null,
   constraint PK_ROLE primary key nonclustered (RoleId)
)
go

if exists (select 1 from  sys.extended_properties
           where major_id = object_id('Systems.Role') and minor_id = 0)
begin 
   execute sp_dropextendedproperty 'MS_Description',  
   'schema', 'Systems', 'table', 'Role' 
 
end 


execute sp_addextendedproperty 'MS_Description',  
   '角色', 
   'schema', 'Systems', 'table', 'Role'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'RoleId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'RoleId'

end


execute sp_addextendedproperty 'MS_Description', 
   '角色标识',
   'schema', 'Systems', 'table', 'Role', 'column', 'RoleId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Code')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'Code'

end


execute sp_addextendedproperty 'MS_Description', 
   '角色编码',
   'schema', 'Systems', 'table', 'Role', 'column', 'Code'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Name')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'Name'

end


execute sp_addextendedproperty 'MS_Description', 
   '角色名称',
   'schema', 'Systems', 'table', 'Role', 'column', 'Name'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'NormalizedName')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'NormalizedName'

end


execute sp_addextendedproperty 'MS_Description', 
   '标准化角色名称',
   'schema', 'Systems', 'table', 'Role', 'column', 'NormalizedName'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Type')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'Type'

end


execute sp_addextendedproperty 'MS_Description', 
   '角色类型',
   'schema', 'Systems', 'table', 'Role', 'column', 'Type'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'IsAdmin')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'IsAdmin'

end


execute sp_addextendedproperty 'MS_Description', 
   '管理员',
   'schema', 'Systems', 'table', 'Role', 'column', 'IsAdmin'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'ParentId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'ParentId'

end


execute sp_addextendedproperty 'MS_Description', 
   '父标识',
   'schema', 'Systems', 'table', 'Role', 'column', 'ParentId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Path')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'Path'

end


execute sp_addextendedproperty 'MS_Description', 
   '路径',
   'schema', 'Systems', 'table', 'Role', 'column', 'Path'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Level')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'Level'

end


execute sp_addextendedproperty 'MS_Description', 
   '层级',
   'schema', 'Systems', 'table', 'Role', 'column', 'Level'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'SortId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'SortId'

end


execute sp_addextendedproperty 'MS_Description', 
   '排序号',
   'schema', 'Systems', 'table', 'Role', 'column', 'SortId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Enabled')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'Enabled'

end


execute sp_addextendedproperty 'MS_Description', 
   '启用',
   'schema', 'Systems', 'table', 'Role', 'column', 'Enabled'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Remark')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'Remark'

end


execute sp_addextendedproperty 'MS_Description', 
   '备注',
   'schema', 'Systems', 'table', 'Role', 'column', 'Remark'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'PinYin')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'PinYin'

end


execute sp_addextendedproperty 'MS_Description', 
   '拼音简码',
   'schema', 'Systems', 'table', 'Role', 'column', 'PinYin'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Sign')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'Sign'

end


execute sp_addextendedproperty 'MS_Description', 
   '签名',
   'schema', 'Systems', 'table', 'Role', 'column', 'Sign'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'CreationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建时间',
   'schema', 'Systems', 'table', 'Role', 'column', 'CreationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreatorId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'CreatorId'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建人编号',
   'schema', 'Systems', 'table', 'Role', 'column', 'CreatorId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModificationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'LastModificationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改时间',
   'schema', 'Systems', 'table', 'Role', 'column', 'LastModificationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModifierId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'LastModifierId'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改人编号',
   'schema', 'Systems', 'table', 'Role', 'column', 'LastModifierId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'IsDeleted')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'IsDeleted'

end


execute sp_addextendedproperty 'MS_Description', 
   '是否删除',
   'schema', 'Systems', 'table', 'Role', 'column', 'IsDeleted'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Role')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Version')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'Version'

end


execute sp_addextendedproperty 'MS_Description', 
   '版本号',
   'schema', 'Systems', 'table', 'Role', 'column', 'Version'
go

/*==============================================================*/
/* Index: cls_idx_creationtime                                  */
/*==============================================================*/




create clustered index cls_idx_creationtime on Systems.Role (CreationTime DESC)
go


if exists (select 1
   from sys.sysreferences r join sys.sysobjects o on (o.id = r.constid and o.type = 'F')
   where r.fkeyid = object_id('Systems.Resource') and o.name = 'FK_Resource_Application')
alter table Systems.Resource
   drop constraint FK_Resource_Application
go

if exists (select 1
            from  sysindexes
           where  id    = object_id('Systems.Resource')
            and   name  = 'cls_idx_sortid'
            and   indid > 0
            and   indid < 255)
   drop index Systems.Resource.cls_idx_sortid
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.Resource')
            and   type = 'U')
   drop table Systems.Resource
go

/*==============================================================*/
/* Table: Resource                                              */
/*==============================================================*/
create table Systems.Resource (
   ResourceId           uniqueidentifier     not null,
   ApplicationId        uniqueidentifier     null,
   Uri                  nvarchar(300)        null,
   Name                 nvarchar(200)        not null,
   Type                 int                  not null,
   ParentId             uniqueidentifier     null,
   Path                 nvarchar(800)        null,
   Level                int                  null,
   Remark               nvarchar(500)        null,
   PinYin               nvarchar(200)        null,
   Enabled              bit                  not null,
   SortId               int                  null,
   Extend               nvarchar(max)        null,
   CreationTime         datetime             null,
   CreatorId            uniqueidentifier     null,
   LastModificationTime datetime             null,
   LastModifierId       uniqueidentifier     null,
   IsDeleted            bit                  not null,
   Version              timestamp            null,
   constraint PK_RESOURCE primary key nonclustered (ResourceId)
)
go

if exists (select 1 from  sys.extended_properties
           where major_id = object_id('Systems.Resource') and minor_id = 0)
begin 
   execute sp_dropextendedproperty 'MS_Description',  
   'schema', 'Systems', 'table', 'Resource' 
 
end 


execute sp_addextendedproperty 'MS_Description',  
   '资源', 
   'schema', 'Systems', 'table', 'Resource'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'ResourceId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'ResourceId'

end


execute sp_addextendedproperty 'MS_Description', 
   '资源标识',
   'schema', 'Systems', 'table', 'Resource', 'column', 'ResourceId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'ApplicationId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'ApplicationId'

end


execute sp_addextendedproperty 'MS_Description', 
   '应用程序标识',
   'schema', 'Systems', 'table', 'Resource', 'column', 'ApplicationId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Uri')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'Uri'

end


execute sp_addextendedproperty 'MS_Description', 
   '资源标识',
   'schema', 'Systems', 'table', 'Resource', 'column', 'Uri'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Name')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'Name'

end


execute sp_addextendedproperty 'MS_Description', 
   '资源名称',
   'schema', 'Systems', 'table', 'Resource', 'column', 'Name'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Type')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'Type'

end


execute sp_addextendedproperty 'MS_Description', 
   '资源类型',
   'schema', 'Systems', 'table', 'Resource', 'column', 'Type'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'ParentId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'ParentId'

end


execute sp_addextendedproperty 'MS_Description', 
   '父标识',
   'schema', 'Systems', 'table', 'Resource', 'column', 'ParentId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Path')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'Path'

end


execute sp_addextendedproperty 'MS_Description', 
   '路径',
   'schema', 'Systems', 'table', 'Resource', 'column', 'Path'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Level')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'Level'

end


execute sp_addextendedproperty 'MS_Description', 
   '层级',
   'schema', 'Systems', 'table', 'Resource', 'column', 'Level'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Remark')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'Remark'

end


execute sp_addextendedproperty 'MS_Description', 
   '备注',
   'schema', 'Systems', 'table', 'Resource', 'column', 'Remark'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'PinYin')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'PinYin'

end


execute sp_addextendedproperty 'MS_Description', 
   '拼音简码',
   'schema', 'Systems', 'table', 'Resource', 'column', 'PinYin'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Enabled')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'Enabled'

end


execute sp_addextendedproperty 'MS_Description', 
   '启用',
   'schema', 'Systems', 'table', 'Resource', 'column', 'Enabled'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'SortId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'SortId'

end


execute sp_addextendedproperty 'MS_Description', 
   '排序号',
   'schema', 'Systems', 'table', 'Resource', 'column', 'SortId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Extend')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'Extend'

end


execute sp_addextendedproperty 'MS_Description', 
   '扩展',
   'schema', 'Systems', 'table', 'Resource', 'column', 'Extend'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'CreationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建时间',
   'schema', 'Systems', 'table', 'Resource', 'column', 'CreationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreatorId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'CreatorId'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建人编号',
   'schema', 'Systems', 'table', 'Resource', 'column', 'CreatorId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModificationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'LastModificationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改时间',
   'schema', 'Systems', 'table', 'Resource', 'column', 'LastModificationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModifierId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'LastModifierId'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改人编号',
   'schema', 'Systems', 'table', 'Resource', 'column', 'LastModifierId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'IsDeleted')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'IsDeleted'

end


execute sp_addextendedproperty 'MS_Description', 
   '是否删除',
   'schema', 'Systems', 'table', 'Resource', 'column', 'IsDeleted'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Resource')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Version')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'Version'

end


execute sp_addextendedproperty 'MS_Description', 
   '版本号',
   'schema', 'Systems', 'table', 'Resource', 'column', 'Version'
go

/*==============================================================*/
/* Index: cls_idx_sortid                                        */
/*==============================================================*/




create clustered index cls_idx_sortid on Systems.Resource (SortId ASC)
go

alter table Systems.Resource
   add constraint FK_Resource_Application foreign key (ApplicationId)
      references Systems.Application (ApplicationId)
go


if exists (select 1
   from sys.sysreferences r join sys.sysobjects o on (o.id = r.constid and o.type = 'F')
   where r.fkeyid = object_id('Systems.UserRole') and o.name = 'FK_UserRole_Role')
alter table Systems.UserRole
   drop constraint FK_UserRole_Role
go

if exists (select 1
   from sys.sysreferences r join sys.sysobjects o on (o.id = r.constid and o.type = 'F')
   where r.fkeyid = object_id('Systems.UserRole') and o.name = 'FK_UserRole_User')
alter table Systems.UserRole
   drop constraint FK_UserRole_User
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.UserRole')
            and   type = 'U')
   drop table Systems.UserRole
go

/*==============================================================*/
/* Table: UserRole                                              */
/*==============================================================*/
create table Systems.UserRole (
   RoleId               uniqueidentifier     not null,
   UserId               uniqueidentifier     not null,
   constraint PK_USERROLE primary key (RoleId, UserId)
)
go

if exists (select 1 from  sys.extended_properties
           where major_id = object_id('Systems.UserRole') and minor_id = 0)
begin 
   execute sp_dropextendedproperty 'MS_Description',  
   'schema', 'Systems', 'table', 'UserRole' 
 
end 


execute sp_addextendedproperty 'MS_Description',  
   '用户角色', 
   'schema', 'Systems', 'table', 'UserRole'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.UserRole')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'RoleId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'UserRole', 'column', 'RoleId'

end


execute sp_addextendedproperty 'MS_Description', 
   '角色标识',
   'schema', 'Systems', 'table', 'UserRole', 'column', 'RoleId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.UserRole')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'UserId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'UserRole', 'column', 'UserId'

end


execute sp_addextendedproperty 'MS_Description', 
   '用户标识',
   'schema', 'Systems', 'table', 'UserRole', 'column', 'UserId'
go

alter table Systems.UserRole
   add constraint FK_UserRole_Role foreign key (RoleId)
      references Systems.Role (RoleId)
go

alter table Systems.UserRole
   add constraint FK_UserRole_User foreign key (UserId)
      references Systems."User" (UserId)
go


if exists (select 1
   from sys.sysreferences r join sys.sysobjects o on (o.id = r.constid and o.type = 'F')
   where r.fkeyid = object_id('Systems.Permission') and o.name = 'FK_Permission_Resource')
alter table Systems.Permission
   drop constraint FK_Permission_Resource
go

if exists (select 1
   from sys.sysreferences r join sys.sysobjects o on (o.id = r.constid and o.type = 'F')
   where r.fkeyid = object_id('Systems.Permission') and o.name = 'FK_Permission_Role')
alter table Systems.Permission
   drop constraint FK_Permission_Role
go

if exists (select 1
            from  sysindexes
           where  id    = object_id('Systems.Permission')
            and   name  = 'cls_idx_creationtime'
            and   indid > 0
            and   indid < 255)
   drop index Systems.Permission.cls_idx_creationtime
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.Permission')
            and   type = 'U')
   drop table Systems.Permission
go

/*==============================================================*/
/* Table: Permission                                            */
/*==============================================================*/
create table Systems.Permission (
   PermissionId         uniqueidentifier     not null,
   RoleId               uniqueidentifier     not null,
   ResourceId           uniqueidentifier     not null,
   IsDeny               bit                  not null,
   Sign                 nvarchar(256)        null,
   CreationTime         datetime             null,
   CreatorId            uniqueidentifier     null,
   LastModificationTime datetime             null,
   LastModifierId       uniqueidentifier     null,
   IsDeleted            bit                  not null,
   Version              timestamp            null,
   constraint PK_PERMISSION primary key nonclustered (PermissionId)
)
go

if exists (select 1 from  sys.extended_properties
           where major_id = object_id('Systems.Permission') and minor_id = 0)
begin 
   execute sp_dropextendedproperty 'MS_Description',  
   'schema', 'Systems', 'table', 'Permission' 
 
end 


execute sp_addextendedproperty 'MS_Description',  
   '权限', 
   'schema', 'Systems', 'table', 'Permission'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Permission')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'PermissionId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Permission', 'column', 'PermissionId'

end


execute sp_addextendedproperty 'MS_Description', 
   '权限标识',
   'schema', 'Systems', 'table', 'Permission', 'column', 'PermissionId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Permission')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'RoleId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Permission', 'column', 'RoleId'

end


execute sp_addextendedproperty 'MS_Description', 
   '角色标识',
   'schema', 'Systems', 'table', 'Permission', 'column', 'RoleId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Permission')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'ResourceId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Permission', 'column', 'ResourceId'

end


execute sp_addextendedproperty 'MS_Description', 
   '资源标识',
   'schema', 'Systems', 'table', 'Permission', 'column', 'ResourceId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Permission')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'IsDeny')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Permission', 'column', 'IsDeny'

end


execute sp_addextendedproperty 'MS_Description', 
   '拒绝',
   'schema', 'Systems', 'table', 'Permission', 'column', 'IsDeny'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Permission')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Sign')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Permission', 'column', 'Sign'

end


execute sp_addextendedproperty 'MS_Description', 
   '签名',
   'schema', 'Systems', 'table', 'Permission', 'column', 'Sign'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Permission')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Permission', 'column', 'CreationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建时间',
   'schema', 'Systems', 'table', 'Permission', 'column', 'CreationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Permission')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreatorId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Permission', 'column', 'CreatorId'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建人',
   'schema', 'Systems', 'table', 'Permission', 'column', 'CreatorId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Permission')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModificationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Permission', 'column', 'LastModificationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改时间',
   'schema', 'Systems', 'table', 'Permission', 'column', 'LastModificationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Permission')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModifierId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Permission', 'column', 'LastModifierId'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改人',
   'schema', 'Systems', 'table', 'Permission', 'column', 'LastModifierId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Permission')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'IsDeleted')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Permission', 'column', 'IsDeleted'

end


execute sp_addextendedproperty 'MS_Description', 
   '是否删除',
   'schema', 'Systems', 'table', 'Permission', 'column', 'IsDeleted'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Permission')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Version')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Permission', 'column', 'Version'

end


execute sp_addextendedproperty 'MS_Description', 
   '版本号',
   'schema', 'Systems', 'table', 'Permission', 'column', 'Version'
go

/*==============================================================*/
/* Index: cls_idx_creationtime                                  */
/*==============================================================*/




create clustered index cls_idx_creationtime on Systems.Permission (CreationTime DESC)
go

alter table Systems.Permission
   add constraint FK_Permission_Resource foreign key (ResourceId)
      references Systems.Resource (ResourceId)
go

alter table Systems.Permission
   add constraint FK_Permission_Role foreign key (RoleId)
      references Systems.Role (RoleId)
go


/*==============================================================*/
/* 默认数据                                                      */
/*==============================================================*/

/*==============================================================*/
/* 应用程序                                                      */
/*==============================================================*/
INSERT [Systems].[Application] ([ApplicationId], [Code], [Name], [Enabled], [RegisterEnabled], [Remark], [Extend], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'e9138a35-a4ff-460e-ac55-b743d55b9691', N'GreatWall-Admin', N'权限管理后台', 1, 1, NULL, NULL, CAST(N'2019-07-07 16:06:09.470' AS DateTime), NULL, CAST(N'2019-07-07 16:06:09.470' AS DateTime), NULL, 0)
GO
/*==============================================================*/
/* 模块                                                         */
/*==============================================================*/
INSERT [Systems].[Resource] ([ResourceId], [ApplicationId], [Uri], [Name], [Type], [ParentId], [Path], [Level], [Remark], [PinYin], [Enabled], [SortId], [Extend], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'1b75baf9-cd60-46f6-9c37-7490d8a2fd4f', N'e9138a35-a4ff-460e-ac55-b743d55b9691', NULL, N'主菜单', 1, NULL, N'1b75baf9-cd60-46f6-9c37-7490d8a2fd4f,', 1, NULL, N'zcc', 1, 1, N'{"Icon":null,"Expanded":null}', CAST(N'2019-07-07T16:31:31.963' AS DateTime), NULL, CAST(N'2019-07-07T16:31:31.963' AS DateTime), NULL, 0)
GO
INSERT [Systems].[Resource] ([ResourceId], [ApplicationId], [Uri], [Name], [Type], [ParentId], [Path], [Level], [Remark], [PinYin], [Enabled], [SortId], [Extend], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'd24aee38-3780-4d32-aff1-38236b348b86', N'e9138a35-a4ff-460e-ac55-b743d55b9691', N'/dashboard/v1', N'仪表盘', 1, N'1b75baf9-cd60-46f6-9c37-7490d8a2fd4f', N'1b75baf9-cd60-46f6-9c37-7490d8a2fd4f,d24aee38-3780-4d32-aff1-38236b348b86,', 2, NULL, N'ybp', 1, 1, N'{"Icon":"anticon anticon-dashboard","Expanded":null}', CAST(N'2019-07-07T16:34:40.193' AS DateTime), NULL, CAST(N'2019-07-07T16:34:40.193' AS DateTime), NULL, 0)
GO
INSERT [Systems].[Resource] ([ResourceId], [ApplicationId], [Uri], [Name], [Type], [ParentId], [Path], [Level], [Remark], [PinYin], [Enabled], [SortId], [Extend], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'ec9c35b4-3dfd-4cee-be70-9d83993b40e5', N'e9138a35-a4ff-460e-ac55-b743d55b9691', NULL, N'系统管理', 1, NULL, N'ec9c35b4-3dfd-4cee-be70-9d83993b40e5,', 1, NULL, N'jtgl', 1, 2, N'{"Icon":null,"Expanded":null}', CAST(N'2019-07-07T16:35:32.610' AS DateTime), NULL, CAST(N'2019-07-07T16:35:32.610' AS DateTime), NULL, 0)
GO
INSERT [Systems].[Resource] ([ResourceId], [ApplicationId], [Uri], [Name], [Type], [ParentId], [Path], [Level], [Remark], [PinYin], [Enabled], [SortId], [Extend], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'ccb74938-7e59-4532-a1ec-850ca75dd7b9', N'e9138a35-a4ff-460e-ac55-b743d55b9691', N'/systems/claim', N'声明', 1, N'ec9c35b4-3dfd-4cee-be70-9d83993b40e5', N'ec9c35b4-3dfd-4cee-be70-9d83993b40e5,ccb74938-7e59-4532-a1ec-850ca75dd7b9,', 2, NULL, N'sm', 1, 1, N'{"Icon":"anticon anticon-tag","Expanded":true}', CAST(N'2019-07-16T15:01:15.083' AS DateTime), N'55ba53a6-e482-4d9b-8d91-3fba6610b896', CAST(N'2019-07-16T15:04:33.857' AS DateTime), N'55ba53a6-e482-4d9b-8d91-3fba6610b896', 0)
GO
INSERT [Systems].[Resource] ([ResourceId], [ApplicationId], [Uri], [Name], [Type], [ParentId], [Path], [Level], [Remark], [PinYin], [Enabled], [SortId], [Extend], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'9412c649-2353-4f37-a178-21a66a7ad3bf', N'e9138a35-a4ff-460e-ac55-b743d55b9691', N'/systems/application', N'应用程序', 1, N'ec9c35b4-3dfd-4cee-be70-9d83993b40e5', N'ec9c35b4-3dfd-4cee-be70-9d83993b40e5,9412c649-2353-4f37-a178-21a66a7ad3bf,', 2, NULL, N'yycx', 1, 1, N'{"Icon":"anticon anticon-database","Expanded":null}', CAST(N'2019-07-07T16:36:39.367' AS DateTime), NULL, CAST(N'2019-07-07T16:36:39.367' AS DateTime), NULL, 0)
GO
INSERT [Systems].[Resource] ([ResourceId], [ApplicationId], [Uri], [Name], [Type], [ParentId], [Path], [Level], [Remark], [PinYin], [Enabled], [SortId], [Extend], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'f85e2381-f85f-4978-aeaa-dd0a3106d1ab', N'e9138a35-a4ff-460e-ac55-b743d55b9691', N'/systems/module', N'模块', 1, N'ec9c35b4-3dfd-4cee-be70-9d83993b40e5', N'ec9c35b4-3dfd-4cee-be70-9d83993b40e5,f85e2381-f85f-4978-aeaa-dd0a3106d1ab,', 2, NULL, N'mk', 1, 2, N'{"Icon":"anticon anticon-menu-fold","Expanded":null}', CAST(N'2019-07-07T16:37:13.687' AS DateTime), NULL, CAST(N'2019-07-07T16:37:13.687' AS DateTime), NULL, 0)
GO
INSERT [Systems].[Resource] ([ResourceId], [ApplicationId], [Uri], [Name], [Type], [ParentId], [Path], [Level], [Remark], [PinYin], [Enabled], [SortId], [Extend], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'a4c32fa8-f8eb-4ce9-a517-d96d431fcb04', N'e9138a35-a4ff-460e-ac55-b743d55b9691', N'/systems/user', N'用户', 1, N'ec9c35b4-3dfd-4cee-be70-9d83993b40e5', N'ec9c35b4-3dfd-4cee-be70-9d83993b40e5,a4c32fa8-f8eb-4ce9-a517-d96d431fcb04,', 2, NULL, N'yh', 1, 3, N'{"Icon":"anticon anticon-user","Expanded":null}', CAST(N'2019-07-07T16:37:34.693' AS DateTime), NULL, CAST(N'2019-07-07T16:37:34.693' AS DateTime), NULL, 0)
GO
INSERT [Systems].[Resource] ([ResourceId], [ApplicationId], [Uri], [Name], [Type], [ParentId], [Path], [Level], [Remark], [PinYin], [Enabled], [SortId], [Extend], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'1a01a8c3-1e6f-47d8-be75-b66da7f7a746', N'e9138a35-a4ff-460e-ac55-b743d55b9691', N'/systems/role', N'角色', 1, N'ec9c35b4-3dfd-4cee-be70-9d83993b40e5', N'ec9c35b4-3dfd-4cee-be70-9d83993b40e5,1a01a8c3-1e6f-47d8-be75-b66da7f7a746,', 2, NULL, N'js', 1, 4, N'{"Icon":"anticon anticon-team","Expanded":null}', CAST(N'2019-07-07T16:37:56.583' AS DateTime), NULL, CAST(N'2019-07-07T16:37:56.583' AS DateTime), NULL, 0)
GO
/*==============================================================*/
/* 用户                                                         */
/*==============================================================*/
INSERT [Systems].[User] ([UserId], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PhoneNumber], [PhoneNumberConfirmed], [Password], [PasswordHash], [SafePassword], [SafePasswordHash], [TwoFactorEnabled], [Enabled], [DisabledTime], [LockoutEnabled], [LockoutEnd], [AccessFailedCount], [LoginCount], [RegisterIp], [LastLoginTime], [LastLoginIp], [CurrentLoginTime], [CurrentLoginIp], [SecurityStamp], [Remark], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'55ba53a6-e482-4d9b-8d91-3fba6610b896', N'admin', N'ADMIN', NULL, NULL, 0, NULL, 0, NULL, N'AQAAAAEAACcQAAAAEP+Co27jHqc5JQ0LPfqcMbUtsrCHkZhK/oRC/xPysrV9FTT+siiMEOELuOL+LeN7Jw==', NULL, NULL, 0, 1, NULL, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'E3LEMZVTQRBJD2GDJXDNNJ7BF3GEEUBF', NULL, CAST(N'2019-07-07 16:19:12.397' AS DateTime), NULL, CAST(N'2019-07-07 16:19:12.397' AS DateTime), NULL, 0)
GO
/*==============================================================*/
/* 角色                                                         */
/*==============================================================*/
INSERT [Systems].[Role] ([RoleId], [Code], [Name], [NormalizedName], [Type], [IsAdmin], [ParentId], [Path], [Level], [SortId], [Enabled], [Remark], [PinYin], [Sign], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'd5c3cbde-f2be-47ac-bc85-a329f79588f8', N'admin', N'管理员', N'管理员', N'Role', 1, NULL, N'd5c3cbde-f2be-47ac-bc85-a329f79588f8,', 1, 1, 1, NULL, N'gly', NULL, CAST(N'2019-07-07 16:21:10.497' AS DateTime), NULL, CAST(N'2019-07-07 16:21:10.497' AS DateTime), NULL, 0)
GO
/*==============================================================*/
/* 角色用户                                                      */
/*==============================================================*/
INSERT [Systems].[UserRole] ([RoleId], [UserId]) VALUES (N'd5c3cbde-f2be-47ac-bc85-a329f79588f8', N'55ba53a6-e482-4d9b-8d91-3fba6610b896')
GO
/*==============================================================*/
/* 权限                                                         */
/*==============================================================*/
INSERT [Systems].[Permission] ([PermissionId], [RoleId], [ResourceId], [IsDeny], [Sign], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'50ca7540-fd71-4767-8477-d630148635e3', N'd5c3cbde-f2be-47ac-bc85-a329f79588f8', N'ccb74938-7e59-4532-a1ec-850ca75dd7b9', 0, NULL, CAST(N'2019-07-16T15:01:30.767' AS DateTime), N'55ba53a6-e482-4d9b-8d91-3fba6610b896', CAST(N'2019-07-16T15:01:30.767' AS DateTime), N'55ba53a6-e482-4d9b-8d91-3fba6610b896', 0)
GO
INSERT [Systems].[Permission] ([PermissionId], [RoleId], [ResourceId], [IsDeny], [Sign], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'48c5c904-efca-4a89-95ed-1df05f062c6e', N'd5c3cbde-f2be-47ac-bc85-a329f79588f8', N'1b75baf9-cd60-46f6-9c37-7490d8a2fd4f', 0, NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, 0)
GO
INSERT [Systems].[Permission] ([PermissionId], [RoleId], [ResourceId], [IsDeny], [Sign], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'a185255d-9432-4886-8136-0d973de4c7fd', N'd5c3cbde-f2be-47ac-bc85-a329f79588f8', N'd24aee38-3780-4d32-aff1-38236b348b86', 0, NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, 0)
GO
INSERT [Systems].[Permission] ([PermissionId], [RoleId], [ResourceId], [IsDeny], [Sign], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'6d7d6934-dce2-4f0e-a2d1-459dba8ea774', N'd5c3cbde-f2be-47ac-bc85-a329f79588f8', N'ec9c35b4-3dfd-4cee-be70-9d83993b40e5', 0, NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, 0)
GO
INSERT [Systems].[Permission] ([PermissionId], [RoleId], [ResourceId], [IsDeny], [Sign], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'5475775f-5cb5-4f28-bdc1-bd83072649f8', N'd5c3cbde-f2be-47ac-bc85-a329f79588f8', N'9412c649-2353-4f37-a178-21a66a7ad3bf', 0, NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, 0)
GO
INSERT [Systems].[Permission] ([PermissionId], [RoleId], [ResourceId], [IsDeny], [Sign], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'c17d6df8-ad61-4898-a94f-0706fdf453d9', N'd5c3cbde-f2be-47ac-bc85-a329f79588f8', N'f85e2381-f85f-4978-aeaa-dd0a3106d1ab', 0, NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, 0)
GO
INSERT [Systems].[Permission] ([PermissionId], [RoleId], [ResourceId], [IsDeny], [Sign], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'd927d7ac-b5f5-4157-b659-945f77b5cb39', N'd5c3cbde-f2be-47ac-bc85-a329f79588f8', N'a4c32fa8-f8eb-4ce9-a517-d96d431fcb04', 0, NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, 0)
GO
INSERT [Systems].[Permission] ([PermissionId], [RoleId], [ResourceId], [IsDeny], [Sign], [CreationTime], [CreatorId], [LastModificationTime], [LastModifierId], [IsDeleted]) VALUES (N'7a153330-86f4-4aeb-adef-41722bc50f98', N'd5c3cbde-f2be-47ac-bc85-a329f79588f8', N'1a01a8c3-1e6f-47d8-be75-b66da7f7a746', 0, NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, CAST(N'2019-07-07 16:42:45.127' AS DateTime), NULL, 0)
GO