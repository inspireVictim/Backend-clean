using System.Net;

namespace YessBackend.Infrastructure.Helpers;

/// <summary>
/// Вспомогательный класс для работы с IP-адресами и подсетями
/// </summary>
public static class IpAddressHelper
{
    /// <summary>
    /// Проверяет, принадлежит ли IP-адрес указанной подсети (CIDR)
    /// </summary>
    /// <param name="ipAddress">IP-адрес для проверки</param>
    /// <param name="subnet">Подсеть в формате CIDR (например, "192.168.1.0/24")</param>
    /// <returns>true, если IP-адрес принадлежит подсети, иначе false</returns>
    public static bool IsIpInSubnet(IPAddress? ipAddress, string subnet)
    {
        if (ipAddress == null || string.IsNullOrWhiteSpace(subnet))
        {
            return false;
        }

        var parts = subnet.Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!IPAddress.TryParse(parts[0], out var subnetIp))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var prefixLength) || prefixLength < 0 || prefixLength > 32)
        {
            return false;
        }

        var ipBytes = ipAddress.GetAddressBytes();
        var subnetBytes = subnetIp.GetAddressBytes();

        // Проверяем, что оба адреса IPv4
        if (ipBytes.Length != 4 || subnetBytes.Length != 4)
        {
            return false;
        }

        // Вычисляем количество байт для проверки
        var bytesToCheck = prefixLength / 8;
        var bitsToCheck = prefixLength % 8;

        // Проверяем полные байты
        for (int i = 0; i < bytesToCheck; i++)
        {
            if (ipBytes[i] != subnetBytes[i])
            {
                return false;
            }
        }

        // Проверяем частичный байт (если есть)
        if (bitsToCheck > 0 && bytesToCheck < 4)
        {
            var mask = (byte)(0xFF << (8 - bitsToCheck));
            if ((ipBytes[bytesToCheck] & mask) != (subnetBytes[bytesToCheck] & mask))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Проверяет, принадлежит ли IP-адрес хотя бы одной из указанных подсетей
    /// </summary>
    /// <param name="ipAddress">IP-адрес для проверки</param>
    /// <param name="subnets">Список подсетей в формате CIDR</param>
    /// <returns>true, если IP-адрес принадлежит хотя бы одной подсети, иначе false</returns>
    public static bool IsIpInAnySubnet(IPAddress? ipAddress, IEnumerable<string> subnets)
    {
        if (ipAddress == null || subnets == null)
        {
            return false;
        }

        return subnets.Any(subnet => IsIpInSubnet(ipAddress, subnet));
    }
}

