using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services;
using server.Application.Services.Security;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Infrastructure;
using Operator = server.Domain.Entities.Operator;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// Seeds deterministic test data directly into the container-backed database and
/// issues matching tokens via the real <see cref="ITokenServices"/>. Every call uses
/// unique identifiers so tests can run side-by-side inside the same database without
/// colliding on unique constraints (email, <c>(UserId, BranchId)</c>, etc.).
/// </summary>
internal static class TestSeeder
{
    public const string DefaultPassword = "Password123";

    // Uncounted placeholder sessions created for seeded cashbox rows, keyed by cashbox and day.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Guid AccountId, DateTime Date), Guid>
        SeedPlaceholderSessions = new();

    // Seeded sessions handed to tests, refreshed after a chain renumber so a held Version/Sequence stays current.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, WeakReference<DailyClose>>
        SeededSessions = new();

    extension(ServerWebApplicationFactory factory)
    {
        public async Task<(User User, Branch Branch, BranchUser Membership, string Token)> SeedFullBranchContextAsync(
            string label,
            Role role = Role.Admin,
            DateTime? branchCreatedAt = null)
        {
            var user = await factory.SeedUserAsync();
            var branch = await factory.SeedBranchAsync($"{label} {Guid.NewGuid():N}", branchCreatedAt);
            var membership = await factory.SeedBranchUserAsync(user.Id, branch.Id, role);
            var token = factory.IssueBranchToken(membership);

            return (user, branch, membership, token);
        }

        public async Task<User> SeedUserAsync(string? email = null,
            string? name = null)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            var passwordEncryption = scope.ServiceProvider.GetRequiredService<PasswordEncryption>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"Test User {Guid.NewGuid():N}",
                Email = await scope.ServiceProvider.GetRequiredService<IUsersRepository>().NormalizeEmail(email ?? $"user-{Guid.NewGuid():N}@example.com"),
                Password = passwordEncryption.HashPassword(DefaultPassword)
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            return user;
        }

        // Test-owned bootstrap after the public create route is closed. It exercises the
        // same seed writer as operated onboarding without exposing another HTTP escape.
        public async Task<Branch> SeedConfiguredBranchAsync(string name, string? cnpj = null,
            string? address = null, string? phone = null)
        {
            using var scope = factory.Services.CreateScope();
            var branch = await scope.ServiceProvider.GetRequiredService<BranchBootstrap>().Create(new RequestCreateBranchJson
            { Name = name, Cnpj = cnpj, Address = address, Phone = phone });
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Commit();
            return branch;
        }

        private async Task<Branch> SeedBranchAsync(string? name = null, DateTime? createdAt = null)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var branch = new Branch
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"Branch {Guid.NewGuid():N}",
                CreatedAt = ServerWebApplicationFactory.AsUtc(createdAt ?? DateTime.UtcNow)
            };
            dbContext.Branches.Add(branch);
            await dbContext.SaveChangesAsync();
            return branch;
        }

        /// <summary>
        /// Seeds a standalone branch for cross-branch isolation tests where the caller only
        /// needs a different <see cref="Branch"/> id (no user/membership context).
        /// </summary>
        public Task<Branch> SeedBranchForOtherContextAsync(string? name = null)
        {
            return factory.SeedBranchAsync(name ?? $"Other Branch {Guid.NewGuid():N}");
        }

        public async Task<BranchUser> SeedBranchUserAsync(Guid userId,
            Guid branchId,
            Role role)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var branchUser = new BranchUser
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BranchId = branchId,
                Role = role
            };
            dbContext.BranchUsers.Add(branchUser);
            await dbContext.SaveChangesAsync();
            return branchUser;
        }

        /// <summary>
        /// Issues a real global JWT for the given user using the production
        /// <see cref="ITokenServices"/> wired into the factory.
        /// </summary>
        public string IssueGlobalToken(User user)
        {
            using var scope = factory.Services.CreateScope();
            var tokenServices = scope.ServiceProvider.GetRequiredService<ITokenServices>();
            return tokenServices.GenerateGlobalToken(user);
        }

        /// <summary>
        /// Issues a real branch-scoped JWT for the given membership using the production
        /// <see cref="ITokenServices"/> wired into the factory.
        /// </summary>
        public string IssueBranchToken(BranchUser branchUser)
        {
            using var scope = factory.Services.CreateScope();
            var tokenServices = scope.ServiceProvider.GetRequiredService<ITokenServices>();
            return tokenServices.GenerateBranchToken(branchUser);
        }

        public async Task<RefreshToken> SeedRefreshTokenAsync(Guid userId)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Value = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.RefreshTokens.Add(refreshToken);
            await dbContext.SaveChangesAsync();
            return refreshToken;
        }

        public async Task<Client> SeedClientAsync(
            Guid branchId,
            string? name = null,
            string? phone = null,
            string? cpf = null)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var client = new Client
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"Client {Guid.NewGuid():N}",
                Phone = phone ?? "11999999999",
                Cpf = cpf,
                BranchId = branchId
            };
            dbContext.Clients.Add(client);
            await dbContext.SaveChangesAsync();
            return client;
        }

        public async Task<Operator> SeedOperatorAsync(Guid branchId, string? name = null, Guid? userId = null, Guid? id = null, bool active = true)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            if (userId is { } linkedUserId)
            {
                var existingOperatorId = await dbContext.Operators
                    .AsNoTracking()
                    .Where(op =>
                        op.BranchId == branchId &&
                        op.UserId == linkedUserId &&
                        op.Active)
                    .Select(op => (Guid?)op.Id)
                    .FirstOrDefaultAsync();

                if (existingOperatorId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"SeedOperatorAsync fixture setup violated the active operator user-link invariant. " +
                        $"BranchId '{branchId}' and UserId '{linkedUserId}' are already linked to active OperatorId '{existingOperatorId}'. " +
                        "Deactivate the existing seeded operator, seed the new operator without a UserId, or drive the duplicate-link scenario through the API/use case.");
                }
            }

            var op = new Operator
            {
                Id = id ?? Guid.NewGuid(),
                Name = name ?? $"Operator {Guid.NewGuid():N}",
                Active = active,
                BranchId = branchId,
                UserId = userId
            };
            dbContext.Operators.Add(op);
            await dbContext.SaveChangesAsync();
            return op;
        }

        public async Task<Account> SeedAccountAsync(
            Guid branchId,
            AccountType type = AccountType.Terminal,
            string? name = null,
            string? institution = null,
            string? number = null,
            Guid? tabAccountId = null,
            bool active = true)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var account = new Account
            {
                Id = Guid.NewGuid(),
                Type = type,
                Name = name ?? $"Account {Guid.NewGuid():N}",
                Institution = institution,
                Number = number,
                BranchId = branchId,
                TabAccountId = tabAccountId,
                Active = active
            };

            // Every seeded cashbox has its physical CAIXA terminal (M9.6 Phase 2), so sessions can start on it.
            if (type == AccountType.Terminal)
            {
                var terminal = new Terminal { BranchId = branchId, Name = account.Name };
                dbContext.Terminals.Add(terminal);
                account.TerminalId = terminal.Id;
            }

            dbContext.Accounts.Add(account);
            await dbContext.SaveChangesAsync();
            return account;
        }

        public async Task<OperatorAccount> SeedOperatorAccountAsync(
            Guid operatorId,
            Guid accountId,
            bool isPrimary = false,
            bool active = true)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var operatorAccount = new OperatorAccount
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                AccountId = accountId,
                IsPrimary = isPrimary,
                Active = active
            };

            dbContext.OperatorAccounts.Add(operatorAccount);
            await dbContext.SaveChangesAsync();
            return operatorAccount;
        }

        public async Task<List<OperatorAccount>> ListOperatorAccountsByOperatorIdAsync(Guid operatorId)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            return await dbContext.OperatorAccounts
                .AsNoTracking()
                .Where(operatorAccount => operatorAccount.OperatorId == operatorId)
                .OrderBy(operatorAccount => operatorAccount.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<OperatorAccount>> ListOperatorAccountsByAccountIdAsync(Guid accountId)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            return await dbContext.OperatorAccounts
                .AsNoTracking()
                .Where(operatorAccount => operatorAccount.AccountId == accountId)
                .OrderBy(operatorAccount => operatorAccount.CreatedAt)
                .ToListAsync();
        }

        public async Task<Category> SeedCategoryAsync(
            Guid branchId,
            string? name = null,
            Direction defaultDirection = Direction.In)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"Category {Guid.NewGuid():N}",
                DefaultDirection = defaultDirection,
                BranchId = branchId
            };
            dbContext.Categories.Add(category);
            await dbContext.SaveChangesAsync();
            return category;
        }

        public async Task<TransactionType> SeedTransactionTypeAsync(
            Guid categoryId,
            string? name = null,
            SettlementRule settlementRule = SettlementRule.SameDay,
            bool requiresTabAccountAndClient = false,
            bool active = true,
            FiadoPaymentKind? fiadoPaymentKind = null,
            TransactionTypeSystemRole? systemRole = null,
            SettlementInstrument settlementInstrument = SettlementInstrument.None,
            MonthlyFinancialRole monthlyFinancialRole = MonthlyFinancialRole.None)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var transactionType = new TransactionType
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"TType {Guid.NewGuid():N}",
                CategoryId = categoryId,
                SettlementRule = settlementRule,
                RequiresTabAccountAndClient = requiresTabAccountAndClient,
                Active = active,
                FiadoPaymentKind = fiadoPaymentKind,
                SystemRole = systemRole,
                SettlementInstrument = settlementInstrument,
                MonthlyFinancialRole = monthlyFinancialRole
            };
            dbContext.TransactionTypes.Add(transactionType);
            await dbContext.SaveChangesAsync();
            return transactionType;
        }

        /// <summary>
        /// M7.7 Phase 5.6: the two system-owned counterpart types (and their categories) that every real branch gets
        /// from <c>CreateBranchSeedFactory</c> or the migration; seeded branches in tests start without them.
        /// </summary>
        public async Task<(TransactionType StockSale, TransactionType CashRepayment)> SeedFiadoSystemTypesAsync(Guid branchId)
        {
            var outCategory = await factory.SeedCategoryAsync(branchId, "Movimentos automáticos de fiado (saída)", Direction.Out);
            var inCategory = await factory.SeedCategoryAsync(branchId, "Movimentos automáticos de fiado (entrada)", Direction.In);
            var stockSale = await factory.SeedTransactionTypeAsync(
                outCategory.Id,
                "Saída de estoque contado — venda fiado",
                systemRole: TransactionTypeSystemRole.FiadoCountedStockSaleCounterpart);
            var cashRepayment = await factory.SeedTransactionTypeAsync(
                inCategory.Id,
                "Entrada de dinheiro — recebimento fiado",
                systemRole: TransactionTypeSystemRole.FiadoCashRepaymentCounterpart);
            return (stockSale, cashRepayment);
        }

        public async Task<Transaction> SeedTransactionAsync(
            Guid branchId,
            Guid accountId,
            Guid transactionTypeId,
            Guid categoryId,
            Direction direction,
            Guid recordedByOperatorId,
            Guid createdByUserId,
            DateTime? date = null,
            decimal value = 10m,
            string? description = null,
            TimeOnly? transactionTime = null,
            Guid? clientId = null,
            DateTime? dueDate = null,
            DateTime? paidAt = null,
            Guid? originTransactionId = null,
            TransactionStatus status = TransactionStatus.Active,
            DateTime? createdAt = null,
            Guid? id = null,
            bool active = true,
            Guid? sessionId = null,
            bool legacyUnsessioned = false)
        {
            var transactionDate = date ?? DateTime.Today;
            var boundSessionId = legacyUnsessioned
                ? null
                : sessionId ?? await factory.ResolveSeedSessionIdAsync(branchId, accountId, transactionDate.Date);

            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var transaction = new Transaction
            {
                Id = id ?? Guid.NewGuid(),
                Date = transactionDate,
                Value = value,
                Description = description ?? $"Transaction {Guid.NewGuid():N}",
                TransactionTime = transactionTime,
                TransactionTypeId = transactionTypeId,
                CategoryId = categoryId,
                Direction = direction,
                AccountId = accountId,
                ClientId = clientId,
                DueDate = dueDate ?? transactionDate,
                PaidAt = paidAt is null ? null : AsUtc(paidAt.Value),
                OriginTransactionId = originTransactionId,
                RecordedByOperatorId = recordedByOperatorId,
                CreatedByUserId = createdByUserId,
                Status = status,
                BranchId = branchId,
                Active = active,
                CreatedAt = ServerWebApplicationFactory.AsUtc(createdAt ?? DateTime.UtcNow),
                SessionId = boundSessionId
            };

            if (legacyUnsessioned)
                return await factory.SeedLegacyUnsessionedTransactionAsync(transaction);

            dbContext.Transactions.Add(transaction);
            await dbContext.SaveChangesAsync();
            return transaction;
        }

        public async Task<Setting> SeedSettingAsync(
            Guid branchId,
            DateTime? lockDate = null,
            decimal dailyTargetHours = 7.33m,
            decimal lunchDeductionOver6H = 1.0m,
            decimal lunchDeductionOver4H = 0.25m)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var setting = new Setting
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                LockDate = lockDate ?? DateTime.MinValue,
                DailyTargetHours = dailyTargetHours,
                LunchDeductionOver6H = lunchDeductionOver6H,
                LunchDeductionOver4H = lunchDeductionOver4H
            };
            dbContext.Settings.Add(setting);

            // Mirror the production invariant (seed factory / migration backfill): every
            // branch with a Setting also has the MinValue-dated initial TimeEntryPolicy
            // row carrying the same constants, so per-date resolution is total.
            dbContext.TimeEntryPolicies.Add(new TimeEntryPolicy
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                EffectiveFrom = DateTime.MinValue,
                DailyTargetHours = dailyTargetHours,
                LunchDeductionOver6H = lunchDeductionOver6H,
                LunchDeductionOver4H = lunchDeductionOver4H
            });
            await dbContext.SaveChangesAsync();
            return setting;
        }

        public async Task<TimeEntryPolicy> SeedTimeEntryPolicyAsync(
            Guid branchId,
            DateTime effectiveFrom,
            decimal dailyTargetHours = 7.33m,
            decimal lunchDeductionOver6H = 1.0m,
            decimal lunchDeductionOver4H = 0.25m)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var policy = new TimeEntryPolicy
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                EffectiveFrom = effectiveFrom,
                DailyTargetHours = dailyTargetHours,
                LunchDeductionOver6H = lunchDeductionOver6H,
                LunchDeductionOver4H = lunchDeductionOver4H
            };
            dbContext.TimeEntryPolicies.Add(policy);
            await dbContext.SaveChangesAsync();
            return policy;
        }

        public async Task<Product> SeedProductAsync(
            Guid branchId,
            string? name = null,
            int displayOrder = 1,
            bool active = true)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"Product {Guid.NewGuid():N}",
                DisplayOrder = displayOrder,
                BranchId = branchId,
                Active = active
            };
            dbContext.Products.Add(product);
            await dbContext.SaveChangesAsync();
            return product;
        }

        public async Task<DailyCloseItem> SeedDailyCloseItemAsync(
            Guid dailyCloseId,
            Guid productId,
            decimal value = 100m)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var item = new DailyCloseItem
            {
                Id = Guid.NewGuid(),
                DailyCloseId = dailyCloseId,
                ProductId = productId,
                Value = value
            };
            dbContext.DailyCloseItems.Add(item);
            await dbContext.SaveChangesAsync();
            return item;
        }

        public async Task<Holiday> SeedHolidayAsync(
            Guid branchId,
            DateTime date,
            string? description = null)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var holiday = new Holiday
            {
                Id = Guid.NewGuid(),
                Date = date,
                Description = description,
                BranchId = branchId
            };
            dbContext.Holidays.Add(holiday);
            await dbContext.SaveChangesAsync();
            return holiday;
        }

        public async Task<List<Holiday>> ListActiveHolidaysByBranchIdAndYearAsync(Guid branchId, int year)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            var start = new DateTime(year, 1, 1);
            var end = start.AddYears(1);

            return await dbContext.Holidays
                .AsNoTracking()
                .Where(holiday =>
                    holiday.BranchId == branchId &&
                    holiday.Active &&
                    holiday.Date >= start &&
                    holiday.Date < end)
                .OrderBy(holiday => holiday.Date)
                .ThenBy(holiday => holiday.Id)
                .ToListAsync();
        }

        public async Task<List<DailyCloseItem>> ListDailyCloseItemsByDailyCloseIdAsync(Guid dailyCloseId)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            return await dbContext.DailyCloseItems
                .AsNoTracking()
                .Where(item => item.DailyCloseId == dailyCloseId)
                .OrderBy(item => item.CreatedAt)
                .ToListAsync();
        }

        public async Task<DailyClose> SeedDailyCloseAsync(
            Guid branchId,
            Guid accountId,
            DateTime? date = null,
            DailyCloseStatus status = DailyCloseStatus.Draft,
            Guid? submittedByOperatorId = null,
            DateTime? submittedAt = null,
            Guid? approvedByUserId = null,
            DateTime? approvedAt = null,
            string? rejectionReason = null,
            string? notes = null,
            DateTime? createdAt = null,
            Guid? id = null,
            DateTime? itemsFirstRecordedAt = null,
            bool itemsRecorded = true,
            DateTime? openingRecheckRequiredAt = null,
            Guid? openingRecheckTriggeredByDailyCloseId = null,
            Guid? openingRecheckTriggeredByUserId = null,
            Guid? openedByUserId = null,
            Guid? recordedByUserId = null,
            Guid? recordedByOperatorId = null,
            Guid? submittedByUserId = null,
            bool active = true,
            SessionCustodyState? custodyState = null,
            Guid? custodianOperatorId = null,
            SessionMappingState mappingState = SessionMappingState.Ready)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            var sessionDate = (date ?? DateTime.Today).Date;

            // A cashbox row seeded before its close created an uncounted placeholder session for that day; the
            // explicit close takes over the placeholder's identity so those rows stay bound to it.
            var placeholderId = id is null && SeedPlaceholderSessions.TryRemove((accountId, sessionDate), out var reused)
                ? reused
                : (Guid?)null;

            var fallbackUserId = await dbContext.BranchUsers
                .AsNoTracking()
                .Where(branchUser => branchUser.BranchId == branchId && branchUser.Active)
                .OrderBy(branchUser => branchUser.CreatedAt)
                .ThenBy(branchUser => branchUser.Id)
                .Select(branchUser => (Guid?)branchUser.UserId)
                .FirstOrDefaultAsync()
                ?? await dbContext.Users
                    .AsNoTracking()
                    .OrderBy(user => user.CreatedAt)
                    .ThenBy(user => user.Id)
                    .Select(user => user.Id)
                    .FirstAsync();
            var effectiveOpenedByUserId = openedByUserId ?? fallbackUserId;
            DateTime? effectiveItemsFirstRecordedAt = itemsFirstRecordedAt is null
                ? itemsRecorded ? DateTime.UtcNow : null
                : AsUtc(itemsFirstRecordedAt.Value);
            Guid? effectiveRecordedByOperatorId = effectiveItemsFirstRecordedAt is null
                ? null
                : recordedByOperatorId ?? submittedByOperatorId;
            var recorderOperatorUserId = effectiveRecordedByOperatorId is null
                ? null
                : await dbContext.Operators
                    .AsNoTracking()
                    .Where(op => op.Id == effectiveRecordedByOperatorId)
                    .Select(op => op.UserId)
                    .FirstOrDefaultAsync();
            Guid? effectiveRecordedByUserId = effectiveItemsFirstRecordedAt is null
                ? null
                : recordedByUserId ?? recorderOperatorUserId ?? effectiveOpenedByUserId;
            var submitterOperatorUserId = submittedByOperatorId is null
                ? null
                : await dbContext.Operators
                    .AsNoTracking()
                    .Where(op => op.Id == submittedByOperatorId)
                    .Select(op => op.UserId)
                    .FirstOrDefaultAsync();

            // Only a cashbox holds sessions; a close seeded on a Tab/Bank account can only be unresolved legacy history.
            var accountType = await dbContext.Accounts
                .Where(account => account.Id == accountId)
                .Select(account => (AccountType?)account.Type)
                .FirstOrDefaultAsync();
            if (accountType is not AccountType.Terminal)
                mappingState = SessionMappingState.Unresolved;
            var isReady = mappingState == SessionMappingState.Ready;
            var custodianId = isReady
                ? custodianOperatorId ?? effectiveRecordedByOperatorId ?? submittedByOperatorId
                    ?? await ResolveSeedCustodianAsync(dbContext, branchId, accountId)
                : (Guid?)null;
            var latestDate = await dbContext.DailyCloses
                .Where(close => close.AccountId == accountId && close.MappingState == SessionMappingState.Ready)
                .MaxAsync(close => (DateTime?)close.Date);
            var effectiveCustodyState = isReady
                ? custodyState ?? (latestDate is null || sessionDate >= latestDate ? SessionCustodyState.Active : SessionCustodyState.Finished)
                : (SessionCustodyState?)null;
            var terminalId = await dbContext.Accounts
                .Where(account => account.Id == accountId)
                .Select(account => account.TerminalId)
                .FirstOrDefaultAsync();
            var startedAt = SeedSessionStart(sessionDate, 0);

            var dailyClose = new DailyClose
            {
                Id = placeholderId ?? id ?? Guid.NewGuid(),
                CreatedAt = AsUtc(createdAt ?? DateTime.UtcNow),
                BranchId = branchId,
                AccountId = accountId,
                Date = sessionDate,
                MappingState = mappingState,
                Origin = isReady ? SessionOrigin.Started : SessionOrigin.Migrated,
                CustodyState = effectiveCustodyState,
                CustodianOperatorId = custodianId,
                TerminalId = isReady ? terminalId : null,
                Sequence = isReady ? 900_000 : null,
                StartedAt = isReady ? startedAt : null,
                EndedAt = effectiveCustodyState is null or SessionCustodyState.Active ? null : startedAt.AddMinutes(1),
                CustodyHistoryComplete = isReady,
                Status = status,
                OpenedByUserId = effectiveOpenedByUserId,
                RecordedByUserId = effectiveRecordedByUserId,
                RecordedByOperatorId = effectiveRecordedByOperatorId,
                SubmittedByUserId = submittedByUserId ?? submitterOperatorUserId,
                SubmittedByOperatorId = submittedByOperatorId,
                SubmittedAt = submittedAt is null ? null : AsUtc(submittedAt.Value),
                ApprovedByUserId = approvedByUserId,
                ApprovedAt = approvedAt is null ? null : AsUtc(approvedAt.Value),
                RejectionReason = rejectionReason,
                Notes = notes,
                ItemsFirstRecordedAt = effectiveItemsFirstRecordedAt,
                OpeningRecheckRequiredAt = openingRecheckRequiredAt is null
                    ? null
                    : AsUtc(openingRecheckRequiredAt.Value),
                OpeningRecheckTriggeredByDailyCloseId = openingRecheckTriggeredByDailyCloseId,
                OpeningRecheckTriggeredByUserId = openingRecheckTriggeredByUserId,
                Active = active
            };

            // Seeding writes fixture state directly: the custody guards are bypassed only for this transaction while
            // the cashbox chain is renumbered into non-overlapping intervals in date order.
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            await dbContext.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica");
            if (placeholderId is { } placeholder)
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM \"DailyCloses\" WHERE \"Id\" = {placeholder}");
            }

            if (effectiveCustodyState == SessionCustodyState.Active)
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "DailyCloses"
                    SET "CustodyState" = 2, "EndedAt" = "StartedAt" + interval '1 minute'
                    WHERE "AccountId" = {accountId} AND "MappingState" = 0 AND "CustodyState" = 0
                    """);
            }

            dbContext.DailyCloses.Add(dailyClose);
            await dbContext.SaveChangesAsync();
            if (isReady)
                await RenumberSeedChainAsync(dbContext, accountId);
            await transaction.CommitAsync();
            SeededSessions[dailyClose.Id] = new WeakReference<DailyClose>(dailyClose);
            await RefreshSeededSessionsAsync(dbContext, accountId);
            return dailyClose;
        }

        /// <summary>
        /// The session a seeded cashbox row belongs to: the latest Ready session of that cashbox and day, else a new
        /// uncounted Draft placeholder session (the row's day had activity without a count). Tab/Bank rows get none.
        /// </summary>
        public async Task<Guid?> ResolveSeedSessionIdAsync(Guid branchId, Guid accountId, DateTime date)
        {
            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
                var type = await dbContext.Accounts
                    .Where(account => account.Id == accountId)
                    .Select(account => (AccountType?)account.Type)
                    .FirstOrDefaultAsync();
                if (type != AccountType.Terminal)
                    return null;

                var existing = await dbContext.DailyCloses
                    .Where(close =>
                        close.AccountId == accountId &&
                        close.Date == date &&
                        close.MappingState == SessionMappingState.Ready)
                    .OrderByDescending(close => close.Sequence)
                    .Select(close => (Guid?)close.Id)
                    .FirstOrDefaultAsync();
                if (existing is not null)
                    return existing;
            }

            var placeholder = await factory.SeedDailyCloseAsync(branchId, accountId, date, itemsRecorded: false);
            SeedPlaceholderSessions[(accountId, date)] = placeholder.Id;
            return placeholder.Id;
        }

        /// <summary>
        /// Reloads an entity from the database so tests can assert persisted state without
        /// relying on cached tracking from the seed calls.
        /// </summary>
        public async Task<TEntity?> ReloadAsync<TEntity>(Guid entityId)
            where TEntity : EntityBase
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            return await dbContext.Set<TEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Id == entityId);
        }

        private static DateTime SeedSessionStart(DateTime businessDate, int indexOnDay)
        {
            // Noon in São Paulo (UTC−3), two minutes apart per session of the same cashbox and day.
            return DateTime.SpecifyKind(businessDate.Date.AddHours(15).AddMinutes(2 * indexOnDay), DateTimeKind.Utc);
        }

        private static async Task<Guid> ResolveSeedCustodianAsync(ServerDbContext dbContext, Guid branchId, Guid accountId)
        {
            var granted = await dbContext.OperatorAccounts
                .Where(link => link.AccountId == accountId && link.Active && link.Operator.Active)
                .OrderByDescending(link => link.Operator.UserId != null)
                .ThenBy(link => link.CreatedAt)
                .Select(link => (Guid?)link.OperatorId)
                .FirstOrDefaultAsync();
            if (granted is { } grantedOperatorId)
                return grantedOperatorId;

            var anyOperator = await dbContext.Operators
                .Where(op => op.BranchId == branchId && op.Active)
                .OrderBy(op => op.CreatedAt)
                .Select(op => (Guid?)op.Id)
                .FirstOrDefaultAsync();
            if (anyOperator is { } operatorId)
                return operatorId;

            var custodian = new Operator { BranchId = branchId, Name = $"Custodian {Guid.NewGuid():N}" };
            dbContext.Operators.Add(custodian);
            await dbContext.SaveChangesAsync();
            return custodian.Id;
        }

        private static async Task RefreshSeededSessionsAsync(ServerDbContext dbContext, Guid accountId)
        {
            var rows = await dbContext.DailyCloses
                .AsNoTracking()
                .Where(close => close.AccountId == accountId)
                .Select(close => new { close.Id, close.Version, close.Sequence, close.CustodyState, close.StartedAt, close.EndedAt })
                .ToListAsync();
            foreach (var row in rows)
            {
                if (SeededSessions.TryGetValue(row.Id, out var reference) && reference.TryGetTarget(out var held))
                {
                    held.Version = row.Version;
                    held.Sequence = row.Sequence;
                    held.CustodyState = row.CustodyState;
                    held.StartedAt = row.StartedAt;
                    held.EndedAt = row.EndedAt;
                }
            }
        }

        /// <summary>
        /// A cashbox row without a session, as only the M9.6 legacy quarantine leaves behind (unresolved orphan activity).
        /// Written past the row guards on purpose; every other seeded cashbox row is bound to its session.
        /// </summary>
        public async Task<Transaction> SeedLegacyUnsessionedTransactionAsync(Transaction transaction)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            await using var dbTransaction = await dbContext.Database.BeginTransactionAsync();
            await dbContext.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica");
            transaction.SessionId = null;
            dbContext.Transactions.Add(transaction);
            await dbContext.SaveChangesAsync();
            await dbTransaction.CommitAsync();
            return transaction;
        }

        private static async Task RenumberSeedChainAsync(ServerDbContext dbContext, Guid accountId)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                WITH ordered AS (
                    SELECT "Id",
                           row_number() OVER (ORDER BY "Date", "CreatedAt", "Id") AS rn,
                           row_number() OVER (PARTITION BY "Date" ORDER BY "CreatedAt", "Id") - 1 AS j
                    FROM "DailyCloses"
                    WHERE "AccountId" = {accountId} AND "MappingState" = 0)
                UPDATE "DailyCloses" d
                SET "Sequence" = o.rn + 1000000,
                    "StartedAt" = (d."Date"::timestamp + interval '15 hours' + o.j * interval '2 minutes') AT TIME ZONE 'UTC',
                    "EndedAt" = CASE WHEN d."CustodyState" = 0 THEN NULL
                                     ELSE (d."Date"::timestamp + interval '15 hours' + o.j * interval '2 minutes' + interval '1 minute') AT TIME ZONE 'UTC' END
                FROM ordered o
                WHERE d."Id" = o."Id";

                UPDATE "DailyCloses" SET "Sequence" = "Sequence" - 1000000
                WHERE "AccountId" = {accountId} AND "MappingState" = 0;
                """);
        }

        private static DateTime AsUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}
