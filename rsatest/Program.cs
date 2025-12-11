using System;
using System.Security.Cryptography;

var pem = File.ReadAllText("/home/yesgoadm/Backend/finik_private_rsa.pem");

try
{
    using RSA rsa = RSA.Create();
    rsa.ImportFromPem(pem);

    Console.WriteLine("✔ .NET: ключ успешно импортирован");
}
catch (Exception ex)
{
    Console.WriteLine("❌ Ошибка: " + ex.Message);
}
