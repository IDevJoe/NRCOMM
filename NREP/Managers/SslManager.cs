using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Serilog;

namespace NREP.Managers
{
    public class SslManager
    {
        public static X509Certificate2 Certificate { get; private set; }
        public static X509Certificate2 CACertificate { get; private set; }
        public static async Task Initialize()
        {
            await Task.Run(() =>
            {

            });
            if (NREP.Config.X509 == null) return;
            Log.Information("Initializing SSL");
            PemReader keyReader = new PemReader(File.OpenText(NREP.Config.X509.KeyFile));
            AsymmetricCipherKeyPair obj = (AsymmetricCipherKeyPair)keyReader.ReadObject();
            Certificate = new X509Certificate2(NREP.Config.X509.CertificateFile);
            RsaPrivateCrtKeyParameters v = (RsaPrivateCrtKeyParameters)obj.Private;
            Certificate = Certificate.CopyWithPrivateKey(DotNetUtilities.ToRSA(v));
            Log.Information("Server Certificate: {CN}", Certificate.Subject);
            try
            {
                string ts = "abcdefg";
                byte[] enc = await Encrypt(Encoding.UTF8.GetBytes(ts), Certificate);
                Encoding.UTF8.GetString(await Decrypt(enc));
                Log.Information("SSL KeyPair test OK");
            }
            catch (Exception)
            {
                Certificate = null;
                Log.Error("Certificate/Key Validation failed - SSL is disabled");
            }

            if (NREP.Config.CA == null) return;
            CACertificate = new X509Certificate2(NREP.Config.CA);
            Log.Information("CA Certificate: {CA}", CACertificate.Subject);
            bool success = await ValidateAgainstCA(Certificate);
            if (success)
            {
                Log.Information("CA test OK");
            }
            else
            {
                Log.Warning("Certificate failed validation against CA. Your certificate may be rejected by other clients");
            }
        }

        public static async Task<byte[]> Decrypt(byte[] data)
        {
            return await Task.Run(() => Certificate.GetRSAPrivateKey().Decrypt(data, RSAEncryptionPadding.OaepSHA512));
        }

        public static async Task<byte[]> Encrypt(byte[] data, X509Certificate2 cert)
        {
            return await Task.Run(() => cert.GetRSAPublicKey().Encrypt(data, RSAEncryptionPadding.OaepSHA512));
        }

        public static async Task<bool> ValidateAgainstCA(X509Certificate2 c2)
        {
            return await Task.Run(() =>
            {
                if (CACertificate == null) return true;
                X509Chain ch = new X509Chain();
                ch.ChainPolicy.ExtraStore.Add(CACertificate);
                ch.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                ch.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreWrongUsage;
                bool valid = ch.Build(c2);
                if(!valid) Log.Warning("{@CS}", ch.ChainStatus);
                return valid;
            });
        }
    }
}