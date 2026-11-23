using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MinhaGaragem.Application;
using MinhaGaragem.Domain;

namespace MinhaGaragem.Infrastructure;

/// <summary>Account creation is an installation task, never an HTTP endpoint.</summary>
public sealed class AccountProvisioner(GarageDbContext db, UserManager<User> users)
{
    public async Task<bool> InitializeFirstUser(
        string? email,
        string? password,
        CancellationToken ct = default
    )
    {
        // Existing installations keep their accounts, password hashes and vehicle data.
        if (await db.Users.AnyAsync(ct))
            return false;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Configure BOOTSTRAP_EMAIL e BOOTSTRAP_PASSWORD no .env antes de iniciar uma instalação vazia."
            );
        await Create(email, password, ct);
        return true;
    }

    public async Task<User> Create(string email, string password, CancellationToken ct = default)
    {
        Rules.Text(email, "E-mail", 254, true);
        Rules.Text(password, "Senha", 128, true);
        email = email.Trim();
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new DomainException("E-mail de acesso inválido.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new DomainException(
                "Não foi possível criar o acesso. Use um e-mail disponível e senha com 10 a 128 caracteres, maiúscula, minúscula e número."
            );
        var vehicle = new Vehicle
        {
            UserId = user.Id,
            Manufacturer = "Volkswagen",
            Model = "Saveiro",
            ManufactureYear = 2013,
            ModelYear = 2013,
            Color = "Prata",
            ColorHex = "#C0C0C0",
            PaintFinish = PaintFinish.Metallic,
        };
        db.Vehicles.Add(vehicle);
        foreach (var component in ComponentCatalog.All)
            db.Components.Add(
                SubcomponentCatalog.CreateComponent(vehicle.Id, component.Key, component.Name)
            );
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return user;
    }
}
