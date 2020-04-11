
IF OBJECT_ID(N'[dbo].[Log]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Log];

-- --------------------------------------------------
-- Creating all tables
-- --------------------------------------------------

-- Creating table 'Log'
CREATE TABLE [dbo].[Log] (
    [Id] bigint IDENTITY(1,1) NOT NULL,
    [Date] datetime  NOT NULL,
    [Thread] nvarchar(255)  NOT NULL,
    [Level] nvarchar(50)  NOT NULL,
    [Logger] nvarchar(255)  NOT NULL,
    [Message] nvarchar(4000)  NOT NULL,
    [Exception] nvarchar(2000)  NULL,
    [EntityId] uniqueidentifier  NULL,
    [EntityLogicalName] nvarchar(100)  NULL,
    [ConfigId] bigint  NOT NULL,
    [SdkMessage] nvarchar(20)  NULL,
    [CrmUser] uniqueidentifier  NULL,
    [CorrelationId] uniqueidentifier  NULL,
    [OrganizationId] uniqueidentifier  NULL
);

-- Creating primary key on [Id] in table 'Log'
ALTER TABLE [dbo].[Log]
ADD CONSTRAINT [PK_Log]
    PRIMARY KEY CLUSTERED ([Id] ASC);
