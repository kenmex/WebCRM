BEGIN TRANSACTION;
CREATE TABLE [AccountStatuses] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [SortOrder] int NOT NULL DEFAULT 0,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [SystemCode] nvarchar(30) NULL,
    CONSTRAINT [PK_AccountStatuses] PRIMARY KEY ([Id])
);

CREATE TABLE [ActivityTypes] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [SortOrder] int NOT NULL DEFAULT 0,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [SystemCode] nvarchar(30) NULL,
    CONSTRAINT [PK_ActivityTypes] PRIMARY KEY ([Id])
);

CREATE TABLE [ApiTokens] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [TokenHash] binary(32) NOT NULL,
    [TokenPrefix] char(8) NOT NULL,
    [Scope] tinyint NOT NULL DEFAULT CAST(1 AS tinyint),
    [ExpiresAt] datetime2(0) NOT NULL,
    [LastUsedAt] datetime2(0) NULL,
    [RevokedAt] datetime2(0) NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [CreatedBy] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_ApiTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ApiTokens_Scope] CHECK ([Scope] IN (1, 2)),
    CONSTRAINT [FK_ApiTokens_AspNetUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ApiTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [AuditLogs] (
    [Id] bigint NOT NULL IDENTITY,
    [UserId] nvarchar(450) NULL,
    [Action] nvarchar(20) NOT NULL,
    [EntityName] nvarchar(50) NULL,
    [EntityId] int NULL,
    [Changes] nvarchar(max) NULL,
    [Source] nvarchar(50) NOT NULL DEFAULT N'UI',
    [ChangedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_AuditLogs_Changes] CHECK (ISJSON([Changes]) = 1),
    CONSTRAINT [FK_AuditLogs_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [CompanySettings] (
    [Id] int NOT NULL,
    [CompanyName] nvarchar(200) NOT NULL,
    [LogoData] varbinary(max) NULL,
    [LogoContentType] nvarchar(50) NULL,
    [PrimaryColor] char(7) NOT NULL DEFAULT '#594AE2',
    [SecondaryColor] char(7) NOT NULL DEFAULT '#C2185B',
    [DefaultCurrency] char(3) NOT NULL DEFAULT 'EUR',
    [DefaultTimeZoneId] nvarchar(64) NOT NULL DEFAULT N'Europe/Athens',
    [DefaultCountryCode] char(2) NULL,
    [DateFormat] nvarchar(20) NOT NULL DEFAULT N'dd/MM/yyyy',
    [DemoMode] bit NOT NULL DEFAULT CAST(0 AS bit),
    [UpdatedAt] datetime2(0) NULL,
    [UpdatedBy] nvarchar(450) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_CompanySettings] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_CompanySettings_PrimaryColor] CHECK ([PrimaryColor] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'),
    CONSTRAINT [CK_CompanySettings_SecondaryColor] CHECK ([SecondaryColor] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'),
    CONSTRAINT [CK_CompanySettings_SingleRow] CHECK ([Id] = 1),
    CONSTRAINT [FK_CompanySettings_AspNetUsers_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Favourites] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [EntityName] nvarchar(50) NOT NULL,
    [EntityId] int NOT NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    CONSTRAINT [PK_Favourites] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Favourites_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ImportBatches] (
    [Id] int NOT NULL IDENTITY,
    [Entity] nvarchar(20) NOT NULL,
    [FileName] nvarchar(255) NOT NULL,
    [Status] tinyint NOT NULL DEFAULT CAST(0 AS tinyint),
    [DuplicateRule] tinyint NOT NULL DEFAULT CAST(0 AS tinyint),
    [TotalRows] int NOT NULL DEFAULT 0,
    [OkRows] int NOT NULL DEFAULT 0,
    [WarningRows] int NOT NULL DEFAULT 0,
    [ErrorRows] int NOT NULL DEFAULT 0,
    [ErrorReportName] nvarchar(100) NULL,
    [StartedAt] datetime2(0) NULL,
    [CompletedAt] datetime2(0) NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [CreatedBy] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_ImportBatches] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ImportBatches_DuplicateRule] CHECK ([DuplicateRule] IN (0, 1)),
    CONSTRAINT [CK_ImportBatches_Status] CHECK ([Status] IN (0, 1, 2, 3, 4)),
    CONSTRAINT [FK_ImportBatches_AspNetUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Industries] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [SortOrder] int NOT NULL DEFAULT 0,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [SystemCode] nvarchar(30) NULL,
    CONSTRAINT [PK_Industries] PRIMARY KEY ([Id])
);

CREATE TABLE [LeadSources] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [SortOrder] int NOT NULL DEFAULT 0,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [SystemCode] nvarchar(30) NULL,
    CONSTRAINT [PK_LeadSources] PRIMARY KEY ([Id])
);

CREATE TABLE [LeadStatuses] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [SortOrder] int NOT NULL DEFAULT 0,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [SystemCode] nvarchar(30) NULL,
    CONSTRAINT [PK_LeadStatuses] PRIMARY KEY ([Id])
);

CREATE TABLE [LostReasons] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [SortOrder] int NOT NULL DEFAULT 0,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [SystemCode] nvarchar(30) NULL,
    CONSTRAINT [PK_LostReasons] PRIMARY KEY ([Id])
);

CREATE TABLE [Notifications] (
    [Id] bigint NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [Type] nvarchar(30) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Link] nvarchar(300) NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [ReadAt] datetime2(0) NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Notifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [RecentViews] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [EntityName] nvarchar(50) NOT NULL,
    [EntityId] int NOT NULL,
    [ViewedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    CONSTRAINT [PK_RecentViews] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecentViews_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [SavedViews] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ListKey] nvarchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [QueryString] nvarchar(2000) NOT NULL,
    [IsPublic] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_SavedViews] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SavedViews_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Stages] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [SortOrder] int NOT NULL,
    [DefaultProbability] decimal(5,2) NOT NULL DEFAULT 0.0,
    [IsWon] bit NOT NULL DEFAULT CAST(0 AS bit),
    [IsLost] bit NOT NULL DEFAULT CAST(0 AS bit),
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_Stages] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Stages_DefaultProbability] CHECK ([DefaultProbability] BETWEEN 0 AND 100),
    CONSTRAINT [CK_Stages_NotWonAndLost] CHECK (NOT ([IsWon] = 1 AND [IsLost] = 1))
);

CREATE TABLE [Teams] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [ManagerId] nvarchar(450) NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [CreatedBy] nvarchar(450) NOT NULL,
    [UpdatedAt] datetime2(0) NULL,
    [UpdatedBy] nvarchar(450) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Teams] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Teams_AspNetUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Teams_AspNetUsers_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Teams_AspNetUsers_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Accounts] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) COLLATE Greek_100_CI_AI NOT NULL,
    [VatNumber] nvarchar(20) NULL,
    [IndustryId] int NULL,
    [AccountStatusId] int NOT NULL,
    [Phone] nvarchar(30) NULL,
    [Website] nvarchar(300) NULL,
    [OwnerId] nvarchar(450) NOT NULL,
    [ImportBatchId] int NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [CreatedBy] nvarchar(450) NOT NULL,
    [UpdatedAt] datetime2(0) NULL,
    [UpdatedBy] nvarchar(450) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Accounts_AccountStatuses_AccountStatusId] FOREIGN KEY ([AccountStatusId]) REFERENCES [AccountStatuses] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Accounts_AspNetUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Accounts_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Accounts_AspNetUsers_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Accounts_ImportBatches_ImportBatchId] FOREIGN KEY ([ImportBatchId]) REFERENCES [ImportBatches] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Accounts_Industries_IndustryId] FOREIGN KEY ([IndustryId]) REFERENCES [Industries] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Addresses] (
    [Id] int NOT NULL IDENTITY,
    [AccountId] int NOT NULL,
    [AddressType] tinyint NOT NULL DEFAULT CAST(1 AS tinyint),
    [Street] nvarchar(200) NULL,
    [City] nvarchar(100) COLLATE Greek_100_CI_AI NULL,
    [Postcode] nvarchar(20) NULL,
    [CountryCode] char(2) NULL,
    CONSTRAINT [PK_Addresses] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Addresses_AddressType] CHECK ([AddressType] IN (1, 2)),
    CONSTRAINT [FK_Addresses_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Contacts] (
    [Id] int NOT NULL IDENTITY,
    [FirstName] nvarchar(100) COLLATE Greek_100_CI_AI NULL,
    [LastName] nvarchar(100) COLLATE Greek_100_CI_AI NOT NULL,
    [FullName] AS CONCAT_WS(' ', [FirstName], [LastName]) PERSISTED,
    [AccountId] int NOT NULL,
    [JobTitle] nvarchar(100) NULL,
    [Email] nvarchar(254) COLLATE Greek_100_CI_AI NULL,
    [Phone] nvarchar(30) NULL,
    [Mobile] nvarchar(30) NULL,
    [OwnerId] nvarchar(450) NOT NULL,
    [ImportBatchId] int NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [CreatedBy] nvarchar(450) NOT NULL,
    [UpdatedAt] datetime2(0) NULL,
    [UpdatedBy] nvarchar(450) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Contacts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Contacts_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Contacts_AspNetUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Contacts_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Contacts_AspNetUsers_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Contacts_ImportBatches_ImportBatchId] FOREIGN KEY ([ImportBatchId]) REFERENCES [ImportBatches] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Opportunities] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) COLLATE Greek_100_CI_AI NOT NULL,
    [AccountId] int NOT NULL,
    [PrimaryContactId] int NULL,
    [StageId] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL DEFAULT 0.0,
    [Currency] char(3) NOT NULL DEFAULT 'EUR',
    [Probability] decimal(5,2) NOT NULL,
    [ProbabilityOverridden] bit NOT NULL DEFAULT CAST(0 AS bit),
    [CloseDate] date NOT NULL,
    [ClosedAt] datetime2(0) NULL,
    [LostReasonId] int NULL,
    [OwnerId] nvarchar(450) NOT NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [CreatedBy] nvarchar(450) NOT NULL,
    [UpdatedAt] datetime2(0) NULL,
    [UpdatedBy] nvarchar(450) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Opportunities] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Opportunities_Amount] CHECK ([Amount] >= 0),
    CONSTRAINT [CK_Opportunities_Probability] CHECK ([Probability] BETWEEN 0 AND 100),
    CONSTRAINT [FK_Opportunities_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Opportunities_AspNetUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Opportunities_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Opportunities_AspNetUsers_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Opportunities_Contacts_PrimaryContactId] FOREIGN KEY ([PrimaryContactId]) REFERENCES [Contacts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Opportunities_LostReasons_LostReasonId] FOREIGN KEY ([LostReasonId]) REFERENCES [LostReasons] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Opportunities_Stages_StageId] FOREIGN KEY ([StageId]) REFERENCES [Stages] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Attachments] (
    [Id] int NOT NULL IDENTITY,
    [StoredName] nvarchar(100) NOT NULL,
    [OriginalName] nvarchar(255) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    [SizeBytes] bigint NOT NULL,
    [AccountId] int NULL,
    [ContactId] int NULL,
    [OpportunityId] int NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [CreatedBy] nvarchar(450) NOT NULL,
    [UpdatedAt] datetime2(0) NULL,
    [UpdatedBy] nvarchar(450) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Attachments] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Attachments_ExactlyOneLink] CHECK ((CASE WHEN [AccountId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [ContactId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [OpportunityId] IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT [CK_Attachments_SizeBytes] CHECK ([SizeBytes] <= 10485760),
    CONSTRAINT [FK_Attachments_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Attachments_AspNetUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Attachments_AspNetUsers_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Attachments_Contacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [Contacts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Attachments_Opportunities_OpportunityId] FOREIGN KEY ([OpportunityId]) REFERENCES [Opportunities] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Leads] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) COLLATE Greek_100_CI_AI NOT NULL,
    [Company] nvarchar(200) COLLATE Greek_100_CI_AI NULL,
    [Email] nvarchar(254) COLLATE Greek_100_CI_AI NULL,
    [Phone] nvarchar(30) NULL,
    [LeadSourceId] int NULL,
    [LeadStatusId] int NOT NULL,
    [OwnerId] nvarchar(450) NOT NULL,
    [ConvertedAt] datetime2(0) NULL,
    [ConvertedAccountId] int NULL,
    [ConvertedContactId] int NULL,
    [ConvertedOpportunityId] int NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [CreatedBy] nvarchar(450) NOT NULL,
    [UpdatedAt] datetime2(0) NULL,
    [UpdatedBy] nvarchar(450) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Leads] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Leads_Accounts_ConvertedAccountId] FOREIGN KEY ([ConvertedAccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Leads_AspNetUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Leads_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Leads_AspNetUsers_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Leads_Contacts_ConvertedContactId] FOREIGN KEY ([ConvertedContactId]) REFERENCES [Contacts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Leads_LeadSources_LeadSourceId] FOREIGN KEY ([LeadSourceId]) REFERENCES [LeadSources] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Leads_LeadStatuses_LeadStatusId] FOREIGN KEY ([LeadStatusId]) REFERENCES [LeadStatuses] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Leads_Opportunities_ConvertedOpportunityId] FOREIGN KEY ([ConvertedOpportunityId]) REFERENCES [Opportunities] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Activities] (
    [Id] int NOT NULL IDENTITY,
    [ActivityTypeId] int NOT NULL,
    [Subject] nvarchar(200) NOT NULL,
    [Description] nvarchar(max) NULL,
    [DueAt] datetime2(0) NULL,
    [DoneAt] datetime2(0) NULL,
    [DurationMinutes] int NULL,
    [OwnerId] nvarchar(450) NOT NULL,
    [AccountId] int NULL,
    [ContactId] int NULL,
    [OpportunityId] int NULL,
    [LeadId] int NULL,
    [ReminderSentAt] datetime2(0) NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [CreatedBy] nvarchar(450) NOT NULL,
    [UpdatedAt] datetime2(0) NULL,
    [UpdatedBy] nvarchar(450) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Activities] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Activities_DurationMinutes] CHECK ([DurationMinutes] > 0),
    CONSTRAINT [CK_Activities_ExactlyOneLink] CHECK ((CASE WHEN [AccountId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [ContactId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [OpportunityId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [LeadId] IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT [FK_Activities_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Activities_ActivityTypes_ActivityTypeId] FOREIGN KEY ([ActivityTypeId]) REFERENCES [ActivityTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Activities_AspNetUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Activities_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Activities_AspNetUsers_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Activities_Contacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [Contacts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Activities_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Activities_Opportunities_OpportunityId] FOREIGN KEY ([OpportunityId]) REFERENCES [Opportunities] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Notes] (
    [Id] int NOT NULL IDENTITY,
    [Body] nvarchar(4000) NOT NULL,
    [AccountId] int NULL,
    [ContactId] int NULL,
    [OpportunityId] int NULL,
    [LeadId] int NULL,
    [CreatedAt] datetime2(0) NOT NULL DEFAULT (sysutcdatetime()),
    [CreatedBy] nvarchar(450) NOT NULL,
    [UpdatedAt] datetime2(0) NULL,
    [UpdatedBy] nvarchar(450) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Notes] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Notes_ExactlyOneLink] CHECK ((CASE WHEN [AccountId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [ContactId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [OpportunityId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [LeadId] IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT [FK_Notes_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Notes_AspNetUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Notes_AspNetUsers_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Notes_Contacts_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [Contacts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Notes_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Notes_Opportunities_OpportunityId] FOREIGN KEY ([OpportunityId]) REFERENCES [Opportunities] ([Id]) ON DELETE NO ACTION
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'IsActive', N'Name', N'SortOrder', N'SystemCode') AND [object_id] = OBJECT_ID(N'[ActivityTypes]'))
    SET IDENTITY_INSERT [ActivityTypes] ON;
INSERT INTO [ActivityTypes] ([Id], [IsActive], [Name], [SortOrder], [SystemCode])
VALUES (1, CAST(1 AS bit), N'Task', 10, N'TASK'),
(2, CAST(1 AS bit), N'Call', 20, N'CALL'),
(3, CAST(1 AS bit), N'Meeting', 30, N'MEETING');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'IsActive', N'Name', N'SortOrder', N'SystemCode') AND [object_id] = OBJECT_ID(N'[ActivityTypes]'))
    SET IDENTITY_INSERT [ActivityTypes] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'IsActive', N'Name', N'SortOrder', N'SystemCode') AND [object_id] = OBJECT_ID(N'[LeadStatuses]'))
    SET IDENTITY_INSERT [LeadStatuses] ON;
INSERT INTO [LeadStatuses] ([Id], [IsActive], [Name], [SortOrder], [SystemCode])
VALUES (1, CAST(1 AS bit), N'New', 10, N'NEW'),
(2, CAST(1 AS bit), N'Contacted', 20, NULL),
(3, CAST(1 AS bit), N'Qualified', 30, NULL),
(4, CAST(1 AS bit), N'Disqualified', 80, N'DISQUALIFIED'),
(5, CAST(1 AS bit), N'Converted', 90, N'CONVERTED');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'IsActive', N'Name', N'SortOrder', N'SystemCode') AND [object_id] = OBJECT_ID(N'[LeadStatuses]'))
    SET IDENTITY_INSERT [LeadStatuses] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DefaultProbability', N'IsActive', N'Name', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Stages]'))
    SET IDENTITY_INSERT [Stages] ON;
INSERT INTO [Stages] ([Id], [DefaultProbability], [IsActive], [Name], [SortOrder])
VALUES (1, 10.0, CAST(1 AS bit), N'Prospecting', 10),
(2, 20.0, CAST(1 AS bit), N'Qualification', 20),
(3, 50.0, CAST(1 AS bit), N'Proposal', 30),
(4, 75.0, CAST(1 AS bit), N'Negotiation', 40);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DefaultProbability', N'IsActive', N'Name', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Stages]'))
    SET IDENTITY_INSERT [Stages] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DefaultProbability', N'IsActive', N'IsWon', N'Name', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Stages]'))
    SET IDENTITY_INSERT [Stages] ON;
INSERT INTO [Stages] ([Id], [DefaultProbability], [IsActive], [IsWon], [Name], [SortOrder])
VALUES (5, 100.0, CAST(1 AS bit), CAST(1 AS bit), N'Won', 90);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DefaultProbability', N'IsActive', N'IsWon', N'Name', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Stages]'))
    SET IDENTITY_INSERT [Stages] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'IsActive', N'IsLost', N'Name', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Stages]'))
    SET IDENTITY_INSERT [Stages] ON;
INSERT INTO [Stages] ([Id], [IsActive], [IsLost], [Name], [SortOrder])
VALUES (6, CAST(1 AS bit), CAST(1 AS bit), N'Lost', 100);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'IsActive', N'IsLost', N'Name', N'SortOrder') AND [object_id] = OBJECT_ID(N'[Stages]'))
    SET IDENTITY_INSERT [Stages] OFF;

CREATE INDEX [IX_AspNetUsers_TeamId] ON [AspNetUsers] ([TeamId]);

ALTER TABLE [AspNetUsers] ADD CONSTRAINT [CK_AspNetUsers_Theme] CHECK ([Theme] IN (0, 1, 2));

CREATE INDEX [IX_Accounts_AccountStatusId] ON [Accounts] ([AccountStatusId]);

CREATE INDEX [IX_Accounts_ImportBatchId] ON [Accounts] ([ImportBatchId]);

CREATE INDEX [IX_Accounts_IndustryId] ON [Accounts] ([IndustryId]);

CREATE UNIQUE INDEX [IX_Accounts_Name] ON [Accounts] ([Name]) WHERE [IsActive] = 1;

CREATE INDEX [IX_Accounts_OwnerId_IsActive] ON [Accounts] ([OwnerId], [IsActive]);

CREATE UNIQUE INDEX [IX_Accounts_VatNumber] ON [Accounts] ([VatNumber]) WHERE [VatNumber] IS NOT NULL AND [IsActive] = 1;

CREATE UNIQUE INDEX [IX_AccountStatuses_Name] ON [AccountStatuses] ([Name]);

CREATE UNIQUE INDEX [IX_AccountStatuses_SystemCode] ON [AccountStatuses] ([SystemCode]) WHERE [SystemCode] IS NOT NULL;

CREATE INDEX [IX_Activities_AccountId] ON [Activities] ([AccountId]);

CREATE INDEX [IX_Activities_ActivityTypeId] ON [Activities] ([ActivityTypeId]);

CREATE INDEX [IX_Activities_ContactId] ON [Activities] ([ContactId]);

CREATE INDEX [IX_Activities_LeadId] ON [Activities] ([LeadId]);

CREATE INDEX [IX_Activities_OpportunityId] ON [Activities] ([OpportunityId]);

CREATE INDEX [IX_Activities_OwnerId_DoneAt_DueAt] ON [Activities] ([OwnerId], [DoneAt], [DueAt]);

CREATE UNIQUE INDEX [IX_ActivityTypes_Name] ON [ActivityTypes] ([Name]);

CREATE UNIQUE INDEX [IX_ActivityTypes_SystemCode] ON [ActivityTypes] ([SystemCode]) WHERE [SystemCode] IS NOT NULL;

CREATE UNIQUE INDEX [IX_Addresses_AccountId_AddressType] ON [Addresses] ([AccountId], [AddressType]);

CREATE INDEX [IX_Addresses_City] ON [Addresses] ([City]);

CREATE UNIQUE INDEX [IX_ApiTokens_Name] ON [ApiTokens] ([Name]) WHERE [RevokedAt] IS NULL;

CREATE UNIQUE INDEX [IX_ApiTokens_TokenHash] ON [ApiTokens] ([TokenHash]);

CREATE INDEX [IX_ApiTokens_UserId] ON [ApiTokens] ([UserId]);

CREATE INDEX [IX_Attachments_AccountId] ON [Attachments] ([AccountId]);

CREATE INDEX [IX_Attachments_ContactId] ON [Attachments] ([ContactId]);

CREATE INDEX [IX_Attachments_OpportunityId] ON [Attachments] ([OpportunityId]);

CREATE UNIQUE INDEX [IX_Attachments_StoredName] ON [Attachments] ([StoredName]);

CREATE INDEX [IX_AuditLogs_EntityName_EntityId_ChangedAt] ON [AuditLogs] ([EntityName], [EntityId], [ChangedAt]);

CREATE INDEX [IX_AuditLogs_UserId_ChangedAt] ON [AuditLogs] ([UserId], [ChangedAt]);

CREATE INDEX [IX_Contacts_AccountId] ON [Contacts] ([AccountId]);

CREATE INDEX [IX_Contacts_Email] ON [Contacts] ([Email]);

CREATE INDEX [IX_Contacts_FullName] ON [Contacts] ([FullName]);

CREATE INDEX [IX_Contacts_ImportBatchId] ON [Contacts] ([ImportBatchId]);

CREATE INDEX [IX_Contacts_OwnerId_IsActive] ON [Contacts] ([OwnerId], [IsActive]);

CREATE UNIQUE INDEX [IX_Favourites_UserId_EntityName_EntityId] ON [Favourites] ([UserId], [EntityName], [EntityId]);

CREATE UNIQUE INDEX [IX_Industries_Name] ON [Industries] ([Name]);

CREATE UNIQUE INDEX [IX_Industries_SystemCode] ON [Industries] ([SystemCode]) WHERE [SystemCode] IS NOT NULL;

CREATE INDEX [IX_Leads_ConvertedAccountId] ON [Leads] ([ConvertedAccountId]);

CREATE INDEX [IX_Leads_ConvertedContactId] ON [Leads] ([ConvertedContactId]);

CREATE INDEX [IX_Leads_ConvertedOpportunityId] ON [Leads] ([ConvertedOpportunityId]);

CREATE INDEX [IX_Leads_LeadSourceId] ON [Leads] ([LeadSourceId]);

CREATE INDEX [IX_Leads_LeadStatusId] ON [Leads] ([LeadStatusId]);

CREATE INDEX [IX_Leads_OwnerId] ON [Leads] ([OwnerId]);

CREATE UNIQUE INDEX [IX_LeadSources_Name] ON [LeadSources] ([Name]);

CREATE UNIQUE INDEX [IX_LeadSources_SystemCode] ON [LeadSources] ([SystemCode]) WHERE [SystemCode] IS NOT NULL;

CREATE UNIQUE INDEX [IX_LeadStatuses_Name] ON [LeadStatuses] ([Name]);

CREATE UNIQUE INDEX [IX_LeadStatuses_SystemCode] ON [LeadStatuses] ([SystemCode]) WHERE [SystemCode] IS NOT NULL;

CREATE UNIQUE INDEX [IX_LostReasons_Name] ON [LostReasons] ([Name]);

CREATE UNIQUE INDEX [IX_LostReasons_SystemCode] ON [LostReasons] ([SystemCode]) WHERE [SystemCode] IS NOT NULL;

CREATE INDEX [IX_Notes_AccountId] ON [Notes] ([AccountId]);

CREATE INDEX [IX_Notes_ContactId] ON [Notes] ([ContactId]);

CREATE INDEX [IX_Notes_LeadId] ON [Notes] ([LeadId]);

CREATE INDEX [IX_Notes_OpportunityId] ON [Notes] ([OpportunityId]);

CREATE INDEX [IX_Notifications_UserId_ReadAt_CreatedAt] ON [Notifications] ([UserId], [ReadAt], [CreatedAt]);

CREATE INDEX [IX_Opportunities_AccountId] ON [Opportunities] ([AccountId]);

CREATE INDEX [IX_Opportunities_CloseDate] ON [Opportunities] ([CloseDate]);

CREATE INDEX [IX_Opportunities_LostReasonId] ON [Opportunities] ([LostReasonId]);

CREATE INDEX [IX_Opportunities_OwnerId] ON [Opportunities] ([OwnerId]);

CREATE INDEX [IX_Opportunities_PrimaryContactId] ON [Opportunities] ([PrimaryContactId]);

CREATE INDEX [IX_Opportunities_StageId_OwnerId_IsActive] ON [Opportunities] ([StageId], [OwnerId], [IsActive]);

CREATE UNIQUE INDEX [IX_RecentViews_UserId_EntityName_EntityId] ON [RecentViews] ([UserId], [EntityName], [EntityId]);

CREATE UNIQUE INDEX [IX_SavedViews_UserId_ListKey_Name] ON [SavedViews] ([UserId], [ListKey], [Name]);

CREATE UNIQUE INDEX [IX_Stages_IsLost] ON [Stages] ([IsLost]) WHERE [IsLost] = 1;

CREATE UNIQUE INDEX [IX_Stages_IsWon] ON [Stages] ([IsWon]) WHERE [IsWon] = 1;

CREATE UNIQUE INDEX [IX_Stages_Name] ON [Stages] ([Name]);

CREATE INDEX [IX_Teams_ManagerId] ON [Teams] ([ManagerId]);

CREATE UNIQUE INDEX [IX_Teams_Name] ON [Teams] ([Name]) WHERE [IsActive] = 1;

ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261009092152_CoreSchema', N'10.0.12');

COMMIT;
GO

