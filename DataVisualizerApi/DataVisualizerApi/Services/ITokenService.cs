using DataVisualizerApi.Models;

namespace DataVisualizerApi.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}
