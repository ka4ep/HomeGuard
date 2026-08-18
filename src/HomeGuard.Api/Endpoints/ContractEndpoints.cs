using HomeGuard.Application.Interfaces.Repositories;
using HomeGuard.Application.Services;
using HomeGuard.Domain.Entities;
using HomeGuard.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HomeGuard.Api.Endpoints;

public static class ContractEndpoints
{
    public static void MapContractEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/api/contracts").WithTags("Contracts").RequireAuthorization();

        grp.MapGet   ("/",                          GetAll);
        grp.MapGet   ("/expiring",                  GetExpiring);
        grp.MapGet   ("/upcoming",                  GetUpcoming);
        grp.MapGet   ("/by-equipment/{equipId:guid}", GetByEquipment);
        grp.MapGet   ("/{id:guid}",                 Get);
        grp.MapPost  ("/",                          Create);
        grp.MapPut   ("/{id:guid}",                 Update);
        grp.MapDelete("/{id:guid}",                 Delete);

        grp.MapGet   ("/{id:guid}/summary",         GetSummary);
        grp.MapGet   ("/{id:guid}/schedule",        GetSchedule);
        grp.MapPatch ("/{id:guid}/status",          SetStatus);
        grp.MapPatch ("/{id:guid}/summary-markdown", SetSummaryMarkdown);
        grp.MapPatch ("/{id:guid}/notifications",   SetNotifications);
        grp.MapPut   ("/{id:guid}/opening",         SetOpening);
        grp.MapDelete("/{id:guid}/opening",         ClearOpening);

        grp.MapGet   ("/{id:guid}/revisions",       GetRevisions);
        grp.MapPost  ("/{id:guid}/revisions",       AddRevision);
        grp.MapPost  ("/{id:guid}/early-payment/preview", PreviewEarlyPayment);

        grp.MapGet   ("/{id:guid}/payments",        GetPayments);
        grp.MapPost  ("/{id:guid}/payments",        AddPayment);

        var payments = app.MapGroup("/api/payments").WithTags("Contracts").RequireAuthorization();
        payments.MapPost  ("/{id:guid}/confirm",    ConfirmPayment);
        payments.MapPut   ("/{id:guid}",            UpdatePayment);
        payments.MapDelete("/{id:guid}",            DeletePayment);

        var finance = app.MapGroup("/api/finance").WithTags("Contracts").RequireAuthorization();
        finance.MapGet("/monthly", GetMonthlyLoad);
    }

    // ── Contracts ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetAll(
        ContractService svc,
        CancellationToken ct,
        [FromQuery] ContractKind? kind = null,
        [FromQuery] ContractStatus? status = null,
        [FromQuery] Guid? equipmentId = null)
    {
        var list = await svc.GetAllAsync(kind, status, equipmentId, ct);
        return Results.Ok(list.Select(ContractDto.From));
    }

    private static async Task<IResult> GetExpiring(
        ContractService svc, CancellationToken ct, [FromQuery] int days = 60)
    {
        var list = await svc.GetExpiringAsync(days, ct);
        return Results.Ok(list.Select(ContractDto.From));
    }

    private static async Task<IResult> GetUpcoming(
        ContractService svc, CancellationToken ct, [FromQuery] int days = 30)
    {
        var list = await svc.GetUpcomingAsync(days, ct);
        return Results.Ok(list);
    }

    private static async Task<IResult> GetByEquipment(
        Guid equipId, ContractService svc, CancellationToken ct)
    {
        var list = await svc.GetByEquipmentAsync(equipId, ct);
        return Results.Ok(list.Select(ContractDto.From));
    }

    private static async Task<IResult> Get(
        Guid id, ContractService svc, IBlobEntryRepository blobs, CancellationToken ct)
    {
        var contract = await svc.GetAsync(id, ct);
        if (contract is null) return Results.NotFound();

        // Contract.Attachments is intentionally unmapped in EF (see the comment on
        // HomeGuardDbContext's Contract config) — resolved here instead.
        var attachments = await blobs.GetByOwnerAsync(id, ct);
        return Results.Ok(ContractDetailDto.From(
            contract, [.. attachments.Select(BlobDto.From)]));
    }

    private static async Task<IResult> Create(
        [FromBody] CreateContractRequest req, ContractService svc, CancellationToken ct)
    {
        try
        {
            var contract = await svc.CreateAsync(new CreateContractCommand(
                req.Kind, req.Name, req.StartDate, req.Currency,
                req.EquipmentId, req.Provider, req.ContractNumber, req.EndDate,
                req.Renewal, req.CancellationNoticeDays, req.SummaryMarkdown, req.Notes,
                req.CoverageAmount, req.Deductible, req.Tags, req.PreviousContractId), ct);

            return Results.Created($"/api/contracts/{contract.Id}", ContractDto.From(contract));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> Update(
        Guid id, [FromBody] UpdateContractRequest req, ContractService svc, CancellationToken ct)
    {
        try
        {
            var contract = await svc.UpdateAsync(new UpdateContractCommand(
                id, req.Name, req.StartDate, req.EndDate, req.Provider, req.ContractNumber,
                req.Renewal, req.CancellationNoticeDays, req.SummaryMarkdown, req.Notes,
                req.CoverageAmount, req.Deductible, req.Tags), ct);

            return contract is null ? Results.NotFound() : Results.Ok(ContractDto.From(contract));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> Delete(Guid id, ContractService svc, CancellationToken ct)
        => await svc.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound();

    private static async Task<IResult> SetStatus(
        Guid id, [FromBody] SetContractStatusRequest req, ContractService svc, CancellationToken ct)
    {
        var contract = await svc.SetStatusAsync(id, req.Status, ct);
        return contract is null ? Results.NotFound() : Results.Ok(ContractDto.From(contract));
    }

    private static async Task<IResult> SetSummaryMarkdown(
        Guid id, [FromBody] SetSummaryMarkdownRequest req, ContractService svc, CancellationToken ct)
    {
        var contract = await svc.SetSummaryMarkdownAsync(id, req.SummaryMarkdown, ct);
        return contract is null ? Results.NotFound() : Results.Ok(ContractDto.From(contract));
    }

    private static async Task<IResult> SetNotifications(
        Guid id, [FromBody] SetContractNotificationsRequest req,
        ContractService svc, CancellationToken ct)
    {
        var rules = req.Rules.Select(r => (r.Offset, r.Enabled)).ToList();
        var contract = await svc.SetNotificationRulesAsync(id, rules, ct);
        return contract is null ? Results.NotFound() : Results.Ok(ContractDto.From(contract));
    }

    // ── Summary and schedule ──────────────────────────────────────────────────

    private static async Task<IResult> GetSummary(Guid id, ContractService svc, CancellationToken ct)
    {
        var summary = await svc.GetSummaryAsync(id, ct);
        return summary is null ? Results.NotFound() : Results.Ok(summary);
    }

    private static async Task<IResult> GetSchedule(
        Guid id, ContractService svc, CancellationToken ct,
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
    {
        // Default window: a year back for context, two years ahead for planning.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? today.AddYears(-1);
        var end   = to   ?? today.AddYears(2);

        var entries = await svc.GetScheduleAsync(id, start, end, ct);
        return Results.Ok(entries);
    }

    // ── Opening position ──────────────────────────────────────────────────────

    private static async Task<IResult> SetOpening(
        Guid id, [FromBody] SetOpeningRequest req, ContractService svc, CancellationToken ct)
    {
        try
        {
            var contract = await svc.SetOpeningAsync(new SetOpeningCommand(
                id, req.AsOfDate, req.InstallmentsPaid, req.AmountPaid, req.RemainingBalance), ct);

            return contract is null ? Results.NotFound() : Results.Ok(ContractDetailDto.From(contract));
        }
        catch (InvalidOperationException ex)
        {
            // A payment already sits before the proposed cut-off — accepting it would
            // count that money twice.
            return Results.Problem(
                title: "Opening position overlaps recorded payments",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> ClearOpening(
        Guid id, ContractService svc, CancellationToken ct)
    {
        var contract = await svc.ClearOpeningAsync(id, ct);
        return contract is null ? Results.NotFound() : Results.Ok(ContractDetailDto.From(contract));
    }

    // ── Revisions ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetRevisions(Guid id, ContractService svc, CancellationToken ct)
    {
        var contract = await svc.GetAsync(id, ct);
        if (contract is null) return Results.NotFound();

        return Results.Ok(contract.Revisions
            .OrderBy(r => r.Version)
            .Select(PlanRevisionDto.From));
    }

    private static async Task<IResult> AddRevision(
        Guid id, [FromBody] AddRevisionRequest req, ContractService svc, CancellationToken ct)
    {
        try
        {
            var revision = await svc.AddRevisionAsync(new AddRevisionCommand(
                id, req.EffectiveFrom, req.Reason, req.FirstDueDate, req.IntervalMonths,
                req.InstallmentAmount, req.InstallmentCount, req.RemainingPrincipal,
                req.AnnualInterestRate, req.ResidualAmount, req.ResidualDueDate, req.Note,
                req.Adjustments?.Select(a => (a.Name, a.Amount)).ToList()), ct);

            return revision is null
                ? Results.NotFound()
                : Results.Created($"/api/contracts/{id}/revisions", PlanRevisionDto.From(revision));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                title: "Revision cannot take effect before the active one",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// A dry run: what paying <c>req.ExtraAmount</c> today would change, without writing
    /// anything. The dialog turns this into the "было → станет" table before the household
    /// commits to it via <see cref="AddRevision"/>.
    /// </summary>
    private static async Task<IResult> PreviewEarlyPayment(
        Guid id, [FromBody] EarlyPaymentPreviewRequest req, ContractService svc, CancellationToken ct)
    {
        var contract = await svc.GetAsync(id, ct);
        if (contract is null) return Results.NotFound();

        try
        {
            var preview = ContractService.PreviewEarlyPayment(contract, req.ExtraAmount, req.Effect);
            return Results.Ok(preview);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    // ── Payments ──────────────────────────────────────────────────────────────

    private static async Task<IResult> GetPayments(Guid id, ContractService svc, CancellationToken ct)
    {
        var contract = await svc.GetAsync(id, ct);
        if (contract is null) return Results.NotFound();

        return Results.Ok(contract.Payments
            .OrderBy(p => p.DueDate)
            .Select(PaymentDto.From));
    }

    private static async Task<IResult> AddPayment(
        Guid id, [FromBody] AddPaymentRequest req, ContractService svc, CancellationToken ct)
    {
        try
        {
            var payment = await svc.AddPaymentAsync(new AddPaymentCommand(
                id, req.DueDate, req.AmountDue, req.Kind, req.InstallmentNo, req.Note), ct);

            return payment is null
                ? Results.NotFound()
                : Results.Created($"/api/payments/{payment.Id}", PaymentDto.From(payment));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                title: "Payment falls inside the opening position",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> ConfirmPayment(
        Guid id, [FromBody] ConfirmPaymentRequest req, ContractService svc, CancellationToken ct)
    {
        var payment = await svc.ConfirmPaymentAsync(
            new ConfirmPaymentCommand(id, req.PaidDate, req.AmountPaid, req.Note), ct);

        return payment is null ? Results.NotFound() : Results.Ok(PaymentDto.From(payment));
    }

    private static async Task<IResult> UpdatePayment(
        Guid id, [FromBody] UpdatePaymentRequest req, ContractService svc, CancellationToken ct)
    {
        var payment = await svc.UpdatePaymentAsync(new UpdatePaymentCommand(
            id, req.DueDate, req.AmountDue, req.Kind, req.Note, req.Reopen), ct);

        return payment is null ? Results.NotFound() : Results.Ok(PaymentDto.From(payment));
    }

    private static async Task<IResult> DeletePayment(Guid id, ContractService svc, CancellationToken ct)
        => await svc.DeletePaymentAsync(id, ct) ? Results.NoContent() : Results.NotFound();

    // ── Finance rollup ───────────────────────────────────────────────────────

    private static async Task<IResult> GetMonthlyLoad(
        ContractService svc, CancellationToken ct, [FromQuery] int months = 12)
    {
        var entries = await svc.GetMonthlyLoadAsync(Math.Clamp(months, 1, 36), ct);
        return Results.Ok(entries);
    }
}

// ── Requests ──────────────────────────────────────────────────────────────────

public sealed record CreateContractRequest(
    ContractKind Kind,
    string Name,
    DateOnly StartDate,
    string Currency,
    Guid? EquipmentId = null,
    string? Provider = null,
    string? ContractNumber = null,
    DateOnly? EndDate = null,
    RenewalMode Renewal = RenewalMode.None,
    int? CancellationNoticeDays = null,
    string? SummaryMarkdown = null,
    string? Notes = null,
    decimal? CoverageAmount = null,
    decimal? Deductible = null,
    IReadOnlyList<string>? Tags = null,
    Guid? PreviousContractId = null);

public sealed record UpdateContractRequest(
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate = null,
    string? Provider = null,
    string? ContractNumber = null,
    RenewalMode Renewal = RenewalMode.None,
    int? CancellationNoticeDays = null,
    string? SummaryMarkdown = null,
    string? Notes = null,
    decimal? CoverageAmount = null,
    decimal? Deductible = null,
    IReadOnlyList<string>? Tags = null);

public sealed record SetContractStatusRequest(ContractStatus Status);
public sealed record SetSummaryMarkdownRequest(string? SummaryMarkdown);
public sealed record ContractNotificationRuleRequest(NotificationOffset Offset, bool Enabled);
public sealed record SetContractNotificationsRequest(IReadOnlyList<ContractNotificationRuleRequest> Rules);

public sealed record SetOpeningRequest(
    DateOnly AsOfDate,
    int InstallmentsPaid,
    decimal AmountPaid,
    decimal? RemainingBalance = null);

public sealed record PlanAdjustmentRequest(string Name, decimal Amount);

public sealed record AddRevisionRequest(
    DateOnly EffectiveFrom,
    RevisionReason Reason,
    DateOnly FirstDueDate,
    int IntervalMonths,
    decimal InstallmentAmount,
    int? InstallmentCount = null,
    decimal? RemainingPrincipal = null,
    decimal? AnnualInterestRate = null,
    decimal? ResidualAmount = null,
    DateOnly? ResidualDueDate = null,
    string? Note = null,
    IReadOnlyList<PlanAdjustmentRequest>? Adjustments = null);

public sealed record AddPaymentRequest(
    DateOnly DueDate,
    decimal AmountDue,
    PaymentKind Kind = PaymentKind.Scheduled,
    int? InstallmentNo = null,
    string? Note = null);

public sealed record UpdatePaymentRequest(
    DateOnly DueDate,
    decimal AmountDue,
    PaymentKind Kind = PaymentKind.Scheduled,
    string? Note = null,
    bool Reopen = false);

public sealed record ConfirmPaymentRequest(
    DateOnly PaidDate,
    decimal? AmountPaid = null,
    string? Note = null);

public sealed record EarlyPaymentPreviewRequest(
    decimal ExtraAmount,
    EarlyPaymentEffect Effect = EarlyPaymentEffect.ReduceTerm);

// ── Responses ─────────────────────────────────────────────────────────────────

public sealed record ContractDto(
    Guid Id,
    Guid? EquipmentId,
    ContractKind Kind,
    string Name,
    string? Provider,
    string? ContractNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly? CancellationDeadline,
    RenewalMode Renewal,
    ContractStatus Status,
    string Currency,
    decimal? CurrentInstallment,
    int? IntervalMonths,
    IReadOnlyList<string> Tags)
{
    public static ContractDto From(Contract c)
    {
        var revision = c.ActiveRevision;
        return new ContractDto(
            c.Id, c.EquipmentId, c.Kind, c.Name, c.Provider, c.ContractNumber,
            c.StartDate, c.EndDate, c.CancellationDeadline, c.Renewal, c.Status, c.Currency,
            revision?.EffectiveInstallment, revision?.IntervalMonths, c.Tags);
    }
}

public sealed record ContractDetailDto(
    ContractDto Contract,
    string? SummaryMarkdown,
    string? Notes,
    decimal? CoverageAmount,
    decimal? Deductible,
    OpeningPositionDto? Opening,
    IReadOnlyList<PlanRevisionDto> Revisions,
    IReadOnlyList<PaymentDto> Payments,
    IReadOnlyList<ContractNotificationRuleRequest> NotificationRules,
    IReadOnlyList<BlobDto> Attachments)
{
    // attachments defaults to empty for the handful of write endpoints (SetOpening,
    // ClearOpening) that return a ContractDetailDto without re-querying blobs — the
    // client always follows those with a full reload, so the field is never read stale.
    public static ContractDetailDto From(Contract c, IReadOnlyList<BlobDto>? attachments = null) => new(
        ContractDto.From(c),
        c.SummaryMarkdown,
        c.Notes,
        c.CoverageAmount,
        c.Deductible,
        c.Opening is null ? null : new OpeningPositionDto(
            c.Opening.AsOfDate, c.Opening.InstallmentsPaid,
            c.Opening.AmountPaid, c.Opening.RemainingBalance),
        [.. c.Revisions.OrderBy(r => r.Version).Select(PlanRevisionDto.From)],
        [.. c.Payments.OrderBy(p => p.DueDate).Select(PaymentDto.From)],
        [.. c.NotificationRules.Select(r => new ContractNotificationRuleRequest(r.Offset, r.IsEnabled))],
        attachments ?? []);
}

public sealed record OpeningPositionDto(
    DateOnly AsOfDate, int InstallmentsPaid, decimal AmountPaid, decimal? RemainingBalance);

public sealed record PlanAdjustmentDto(string Name, decimal Amount);

public sealed record PlanRevisionDto(
    Guid Id,
    int Version,
    DateOnly EffectiveFrom,
    RevisionReason Reason,
    DateOnly FirstDueDate,
    int IntervalMonths,
    int? InstallmentCount,
    decimal InstallmentAmount,
    decimal EffectiveInstallment,
    decimal? RemainingPrincipal,
    decimal? AnnualInterestRate,
    decimal? ResidualAmount,
    DateOnly? ResidualDueDate,
    string? Note,
    IReadOnlyList<PlanAdjustmentDto> Adjustments)
{
    public static PlanRevisionDto From(PaymentPlanRevision r) => new(
        r.Id, r.Version, r.EffectiveFrom, r.Reason, r.FirstDueDate, r.IntervalMonths,
        r.InstallmentCount, r.InstallmentAmount, r.EffectiveInstallment,
        r.RemainingPrincipal, r.AnnualInterestRate, r.ResidualAmount, r.ResidualDueDate,
        r.Note,
        [.. r.Adjustments.Select(a => new PlanAdjustmentDto(a.Name, a.Amount))]);
}

public sealed record PaymentDto(
    Guid Id,
    Guid ContractId,
    int? InstallmentNo,
    PaymentKind Kind,
    PaymentStatus Status,
    DateOnly DueDate,
    decimal AmountDue,
    DateOnly? PaidDate,
    decimal? AmountPaid,
    decimal? PrincipalPart,
    decimal? InterestPart,
    string? Note)
{
    public static PaymentDto From(Payment p) => new(
        p.Id, p.ContractId, p.InstallmentNo, p.Kind, p.Status, p.DueDate, p.AmountDue,
        p.PaidDate, p.AmountPaid, p.PrincipalPart, p.InterestPart, p.Note);
}
