-- =============================================
-- Script: Add Notifications and NotificationPreferences Tables
-- Description: Creates tables for real-time notification system
-- Date: 2026-01-25
-- =============================================

USE [DocNDb]
GO

-- Create Notifications table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Notifications]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Notifications] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [Type] NVARCHAR(50) NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Message] NVARCHAR(1000) NOT NULL,
        [Link] NVARCHAR(500) NULL,
        [Icon] NVARCHAR(50) NOT NULL DEFAULT 'info',
        [IsRead] BIT NOT NULL DEFAULT 0,
        [IsImportant] BIT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
        [ReadAt] DATETIME2(7) NULL,
        [Metadata] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Notifications_AspNetUsers_UserId] FOREIGN KEY([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id])
            ON DELETE CASCADE
    )
    
    -- Create indexes for performance
    CREATE INDEX [IX_Notifications_UserId] ON [dbo].[Notifications]([UserId])
    CREATE INDEX [IX_Notifications_UserId_IsRead] ON [dbo].[Notifications]([UserId], [IsRead])
    CREATE INDEX [IX_Notifications_UserId_CreatedAt] ON [dbo].[Notifications]([UserId], [CreatedAt])
    CREATE INDEX [IX_Notifications_CreatedAt] ON [dbo].[Notifications]([CreatedAt])
    
    PRINT 'Created Notifications table'
END
ELSE
BEGIN
    PRINT 'Notifications table already exists'
END
GO

-- Create NotificationPreferences table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[NotificationPreferences]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[NotificationPreferences] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [EnableDocumentProcessed] BIT NOT NULL DEFAULT 1,
        [EnableComments] BIT NOT NULL DEFAULT 1,
        [EnableMentions] BIT NOT NULL DEFAULT 1,
        [EnableSystemAlerts] BIT NOT NULL DEFAULT 1,
        [EnableTaskCompleted] BIT NOT NULL DEFAULT 1,
        [EnableSound] BIT NOT NULL DEFAULT 1,
        [EnableDesktopNotifications] BIT NOT NULL DEFAULT 0,
        [EmailDigestFrequency] NVARCHAR(20) NOT NULL DEFAULT 'none',
        [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2(7) NULL,
        CONSTRAINT [PK_NotificationPreferences] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_NotificationPreferences_AspNetUsers_UserId] FOREIGN KEY([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id])
            ON DELETE CASCADE
    )
    
    -- Create unique index on UserId - each user can only have one preference record
    CREATE UNIQUE INDEX [IX_NotificationPreferences_UserId] ON [dbo].[NotificationPreferences]([UserId])
    
    PRINT 'Created NotificationPreferences table'
END
ELSE
BEGIN
    PRINT 'NotificationPreferences table already exists'
END
GO

-- Create stored procedure to clean up old notifications (retention policy: 30 days)
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_CleanupOldNotifications]') AND type in (N'P'))
BEGIN
    DROP PROCEDURE [dbo].[sp_CleanupOldNotifications]
END
GO

CREATE PROCEDURE [dbo].[sp_CleanupOldNotifications]
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CutoffDate DATETIME2 = DATEADD(DAY, -30, GETUTCDATE())
    DECLARE @DeletedCount INT
    
    DELETE FROM [dbo].[Notifications]
    WHERE [CreatedAt] < @CutoffDate
    
    SET @DeletedCount = @@ROWCOUNT
    
    PRINT 'Deleted ' + CAST(@DeletedCount AS VARCHAR(10)) + ' notifications older than 30 days'
    
    RETURN @DeletedCount
END
GO

PRINT 'Notification system database objects created successfully'
GO
