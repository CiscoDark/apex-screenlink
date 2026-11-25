using System.Security.Cryptography;
using QRCoder;

namespace Apex.Core;
public sealed class QrPairing {
    public string ServiceName {get;}="studio-"+Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
    public string Secret {get;}=Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    public DateTime Expires {get;}=DateTime.UtcNow.AddMinutes(2);
    public string Payload => $"WIFI:T:ADB;S:{ServiceName};P:{Secret};;";
    public bool Matches(Service service) => service.Pairing && service.Name.Equals(ServiceName,StringComparison.Ordinal);
    public byte[] Png() {
        using var data=QRCodeGenerator.GenerateQrCode(Payload,QRCodeGenerator.ECCLevel.M);
        using var png=new PngByteQRCode(data);return png.GetGraphic(8);
    }
    public override string ToString()=>"Temporary Android QR pairing session";
}
