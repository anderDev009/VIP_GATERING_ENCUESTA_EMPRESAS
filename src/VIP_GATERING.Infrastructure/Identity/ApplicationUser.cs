using System;
using Microsoft.AspNetCore.Identity;

namespace VIP_GATERING.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? EmpleadoId { get; set; }
    public Guid? EmpresaId { get; set; }
}
