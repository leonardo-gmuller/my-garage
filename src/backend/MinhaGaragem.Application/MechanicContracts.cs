using System.Net.Mail;
using System.Text.RegularExpressions;
using MinhaGaragem.Domain;

namespace MinhaGaragem.Application;

public sealed record MechanicInput(
    string Name,
    string? Workshop = null,
    string? Specialty = null,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    string? Notes = null,
    bool IsArchived = false
);

public sealed record MechanicDto(
    Guid Id,
    string Name,
    string? Workshop,
    string? Specialty,
    string? Phone,
    string? Email,
    string? Address,
    string? Notes,
    bool IsArchived,
    int ServiceCount,
    DateOnly? LastServiceDate
);

public static class MechanicRules
{
    public static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static void Validate(MechanicInput input)
    {
        Rules.Text(input.Name, "Nome do mecânico", 120, true);
        Rules.Text(input.Workshop, "Oficina", 200);
        Rules.Text(input.Specialty, "Especialidade", 120);
        Rules.Text(input.Phone, "Telefone", 40);
        Rules.Text(input.Email, "E-mail", 254);
        Rules.Text(input.Address, "Endereço", 300);
        Rules.Text(input.Notes, "Observações", 4000);
        var phone = Optional(input.Phone);
        if (
            phone is not null
            && (
                !Regex.IsMatch(phone, @"^\+?[0-9\s().-]+$")
                || phone.Count(char.IsAsciiDigit) is < 8 or > 15
            )
        )
            throw new DomainException("Informe um telefone com 8 a 15 dígitos, incluindo o DDD.");
        var email = Optional(input.Email);
        if (
            email is not null
            && (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        )
            throw new DomainException("Informe um e-mail válido.");
    }
}
