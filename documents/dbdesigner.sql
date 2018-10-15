/*==============================================================*/
/* DBMS name:      Microsoft SQL Server 2012                    */
/* Created on:     2018/10/15 21:49:56                          */
/*==============================================================*/


if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.Application')
            and   type = 'U')
   drop table Systems.Application
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.ApplicationRole')
            and   type = 'U')
   drop table Systems.ApplicationRole
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.ApplicationTenant')
            and   type = 'U')
   drop table Systems.ApplicationTenant
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.ApplicationUser')
            and   type = 'U')
   drop table Systems.ApplicationUser
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.Permission')
            and   type = 'U')
   drop table Systems.Permission
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.Resource')
            and   type = 'U')
   drop table Systems.Resource
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.Role')
            and   type = 'U')
   drop table Systems.Role
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.Tenant')
            and   type = 'U')
   drop table Systems.Tenant
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.TenantRole')
            and   type = 'U')
   drop table Systems.TenantRole
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.TenantUser')
            and   type = 'U')
   drop table Systems.TenantUser
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems."User"')
            and   type = 'U')
   drop table Systems."User"
go

if exists (select 1
            from  sysobjects
           where  id = object_id('Systems.UserRole')
            and   type = 'U')
   drop table Systems.UserRole
go

/*==============================================================*/
/* Table: Application                                           */
/*==============================================================*/
create table Systems.Application (
   ApplicationId        uniqueidentifier     not null,
   Code                 nvarchar(50)         not null,
   Name                 nvarchar(200)        not null,
   Device               int                  not null,
   Note                 nvarchar(500)        null,
   Enabled              bit                  not null,
   RegisterEnabled      bit                  not null,
   CreationTime         datetime             null,
   CreatorId            uniqueidentifier     null,
   LastModificationTime datetime             null,
   LastModifierId       uniqueidentifier     null,
   IsDeleted            bit                  not null,
   Version              timestamp            null,
   constraint PK_APPLICATION primary key (ApplicationId)
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
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Device')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'Device'

end


execute sp_addextendedproperty 'MS_Description', 
   '终端设备',
   'schema', 'Systems', 'table', 'Application', 'column', 'Device'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Application')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Note')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Application', 'column', 'Note'

end


execute sp_addextendedproperty 'MS_Description', 
   '备注',
   'schema', 'Systems', 'table', 'Application', 'column', 'Note'
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
   '创建人',
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
   '最后修改人',
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
/* Table: ApplicationRole                                       */
/*==============================================================*/
create table Systems.ApplicationRole (
   ApplicationId        uniqueidentifier     not null,
   RoleId               uniqueidentifier     not null,
   constraint PK_APPLICATIONROLE primary key (ApplicationId, RoleId)
)
go

if exists (select 1 from  sys.extended_properties
           where major_id = object_id('Systems.ApplicationRole') and minor_id = 0)
begin 
   execute sp_dropextendedproperty 'MS_Description',  
   'schema', 'Systems', 'table', 'ApplicationRole' 
 
end 


execute sp_addextendedproperty 'MS_Description',  
   '应用程序角色', 
   'schema', 'Systems', 'table', 'ApplicationRole'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.ApplicationRole')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'ApplicationId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'ApplicationRole', 'column', 'ApplicationId'

end


execute sp_addextendedproperty 'MS_Description', 
   '应用程序标识',
   'schema', 'Systems', 'table', 'ApplicationRole', 'column', 'ApplicationId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.ApplicationRole')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'RoleId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'ApplicationRole', 'column', 'RoleId'

end


execute sp_addextendedproperty 'MS_Description', 
   '角色标识',
   'schema', 'Systems', 'table', 'ApplicationRole', 'column', 'RoleId'
go

/*==============================================================*/
/* Table: ApplicationTenant                                     */
/*==============================================================*/
create table Systems.ApplicationTenant (
   ApplicationId        uniqueidentifier     not null,
   TenantId             uniqueidentifier     not null,
   constraint PK_APPLICATIONTENANT primary key (ApplicationId, TenantId)
)
go

if exists (select 1 from  sys.extended_properties
           where major_id = object_id('Systems.ApplicationTenant') and minor_id = 0)
begin 
   execute sp_dropextendedproperty 'MS_Description',  
   'schema', 'Systems', 'table', 'ApplicationTenant' 
 
end 


execute sp_addextendedproperty 'MS_Description',  
   '应用程序租户', 
   'schema', 'Systems', 'table', 'ApplicationTenant'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.ApplicationTenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'ApplicationId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'ApplicationTenant', 'column', 'ApplicationId'

end


execute sp_addextendedproperty 'MS_Description', 
   '应用程序编号',
   'schema', 'Systems', 'table', 'ApplicationTenant', 'column', 'ApplicationId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.ApplicationTenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'TenantId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'ApplicationTenant', 'column', 'TenantId'

end


execute sp_addextendedproperty 'MS_Description', 
   '租户编号',
   'schema', 'Systems', 'table', 'ApplicationTenant', 'column', 'TenantId'
go

/*==============================================================*/
/* Table: ApplicationUser                                       */
/*==============================================================*/
create table Systems.ApplicationUser (
   ApplicationId        uniqueidentifier     not null,
   UserId               uniqueidentifier     not null,
   constraint PK_APPLICATIONUSER primary key (ApplicationId, UserId)
)
go

if exists (select 1 from  sys.extended_properties
           where major_id = object_id('Systems.ApplicationUser') and minor_id = 0)
begin 
   execute sp_dropextendedproperty 'MS_Description',  
   'schema', 'Systems', 'table', 'ApplicationUser' 
 
end 


execute sp_addextendedproperty 'MS_Description',  
   '应用程序用户', 
   'schema', 'Systems', 'table', 'ApplicationUser'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.ApplicationUser')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'ApplicationId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'ApplicationUser', 'column', 'ApplicationId'

end


execute sp_addextendedproperty 'MS_Description', 
   '应用程序标识',
   'schema', 'Systems', 'table', 'ApplicationUser', 'column', 'ApplicationId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.ApplicationUser')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'UserId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'ApplicationUser', 'column', 'UserId'

end


execute sp_addextendedproperty 'MS_Description', 
   '用户标识',
   'schema', 'Systems', 'table', 'ApplicationUser', 'column', 'UserId'
go

/*==============================================================*/
/* Table: Permission                                            */
/*==============================================================*/
create table Systems.Permission (
   PermissionId         uniqueidentifier     not null,
   RoleId               uniqueidentifier     null,
   ResourceId           uniqueidentifier     null,
   IsDeny               bit                  not null,
   Sign                 nvarchar(50)         null,
   CreationTime         datetime             null,
   CreatorId            uniqueidentifier     null,
   LastModificationTime datetime             null,
   LastModifierId       uniqueidentifier     null,
   IsDeleted            bit                  not null,
   Version              timestamp            null,
   constraint PK_PERMISSION primary key (PermissionId)
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
/* Table: Resource                                              */
/*==============================================================*/
create table Systems.Resource (
   ResourceId           uniqueidentifier     not null,
   ApplicationId        uniqueidentifier     null,
   Uri                  nvarchar(300)        null,
   Name                 nvarchar(200)        not null,
   Type                 int                  not null,
   CreationTime         datetime             null,
   ParentId             uniqueidentifier     null,
   Path                 nvarchar(800)        not null,
   Level                int                  not null,
   Enabled              bit                  not null,
   SortId               int                  null,
   Note                 nvarchar(500)        null,
   PinYin               nvarchar(200)        not null,
   Extend               nvarchar(max)        null,
   CreatorId            uniqueidentifier     null,
   LastModificationTime datetime             null,
   LastModifierId       uniqueidentifier     null,
   IsDeleted            bit                  not null,
   Version              timestamp            null,
   constraint PK_RESOURCE primary key (ResourceId)
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
   '资源地址',
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
   '级数',
   'schema', 'Systems', 'table', 'Resource', 'column', 'Level'
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
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Note')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'Note'

end


execute sp_addextendedproperty 'MS_Description', 
   '备注',
   'schema', 'Systems', 'table', 'Resource', 'column', 'Note'
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
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreatorId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Resource', 'column', 'CreatorId'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建人',
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
   '最后修改人',
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
/* Table: Role                                                  */
/*==============================================================*/
create table Systems.Role (
   RoleId               uniqueidentifier     not null,
   Code                 nvarchar(50)         not null,
   Name                 nvarchar(200)        not null,
   Type                 nvarchar(80)         not null,
   IsAdmin              bit                  not null,
   ParentId             uniqueidentifier     null,
   Path                 nvarchar(800)        not null,
   Level                int                  not null,
   Enabled              bit                  not null,
   SortId               int                  null,
   Note                 nvarchar(500)        null,
   PinYin               nvarchar(200)        null,
   Sign                 nvarchar(50)         null,
   CreationTime         datetime             null,
   CreatorId            uniqueidentifier     null,
   LastModificationTime datetime             null,
   LastModifierId       uniqueidentifier     null,
   IsDeleted            bit                  not null,
   Version              timestamp            null,
   constraint PK_ROLE primary key (RoleId)
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
   '是否管理员',
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
   '级数',
   'schema', 'Systems', 'table', 'Role', 'column', 'Level'
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
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Note')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Role', 'column', 'Note'

end


execute sp_addextendedproperty 'MS_Description', 
   '备注',
   'schema', 'Systems', 'table', 'Role', 'column', 'Note'
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
   '创建人',
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
   '最后修改人',
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
/* Table: Tenant                                                */
/*==============================================================*/
create table Systems.Tenant (
   TenantId             uniqueidentifier     not null,
   Code                 nvarchar(50)         not null,
   Name                 nvarchar(200)        not null,
   Enabled              bit                  not null,
   PinYin               nvarchar(200)        not null,
   Note                 nvarchar(500)        null,
   CreationTime         datetime             null,
   CreatorId            uniqueidentifier     null,
   LastModificationTime datetime             null,
   LastModifierId       uniqueidentifier     null,
   IsDeleted            bit                  not null,
   Version              timestamp            null,
   constraint PK_TENANT primary key (TenantId)
)
go

if exists (select 1 from  sys.extended_properties
           where major_id = object_id('Systems.Tenant') and minor_id = 0)
begin 
   execute sp_dropextendedproperty 'MS_Description',  
   'schema', 'Systems', 'table', 'Tenant' 
 
end 


execute sp_addextendedproperty 'MS_Description',  
   '租户', 
   'schema', 'Systems', 'table', 'Tenant'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Tenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'TenantId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Tenant', 'column', 'TenantId'

end


execute sp_addextendedproperty 'MS_Description', 
   '租户标识',
   'schema', 'Systems', 'table', 'Tenant', 'column', 'TenantId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Tenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Code')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Tenant', 'column', 'Code'

end


execute sp_addextendedproperty 'MS_Description', 
   '租户编码',
   'schema', 'Systems', 'table', 'Tenant', 'column', 'Code'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Tenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Name')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Tenant', 'column', 'Name'

end


execute sp_addextendedproperty 'MS_Description', 
   '租户名称',
   'schema', 'Systems', 'table', 'Tenant', 'column', 'Name'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Tenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Enabled')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Tenant', 'column', 'Enabled'

end


execute sp_addextendedproperty 'MS_Description', 
   '启用',
   'schema', 'Systems', 'table', 'Tenant', 'column', 'Enabled'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Tenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'PinYin')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Tenant', 'column', 'PinYin'

end


execute sp_addextendedproperty 'MS_Description', 
   '拼音简码',
   'schema', 'Systems', 'table', 'Tenant', 'column', 'PinYin'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Tenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Note')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Tenant', 'column', 'Note'

end


execute sp_addextendedproperty 'MS_Description', 
   '备注',
   'schema', 'Systems', 'table', 'Tenant', 'column', 'Note'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Tenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Tenant', 'column', 'CreationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建时间',
   'schema', 'Systems', 'table', 'Tenant', 'column', 'CreationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Tenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'CreatorId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Tenant', 'column', 'CreatorId'

end


execute sp_addextendedproperty 'MS_Description', 
   '创建人',
   'schema', 'Systems', 'table', 'Tenant', 'column', 'CreatorId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Tenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModificationTime')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Tenant', 'column', 'LastModificationTime'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改时间',
   'schema', 'Systems', 'table', 'Tenant', 'column', 'LastModificationTime'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Tenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'LastModifierId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Tenant', 'column', 'LastModifierId'

end


execute sp_addextendedproperty 'MS_Description', 
   '最后修改人',
   'schema', 'Systems', 'table', 'Tenant', 'column', 'LastModifierId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Tenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'IsDeleted')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Tenant', 'column', 'IsDeleted'

end


execute sp_addextendedproperty 'MS_Description', 
   '是否删除',
   'schema', 'Systems', 'table', 'Tenant', 'column', 'IsDeleted'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.Tenant')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Version')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'Tenant', 'column', 'Version'

end


execute sp_addextendedproperty 'MS_Description', 
   '版本号',
   'schema', 'Systems', 'table', 'Tenant', 'column', 'Version'
go

/*==============================================================*/
/* Table: TenantRole                                            */
/*==============================================================*/
create table Systems.TenantRole (
   RoleId               uniqueidentifier     not null,
   TenantId             uniqueidentifier     not null,
   constraint PK_TENANTROLE primary key (RoleId, TenantId)
)
go

if exists (select 1 from  sys.extended_properties
           where major_id = object_id('Systems.TenantRole') and minor_id = 0)
begin 
   execute sp_dropextendedproperty 'MS_Description',  
   'schema', 'Systems', 'table', 'TenantRole' 
 
end 


execute sp_addextendedproperty 'MS_Description',  
   '租户角色', 
   'schema', 'Systems', 'table', 'TenantRole'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.TenantRole')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'RoleId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'TenantRole', 'column', 'RoleId'

end


execute sp_addextendedproperty 'MS_Description', 
   '角色标识',
   'schema', 'Systems', 'table', 'TenantRole', 'column', 'RoleId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.TenantRole')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'TenantId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'TenantRole', 'column', 'TenantId'

end


execute sp_addextendedproperty 'MS_Description', 
   '租户标识',
   'schema', 'Systems', 'table', 'TenantRole', 'column', 'TenantId'
go

/*==============================================================*/
/* Table: TenantUser                                            */
/*==============================================================*/
create table Systems.TenantUser (
   UserId               uniqueidentifier     not null,
   TenantId             uniqueidentifier     not null,
   constraint PK_TENANTUSER primary key (UserId, TenantId)
)
go

if exists (select 1 from  sys.extended_properties
           where major_id = object_id('Systems.TenantUser') and minor_id = 0)
begin 
   execute sp_dropextendedproperty 'MS_Description',  
   'schema', 'Systems', 'table', 'TenantUser' 
 
end 


execute sp_addextendedproperty 'MS_Description',  
   '租户用户', 
   'schema', 'Systems', 'table', 'TenantUser'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.TenantUser')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'UserId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'TenantUser', 'column', 'UserId'

end


execute sp_addextendedproperty 'MS_Description', 
   '用户标识',
   'schema', 'Systems', 'table', 'TenantUser', 'column', 'UserId'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems.TenantUser')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'TenantId')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'TenantUser', 'column', 'TenantId'

end


execute sp_addextendedproperty 'MS_Description', 
   '租户标识',
   'schema', 'Systems', 'table', 'TenantUser', 'column', 'TenantId'
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
   PasswordHash         nvarchar(1024)       not null,
   SafePassword         nvarchar(256)        null,
   SafePasswordHash     nvarchar(1024)       null,
   TwoFactorEnabled     bit                  not null,
   Enabled              bit                  not null,
   DisabledTime         datetime             null,
   LockoutEnabled       bit                  not null,
   LockoutEnd           datetimeoffset(7)    null,
   AccessFailedCount    int                  not null,
   LoginCount           int                  null,
   RegisterIp           nvarchar(30)         null,
   LastLoginTime        datetime             null,
   LastLoginIp          nvarchar(30)         null,
   CurrentLoginTime     datetime             null,
   CurrentLoginIp       nvarchar(30)         null,
   SecunityStamp        nvarchar(1024)       null,
   Note                 nvarchar(500)        null,
   CreationTime         datetime             null,
   CreatorId            uniqueidentifier     null,
   LastModificationTime datetime             null,
   LastModifierId       uniqueidentifier     null,
   IsDeleted            bit                  not null,
   Version              timestamp            null,
   constraint PK_USER primary key (UserId)
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
   '注册IP',
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
   '上次登录时间',
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
   '上次登录IP',
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
   '本次登录时间',
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
   '本次登录IP',
   'schema', 'Systems', 'table', 'User', 'column', 'CurrentLoginIp'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'SecunityStamp')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'SecunityStamp'

end


execute sp_addextendedproperty 'MS_Description', 
   '安全戳',
   'schema', 'Systems', 'table', 'User', 'column', 'SecunityStamp'
go

if exists(select 1 from sys.extended_properties p where
      p.major_id = object_id('Systems."User"')
  and p.minor_id = (select c.column_id from sys.columns c where c.object_id = p.major_id and c.name = 'Note')
)
begin
   execute sp_dropextendedproperty 'MS_Description', 
   'schema', 'Systems', 'table', 'User', 'column', 'Note'

end


execute sp_addextendedproperty 'MS_Description', 
   '备注',
   'schema', 'Systems', 'table', 'User', 'column', 'Note'
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

