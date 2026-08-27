using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShippingManagementApi.Application.Merchants;
using ShippingManagementApi.Application.Security;
using ShippingManagementApi.Domain.Merchants;
using ShippingManagementApi.Infrastructure.Identity;
using ShippingManagementApi.Infrastructure.Persistence;

namespace ShippingManagementApi.Infrastructure.Merchants;

internal sealed class MerchantService(
    ShippingManagementDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ICurrentUserContext currentUser,
    TimeProvider timeProvider) : IMerchantService
{
    public async Task<ServiceResult<MerchantResponse>> ProvisionAsync(ProvisionMerchantRequest request, CancellationToken cancellationToken)
    {
        string code;
        try { code = Merchant.NormalizeCode(request.Code); }
        catch (ArgumentException ex) { return ServiceResult<MerchantResponse>.Fail(ServiceError.Validation, ex.Message); }

        if (await dbContext.Merchants.AnyAsync(x => x.Code == code, cancellationToken))
            return ServiceResult<MerchantResponse>.Fail(ServiceError.Conflict, "A merchant with this code already exists.");
        if (await userManager.FindByEmailAsync(request.InitialUserEmail) is not null)
            return ServiceResult<MerchantResponse>.Fail(ServiceError.Conflict, "A user with this email already exists.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        Merchant merchant;
        try { merchant = new Merchant(request.Name, code, timeProvider.GetUtcNow()); }
        catch (ArgumentException ex) { return ServiceResult<MerchantResponse>.Fail(ServiceError.Validation, ex.Message); }
        dbContext.Merchants.Add(merchant);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            return ServiceResult<MerchantResponse>.Fail(ServiceError.Conflict, "A merchant with this code already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = request.InitialUserEmail, Email = request.InitialUserEmail,
            EmailConfirmed = true, IsActive = true, MerchantId = merchant.Id, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var createResult = await userManager.CreateAsync(user, request.InitialUserPassword);
        if (!createResult.Succeeded)
            return ServiceResult<MerchantResponse>.Fail(
                createResult.Errors.Any(x => x.Code is "DuplicateEmail" or "DuplicateUserName") ? ServiceError.Conflict : ServiceError.Validation,
                string.Join(" ", createResult.Errors.Select(x => x.Description)));
        var roleResult = await userManager.AddToRoleAsync(user, AppRoles.Merchant);
        if (!roleResult.Succeeded) throw new InvalidOperationException("The Merchant role is not available.");
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<MerchantResponse>.Success(Map(merchant));
    }

    public async Task<ServiceResult<MerchantResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (currentUser.Roles.Contains(AppRoles.Operator))
            return ServiceResult<MerchantResponse>.Fail(ServiceError.Forbidden, "Access is forbidden.");

        var query = dbContext.Merchants.AsNoTracking().Where(x => x.Id == id);
        if (currentUser.Roles.Contains(AppRoles.Merchant))
        {
            if (currentUser.MerchantId is not { } merchantId)
                return ServiceResult<MerchantResponse>.Fail(ServiceError.Forbidden, "Access is forbidden.");
            query = query.Where(x => x.Id == merchantId);
        }
        else if (!currentUser.Roles.Contains(AppRoles.Admin))
            return ServiceResult<MerchantResponse>.Fail(ServiceError.Forbidden, "Access is forbidden.");

        var merchant = await query.SingleOrDefaultAsync(cancellationToken);
        return merchant is null
            ? ServiceResult<MerchantResponse>.Fail(ServiceError.NotFound, "Merchant was not found.")
            : ServiceResult<MerchantResponse>.Success(Map(merchant));
    }

    private static MerchantResponse Map(Merchant x) => new(x.Id, x.Name, x.Code, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc);
}
