using ThucLuc.Application.Common.Models;

namespace ThucLuc.Application.Common.Contracts;

public interface ICurrentUserService
{
    CurrentUserProfile GetCurrentUser();
}