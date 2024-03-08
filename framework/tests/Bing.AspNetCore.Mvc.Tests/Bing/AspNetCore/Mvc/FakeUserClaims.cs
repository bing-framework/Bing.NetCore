using System.Collections.Generic;
using System.Security.Claims;
using Bing.DependencyInjection;

namespace Bing.AspNetCore.Mvc;

public class FakeUserClaims : ISingletonDependency
{
    public List<Claim> Claims { get; } = new List<Claim>();
}
