using Dynamic.Fidelity.Application.Services;
using Dynamic.Fidelity.Application.Contracts.Services;
using Dynamic.Fidelity.Application.Models;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Fidelity.Infrastructure.Repositories;
using Dynamic.Negocios.Domain.Entities;
using Dynamic.Negocios.Domain.Enums;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Negocios.Infrastructure.Repositories;
using Dynamic.Users.Application.DTOs.Requests;
using Dynamic.Users.Application.Services;
using Dynamic.Users.Application.Options;
using Dynamic.Users.Domain.Entities;
using Dynamic.Users.Domain.Enums;
using Dynamic.Users.Infrastructure.Persistence;
using Dynamic.Users.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dynamic.Users.Tests;

public sealed class BusinessCustomerSearchTests : IDisposable
{
    private readonly DynamicUsersDbContext _users = new(new DbContextOptionsBuilder<DynamicUsersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly DynamicNegociosDbContext _businesses = new(new DbContextOptionsBuilder<DynamicNegociosDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly DynamicFidelityDbContext _fidelity = new(new DbContextOptionsBuilder<DynamicFidelityDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Guid _worker = Guid.NewGuid();

    private UserService CreateService() => new(_users, new UserRepository(_users),
        new UserSessionRepository(_users), new UserDeviceRepository(_users),
        new UserCodeDirectoryService(_fidelity, new UserCodeDirectoryRepository(_fidelity)),
        new NegocioUsuarioVinculacionRepository(_businesses));

    private async Task<UserAccount> SeedAsync(UserRole role, UserStatus status = UserStatus.Active,
        bool completed = true)
    {
        var business = new Negocio { Id = Guid.NewGuid(), Activo = true };
        _businesses.Negocios.Add(business);
        _businesses.NegociosUsuariosVinculaciones.Add(new NegocioUsuarioVinculacion
        {
            Id = Guid.NewGuid(), NegocioId = business.Id, UserId = _worker,
            Activa = true, PuedeGestionarPuntos = true,
            TipoVinculacion = TipoVinculacionNegocioUsuario.Trabajador
        });
        await _businesses.SaveChangesAsync();
        var user = new UserAccount
        {
            Id = Guid.NewGuid(), UserName = "customer", NormalizedUserName = "CUSTOMER",
            Email = "customer@example.test", NormalizedEmail = "CUSTOMER@EXAMPLE.TEST",
            PhoneNumber = "+34 612 345 678", NormalizedPhoneNumber = "34612345678",
            Role = role, Status = status, RegistrationCompleted = completed
        };
        _users.Users.Add(user);
        await _users.SaveChangesAsync();
        return user;
    }

    [Theory]
    [InlineData(UserRole.User, " Customer@Example.Test ")]
    [InlineData(UserRole.User, "+34 612-345-678")]
    [InlineData(UserRole.PropietarioNegocio, " Customer@Example.Test ")]
    [InlineData(UserRole.PropietarioNegocio, "+34 612-345-678")]
    [InlineData(UserRole.TrabajadorNegocio, " Customer@Example.Test ")]
    [InlineData(UserRole.TrabajadorNegocio, "+34 612-345-678")]
    public async Task StaffCanFindPersonalAccountsAndResolveThemForAccrual(UserRole role, string contact)
    {
        var customer = await SeedAsync(role);
        var service = CreateService();
        var result = await service.SearchBusinessCustomerByContactAsync(_worker, false,
            new BusinessCustomerSearchRequest { Contact = contact });
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(customer.Id, result.Data!.UserId);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.UserCode));
        var accrualTarget = await service.GetBusinessCustomerByIdAsync(_worker, false, customer.Id);
        Assert.True(accrualTarget.Succeeded, accrualTarget.ErrorMessage);
        Assert.Equal(result.Data.UserCode, accrualTarget.Data!.UserCode);
    }

    [Theory]
    [InlineData(UserRole.User, UserStatus.Disabled, true)]
    [InlineData(UserRole.TrabajadorNegocio, UserStatus.Locked, true)]
    [InlineData(UserRole.PropietarioNegocio, UserStatus.PendingActivation, false)]
    [InlineData(UserRole.User, UserStatus.Deleted, true)]
    [InlineData(UserRole.User, UserStatus.Active, false)]
    [InlineData(UserRole.Admin, UserStatus.Active, true)]
    public async Task IneligibleAccountsRemainBlocked(UserRole role, UserStatus status, bool completed)
    {
        var customer = await SeedAsync(role, status, completed);
        var service = CreateService();
        var search = await service.SearchBusinessCustomerByContactAsync(_worker, false,
            new BusinessCustomerSearchRequest { Contact = customer.Email! });
        Assert.Equal("not_found", search.ErrorCode);
        var target = await service.GetBusinessCustomerByIdAsync(_worker, false, customer.Id);
        Assert.Equal("not_found", target.ErrorCode);
    }

    [Fact]
    public async Task SearchRequiresBusinessPermissions()
    {
        var customer = await SeedAsync(UserRole.User);
        var link = Assert.Single(_businesses.NegociosUsuariosVinculaciones);
        link.PuedeGestionarPuntos = false;
        await _businesses.SaveChangesAsync();
        var result = await CreateService().SearchBusinessCustomerByContactAsync(_worker, false,
            new BusinessCustomerSearchRequest { Contact = customer.Email! });
        Assert.Equal("forbidden", result.ErrorCode);
        Assert.Empty(_fidelity.UserCodeDirectoryEntries);
    }

    public void Dispose()
    {
        _users.Dispose();
        _businesses.Dispose();
        _fidelity.Dispose();
    }

    [Fact]
    public async Task WorkerCanRegisterNewCustomerAndRepeatWithoutDuplicating()
    {
        await SeedAsync(UserRole.User);
        var rewards = new NoWelcomeRewards();
        var service = new BusinessUserProvisioningService(_users, _businesses,
            new UserRepository(_users), new UserAuthEventRepository(_users), new PasswordHasher<UserAccount>(),
            new UserCodeDirectoryService(_fidelity, new UserCodeDirectoryRepository(_fidelity)),
            new NegocioRepository(_businesses), new NegocioUsuarioVinculacionRepository(_businesses),
            new NegocioAudienciaService(_businesses, _fidelity, rewards), rewards, null!,
            Options.Create(new UserRegistrationOptions()), NullLogger<BusinessUserProvisioningService>.Instance);
        var request = new CreateBusinessCustomerUserRequest
        {
            Contact = "new@example.test", PhoneNumber = "+34 699 123 456", Nombre = "Cliente", Apellidos = "Prueba",
            BirthDate = new DateTime(1990, 1, 1), Gender = UserGender.Mujer,
            PostalCode = "28013", Province = SpanishProvince.Madrid,
            TermsAccepted = true, PrivacyPolicyAccepted = true, LinkToBusiness = true
        };
        var businessId = Assert.Single(_businesses.Negocios).Id;
        var result = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false,
            request, null, null);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.Data!.Created);
        Assert.True(result.Data.LinkedNow);
        var customerId = result.Data.User.Id;
        Assert.True((await CreateService().SearchBusinessCustomerByContactAsync(_worker, false,
            new BusinessCustomerSearchRequest { Contact = request.Contact })).Succeeded);
        var repeat = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false,
            request, null, null);
        Assert.True(repeat.Succeeded, repeat.ErrorMessage);
        Assert.False(repeat.Data!.Created);
        Assert.False(repeat.Data.LinkedNow);
        Assert.Equal(customerId, repeat.Data.User.Id);
        Assert.Single(_users.Users.Where(user => user.Email == request.Contact));
        Assert.Single(_businesses.NegociosAudiencias.Where(item => item.UserId == customerId));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RegistrationLinksAndRequestsWelcomeRewardOnlyWhenSelected(bool linkToBusiness, bool alreadyEnjoyed)
    {
        await SeedAsync(UserRole.User);
        var rewards = new NoWelcomeRewards { WelcomeAvailable = true };
        var service = new BusinessUserProvisioningService(_users, _businesses,
            new UserRepository(_users), new UserAuthEventRepository(_users), new PasswordHasher<UserAccount>(),
            new UserCodeDirectoryService(_fidelity, new UserCodeDirectoryRepository(_fidelity)),
            new NegocioRepository(_businesses), new NegocioUsuarioVinculacionRepository(_businesses),
            new NegocioAudienciaService(_businesses, _fidelity, rewards), rewards, null!,
            Options.Create(new UserRegistrationOptions()), NullLogger<BusinessUserProvisioningService>.Instance);
        var request = new CreateBusinessCustomerUserRequest
        {
            Contact = "optional@example.test", PhoneNumber = "+34 699 123 456", Nombre = "Cliente", Apellidos = "Prueba",
            BirthDate = new DateTime(1990, 1, 1), Gender = UserGender.Mujer,
            PostalCode = "28013", Province = SpanishProvince.Madrid,
            TermsAccepted = true, PrivacyPolicyAccepted = true, LinkToBusiness = linkToBusiness,
            WelcomeBonusAlreadyEnjoyed = alreadyEnjoyed
        };
        var businessId = Assert.Single(_businesses.Negocios).Id;
        var result = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.Data!.Created);
        Assert.Equal(linkToBusiness, result.Data.LinkedNow);
        Assert.Equal(linkToBusiness, result.Data.FormaParteAudiencia);
        Assert.Equal(linkToBusiness, result.Data.ReceivedWelcomeTicket);
        Assert.Equal(alreadyEnjoyed, result.Data.WelcomeTicketRedeemed);
        Assert.Equal(alreadyEnjoyed ? _worker : (Guid?)null, rewards.RedeemedByUserId);
        Assert.Equal(linkToBusiness ? 1 : 0, rewards.WelcomeCalls);
        Assert.Equal(linkToBusiness ? 1 : 0, _businesses.NegociosAudiencias.Count());

        // A registration-only customer can be linked later, without creating another account.
        request.LinkToBusiness = true;
        var linked = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(linked.Succeeded, linked.ErrorMessage);
        Assert.False(linked.Data!.Created);
        Assert.Equal(!linkToBusiness, linked.Data.LinkedNow);
        Assert.Equal(!linkToBusiness, linked.Data.ReceivedWelcomeTicket);
        Assert.False(linked.Data.WelcomeTicketRedeemed);
        Assert.Equal(result.Data.User.Id, linked.Data.User.Id);
        Assert.Equal(1, rewards.WelcomeCalls);
        Assert.Single(_businesses.NegociosAudiencias);

        // Leaving the option off later preserves an existing membership and never grants again.
        request.LinkToBusiness = false;
        request.WelcomeBonusAlreadyEnjoyed = false;
        var repeat = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(repeat.Succeeded, repeat.ErrorMessage);
        Assert.False(repeat.Data!.LinkedNow);
        Assert.False(repeat.Data.ReceivedWelcomeTicket);
        Assert.True(repeat.Data.FormaParteAudiencia);
        Assert.Equal(1, rewards.WelcomeCalls);
        Assert.Single(_users.Users.Where(user => user.Email == request.Contact));
    }

    private BusinessUserProvisioningService RegistrationService(NoWelcomeRewards rewards) => new(
        _users, _businesses, new UserRepository(_users), new UserAuthEventRepository(_users),
        new PasswordHasher<UserAccount>(),
        new UserCodeDirectoryService(_fidelity, new UserCodeDirectoryRepository(_fidelity)),
        new NegocioRepository(_businesses), new NegocioUsuarioVinculacionRepository(_businesses),
        new NegocioAudienciaService(_businesses, _fidelity, rewards), rewards, null!,
        Options.Create(new UserRegistrationOptions()), NullLogger<BusinessUserProvisioningService>.Instance);

    private static CreateBusinessCustomerUserRequest EnjoyedRequest() => new()
    {
        Contact = "enjoyed@example.test", PhoneNumber = "+34 699 123 456", Nombre = "Cliente", Apellidos = "Prueba",
        BirthDate = new DateTime(1990, 1, 1), Gender = UserGender.Mujer,
        PostalCode = "28013", Province = SpanishProvince.Madrid,
        TermsAccepted = true, PrivacyPolicyAccepted = true,
        LinkToBusiness = true, WelcomeBonusAlreadyEnjoyed = true
    };

    [Fact]
    public async Task EnjoyedBonusWithoutLinkIsRejectedBeforeCreatingCustomer()
    {
        await SeedAsync(UserRole.User);
        var rewards = new NoWelcomeRewards { WelcomeAvailable = true };
        var request = EnjoyedRequest();
        request.LinkToBusiness = false;
        var result = await RegistrationService(rewards).CreateCustomerByBusinessStaffAsync(
            Assert.Single(_businesses.Negocios).Id, _worker, false, request, null, null);
        Assert.False(result.Succeeded);
        Assert.Equal("validation_error", result.ErrorCode);
        Assert.False(await _users.Users.AnyAsync(user => user.Email == request.Contact));
        Assert.Empty(_businesses.NegociosAudiencias);
        Assert.Equal(0, rewards.WelcomeCalls);
    }

    [Fact]
    public async Task EnjoyedBonusRequiresAuthorizedBusinessStaff()
    {
        await SeedAsync(UserRole.User);
        var rewards = new NoWelcomeRewards { WelcomeAvailable = true };
        var request = EnjoyedRequest();
        var result = await RegistrationService(rewards).CreateCustomerByBusinessStaffAsync(
            Assert.Single(_businesses.Negocios).Id, Guid.NewGuid(), false, request, null, null);
        Assert.False(result.Succeeded);
        Assert.Equal("forbidden", result.ErrorCode);
        Assert.False(await _users.Users.AnyAsync(user => user.Email == request.Contact));
        Assert.Equal(0, rewards.WelcomeCalls);
    }

    [Fact]
    public async Task MissingWelcomeBonusDoesNotReportAnIssuanceOrRedemption()
    {
        await SeedAsync(UserRole.User);
        var rewards = new NoWelcomeRewards { WelcomeAvailable = false };
        var result = await RegistrationService(rewards).CreateCustomerByBusinessStaffAsync(
            Assert.Single(_businesses.Negocios).Id, _worker, false, EnjoyedRequest(), null, null);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.Data!.LinkedNow);
        Assert.False(result.Data.ReceivedWelcomeTicket);
        Assert.False(result.Data.WelcomeTicketRedeemed);
        Assert.Contains("No se ha emitido ni canjeado", result.Data.Message);
    }

    [Fact]
    public async Task ExistingUnlinkedCustomerCanReceiveAlreadyEnjoyedWelcomeOnce()
    {
        var customer = await SeedAsync(UserRole.User);
        var request = EnjoyedRequest();
        request.Contact = customer.Email!;
        var rewards = new NoWelcomeRewards { WelcomeAvailable = true };
        var service = RegistrationService(rewards);
        var businessId = Assert.Single(_businesses.Negocios).Id;
        var result = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(result.Data!.Created);
        Assert.Equal(customer.Id, result.Data.User.Id);
        Assert.True(result.Data.WelcomeTicketRedeemed);
        var repeat = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(repeat.Succeeded, repeat.ErrorMessage);
        Assert.False(repeat.Data!.WelcomeTicketRedeemed);
        Assert.Equal(1, rewards.WelcomeCalls);
    }

    [Fact]
    public async Task EnjoyedOptionOnAlreadyLinkedCustomerDoesNotRedeemTheirExistingBonus()
    {
        await SeedAsync(UserRole.User);
        var rewards = new NoWelcomeRewards { WelcomeAvailable = true };
        var service = RegistrationService(rewards);
        var request = EnjoyedRequest();
        request.WelcomeBonusAlreadyEnjoyed = false;
        var businessId = Assert.Single(_businesses.Negocios).Id;
        var first = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(first.Data!.ReceivedWelcomeTicket);
        request.WelcomeBonusAlreadyEnjoyed = true;
        var repeat = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(repeat.Succeeded, repeat.ErrorMessage);
        Assert.False(repeat.Data!.ReceivedWelcomeTicket);
        Assert.False(repeat.Data.WelcomeTicketRedeemed);
        Assert.Null(rewards.RedeemedByUserId);
        Assert.Equal(1, rewards.WelcomeCalls);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task ExistingCustomerProfileIsNeverUpdatedByRegistration(bool byPhone, bool completed, bool enjoyed)
    {
        var customer = await SeedAsync(UserRole.User,
            completed ? UserStatus.Active : UserStatus.PendingActivation, completed);
        customer.FirstName = "Nombre original";
        customer.LastName = "Apellidos originales";
        customer.DisplayName = "Nombre original Apellidos originales";
        customer.BirthDate = new DateTime(1980, 6, 10);
        customer.Gender = UserGender.Hombre;
        customer.PostalCode = "46001";
        customer.Province = SpanishProvince.Valencia;
        customer.PasswordHash = "original-password-hash";
        customer.EmailConfirmed = completed;
        customer.PhoneNumberConfirmed = completed;
        customer.RegistrationValidationToken = completed ? null : "original-validation-token";
        customer.RegistrationValidationTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1);
        customer.RegistrationInitiatedAtUtc = DateTime.UtcNow.AddDays(-5);
        customer.TermsAccepted = false;
        customer.PrivacyPolicyAccepted = false;
        customer.MarketingAccepted = false;
        customer.CreatedAtUtc = DateTime.UtcNow.AddDays(-5);
        customer.UpdatedAtUtc = customer.CreatedAtUtc;
        await _users.SaveChangesAsync();
        var customerId = customer.Id;
        string Snapshot(UserAccount user) => System.Text.Json.JsonSerializer.Serialize(
            _users.Entry(user).CurrentValues.Properties.ToDictionary(property => property.Name,
                property => _users.Entry(user).CurrentValues[property]));
        var before = Snapshot(customer);
        var request = EnjoyedRequest();
        request.Contact = byPhone ? "+34 612-345-678" : " Customer@Example.Test ";
        if (byPhone) request.PhoneNumber = "+34 612-345-678";
        request.Nombre = "Nombre cambiado por trabajador";
        request.Apellidos = "Apellidos cambiados";
        request.BirthDate = new DateTime(1995, 4, 8);
        request.Gender = UserGender.Mujer;
        request.PostalCode = "28013";
        request.Province = SpanishProvince.Madrid;
        request.TermsAccepted = true;
        request.PrivacyPolicyAccepted = true;
        request.MarketingAccepted = true;
        // Existing contacts are linked even when optional new-customer linking is off.
        request.LinkToBusiness = false;
        request.WelcomeBonusAlreadyEnjoyed = enjoyed;
        var rewards = new NoWelcomeRewards { WelcomeAvailable = true };
        var service = RegistrationService(rewards);
        var businessId = Assert.Single(_businesses.Negocios).Id;
        var first = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.False(first.Data!.Created);
        Assert.True(first.Data.ExistingUser);
        Assert.True(first.Data.LinkedNow);
        Assert.True(first.Data.ReceivedWelcomeTicket);
        Assert.Equal(enjoyed, first.Data.WelcomeTicketRedeemed);
        Assert.Equal(enjoyed ? _worker : (Guid?)null, rewards.RedeemedByUserId);
        Assert.Contains("ya estaba registrado", first.Data.Message);
        Assert.Contains("Sus datos no se han modificado", first.Data.Message);
        _users.ChangeTracker.Clear();
        var persisted = await _users.Users.SingleAsync(user => user.Id == customerId);
        Assert.Equal(before, Snapshot(persisted));
        var repeat = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(repeat.Succeeded, repeat.ErrorMessage);
        Assert.False(repeat.Data!.LinkedNow);
        Assert.False(repeat.Data.ReceivedWelcomeTicket);
        Assert.False(repeat.Data.WelcomeTicketRedeemed);
        Assert.Equal(1, rewards.WelcomeCalls);
        Assert.Single(_businesses.NegociosAudiencias);
        _users.ChangeTracker.Clear();
        Assert.Equal(before, Snapshot(await _users.Users.SingleAsync(user => user.Id == customerId)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingContactCanLinkWithoutNewRegistrationFieldsOrConsents(bool byPhone)
    {
        var customer = await SeedAsync(UserRole.User, UserStatus.PendingActivation, false);
        var request = new CreateBusinessCustomerUserRequest
        {
            Contact = byPhone ? "+34 612-345-678" : " CUSTOMER@EXAMPLE.TEST "
        };
        // Check request-level validation as well as the service: profile fields are
        // required only for creation, which is enforced after resolving the contact.
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            request, new System.ComponentModel.DataAnnotations.ValidationContext(request), errors, true));
        var rewards = new NoWelcomeRewards { WelcomeAvailable = true };
        var result = await RegistrationService(rewards).CreateCustomerByBusinessStaffAsync(
            Assert.Single(_businesses.Negocios).Id, _worker, false, request, null, null);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.Data!.ExistingUser);
        Assert.True(result.Data.LinkedNow);
        Assert.True(result.Data.ReceivedWelcomeTicket);
        Assert.False(customer.RegistrationCompleted);
        Assert.Equal(UserStatus.PendingActivation, customer.Status);
        Assert.False(customer.TermsAccepted);
        Assert.False(customer.PrivacyPolicyAccepted);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("birth-date")]
    [InlineData("province")]
    [InlineData("consents")]
    public async Task NewCustomerStillRequiresRegistrationDataAndConsents(string missing)
    {
        await SeedAsync(UserRole.User);
        var request = EnjoyedRequest();
        switch (missing)
        {
            case "name": request.Nombre = ""; break;
            case "birth-date": request.BirthDate = null; break;
            case "province": request.Province = null; break;
            case "consents": request.TermsAccepted = false; break;
        }
        var rewards = new NoWelcomeRewards { WelcomeAvailable = true };
        var result = await RegistrationService(rewards).CreateCustomerByBusinessStaffAsync(
            Assert.Single(_businesses.Negocios).Id, _worker, false, request, null, null);
        Assert.False(result.Succeeded);
        Assert.Equal("validation_error", result.ErrorCode);
        Assert.False(await _users.Users.AnyAsync(user => user.Email == request.Contact));
        Assert.Empty(_businesses.NegociosAudiencias);
        Assert.Equal(0, rewards.WelcomeCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewCustomerRequiresBothEmailAndPhoneAndPersistsThem(bool missingPhone)
    {
        await SeedAsync(UserRole.User);
        var request = EnjoyedRequest();
        request.Email = request.Contact;
        request.Contact = "";
        if (missingPhone) request.PhoneNumber = null;
        else request.Email = null;
        var rewards = new NoWelcomeRewards { WelcomeAvailable = true };
        var service = RegistrationService(rewards);
        var businessId = Assert.Single(_businesses.Negocios).Id;
        var invalid = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.False(invalid.Succeeded);
        Assert.Equal("validation_error", invalid.ErrorCode);
        Assert.Empty(_businesses.NegociosAudiencias);
        Assert.Equal(0, rewards.WelcomeCalls);
        request.Email = "new-with-phone@example.test";
        request.PhoneNumber = "+34 699-123-456";
        var result = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(result.Succeeded, result.ErrorMessage);
        var saved = await _users.Users.AsNoTracking().SingleAsync(user => user.Id == result.Data!.User.Id);
        Assert.Equal(request.Email, saved.Email);
        Assert.Equal("NEW-WITH-PHONE@EXAMPLE.TEST", saved.NormalizedEmail);
        Assert.Equal(request.PhoneNumber, saved.PhoneNumber);
        Assert.Equal("34699123456", saved.NormalizedPhoneNumber);
        Assert.False(saved.PhoneNumberConfirmed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EitherSeparateContactCanResolveExistingCustomerWithoutUpdatingOtherContact(bool matchPhone, bool enjoyed)
    {
        var customer = await SeedAsync(UserRole.User);
        var request = EnjoyedRequest();
        request.Contact = "";
        request.Email = matchPhone ? "different@example.test" : " CUSTOMER@EXAMPLE.TEST ";
        request.PhoneNumber = matchPhone ? "+34 612-345-678" : "+34 699 123 456";
        request.WelcomeBonusAlreadyEnjoyed = enjoyed;
        var rewards = new NoWelcomeRewards { WelcomeAvailable = true };
        var service = RegistrationService(rewards);
        var businessId = Assert.Single(_businesses.Negocios).Id;
        var response = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(response.Succeeded, response.ErrorMessage);
        Assert.True(response.Data!.ExistingUser);
        Assert.Equal(customer.Id, response.Data.User.Id);
        Assert.True(response.Data.LinkedNow);
        Assert.Equal(enjoyed, response.Data.WelcomeTicketRedeemed);
        _users.ChangeTracker.Clear();
        var saved = await _users.Users.SingleAsync(user => user.Id == customer.Id);
        Assert.Equal("customer@example.test", saved.Email);
        Assert.Equal("+34 612 345 678", saved.PhoneNumber);
        Assert.Null(saved.FirstName);
        Assert.Single(_users.Users);
        // Both identifiers matching the same account remain idempotent.
        request.Email = saved.Email;
        request.PhoneNumber = saved.PhoneNumber;
        var repeat = await service.CreateCustomerByBusinessStaffAsync(businessId, _worker, false, request, null, null);
        Assert.True(repeat.Succeeded, repeat.ErrorMessage);
        Assert.False(repeat.Data!.ReceivedWelcomeTicket);
        Assert.Equal(1, rewards.WelcomeCalls);
    }

    [Fact]
    public async Task SeparateContactsBelongingToDifferentAccountsDoNotLinkEitherCustomer()
    {
        var customer = await SeedAsync(UserRole.User);
        _users.Users.Add(new UserAccount { Id = Guid.NewGuid(), UserName = "other", Email = "other@example.test",
            NormalizedEmail = "OTHER@EXAMPLE.TEST", PhoneNumber = "+34 699 123 456", NormalizedPhoneNumber = "34699123456" });
        await _users.SaveChangesAsync();
        var request = EnjoyedRequest();
        request.Contact = "";
        request.Email = customer.Email;
        request.PhoneNumber = "+34 699-123-456";
        var rewards = new NoWelcomeRewards { WelcomeAvailable = true };
        var result = await RegistrationService(rewards).CreateCustomerByBusinessStaffAsync(
            Assert.Single(_businesses.Negocios).Id, _worker, false, request, null, null);
        Assert.False(result.Succeeded);
        Assert.Equal("conflict", result.ErrorCode);
        Assert.Contains("usuarios diferentes", result.ErrorMessage);
        Assert.Empty(_businesses.NegociosAudiencias);
        Assert.Equal(0, rewards.WelcomeCalls);
        Assert.Equal(2, await _users.Users.CountAsync());
    }

    private sealed class NoWelcomeRewards : IRegistrationRewardService
    {
        public int WelcomeCalls { get; private set; }
        public bool WelcomeAvailable { get; init; }
        public Task<bool> AssignBusinessWelcomeTicketAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken = default)
        {
            WelcomeCalls++;
            return Task.FromResult(WelcomeAvailable);
        }
        public Guid? RedeemedByUserId { get; private set; }
        public Task<bool> AssignBusinessWelcomeTicketAsync(Guid negocioId, Guid userId, Guid redeemedByUserId, CancellationToken cancellationToken = default)
        {
            RedeemedByUserId = redeemedByUserId;
            return AssignBusinessWelcomeTicketAsync(negocioId, userId, cancellationToken);
        }
        public Task<bool> AssignBusinessReferralTicketAsync(Guid negocioId, Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> ValidateQrTokenAsync(string qrToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task PreparePendingAssignmentAsync(Guid userId, string qrToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task FinalizePendingAssignmentsAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<WelcomeTicketClaimResult?> ClaimTicketFromQrAsync(Guid userId, string qrToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
