using System;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
        public static void Initialize()
        {
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
                byte[] enc = Encrypt(Encoding.UTF8.GetBytes(ts), Certificate);
                Encoding.UTF8.GetString(Decrypt(enc));
                Log.Information("SSL KeyPair test OK");
            }
            catch (Exception e)
            {
                Certificate = null;
                Log.Error("Certificate/Key Validation failed - SSL is disabled");
            }

            if (NREP.Config.CA == null) return;
            CACertificate = new X509Certificate2(NREP.Config.CA);
            Log.Information("CA Certificate: {CA}", CACertificate.Subject);
            bool success = ValidateAgainstCA(Certificate);
            if (success)
            {
                Log.Information("CA test OK");
            }
            else
            {
                Log.Warning("Certificate failed validation against CA. Your certificate may be rejected by other clients");
            }
        }

        public static byte[] Decrypt(byte[] data)
        {
            return Certificate.GetRSAPrivateKey().Decrypt(data, RSAEncryptionPadding.OaepSHA512);
        }

        public static byte[] Encrypt(byte[] data, X509Certificate2 cert)
        {
            return cert.GetRSAPublicKey().Encrypt(data, RSAEncryptionPadding.OaepSHA512);
        }

        public static bool ValidateAgainstCA(X509Certificate2 c2)
        {
            if (CACertificate == null) return true;
            X509Chain ch = new X509Chain();
            ch.ChainPolicy.ExtraStore.Add(CACertificate);
            ch.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            ch.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreWrongUsage;
            bool valid = ch.Build(c2);
            if(!valid) Log.Warning("{@CS}", ch.ChainStatus);
            return valid;
        }
    }
}