using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace StockControl.Services.Arca
{
    public record CertificateInfo(string Subject, DateTime ValidUntil)
    {
        public bool Expired => ValidUntil < DateTime.Now;
    }

    // Keeps the ARCA certificate and its private key for one environment
    // (homologación or producción), in the data folder:
    //   certificado.crt       public certificate issued by ARCA
    //   clave.dat             private key, encrypted with Windows DPAPI
    //   clave-pendiente-*.dat keys of requests (.csr) still waiting for their certificate
    //
    // Flow without OpenSSL: CreateRequest() makes a key + .csr, the user uploads the
    // .csr to ARCA, downloads the .crt and ImportCertificate() pairs them.
    // An existing .pfx/.p12 can also be imported directly.
    public class ArcaCertificateStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("StockManager.Arca.v1");
        private readonly string _folder;

        public ArcaCertificateStore(string folder)
        {
            _folder = folder;
        }

        private string CertPath => Path.Combine(_folder, "certificado.crt");
        private string KeyPath => Path.Combine(_folder, "clave.dat");
        // Every request keeps its own key (clave-pendiente-<fecha>.dat), so generating
        // a request again never invalidates one already uploaded to ARCA.
        // (The pattern also finds "clave-pendiente.dat" from the first version.)
        private IEnumerable<string> PendingKeyPaths()
        {
            if (!Directory.Exists(_folder))
                return Enumerable.Empty<string>();
            return Directory.GetFiles(_folder, "clave-pendiente*.dat").OrderByDescending(File.GetLastWriteTimeUtc);
        }

        public bool HasPendingRequest => PendingKeyPaths().Any();

        public CertificateInfo? Info
        {
            get
            {
                try
                {
                    if (!File.Exists(CertPath) || !File.Exists(KeyPath))
                        return null;
                    using var cert = X509CertificateLoader.LoadCertificate(File.ReadAllBytes(CertPath));
                    return new CertificateInfo(cert.Subject, cert.NotAfter);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        // Generates a new key pair and the certificate request (PEM) to upload to ARCA.
        // alias: the name the certificate will have in ARCA (e.g. "stockmanager").
        public string CreateRequest(string cuit, string organization, string alias)
        {
            cuit = new string((cuit ?? "").Where(char.IsDigit).ToArray());
            if (cuit.Length != 11)
                throw new Exception("El CUIT del negocio tiene que tener 11 dígitos (Configuración > Datos del negocio).");
            if (string.IsNullOrWhiteSpace(alias))
                throw new Exception("Indicá un nombre (alias) para el certificado.");

            using var rsa = RSA.Create(2048);
            // The format ARCA asks for: C=AR, O=<empresa>, CN=<alias>, serialNumber=CUIT <cuit>
            var builder = new X500DistinguishedNameBuilder();
            builder.AddCountryOrRegion("AR");
            builder.AddOrganizationName(string.IsNullOrWhiteSpace(Clean(organization)) ? "Empresa" : Clean(organization));
            builder.AddCommonName(Clean(alias));
            builder.Add("2.5.4.5", $"CUIT {cuit}", System.Formats.Asn1.UniversalTagNumber.PrintableString);
            var subject = builder.Build();
            var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            Directory.CreateDirectory(_folder);
            var path = Path.Combine(_folder, $"clave-pendiente-{DateTime.Now:yyyyMMdd-HHmmss-fff}.dat");
            File.WriteAllBytes(path, Protect(rsa.ExportPkcs8PrivateKey()));
            return request.CreateSigningRequestPem();
        }

        // Accepts the .crt/.cer/.pem that ARCA returns for our request, or a .pfx/.p12.
        public CertificateInfo ImportCertificate(string path, string? pfxPassword = null)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            byte[] certDer;
            byte[] keyPkcs8;
            string? usedPendingKey = null;

            if (ext == ".pfx" || ext == ".p12")
            {
                using var pfx = X509CertificateLoader.LoadPkcs12FromFile(path, pfxPassword,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
                using var key = pfx.GetRSAPrivateKey()
                    ?? throw new Exception("El archivo .pfx no tiene la clave privada.");
                certDer = pfx.Export(X509ContentType.Cert);
                keyPkcs8 = ExportKey(key);
            }
            else
            {
                using var cert = LoadCertificateFile(path);
                // Which of our keys does it belong to? (any pending request, or the current one)
                var match = PendingKeyPaths().Append(KeyPath)
                    .Where(File.Exists)
                    .Select(p => (Path: p, Key: TryReadKey(p)))
                    .FirstOrDefault(k => k.Key != null && Matches(cert, k.Key));
                if (match.Key == null)
                    throw new Exception("Ese certificado no corresponde a ningún pedido generado por el programa en esta PC " +
                                        "(la clave de ese pedido ya no está). Generá un pedido nuevo con otro alias, subilo a ARCA " +
                                        "e importá el certificado que te devuelva, o importá un archivo .pfx.");
                certDer = cert.Export(X509ContentType.Cert);
                keyPkcs8 = match.Key;
                if (match.Path != KeyPath)
                    usedPendingKey = match.Path;
            }

            Directory.CreateDirectory(_folder);
            File.WriteAllBytes(KeyPath, Protect(keyPkcs8));
            File.WriteAllBytes(CertPath, certDer);
            // Only the key that was used: other pending requests may still get their certificate
            if (usedPendingKey != null && File.Exists(usedPendingKey))
                File.Delete(usedPendingKey);

            using var saved = X509CertificateLoader.LoadCertificate(certDer);
            return new CertificateInfo(saved.Subject, saved.NotAfter);
        }

        // Certificate + private key to sign the WSAA request. Null if not configured.
        public (X509Certificate2 Certificate, RSA Key)? Load()
        {
            if (!File.Exists(CertPath) || !File.Exists(KeyPath))
                return null;
            var cert = X509CertificateLoader.LoadCertificate(File.ReadAllBytes(CertPath));
            var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Unprotect(File.ReadAllBytes(KeyPath)), out _);
            return (cert, rsa);
        }

        private static X509Certificate2 LoadCertificateFile(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var text = Encoding.ASCII.GetString(bytes);
            if (text.Contains("-----BEGIN CERTIFICATE-----"))
                return X509Certificate2.CreateFromPem(text);
            return X509CertificateLoader.LoadCertificate(bytes);
        }

        // A key that can't be read (corrupt, or from another Windows user) is just skipped
        private static byte[]? TryReadKey(string path)
        {
            try { return Unprotect(File.ReadAllBytes(path)); }
            catch (Exception) { return null; }
        }

        private static bool Matches(X509Certificate2 cert, byte[] pkcs8)
        {
            using var key = RSA.Create();
            key.ImportPkcs8PrivateKey(pkcs8, out _);
            using var certKey = cert.GetRSAPublicKey();
            if (certKey == null)
                return false;
            return key.ExportParameters(false).Modulus!.SequenceEqual(certKey.ExportParameters(false).Modulus!);
        }

        // A key coming from a .pfx may not allow a plain PKCS#8 export on Windows:
        // export it encrypted and re-read it.
        private static byte[] ExportKey(RSA key)
        {
            var pbe = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10_000);
            var encrypted = key.ExportEncryptedPkcs8PrivateKey("tmp", pbe);
            using var copy = RSA.Create();
            copy.ImportEncryptedPkcs8PrivateKey("tmp", encrypted, out _);
            return copy.ExportPkcs8PrivateKey();
        }

        private static string Clean(string? value)
            => new string((value ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '.').ToArray()).Trim();

        // DPAPI on Windows. (Elsewhere — only the console tests — the key is stored as is.)
        private static byte[] Protect(byte[] data)
            => OperatingSystem.IsWindows() ? ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser) : data;

        private static byte[] Unprotect(byte[] data)
            => OperatingSystem.IsWindows() ? ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser) : data;
    }
}
