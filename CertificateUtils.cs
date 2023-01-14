using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Stargate
{
    public static class CertificateUtils
    {
        static public X509Certificate2 LoadCertificate(string certFilePath, string privateRSAKeyFilePath)
        {
            X509Certificate2 cert = new X509Certificate2(certFilePath);
            string[] lines = File.ReadAllLines(privateRSAKeyFilePath, Encoding.UTF8);
            string privateKeyData = "";
            for (int i = 1; i < lines.Length - 1; ++i)
            {
                privateKeyData += lines[i];
            }
            byte[] bytes = Convert.FromBase64String(privateKeyData);

            //try RSA
            try
            {
                RSA rsa = RSA.Create();
                // For now we always expect to have cert with BEGIN PRIVATE KEY label
                // TODO: add handling for other types, maybe??
                rsa.ImportPkcs8PrivateKey(bytes, out _);

                X509Certificate2 pubPrivEphemeral = cert.CopyWithPrivateKey(rsa);
                return new X509Certificate2(pubPrivEphemeral.Export(X509ContentType.Pfx));
            } catch(Exception)
            {
            }

            //try EC
            try
            {
                ECDsa ecdsa = ECDsa.Create();
                // For now we always expect to have cert with BEGIN PRIVATE KEY label
                // TODO: add handling for other types, maybe??
                ecdsa.ImportPkcs8PrivateKey(bytes, out _);

                X509Certificate2 pubPrivEphemeral = cert.CopyWithPrivateKey(ecdsa);
                return new X509Certificate2(pubPrivEphemeral.Export(X509ContentType.Pfx));
            }
            catch (Exception)
            {
            }

            return null;
        }
    }
}
