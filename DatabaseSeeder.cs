using CMCS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CMCS.Data
{
    public class DatabaseSeeder
    {
        public static async Task Initialize(AppDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            try
            {
                // First, ensure the database is created
                await context.Database.EnsureCreatedAsync();

                // Create roles table if it doesn't exist
                try
                {
                    // Check if AspNetRoles table exists, if not create it
                    var rolesTableExists = await context.Database.ExecuteSqlRawAsync(@"
                        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetRoles' AND xtype='U')
                        CREATE TABLE [AspNetRoles] (
                            [Id] nvarchar(450) NOT NULL,
                            [Name] nvarchar(256) NULL,
                            [NormalizedName] nvarchar(256) NULL,
                            [ConcurrencyStamp] nvarchar(max) NULL,
                            CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
                        )");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating roles table: {ex.Message}");
                }

                // Create other Identity tables if they don't exist
                await CreateIdentityTablesIfNotExist(context);

                // Create roles
                string[] roleNames = { "Lecturer", "Coordinator", "Manager", "HR" };
                foreach (var roleName in roleNames)
                {
                    var roleExist = await roleManager.RoleExistsAsync(roleName);
                    if (!roleExist)
                    {
                        await roleManager.CreateAsync(new IdentityRole(roleName));
                    }
                }

                // Check if users already exist in the legacy Users table
                if (!context.Users.Any())
                {
                    var legacyUsers = new[]
                    {
                        new User { UserId = 1, FirstName = "Liso", LastName = "Domingos", Email = "domingos05@university.ac.za", Password = "Liso123", Role = "Lecturer", IsActive = true, CreatedDate = DateTime.Now },
                        new User { UserId = 2, FirstName = "Sarah", LastName = "Van der Merve", Email = "sarahvdm@university.ac.za", Password = "Sarah123", Role = "Lecturer", IsActive = true, CreatedDate = DateTime.Now },
                        new User { UserId = 3, FirstName = "Michael", LastName = "Myers", Email = "myers@university.ac.za", Password = "Myers123", Role = "Coordinator", IsActive = true, CreatedDate = DateTime.Now },
                        new User { UserId = 4, FirstName = "Don", LastName = "Warren", Email = "donwarren@university.ac.za", Password = "Warren123", Role = "Manager", IsActive = true, CreatedDate = DateTime.Now },
                        new User { UserId = 5, FirstName = "Thando", LastName = "Nxumalo", Email = "t.nxumalo@university.ac.za", Password = "Nxumalo123", Role = "HR", IsActive = true, CreatedDate = DateTime.Now }
                    };

                    context.Users.AddRange(legacyUsers);
                    await context.SaveChangesAsync();
                }

                // Also create Identity users for authentication
                if (!userManager.Users.Any())
                {
                    var identityUsers = new[]
                    {
                        new { Id = "1", Email = "domingos05@university.ac.za", Password = "Liso123", FirstName = "Liso", LastName = "Domingos", Role = "Lecturer" },
                        new { Id = "2", Email = "sarahvdm@university.ac.za", Password = "Sarah123", FirstName = "Sarah", LastName = "Van der Merve", Role = "Lecturer" },
                        new { Id = "3", Email = "myers@university.ac.za", Password = "Myers123", FirstName = "Michael", LastName = "Myers", Role = "Coordinator" },
                        new { Id = "4", Email = "donwarren@university.ac.za", Password = "Warren123", FirstName = "Don", LastName = "Warren", Role = "Manager" },
                        new { Id = "5", Email = "t.nxumalo@university.ac.za", Password = "Nxumalo123", FirstName = "Thando", LastName = "Nxumalo", Role = "HR" }
                    };

                    foreach (var userInfo in identityUsers)
                    {
                        // Check if user already exists
                        var existingUser = await userManager.FindByIdAsync(userInfo.Id);
                        if (existingUser != null)
                        {
                            continue; // User already exists, skip creation
                        }

                        var user = new ApplicationUser
                        {
                            Id = userInfo.Id,
                            UserName = userInfo.Email,
                            Email = userInfo.Email,
                            FirstName = userInfo.FirstName,
                            LastName = userInfo.LastName,
                            Role = userInfo.Role,
                            EmailConfirmed = true
                            // REMOVED: IsActive and CreatedDate since they don't exist in ApplicationUser
                        };

                        var result = await userManager.CreateAsync(user, userInfo.Password);
                        if (result.Succeeded)
                        {
                            await userManager.AddToRoleAsync(user, userInfo.Role);
                            Console.WriteLine($"Created user: {userInfo.Email}");
                        }
                        else
                        {
                            // Log errors
                            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                            Console.WriteLine($"Failed to create user {userInfo.Email}: {errors}");
                        }
                    }
                }

                // Seed claims data
                if (!context.Claims.Any())
                {
                    var claims = new[]
                    {
                        new Claim { ClaimId = 1, LecturerId = 1, ClaimMonth = new DateTime(2024, 3, 1), TotalHours = 38.5m, HourlyRate = 250.00m, Amount = 9625.00m, Notes = "Monthly teaching hours for March", Status = "Submitted", SubmittedDate = new DateTime(2024, 3, 15, 9, 30, 0) },
                        new Claim { ClaimId = 2, LecturerId = 1, ClaimMonth = new DateTime(2024, 2, 1), TotalHours = 42.0m, HourlyRate = 250.00m, Amount = 10500.00m, Notes = "February teaching and marking", Status = "With Coordinator", SubmittedDate = new DateTime(2024, 2, 14, 14, 20, 0) },
                        new Claim { ClaimId = 3, LecturerId = 2, ClaimMonth = new DateTime(2024, 2, 1), TotalHours = 35.0m, HourlyRate = 275.00m, Amount = 9625.00m, Notes = "Advanced module teaching", Status = "With Manager", SubmittedDate = new DateTime(2024, 2, 16, 11, 15, 0) },
                        new Claim { ClaimId = 4, LecturerId = 1, ClaimMonth = new DateTime(2024, 1, 1), TotalHours = 40.0m, HourlyRate = 250.00m, Amount = 10000.00m, Notes = "January teaching hours", Status = "Approved", SubmittedDate = new DateTime(2024, 1, 15, 10, 0, 0), ApprovedDate = new DateTime(2024, 1, 20, 16, 45, 0), ApprovedBy = 3 }
                    };

                    context.Claims.AddRange(claims);
                    await context.SaveChangesAsync();
                }

                Console.WriteLine("Database seeding completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while seeding the database: {ex.Message}");
                // Don't throw the exception to allow the application to start
            }
        }

        private static async Task CreateIdentityTablesIfNotExist(AppDbContext context)
        {
            var createTablesSql = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetRoles' AND xtype='U')
                CREATE TABLE [AspNetRoles] (
                    [Id] nvarchar(450) NOT NULL,
                    [Name] nvarchar(256) NULL,
                    [NormalizedName] nvarchar(256) NULL,
                    [ConcurrencyStamp] nvarchar(max) NULL,
                    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUsers' AND xtype='U')
                CREATE TABLE [AspNetUsers] (
                    [Id] nvarchar(450) NOT NULL,
                    [UserName] nvarchar(256) NULL,
                    [NormalizedUserName] nvarchar(256) NULL,
                    [Email] nvarchar(256) NULL,
                    [NormalizedEmail] nvarchar(256) NULL,
                    [EmailConfirmed] bit NOT NULL,
                    [PasswordHash] nvarchar(max) NULL,
                    [SecurityStamp] nvarchar(max) NULL,
                    [ConcurrencyStamp] nvarchar(max) NULL,
                    [PhoneNumber] nvarchar(max) NULL,
                    [PhoneNumberConfirmed] bit NOT NULL,
                    [TwoFactorEnabled] bit NOT NULL,
                    [LockoutEnd] datetimeoffset NULL,
                    [LockoutEnabled] bit NOT NULL,
                    [AccessFailedCount] int NOT NULL,
                    [FirstName] nvarchar(100) NULL,
                    [LastName] nvarchar(100) NULL,
                    [Role] nvarchar(20) NULL,
                    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetRoleClaims' AND xtype='U')
                CREATE TABLE [AspNetRoleClaims] (
                    [Id] int NOT NULL IDENTITY,
                    [RoleId] nvarchar(450) NOT NULL,
                    [ClaimType] nvarchar(max) NULL,
                    [ClaimValue] nvarchar(max) NULL,
                    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUserClaims' AND xtype='U')
                CREATE TABLE [AspNetUserClaims] (
                    [Id] int NOT NULL IDENTITY,
                    [UserId] nvarchar(450) NOT NULL,
                    [ClaimType] nvarchar(max) NULL,
                    [ClaimValue] nvarchar(max) NULL,
                    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUserLogins' AND xtype='U')
                CREATE TABLE [AspNetUserLogins] (
                    [LoginProvider] nvarchar(450) NOT NULL,
                    [ProviderKey] nvarchar(450) NOT NULL,
                    [ProviderDisplayName] nvarchar(max) NULL,
                    [UserId] nvarchar(450) NOT NULL,
                    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
                    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUserRoles' AND xtype='U')
                CREATE TABLE [AspNetUserRoles] (
                    [UserId] nvarchar(450) NOT NULL,
                    [RoleId] nvarchar(450) NOT NULL,
                    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
                    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUserTokens' AND xtype='U')
                CREATE TABLE [AspNetUserTokens] (
                    [UserId] nvarchar(450) NOT NULL,
                    [LoginProvider] nvarchar(450) NOT NULL,
                    [Name] nvarchar(450) NOT NULL,
                    [Value] nvarchar(max) NULL,
                    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
                    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                );";

            try
            {
                await context.Database.ExecuteSqlRawAsync(createTablesSql);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating Identity tables: {ex.Message}");
            }
        }
    }
}