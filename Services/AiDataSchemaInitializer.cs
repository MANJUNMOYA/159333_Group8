using CampusCoffeeSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace CampusCoffeeSystem.Services;

public sealed class AiDataSchemaInitializer(ApplicationDbContext db)
{
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsSqlServer()) return;

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[UserActivities]', N'U') IS NULL
            BEGIN
                CREATE TABLE [UserActivities] (
                    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [UserId] nvarchar(450) NOT NULL,
                    [ActivityType] nvarchar(32) NOT NULL,
                    [ProductName] nvarchar(160) NOT NULL,
                    [ProductCategory] nvarchar(80) NOT NULL,
                    [OccurredAt] datetimeoffset NOT NULL
                );
                CREATE INDEX [IX_UserActivities_UserId_OccurredAt] ON [UserActivities] ([UserId], [OccurredAt]);
            END

            IF OBJECT_ID(N'[AiChatMessages]', N'U') IS NULL
            BEGIN
                CREATE TABLE [AiChatMessages] (
                    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [UserId] nvarchar(450) NOT NULL,
                    [Role] nvarchar(16) NOT NULL,
                    [Content] nvarchar(2000) NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL
                );
                CREATE INDEX [IX_AiChatMessages_UserId_CreatedAt] ON [AiChatMessages] ([UserId], [CreatedAt]);
            END
            """, cancellationToken);
    }
}
