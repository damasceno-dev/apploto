namespace server.Domain.Entities;

public class Client : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public string? Cpf { get; set; }
    public string? Cep { get; set; }
    public string? Address { get; set; }
    public string? PhoneSecondary { get; set; }
    public string? Notes { get; set; }
    public string? Email { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
}
