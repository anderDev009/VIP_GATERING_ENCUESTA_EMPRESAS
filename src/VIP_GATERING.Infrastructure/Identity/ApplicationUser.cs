using System;
using Microsoft.AspNetCore.Identity;

namespace VIP_GATERING.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<int>
{
    public int? EmpleadoId { get; set; }
    public int? EmpresaId { get; set; }
}
