using Newtonsoft.Json;

namespace NREP
{
    public class NREPConfiguration
    {
        [JsonProperty("default")] public bool IsDefault;

        [JsonProperty("x509")] public X509Configuration X509;

        [JsonProperty("ca")] public string CA;

        [JsonProperty("router")] public object RouterConfig;

        [JsonProperty("minLogLevel")] public string MinLogLevel;

        public class X509Configuration
        {
            [JsonProperty("cert")] public string CertificateFile;

            [JsonProperty("key")] public string KeyFile;
        }
    }
}