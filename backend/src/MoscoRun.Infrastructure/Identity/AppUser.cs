using Microsoft.AspNetCore.Identity;

namespace MoscoRun.Infrastructure.Identity;

/// <summary>Utente dell'installazione. L'Id è anche l'ambito di proprietà dei dati.</summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
}
