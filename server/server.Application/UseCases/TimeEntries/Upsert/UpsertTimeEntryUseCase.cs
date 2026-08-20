using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Operator = server.Domain.Entities.Operator;

namespace server.Application.UseCases.TimeEntries.Upsert;

/// <summary>
/// Single PUT /timeentry write path implementing the Phase 3.6 dual-shape contract.
/// <para>
/// Members send <c>{ Action: Open | Close }</c> with no Segments; the server stamps
/// every clock from the branch-local now and treats both Open-when-open and
/// Close-when-closed as idempotent no-ops (no commit, no audit advance) so dropped-
/// response retries always converge.
/// </para>
/// <para>
/// Manager / Admin send <c>{ Segments: [...] }</c> with explicit DateTime clocks and
/// no Action. Reconciliation by Id: missing persisted segments → 400; ClockIn changes
/// on existing segments → 400, use the granular PATCH endpoint to correct a wrong ClockIn;
/// removals must use the granular DELETE endpoint . Removals likewise must use
/// the granular DELETE endpoint.
/// </para>
/// <para>
/// The validator (<see cref="UpsertTimeEntryFluentValidation"/>) enforces only the
/// role-independent shape; this use case owns every Role × Action × Segments
/// combination via <see cref="EnsureRoleShape"/>. <see cref="PersistAsync"/> is the
/// single commit chokepoint and the only place that touches
/// <see cref="ITimeEntryCalculationService"/>.
/// </para>
/// </summary>
public class UpsertTimeEntryUseCase(
    IAuthenticationService authenticationService,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    IOperatorsRepository operatorsRepository,
    ITimeEntriesRepository timeEntriesRepository,
    ITimeEntrySegmentsRepository timeEntrySegmentsRepository,
    ISettingsRepository settingsRepository,
    IHolidaysRepository holidaysRepository,
    ITimeEntryWritePermissionGuard permissionGuard,
    ITimeEntryCalculationService calculationService,
    IBranchClock branchClock,
    LockDateGuard lockDateGuard,
    IMonthLockCoordination monthLockCoordination,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Orchestrates a PUT /timeentry upsert. Runs shape validation
    /// (<see cref="Validate"/>) first, then resolves the authenticated branch user,
    /// loads the target operator and any existing TimeEntry for (BranchId, OperatorId, Date),
    /// evaluates the write permission outcome (with a carve-out via
    /// <see cref="CanAllowPriorDayMemberClose"/> for overnight Close on a prior-day row),
    /// then dispatches to either the Member-tap branch (Open/Close action) or the
    /// Admin-snapshot branch (Segments list). Runs the shape contract
    /// (<see cref="EnsureRoleShape"/>) and calendar pre-checks
    /// (<see cref="EnsureCalendarStatusRulesAsync"/>) before dispatch. The persistence
    /// chokepoint <see cref="PersistAsync"/> owns the single commit.
    /// </summary>
    /// <param name="request">
    /// The upsert payload. Either <c>Action</c> (Member shape) or <c>Segments</c>
    /// (Admin shape) is set; never both. Mutual exclusion is enforced by
    /// <see cref="EnsureRoleShape"/> after authentication.
    /// </param>
    /// <returns>
    /// The persisted (or no-op) TimeEntry projected to <see cref="ResponseTimeEntryJson"/>,
    /// including server-computed <c>TotalHours</c>, <c>BalanceHours</c>, and
    /// <c>IsInProgress</c>.
    /// </returns>
    /// <exception cref="OnValidationException">
    /// Shape, role, segment, or calendar validation failure (400).
    /// </exception>
    /// <exception cref="NotFoundException">
    /// <c>request.OperatorId</c> does not match an active operator on the caller's branch (404).
    /// </exception>
    /// <exception cref="ConflictException">
    /// Status change from Present to non-Present while active segments exist (409).
    /// </exception>
    /// <exception cref="TokenWithoutPermissionException">
    /// Permission guard denies the writing (403).
    /// </exception>
    public async Task<ResponseTimeEntryJson> Execute(
        RequestUpsertTimeEntryJson request,
        CancellationToken ct = default)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        await using var coordination = await monthLockCoordination.TryAcquireShared(branchUser.BranchId, ct)
            ?? throw new ConflictException(ResourcesErrorMessages.SETTING_LOCK_MONTH_COORDINATION_BUSY);

        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);
        var callerOperator = memberScope.LinkedOperator;

        var targetOperator = await operatorsRepository
            .GetActiveByIdAndBranchIdAsNoTracking(request.OperatorId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TIMEENTRY_OPERATOR_NOT_FOUND);

        var utcNow = branchClock.UtcNow();
        var existing = await timeEntriesRepository
            .GetByBranchIdOperatorIdAndDate(branchUser.BranchId, request.OperatorId, request.Date);

        var outcome = permissionGuard.Evaluate(
            branchUser,
            callerOperator,
            request.OperatorId,
            request.Date,
            request.Status,
            utcNow);

        if (CanAllowPriorDayMemberClose(request, branchUser, outcome, existing, utcNow) is false)
            ApplyPermissionOutcome(outcome);

        EnsureRoleShape(request, branchUser.Role);
        await EnsureCalendarStatusRulesAsync(request.Status, request.Date, branchUser.BranchId);
        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            request.Date,
            ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED,
            ct);

        var response = branchUser.Role is Role.Member
            ? await ExecuteMemberTapAsync(request, branchUser, targetOperator, existing, ct)
            : await ExecuteAdminSnapshotAsync(request, branchUser, targetOperator, existing, ct);

        await coordination.Complete(ct);
        return response;
    }

    /// <summary>
    /// Member-shape dispatcher. Rejects non-Present status (Members may only write Present
    /// entries via the tap protocol), captures the current UTC and branch-local timestamps
    /// once, then dispatches to <see cref="ExecuteMemberOpenAsync"/> or
    /// <see cref="ExecuteMemberCloseAsync"/> based on <c>request.Action</c>.
    /// </summary>
    /// <param name="request">The upsert payload; <c>Action</c> is non-null at this point.</param>
    /// <param name="branchUser">The authenticated branch membership of the caller.</param>
    /// <param name="targetOperator">The operator the entry is being written for (already validated as active and on the same branch).</param>
    /// <param name="existing">The currently persisted TimeEntry for (BranchId, OperatorId, Date), or <c>null</c> when none exists.</param>
    /// <returns>The persisted or no-op response.</returns>
    /// <exception cref="OnValidationException">
    /// <c>request.Status</c> is not Present (TIMEENTRY_NON_PRESENT_REJECTS_SEGMENTS).
    /// </exception>
    private async Task<ResponseTimeEntryJson> ExecuteMemberTapAsync(
        RequestUpsertTimeEntryJson request,
        BranchUser branchUser,
        Operator targetOperator,
        TimeEntry? existing,
        CancellationToken ct)
    {
        if (request.Status is not TimeEntryStatus.Present)
            throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_NON_PRESENT_REJECTS_SEGMENTS]);

        var utcNow = branchClock.UtcNow();
        var branchLocalNow = branchClock.LocalBusinessDateTime(utcNow);

        return request.Action!.Value switch
        {
            TimeEntryTapAction.Open => await ExecuteMemberOpenAsync(
                request,
                branchUser,
                targetOperator,
                existing,
                utcNow,
                branchLocalNow,
                ct),
            TimeEntryTapAction.Close => await ExecuteMemberCloseAsync(
                request,
                branchUser,
                targetOperator,
                existing,
                utcNow,
                branchLocalNow,
                ct),
            _ => throw new InvalidOperationException($"Unexpected {nameof(TimeEntryTapAction)}: {request.Action}")
        };
    }

    /// <summary>
    /// Member Open branch. Three outcomes:
    /// (1) No existing entry → create parent + new open segment, persist.
    /// (2) Existing entry has an active open segment → idempotent no-op, return current
    ///     state without commit (covers dropped-response retries).
    /// (3) Existing entry is all-closed → append a new open segment, persist.
    /// </summary>
    /// <param name="request">The upsert payload.</param>
    /// <param name="branchUser">The authenticated branch user.</param>
    /// <param name="targetOperator">The operator the entry is being written for.</param>
    /// <param name="existing">The currently persisted entry, or <c>null</c>.</param>
    /// <param name="utcNow">UTC instant captured by the caller for consistent audit stamps.</param>
    /// <param name="branchLocalNow">Branch-local timestamp derived from <paramref name="utcNow"/>; used as the new segment's <c>ClockIn</c>.</param>
    /// <returns>The persisted entry, or the unchanged existing entry on idempotent no-op.</returns>
    private async Task<ResponseTimeEntryJson> ExecuteMemberOpenAsync(
        RequestUpsertTimeEntryJson request,
        BranchUser branchUser,
        Operator targetOperator,
        TimeEntry? existing,
        DateTime utcNow,
        DateTime branchLocalNow,
        CancellationToken ct)
    {
        if (existing is null)
        {
            var entry = CreateParent(request, branchUser.BranchId, utcNow);
            AddSegment(entry, branchLocalNow, null, utcNow);

            return await PersistAsync(
                entry,
                targetOperator.Name,
                branchUser,
                utcNow,
                branchLocalNow,
                addNew: true,
                ct: ct);
        }

        if (ActiveSegments(existing).Any(segment => segment.ClockOut is null))
            return existing.ToResponse(targetOperator.Name);

        var segment = AddSegment(existing, branchLocalNow, null, utcNow);
        await timeEntrySegmentsRepository.Add(segment);

        return await PersistAsync(
            existing,
            targetOperator.Name,
            branchUser,
            utcNow,
            branchLocalNow,
            addNew: false,
            ct: ct);
    }

    /// <summary>
    /// Member Close branch. Targets the TimeEntry indicated by <c>request.Date</c>:
    /// (1) No existing entry → synthetic empty no-op response (idempotent Close-with-nothing-to-close).
    /// (2) Existing entry but no open segment → idempotent no-op, returns current state.
    /// (3) Existing entry with one open segment → stamps <c>ClockOut = branchLocalNow</c>
    ///     and segment-level audit, then persists.
    /// Overnight vs forgotten-close is decided by the client's choice of <c>request.Date</c>;
    /// the server does not scan other dates to infer intent. See <c>loto-specs.md</c> §6.7.
    /// </summary>
    /// <param name="request">The upsert payload.</param>
    /// <param name="branchUser">The authenticated branch user.</param>
    /// <param name="targetOperator">The operator the entry is being written for.</param>
    /// <param name="existing">The currently persisted entry, or <c>null</c>.</param>
    /// <param name="utcNow">UTC instant for audit stamps.</param>
    /// <param name="branchLocalNow">Branch-local timestamp used as the segment's <c>ClockOut</c>.</param>
    /// <returns>The persisted entry, an unchanged existing entry, or an empty synthetic response on no-op.</returns>
    private async Task<ResponseTimeEntryJson> ExecuteMemberCloseAsync(
        RequestUpsertTimeEntryJson request,
        BranchUser branchUser,
        Operator targetOperator,
        TimeEntry? existing,
        DateTime utcNow,
        DateTime branchLocalNow,
        CancellationToken ct)
    {
        if (existing is null)
            return EmptyNoopResponse(request, targetOperator, branchUser.BranchId);

        // Close targets request.Date. Overnight vs forgotten-close is a client-routing decision; see loto-specs.md §6.7.
        var openSegment = ActiveSegments(existing).FirstOrDefault(segment => segment.ClockOut is null);
        if (openSegment is null)
            return existing.ToResponse(targetOperator.Name);

        openSegment.ClockOut = branchLocalNow;
        openSegment.UpdatedAt = utcNow;
        openSegment.UpdatedByUserId = branchUser.UserId;

        return await PersistAsync(
            existing,
            targetOperator.Name,
            branchUser,
            utcNow,
            branchLocalNow,
            addNew: false,
            ct: ct);
    }

    /// <summary>
    /// Manager/Admin snapshot branch. Validates non-Present statuses do not carry segments
    /// and do not collide with persisted active segments, then delegates the reconciliation
    /// (insert new / mutate ClockOut / reject ClockIn edits / reject removals) to
    /// <see cref="ReconcileAdminSnapshot"/>. Newly inserted segments are explicitly staged
    /// on the segment repository only when the parent already exists; on first creation,
    /// the cascade through <c>entry.Segments.Add</c> in <see cref="AddSegment"/> is sufficient.
    /// </summary>
    /// <param name="request">The upsert payload; <c>Segments</c> is non-null at this point.</param>
    /// <param name="branchUser">The authenticated branch user.</param>
    /// <param name="targetOperator">The operator the entry is being written for.</param>
    /// <param name="existing">The currently persisted entry, or <c>null</c>.</param>
    /// <returns>The persisted entry projected to a response.</returns>
    /// <exception cref="OnValidationException">
    /// Non-Present status carries non-empty segments (TIMEENTRY_NON_PRESENT_REJECTS_SEGMENTS).
    /// </exception>
    /// <exception cref="ConflictException">
    /// Status changes from Present to non-Present while persisted active segments exist
    /// (TIMEENTRY_STATUS_CHANGE_REQUIRES_SEGMENT_CLEANUP, 409).
    /// </exception>
    private async Task<ResponseTimeEntryJson> ExecuteAdminSnapshotAsync(
        RequestUpsertTimeEntryJson request,
        BranchUser branchUser,
        Operator targetOperator,
        TimeEntry? existing,
        CancellationToken ct)
    {
        var payloadSegments = request.Segments ?? [];

        if (request.Status is not TimeEntryStatus.Present && payloadSegments.Count is not 0)
            throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_NON_PRESENT_REJECTS_SEGMENTS]);

        if (request.Status is not TimeEntryStatus.Present && existing is not null && ActiveSegments(existing).Any())
            throw new ConflictException(ResourcesErrorMessages.TIMEENTRY_STATUS_CHANGE_REQUIRES_SEGMENT_CLEANUP);

        var utcNow = branchClock.UtcNow();
        var branchLocalNow = branchClock.LocalBusinessDateTime(utcNow);
        var entry = existing ?? CreateParent(request, branchUser.BranchId, utcNow);
        entry.Status = request.Status;

        var insertedSegments = ReconcileAdminSnapshot(entry, payloadSegments, utcNow, branchUser.UserId);
        if (existing is null)
            return await PersistAsync(
                entry,
                targetOperator.Name,
                branchUser,
                utcNow,
                branchLocalNow,
                addNew: true,
                ct: ct);
        
        foreach (var insertedSegment in insertedSegments)
            await timeEntrySegmentsRepository.Add(insertedSegment);

        return await PersistAsync(
            entry,
            targetOperator.Name,
            branchUser,
            utcNow,
            branchLocalNow,
            addNew: false,
            ct: ct);
    }

    /// <summary>
    /// Reconciles the persisted active segment set on <paramref name="entry"/> against the
    /// payload snapshot. For each payload segment: inserts when <c>Id</c> is null, mutates
    /// only <c>ClockOut</c> when the persisted twin's <c>ClockIn</c> matches, and skips when
    /// nothing changed (avoids audit churn). Removals must go through the granular DELETE
    /// endpoint; missing-from-payload persisted segments are rejected.
    /// </summary>
    /// <param name="entry">The tracked parent entry. Mutated in place.</param>
    /// <param name="payloadSegments">The full segment snapshot supplied by the caller.</param>
    /// <param name="utcNow">UTC instant for audit stamps on inserted or mutated segments.</param>
    /// <param name="updatedByUserId">The caller's user id, recorded on any mutation.</param>
    /// <returns>
    /// The list of segments inserted during reconciliation. The caller is responsible for
    /// staging them on the segment repository when the parent already exists.
    /// </returns>
    /// <exception cref="OnValidationException">
    /// Persisted active segment missing from the payload (TIMEENTRY_SEGMENT_NOT_FOUND);
    /// payload segment with an unknown id (TIMEENTRY_SEGMENT_NOT_FOUND); attempt to change
    /// an existing segment's <c>ClockIn</c> (TIMEENTRY_SEGMENT_CLOCK_IN_LOCKED).
    /// </exception>
    private static List<TimeEntrySegment> ReconcileAdminSnapshot(
        TimeEntry entry,
        IReadOnlyList<RequestTimeEntrySegmentJson> payloadSegments,
        DateTime utcNow,
        Guid updatedByUserId)
    {
        var insertedSegments = new List<TimeEntrySegment>();
        var activeSegments = ActiveSegments(entry).ToList();
        var payloadIds = payloadSegments
            .Where(segment => segment.Id.HasValue)
            .Select(segment => segment.Id!.Value)
            .ToHashSet();

        if (activeSegments.Any(segment => payloadIds.Contains(segment.Id) is false))
            throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_NOT_FOUND]);

        foreach (var payloadSegment in payloadSegments)
        {
            var payloadClockIn = AsUnspecified(payloadSegment.ClockIn);
            var payloadClockOut = AsUnspecified(payloadSegment.ClockOut);

            if (payloadSegment.Id is null)
            {
                insertedSegments.Add(AddSegment(entry, payloadClockIn, payloadClockOut, utcNow));
                continue;
            }

            var persisted = activeSegments.FirstOrDefault(segment => segment.Id == payloadSegment.Id.Value)
                ?? throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_NOT_FOUND]);

            if (persisted.ClockIn != payloadClockIn)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_IN_LOCKED]);

            if (persisted.ClockOut == payloadClockOut)
                continue;

            persisted.ClockOut = payloadClockOut;
            persisted.UpdatedAt = utcNow;
            persisted.UpdatedByUserId = updatedByUserId;
        }

        return insertedSegments;
    }

    /// <summary>
    /// Terminal write chokepoint. Validates segment invariants, loads branch settings,
    /// recomputes <c>TotalHours</c>/<c>BalanceHours</c> via
    /// <see cref="ITimeEntryCalculationService.Calculate"/>, stamps parent audit fields,
    /// attaches the parent to the repository when <paramref name="addNew"/> is true, and
    /// commits exactly once through the unit of work.
    /// </summary>
    /// <param name="entry">The TimeEntry to persist (tracked when <paramref name="addNew"/> is false).</param>
    /// <param name="operatorName">Operator display name, projected into the response.</param>
    /// <param name="branchUser">The authenticated branch user; supplies <c>UpdatedByUserId</c>.</param>
    /// <param name="utcNow">UTC instant for parent audit stamps.</param>
    /// <param name="branchLocalNow">Branch-local timestamp for the calc service (drives live-running for any open segment on the current day).</param>
    /// <param name="addNew">When <c>true</c>, the parent is added to the repository before commit.</param>
    /// <returns>The persisted entry projected to <see cref="ResponseTimeEntryJson"/>.</returns>
    /// <exception cref="OnValidationException">
    /// Segment invariant violation surfaced by <see cref="EnsureSegmentRules"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Branch <c>Setting</c> row missing — system invariant breach, not a user error.
    /// </exception>
    private async Task<ResponseTimeEntryJson> PersistAsync(
        TimeEntry entry,
        string operatorName,
        BranchUser branchUser,
        DateTime utcNow,
        DateTime branchLocalNow,
        bool addNew,
        CancellationToken ct)
    {
        EnsureSegmentRules(entry);

        var setting = await settingsRepository.GetByBranchIdAsNoTracking(branchUser.BranchId, ct)
            ?? throw new InvalidOperationException($"Setting row missing for branch {branchUser.BranchId}.");

        var (totalHours, balanceHours) = calculationService.Calculate(
            entry.Status,
            ActiveSegments(entry)
                .Select(segment => new TimeEntrySegmentInput(segment.ClockIn, segment.ClockOut))
                .ToList(),
            entry.Date,
            branchLocalNow,
            setting.DailyTargetHours,
            setting.LunchDeductionOver6H,
            setting.LunchDeductionOver4H);

        entry.TotalHours = totalHours;
        entry.BalanceHours = balanceHours;
        entry.UpdatedAt = utcNow;
        entry.UpdatedByUserId = branchUser.UserId;

        if (addNew)
            await timeEntriesRepository.Add(entry);

        await unitOfWork.Commit(ct);

        return entry.ToResponse(operatorName);
    }

    /// <summary>
    /// Enforces the dual-shape contract after authentication so the role is known:
    /// Member must supply <c>Action</c> and must not supply <c>Segments</c>; Manager/Admin
    /// must supply <c>Segments</c> and must not supply <c>Action</c>. Each role × shape
    /// combination produces a role-appropriate error key.
    /// </summary>
    /// <param name="request">The upsert payload.</param>
    /// <param name="role">The caller's role on the target branch.</param>
    /// <exception cref="OnValidationException">
    /// TIMEENTRY_MEMBER_SHOULD_NOT_SEND_SEGMENTS, TIMEENTRY_MEMBER_TAP_ACTION_REQUIRED,
    /// TIMEENTRY_ADMIN_SHOULD_NOT_SEND_TAP_ACTION, or TIMEENTRY_ADMIN_REQUIRES_SEGMENTS,
    /// depending on which constraint is broken.
    /// </exception>
    private static void EnsureRoleShape(RequestUpsertTimeEntryJson request, Role role)
    {
        if (role is Role.Member)
        {
            if (request.Segments is not null)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_MEMBER_SHOULD_NOT_SEND_SEGMENTS]);

            if (request.Action is null)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_MEMBER_TAP_ACTION_REQUIRED]);

            return;
        }

        if (request.Action is not null)
            throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_ADMIN_SHOULD_NOT_SEND_TAP_ACTION]);

        if (request.Segments is null)
            throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_ADMIN_REQUIRES_SEGMENTS]);
    }

    /// <summary>
    /// Validates the post-mutation segment set on the parent: required <c>ClockIn</c>,
    /// day-bounds (<c>ClockIn ∈ [Date, Date + 1 day)</c>), <c>ClockOut &gt; ClockIn</c>
    /// when set, span ≤ 24h, at most one open segment (and if present it must be last
    /// by ClockIn ASC), and no overlap between consecutive segments.
    /// </summary>
    /// <param name="entry">The parent entry whose active segments will be checked.</param>
    /// <exception cref="OnValidationException">
    /// One of: TIMEENTRY_SEGMENT_CLOCK_IN_REQUIRED, TIMEENTRY_SEGMENT_OUT_OF_DAY_BOUNDS,
    /// TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN, TIMEENTRY_MULTIPLE_OPEN_SEGMENTS,
    /// TIMEENTRY_SEGMENTS_OVERLAP.
    /// </exception>
    private static void EnsureSegmentRules(TimeEntry entry)
    {
        var segments = ActiveSegments(entry).ToList();
        var dayStart = entry.Date.Date;
        var dayEnd = dayStart.AddDays(1);

        foreach (var segment in segments)
        {
            if (segment.ClockIn == default)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_IN_REQUIRED]);

            if (segment.ClockIn < dayStart || segment.ClockIn >= dayEnd)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_OUT_OF_DAY_BOUNDS]);

            if (segment.ClockOut is not { } clockOut)
                continue;

            if (clockOut <= segment.ClockIn)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN]);

            if (clockOut - segment.ClockIn > TimeSpan.FromHours(24))
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_OUT_OF_DAY_BOUNDS]);
        }

        if (segments.Count(segment => segment.ClockOut is null) > 1)
            throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_MULTIPLE_OPEN_SEGMENTS]);

        for (var index = 0; index < segments.Count - 1; index++)
        {
            var current = segments[index];
            var next = segments[index + 1];

            if (current.ClockOut is null)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_MULTIPLE_OPEN_SEGMENTS]);

            if (current.ClockOut > next.ClockIn)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENTS_OVERLAP]);
        }
    }

    /// <summary>
    /// Pre-flight calendar checks tied to the requested <see cref="TimeEntryStatus"/>.
    /// Sunday status requires <c>date.DayOfWeek == Sunday</c>; Holiday status requires
    /// an active <c>Holiday</c> row for <paramref name="branchId"/> on
    /// <paramref name="date"/>. All other statuses pass without I/O.
    /// </summary>
    /// <param name="status">The requested status.</param>
    /// <param name="date">The entry date (the branch-local business day).</param>
    /// <param name="branchId">The caller's branch id.</param>
    /// <exception cref="OnValidationException">
    /// TIMEENTRY_SUNDAY_REQUIRES_SUNDAY_DATE or TIMEENTRY_HOLIDAY_REQUIRES_ACTIVE_HOLIDAY.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="status"/> is outside the known enum set — system bug, not user input.
    /// </exception>
    private async Task EnsureCalendarStatusRulesAsync(TimeEntryStatus status, DateTime date, Guid branchId)
    {
        switch (status)
        {
            case TimeEntryStatus.Sunday when date.DayOfWeek is not DayOfWeek.Sunday:
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SUNDAY_REQUIRES_SUNDAY_DATE]);
            case TimeEntryStatus.Holiday:
            {
                var holiday = await holidaysRepository.GetActiveByBranchIdAndDate(branchId, date);
                if (holiday is null)
                    throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_HOLIDAY_REQUIRES_ACTIVE_HOLIDAY]);

                break;
            }
            case TimeEntryStatus.Sunday:
            case TimeEntryStatus.Present:
            case TimeEntryStatus.DayOff:
            case TimeEntryStatus.Vacation:
            case TimeEntryStatus.JustifiedAbsence:
            case TimeEntryStatus.UnjustifiedAbsence:
                break;
            default:
                throw new InvalidOperationException($"Unexpected {nameof(TimeEntryStatus)}: {status}");
        }
    }

    /// <summary>
    /// Carve-out gate for the overnight-Close-on-prior-day-row rule. Returns <c>true</c>
    /// only when every condition holds: permission outcome is <c>OlderDayMember</c>; caller
    /// is a Member; the request is a Present Close tap with no segments; the existing entry
    /// has exactly one open segment; and <c>branchLocalNow</c> is after the open segment's
    /// <c>ClockIn</c> and within 24 h of it. See <c>loto-specs.md</c> §6.7.
    /// </summary>
    /// <param name="request">The upsert payload.</param>
    /// <param name="branchUser">The authenticated branch user.</param>
    /// <param name="outcome">The permission guard outcome.</param>
    /// <param name="existing">The currently persisted entry, or <c>null</c>.</param>
    /// <param name="utcNow">UTC instant used to derive <c>branchLocalNow</c> for the 24h window check.</param>
    /// <returns><c>true</c> if the caller should bypass the prior-day-member denial; <c>false</c> otherwise.</returns>
    private bool CanAllowPriorDayMemberClose(
        RequestUpsertTimeEntryJson request,
        BranchUser branchUser,
        TimeEntryWritePermissionOutcome outcome,
        TimeEntry? existing,
        DateTime utcNow)
    {
        if (outcome is not TimeEntryWritePermissionOutcome.OlderDayMember ||
            branchUser.Role is not Role.Member ||
            request is not { Status: TimeEntryStatus.Present, Action: TimeEntryTapAction.Close, Segments: null } ||
            existing is null)
        {
            return false;
        }

        var openSegments = ActiveSegments(existing)
            .Where(segment => segment.ClockOut is null)
            .ToList();

        if (openSegments.Count is not 1)
            return false;

        var branchLocalNow = branchClock.LocalBusinessDateTime(utcNow);
        var openSegment = openSegments.Single();

        return branchLocalNow > openSegment.ClockIn &&
            branchLocalNow - openSegment.ClockIn <= TimeSpan.FromHours(24);
    }

    /// <summary>
    /// Translates a <see cref="TimeEntryWritePermissionOutcome"/> into the appropriate
    /// exception or pass-through. <c>Elevated</c> and <c>SelfSameDayPresent</c> return
    /// silently; every other outcome throws a 403 with a specific resource key.
    /// </summary>
    /// <param name="outcome">The permission guard outcome.</param>
    /// <exception cref="TokenWithoutPermissionException">
    /// TIMEENTRY_REQUIRES_OPERATOR_LINK, TIMEENTRY_NOT_OWN_OPERATOR,
    /// TIMEENTRY_WRITE_REQUIRES_SAME_DAY, or TIMEENTRY_MEMBER_ONLY_PRESENT,
    /// depending on the outcome.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Unknown outcome — system bug, not user input.
    /// </exception>
    private static void ApplyPermissionOutcome(TimeEntryWritePermissionOutcome outcome)
    {
        switch (outcome)
        {
            case TimeEntryWritePermissionOutcome.Elevated:
            case TimeEntryWritePermissionOutcome.SelfSameDayPresent:
                return;
            case TimeEntryWritePermissionOutcome.MissingLinkedOperator:
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TIMEENTRY_REQUIRES_OPERATOR_LINK);
            case TimeEntryWritePermissionOutcome.NotOwnOperator:
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TIMEENTRY_NOT_OWN_OPERATOR);
            case TimeEntryWritePermissionOutcome.OlderDayMember:
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TIMEENTRY_WRITE_REQUIRES_SAME_DAY);
            case TimeEntryWritePermissionOutcome.MemberNonPresent:
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TIMEENTRY_MEMBER_ONLY_PRESENT);
            default:
                throw new InvalidOperationException($"Unexpected {nameof(TimeEntryWritePermissionOutcome)}: {outcome}");
        }
    }

    /// <summary>
    /// Factory for a new <see cref="TimeEntry"/>. Stamps a fresh <c>Id</c>, the supplied
    /// <paramref name="utcNow"/> as <c>CreatedAt</c>, and copies <c>Date</c>,
    /// <c>OperatorId</c>, and <c>Status</c> from the request. <c>BranchId</c> is taken
    /// from the authenticated caller, not the payload.
    /// </summary>
    /// <param name="request">The upsert payload.</param>
    /// <param name="branchId">The authenticated caller's branch id.</param>
    /// <param name="utcNow">UTC instant stamped as <c>CreatedAt</c>.</param>
    /// <returns>A detached <see cref="TimeEntry"/> ready to be staged on the repository.</returns>
    private static TimeEntry CreateParent(RequestUpsertTimeEntryJson request, Guid branchId, DateTime utcNow)
    {
        return new TimeEntry
        {
            Id = Guid.NewGuid(),
            CreatedAt = utcNow,
            Date = request.Date,
            OperatorId = request.OperatorId,
            BranchId = branchId,
            Status = request.Status
        };
    }

    /// <summary>
    /// Factory for a new <see cref="TimeEntrySegment"/>. Stamps a fresh <c>Id</c> and
    /// <c>CreatedAt</c>, normalizes <paramref name="clockIn"/>/<paramref name="clockOut"/>
    /// to <see cref="DateTimeKind.Unspecified"/> to match the
    /// <c>timestamp without time zone</c> column semantics, and appends the new segment
    /// to <c>entry.Segments</c>. The returned reference lets
    /// <see cref="ExecuteAdminSnapshotAsync"/> stage newly inserted segments on
    /// <see cref="ITimeEntrySegmentsRepository"/> when the parent is already tracked
    /// (for a brand-new parent, the cascade through <c>entry.Segments.Add</c> + the
    /// parent's own <c>Add</c> call is sufficient).
    /// </summary>
    /// <param name="entry">The parent entry the segment belongs to. Mutated.</param>
    /// <param name="clockIn">The segment's ClockIn (branch-local wall clock).</param>
    /// <param name="clockOut">The segment's ClockOut, or <c>null</c> for an open segment.</param>
    /// <param name="utcNow">UTC instant stamped as <c>CreatedAt</c>.</param>
    /// <returns>The newly created segment.</returns>
    private static TimeEntrySegment AddSegment(TimeEntry entry, DateTime clockIn, DateTime? clockOut, DateTime utcNow)
    {
        var segment = new TimeEntrySegment
        {
            Id = Guid.NewGuid(),
            CreatedAt = utcNow,
            TimeEntryId = entry.Id,
            ClockIn = AsUnspecified(clockIn),
            ClockOut = AsUnspecified(clockOut)
        };
        entry.Segments.Add(segment);
        return segment;
    }

    /// <summary>
    /// Normalizes a <see cref="DateTime"/> to <see cref="DateTimeKind.Unspecified"/> so it
    /// round-trips through the <c>timestamp without time zone</c> columns without
    /// triggering false-positive equality misses in
    /// <see cref="ReconcileAdminSnapshot"/> (where persisted <c>ClockIn</c> is compared to
    /// payload <c>ClockIn</c> to detect locked-field edits).
    /// </summary>
    /// <param name="value">The DateTime to normalize.</param>
    /// <returns>The same instant with <c>Kind = Unspecified</c>.</returns>
    private static DateTime AsUnspecified(DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Nullable overload of <see cref="AsUnspecified(DateTime)"/>. Returns <c>null</c>
    /// when the input is <c>null</c>; otherwise normalizes the contained value.
    /// </summary>
    /// <param name="value">The DateTime to normalize, or <c>null</c>.</param>
    /// <returns>The normalized value, or <c>null</c>.</returns>
    private static DateTime? AsUnspecified(DateTime? value)
    {
        return value.HasValue ? AsUnspecified(value.Value) : null;
    }

    /// <summary>
    /// Returns the active segments of <paramref name="entry"/> ordered deterministically by
    /// <c>ClockIn ASC, CreatedAt ASC, Id ASC</c>. Single source of truth for "the segments
    /// the use case operates on" — every consumer (validation, calc, reconciliation)
    /// must go through this projection to stay consistent with the repository's
    /// <c>.Include</c> ordering.
    /// </summary>
    /// <param name="entry">The parent entry whose segments are projected.</param>
    /// <returns>The ordered active segments. Deferred enumeration.</returns>
    private static IEnumerable<TimeEntrySegment> ActiveSegments(TimeEntry entry)
    {
        return entry.Segments
            .Where(segment => segment.Active)
            .OrderBy(segment => segment.ClockIn)
            .ThenBy(segment => segment.CreatedAt)
            .ThenBy(segment => segment.Id);
    }

    /// <summary>
    /// Builds a synthetic zero-value response for the Member-Close-with-no-existing-entry
    /// case. Returns a 200-compatible envelope (<c>Id = Guid.Empty</c>,
    /// <c>Active = false</c>, empty segments, totals zero) so the mobile client receives a
    /// well-shaped response on idempotent retry without the use case inserting an empty
    /// parent.
    /// </summary>
    /// <param name="request">The upsert payload; supplies <c>Date</c>, <c>Status</c>, <c>OperatorId</c>.</param>
    /// <param name="targetOperator">The operator whose name is echoed in the response.</param>
    /// <param name="branchId">The caller's branch id.</param>
    /// <returns>A synthetic empty response.</returns>
    private static ResponseTimeEntryJson EmptyNoopResponse(
        RequestUpsertTimeEntryJson request,
        Operator targetOperator,
        Guid branchId)
    {
        return new ResponseTimeEntryJson
        {
            Id = Guid.Empty,
            Date = request.Date,
            Status = request.Status,
            TotalHours = 0m,
            BalanceHours = 0m,
            IsInProgress = false,
            Segments = [],
            OperatorId = request.OperatorId,
            OperatorName = targetOperator.Name,
            BranchId = branchId,
            CreatedAt = default,
            Active = false
        };
    }

    /// <summary>
    /// Runs <see cref="UpsertTimeEntryFluentValidation"/> over the request payload. The
    /// validator owns only the role-independent shape (non-empty OperatorId, non-default
    /// Date, enum validity for Status / Action when present, per-segment shape when
    /// Segments is non-null). Role × Action × Segments mutual exclusion is enforced later
    /// by <see cref="EnsureRoleShape"/> after authentication resolves the caller's role.
    /// </summary>
    /// <param name="request">The upsert payload.</param>
    /// <exception cref="OnValidationException">
    /// One or more FluentValidation rules failed; all messages are aggregated into a
    /// single exception.
    /// </exception>
    private static void Validate(RequestUpsertTimeEntryJson request)
    {
        var result = new UpsertTimeEntryFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
