using System.Text.Json;
using System.Threading.Tasks;

namespace YessBackend.Application.Services;

public interface IWebhookService
{
    Task ProcessFinikWebhookAsync(JsonElement payload);
}
